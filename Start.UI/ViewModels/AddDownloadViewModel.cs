using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Start.App.Services;
using Start.Core.Models;
using System.Collections.Generic;
using System.Globalization;

namespace Start.UI.ViewModels
{
    public partial class PlatformItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _iconColor = "#ff0048";
        [ObservableProperty] private string _defaultUrl = string.Empty;
        [ObservableProperty] private bool _isSelected;
    }

    public partial class LogMessage : ObservableObject
    {
        [ObservableProperty] private string _text = string.Empty;
        [ObservableProperty] private string _color = "#ffffff";
    }

    public partial class AddDownloadViewModel : ObservableObject
    {
        private readonly IDownloadService _downloadService;
        private readonly DispatcherTimer _downloadRefreshTimer;
        private readonly HashSet<Guid> _visibleDownloadIds = new();

        public event EventHandler? NavigateToQueueRequested;
        public event EventHandler? NavigateToHistoryRequested;
        public event EventHandler? NavigateToSettingsRequested;

        [RelayCommand]
        public void NavigateToQueue() => NavigateToQueueRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        public void NavigateToHistory() => NavigateToHistoryRequested?.Invoke(this, EventArgs.Empty);

        [RelayCommand]
        public void NavigateToSettings() => NavigateToSettingsRequested?.Invoke(this, EventArgs.Empty);

        [ObservableProperty] private string _url = string.Empty;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _errorMessage = string.Empty;
        
        // Custom UI states
        [ObservableProperty] private bool _showsTrending = true;
        [ObservableProperty] private string _currentDramaTitle = "No drama loaded";
        [ObservableProperty] private string _currentPosterUrl = string.Empty;
        [ObservableProperty] private int _totalEpisodes = 0;
        
        private int _selectedCount = 0;
        public int SelectedCount
        {
            get => _selectedCount;
            set => SetProperty(ref _selectedCount, value);
        }

        [ObservableProperty] private string _savePath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        
        // Quality selection (populated after fetch)
        [ObservableProperty] private ObservableCollection<string> _availableQualities = new();
        [ObservableProperty] private string _selectedQuality = string.Empty;
        
        // Extracted items
        [ObservableProperty] private ObservableCollection<DownloadJob> _episodes = new();
        [ObservableProperty] private ObservableCollection<DownloadJob> _downloadJobs = new();
        [ObservableProperty] private ObservableCollection<LogMessage> _logs = new();
        [ObservableProperty] private ObservableCollection<PlatformItem> _platforms = new();
        [ObservableProperty] private bool _hasDownloadJobs;
        
        [ObservableProperty] private PlatformItem? _selectedPlatform;

        public AddDownloadViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;

            var defaultPlatforms = new (string Name, string Url, string Color)[]
            {
                ("iQiyi", "https://www.iq.com/?lang=en_us", "#00cc4c"),
                ("DramaBox", "https://www.dramabox.com/", "#ff522e"),
                ("NetShort", "https://www.netshort.com/", "#0984e3"),
                ("Iflix", "https://www.iflix.com/", "#f39c12"),
                ("KissKH", "https://kisskh.co/", "#e84393"),
                ("YouTube", "https://www.youtube.com/", "#ff0000"),
                ("TikTok", "https://www.tiktok.com/", "#00cec9"),
                ("Facebook", "https://www.facebook.com/", "#1877f2"),
                ("Instagram", "https://www.instagram.com/", "#fd79a8"),
                ("Bilibili", "https://www.bilibili.com/", "#00a8ff"),
                ("FlickReels", "", "#ff0048"),
                ("ShortMax", "", "#ff0048"),
                ("DramaWave", "", "#ff0048"),
                ("StardustTV", "", "#ff0048"),
                ("GoodShort", "", "#ff0048"),
                ("ReelShort", "", "#ff0048"),
                ("BiliTV", "", "#ff0048"),
                ("iDrama", "", "#ff0048"),
                ("Melolo", "", "#ff0048"),
                ("DotDrama", "", "#ff0048"),
                ("Reelife", "", "#ff0048"),
                ("Velolo", "", "#ff0048"),
                ("X(Twitter)", "", "#1da1f2")
            };

            foreach (var p in defaultPlatforms)
            {
                Platforms.Add(new PlatformItem 
                { 
                    Name = p.Name, 
                    DefaultUrl = p.Url, 
                    IconColor = p.Color, 
                    IsSelected = p.Name == "DramaBox" 
                });
            }
            SelectedPlatform = Platforms.FirstOrDefault(p => p.Name == "DramaBox") ?? Platforms.First();
            _downloadRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _downloadRefreshTimer.Tick += async (_, _) => await LoadVisibleDownloadJobsAsync();
            _downloadRefreshTimer.Start();

            LogMsg("OK: runtime config ready.");
            LogMsg("Waiting for input...");
        }

        partial void OnSelectedPlatformChanged(PlatformItem? value)
        {
            if (value == null) return;
            foreach (var p in Platforms) p.IsSelected = false;
            value.IsSelected = true;
            
            Url = !string.IsNullOrEmpty(value.DefaultUrl) ? value.DefaultUrl : string.Empty;
            ShowsTrending = true;
            CurrentDramaTitle = "No drama loaded";
            CurrentPosterUrl = string.Empty;
            Episodes.Clear();
            UpdateSelectedCount();
            
            LogMsg($"Switched to platform: {value.Name}", "#00cec9");
        }

        [RelayCommand]
        private async Task FetchUrl()
        {
            if (string.IsNullOrWhiteSpace(Url))
            {
                LogMsg("Paste a drama URL or book ID before searching.", "#f39c12");
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;
            Episodes.Clear();
            AvailableQualities.Clear();
            SelectedQuality = string.Empty;
            ShowsTrending = false;
            LogMsg($"Fetching drama page: {Url}");

            try
            {
                var jobs = await _downloadService.AnalyzeUrlAsync(Url);
                if (jobs == null || !jobs.Any())
                {
                    LogMsg("Failed to analyze: No data returned from server.", "#ff7675");
                    return;
                }

                var firstJob = jobs.First();
                if (firstJob.Status == JobStatus.AnalysisFailed)
                {
                    LogMsg($"Failed to analyze: {firstJob.ErrorMessage}", "#ff7675");
                    return;
                }

                CurrentDramaTitle = firstJob.Title.Split('-').FirstOrDefault()?.Trim() ?? "Drama";
                CurrentPosterUrl = firstJob.ThumbnailUrl;
                TotalEpisodes = jobs.Count;

                var distinctResolutions = firstJob.AvailableVideoStreams
                    .Select(s => s.Resolution)
                    .Distinct()
                    .ToList();

                foreach (var res in distinctResolutions)
                {
                    AvailableQualities.Add(res);
                }
                
                if (AvailableQualities.Any())
                    SelectedQuality = AvailableQualities.First();

                foreach (var job in jobs)
                {
                    Episodes.Add(job);
                }
                
                LogMsg($"Found {TotalEpisodes} episodes. Available qualities: {string.Join(", ", AvailableQualities)}", "#fdcb6e");
                LogMsg($"OK: {CurrentDramaTitle} | {TotalEpisodes} episode(s)", "#00b894");
            }
            catch (Exception ex)
            {
                LogMsg($"Error: {ex.Message}", "#ff7675");
            }
            finally
            {
                UpdateSelectedCount();
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void BackToTrending()
        {
            ShowsTrending = true;
            Episodes.Clear();
            CurrentDramaTitle = "No drama loaded";
            CurrentPosterUrl = string.Empty;
            TotalEpisodes = 0;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectAllNodes()
        {
            foreach (var ep in Episodes)
                ep.IsSelected = true;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void SelectNoneNodes()
        {
            foreach (var ep in Episodes)
                ep.IsSelected = false;
            UpdateSelectedCount();
        }

        [RelayCommand]
        private void ToggleEpisodeSelection(DownloadJob job)
        {
            if (job != null)
            {
                job.IsSelected = !job.IsSelected;
                UpdateSelectedCount();
            }
        }

        [RelayCommand]
        private async Task CancelDownload(DownloadJob job)
        {
            if (job == null) return;
            try
            {
                await _downloadService.CancelJobAsync(job.Id);
                LogMsg($"Cancelled: {job.Title}", "#f39c12");
            }
            catch (Exception ex)
            {
                LogMsg($"Cancel error: {ex.Message}", "#ff7675");
            }
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = Episodes.Count(e => e.IsSelected);
        }

        private async Task LoadVisibleDownloadJobsAsync()
        {
            if (_visibleDownloadIds.Count == 0)
            {
                HasDownloadJobs = DownloadJobs.Count > 0;
                return;
            }

            var visibleJobs = (await _downloadService.GetHistoryAsync())
                .Where(job => _visibleDownloadIds.Contains(job.Id))
                .OrderByDescending(job => job.CreatedAt)
                .ToList();

            App.Current.Dispatcher.Invoke(() =>
            {
                DownloadJobs.Clear();
                foreach (var job in visibleJobs)
                {
                    DownloadJobs.Add(job);
                }
                HasDownloadJobs = DownloadJobs.Count > 0;
            });
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Logs.Clear();
        }

        [RelayCommand]
        private void CopyLog()
        {
            if (Logs == null || Logs.Count == 0)
            {
                LogMsg("No logs to copy.", "#f39c12");
                return;
            }

            try
            {
                var text = string.Join(Environment.NewLine, Logs.Select(l => l.Text));
                System.Windows.Clipboard.SetText(text);
                LogMsg("✅ All logs copied to clipboard.", "#00b894");
            }
            catch (Exception ex)
            {
                LogMsg($"Failed to copy logs: {ex.Message}", "#ff7675");
            }
        }

        [RelayCommand]
        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select download folder",
                InitialDirectory = Directory.Exists(SavePath)
                    ? SavePath
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)
            };

            if (dialog.ShowDialog() == true && Directory.Exists(dialog.FolderName))
            {
                SavePath = dialog.FolderName;
                LogMsg($"Download folder set to: {SavePath}", "#00cec9");
            }
        }

        private void LogMsg(string msg, string color = "#ffffff")
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Logs.Add(new LogMessage { Text = msg, Color = color });
            });
        }

        [RelayCommand]
        private async Task StartDownload()
        {
            if (!Episodes.Any(e => e.IsSelected))
            {
                LogMsg("No episodes selected to download.", "#f39c12");
                return;
            }

            IsBusy = true;
            try
            {
                var selectedJobs = Episodes
                    .Where(e => e.IsSelected && e.Status != JobStatus.AnalysisFailed)
                    .ToList();

                LogMsg($"Queuing {selectedJobs.Count} episode(s) for download...", "#d946ef");

                foreach (var job in selectedJobs)
                {
                    VideoStreamInfo? videoStream = null;
                    if (!string.IsNullOrEmpty(SelectedQuality))
                    {
                        videoStream = job.AvailableVideoStreams
                            .FirstOrDefault(s => s.Resolution == SelectedQuality)
                            ?? job.AvailableVideoStreams.FirstOrDefault();
                    }
                    else
                    {
                        videoStream = job.AvailableVideoStreams.FirstOrDefault();
                    }

                    AudioStreamInfo? audioStream = job.AvailableAudioStreams.FirstOrDefault();

                    string baseSavePath = !string.IsNullOrWhiteSpace(SavePath)
                        ? SavePath
                        : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

                    var dramaFolder = string.Join("_", CurrentDramaTitle.Split(Path.GetInvalidFileNameChars())).Trim();
                    string effectiveSavePath = Path.Combine(baseSavePath, dramaFolder);

                    var downloadPath = effectiveSavePath;
                    if (!string.IsNullOrWhiteSpace(job.SavePath) && !Path.IsPathRooted(job.SavePath))
                    {
                        downloadPath = Path.Combine(effectiveSavePath, job.SavePath);
                    }

                    _visibleDownloadIds.Add(job.Id);
                    await _downloadService.StartDownloadAsync(job.Id, videoStream, audioStream, downloadPath);
                }

                LogMsg($"{selectedJobs.Count} job(s) added to queue.", "#00b894");
                await LoadVisibleDownloadJobsAsync();
                Url = string.Empty;
            }
            catch (Exception ex)
            {
                LogMsg($"Download Error: {ex.Message}", "#ff7675");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
