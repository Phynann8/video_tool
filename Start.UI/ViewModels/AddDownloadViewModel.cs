using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Start.App.Services;
using Start.Core.Models;

namespace Start.UI.ViewModels
{
    public partial class AddDownloadViewModel : ObservableObject
    {
        private readonly IDownloadService _downloadService;

        [ObservableProperty]
        private string _url = string.Empty;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private DownloadJob? _analyzedJob;

        [ObservableProperty]
        private ObservableCollection<VideoStreamInfo> _videoStreams = new();

        [ObservableProperty]
        private ObservableCollection<AudioStreamInfo> _audioStreams = new();

        [ObservableProperty]
        private VideoStreamInfo? _selectedVideoStream;

        [ObservableProperty]
        private AudioStreamInfo? _selectedAudioStream;

        [ObservableProperty]
        private bool _isAudioOnly;

        [ObservableProperty]
        private string _errorMessage = string.Empty;

        public event EventHandler? OnDownloadStarted;

        public AddDownloadViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;
        }

        [RelayCommand]
        private async Task AnalyzeUrl()
        {
            if (string.IsNullOrWhiteSpace(Url)) return;

            IsBusy = true;
            ErrorMessage = string.Empty;
            AnalyzedJob = null;

            try
            {
                var analyzedJob = await _downloadService.AnalyzeUrlAsync(Url);

                if (analyzedJob.Status == JobStatus.AnalysisFailed)
                {
                    ErrorMessage = string.IsNullOrWhiteSpace(analyzedJob.ErrorMessage)
                        ? "Failed to analyze the provided URL."
                        : analyzedJob.ErrorMessage;
                    return;
                }

                AnalyzedJob = analyzedJob;
                VideoStreams = new ObservableCollection<VideoStreamInfo>(analyzedJob.AvailableVideoStreams);
                AudioStreams = new ObservableCollection<AudioStreamInfo>(analyzedJob.AvailableAudioStreams);
                SelectedVideoStream = VideoStreams.FirstOrDefault();
                SelectedAudioStream = AudioStreams.FirstOrDefault();
                IsAudioOnly = false;
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task StartDownload()
        {
            if (AnalyzedJob == null) return;
            if (AnalyzedJob.Status == JobStatus.AnalysisFailed) return;

            try
            {
                var videoStream = IsAudioOnly
                    ? null
                    : SelectedVideoStream ?? AnalyzedJob.SelectedVideoStream ?? VideoStreams.FirstOrDefault();

                var audioStream = SelectedAudioStream ?? AnalyzedJob.SelectedAudioStream ?? AudioStreams.FirstOrDefault();

                if (IsAudioOnly && audioStream == null)
                {
                    throw new InvalidOperationException("No audio stream is available for this media.");
                }

                var savePath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos); 

                AnalyzedJob.SelectedVideoStream = videoStream;
                AnalyzedJob.SelectedAudioStream = audioStream;
                AnalyzedJob.IsAudioOnly = IsAudioOnly;

                await _downloadService.StartDownloadAsync(AnalyzedJob.Id, videoStream, audioStream, savePath);
                
                Url = string.Empty;
                AnalyzedJob = null;
                VideoStreams.Clear();
                AudioStreams.Clear();
                SelectedVideoStream = null;
                SelectedAudioStream = null;
                IsAudioOnly = false;
                
                OnDownloadStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
