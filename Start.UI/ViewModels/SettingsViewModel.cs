using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsRepository? _settingsRepository;
        public event EventHandler? BackRequested;

        [ObservableProperty]
        private string _defaultDownloadPath = string.Empty;

        [ObservableProperty]
        private int _maxSegmentWorkers = 16;

        [ObservableProperty]
        private int _minSegmentSizeMB = 2;

        [ObservableProperty]
        private int _maxConcurrentDownloads = 3;

        [ObservableProperty]
        private bool _enableClipboardMonitoring = true;

        [ObservableProperty]
        private bool _extractAudio = false;

        [ObservableProperty]
        private string _audioFormat = "mp3";

        [ObservableProperty]
        private bool _embedMetadata = true;

        [ObservableProperty]
        private bool _embedThumbnail = true;

        [ObservableProperty]
        private ObservableCollection<IqiyiAccountCredential> _iqiyiAccounts = new();

        [ObservableProperty]
        private string _saveStatusMessage = string.Empty;

        [ObservableProperty]
        private bool _hasSaveStatus;

        public List<string> AvailableAudioFormats { get; } = new() { "mp3", "m4a", "wav", "flac", "aac" };
        public List<int> AvailableSegmentSizes { get; } = new() { 1, 2, 4, 8, 16 };

        public SettingsViewModel() : this(null) { }

        public SettingsViewModel(ISettingsRepository? settingsRepository)
        {
            _settingsRepository = settingsRepository;
            _defaultDownloadPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            _ = LoadSettingsAsync();
        }

        public async Task LoadSettingsAsync()
        {
            DownloadProcessingSettings settings;
            if (_settingsRepository != null)
            {
                try
                {
                    settings = await _settingsRepository.LoadAsync();
                }
                catch
                {
                    settings = new DownloadProcessingSettings();
                }
            }
            else
            {
                settings = new DownloadProcessingSettings();
            }

            DefaultDownloadPath = string.IsNullOrWhiteSpace(settings.DefaultDownloadPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
                : settings.DefaultDownloadPath;

            MaxSegmentWorkers = settings.MaxSegmentWorkers > 0 ? settings.MaxSegmentWorkers : 16;
            MinSegmentSizeMB = (int)Math.Max(1, settings.MinSegmentSizeBytes / (1024 * 1024));
            MaxConcurrentDownloads = settings.MaxConcurrentDownloads > 0 ? settings.MaxConcurrentDownloads : 3;
            EnableClipboardMonitoring = settings.EnableClipboardMonitoring;
            ExtractAudio = settings.ExtractAudio;
            AudioFormat = string.IsNullOrWhiteSpace(settings.AudioFormat) ? "mp3" : settings.AudioFormat;
            EmbedMetadata = settings.EmbedMetadata;
            EmbedThumbnail = settings.EmbedThumbnail;

            IqiyiAccounts.Clear();
            var accounts = settings.IqiyiAccounts != null && settings.IqiyiAccounts.Count > 0
                ? settings.IqiyiAccounts
                : new DownloadProcessingSettings().IqiyiAccounts;

            foreach (var acc in accounts)
            {
                IqiyiAccounts.Add(new IqiyiAccountCredential
                {
                    Label = acc.Label,
                    Email = acc.Email,
                    Password = acc.Password,
                    IsActive = acc.IsActive
                });
            }

            SaveStatusMessage = string.Empty;
            HasSaveStatus = false;
        }

        [RelayCommand]
        private void Back()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Default Download Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                DefaultDownloadPath = dialog.FolderName;
            }
        }

        [RelayCommand]
        private async Task SaveSettings()
        {
            var settings = new DownloadProcessingSettings
            {
                DefaultDownloadPath = DefaultDownloadPath,
                MaxSegmentWorkers = MaxSegmentWorkers,
                MinSegmentSizeBytes = (long)MinSegmentSizeMB * 1024 * 1024,
                MaxConcurrentDownloads = MaxConcurrentDownloads,
                EnableClipboardMonitoring = EnableClipboardMonitoring,
                ExtractAudio = ExtractAudio,
                AudioFormat = AudioFormat,
                EmbedMetadata = EmbedMetadata,
                EmbedThumbnail = EmbedThumbnail,
                IqiyiAccounts = IqiyiAccounts.ToList()
            };

            if (_settingsRepository != null)
            {
                await _settingsRepository.SaveAsync(settings);
            }

            SaveStatusMessage = "Settings saved successfully! ✅";
            HasSaveStatus = true;
        }

        [RelayCommand]
        private async Task ResetDefaults()
        {
            var defaults = new DownloadProcessingSettings();
            if (_settingsRepository != null)
            {
                await _settingsRepository.SaveAsync(defaults);
            }
            await LoadSettingsAsync();
            SaveStatusMessage = "Restored default settings! ↺";
            HasSaveStatus = true;
        }

        [RelayCommand]
        private void SetPrimaryActive(IqiyiAccountCredential target)
        {
            if (target == null) return;
            // Activate this account and make others inactive, or simply toggle
            target.IsActive = !target.IsActive;
        }
    }
}
