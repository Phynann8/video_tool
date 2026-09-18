using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Start.Core.Models
{
    public enum JobStatus
    {
        PendingAnalysis,
        AnalysisFailed,
        Queued,
        Downloading,
        Processing,
        Completed,
        Failed,
        Cancelled,
        Paused
    }

    public class PlaylistInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int TotalItems { get; set; }
        public List<DownloadJob> Items { get; set; } = new();
    }

    public class DownloadJob : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Guid Id { get; set; } = Guid.NewGuid();
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ThumbnailUrl { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public JobStatus Status { get; set; } = JobStatus.PendingAnalysis;
        public double Progress { get; set; }
        public string Speed { get; set; } = string.Empty;
        public string Eta { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string SourcePlatform { get; set; } = string.Empty;
        public string SavePath { get; set; } = string.Empty;
        public string DownloadMetadata { get; set; } = string.Empty;

        public VideoStreamInfo? SelectedVideoStream { get; set; }
        public AudioStreamInfo? SelectedAudioStream { get; set; }
        public List<VideoStreamInfo> AvailableVideoStreams { get; set; } = new();
        public List<AudioStreamInfo> AvailableAudioStreams { get; set; } = new();
        public bool IsAudioOnly { get; set; }

        // Playlist support property
        public string? PlaylistId { get; set; }

        // Episode selection support
        public int EpisodeNumber { get; set; }
        public bool IsLocked { get; set; }

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }

        public string? OriginalPageUrl { get; set; }
        public DateTime? UrlExpiresAt { get; set; }
        public bool IsUrlExpired => UrlExpiresAt.HasValue && DateTime.UtcNow >= UrlExpiresAt.Value;

        public string? Cookies { get; set; }
        public string? Referer { get; set; }
        public string? UserAgent { get; set; }
    }

    public class VideoStreamInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Codec { get; set; } = string.Empty;

        public string DisplayName
        {
            get
            {
                var size = SizeBytes > 0 ? $"{SizeBytes / (1024d * 1024d):0.#} MB" : "Unknown size";
                var codec = string.IsNullOrWhiteSpace(Codec) ? "Unknown codec" : Codec;
                var ext = string.IsNullOrWhiteSpace(Extension) ? "?" : Extension.ToUpperInvariant();
                var resolution = string.IsNullOrWhiteSpace(Resolution) ? "Unknown resolution" : Resolution;
                return $"{resolution} ({ext}) - {size} - {codec}";
            }
        }
    }

    public class AudioStreamInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Bitrate { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Codec { get; set; } = string.Empty;

        public string DisplayName
        {
            get
            {
                var size = SizeBytes > 0 ? $"{SizeBytes / (1024d * 1024d):0.#} MB" : "Unknown size";
                var codec = string.IsNullOrWhiteSpace(Codec) ? "Unknown codec" : Codec;
                var ext = string.IsNullOrWhiteSpace(Extension) ? "?" : Extension.ToUpperInvariant();
                var bitrate = string.IsNullOrWhiteSpace(Bitrate) ? "Unknown bitrate" : Bitrate;
                return $"{bitrate} ({ext}) - {size} - {codec}";
            }
        }
    }

    public class IqiyiAccountCredential
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class DownloadProcessingSettings
    {
        public string DefaultDownloadPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        public int MaxSegmentWorkers { get; set; } = 16;
        public long MinSegmentSizeBytes { get; set; } = 2 * 1024 * 1024; // 2 MB
        public int MaxConcurrentDownloads { get; set; } = 3;
        public bool EnableClipboardMonitoring { get; set; } = true;
        public bool ExtractAudio { get; set; } = false;
        public string AudioFormat { get; set; } = "mp3";
        public bool EmbedMetadata { get; set; } = true;
        public bool EmbedThumbnail { get; set; } = true;
        public List<IqiyiAccountCredential> IqiyiAccounts { get; set; } = new()
        {
            new IqiyiAccountCredential { Label = "Primary", Email = "pongsawat_lee@hotmail.com", Password = "Lee5929354!@#", IsActive = true },
            new IqiyiAccountCredential { Label = "Primary Backup", Email = "dannydaemon666@yahoo.co.uk", Password = "cucumber666", IsActive = true },
            new IqiyiAccountCredential { Label = "Secondary Backup", Email = "kero_aum@hotmail.com", Password = "sichul13102", IsActive = true }
        };
    }
}

namespace Start.Core.Interfaces
{
    public interface IDownloadRepository
    {
        Task AddJobAsync(Models.DownloadJob job);
        Task UpdateJobAsync(Models.DownloadJob job);
        Task<Models.DownloadJob?> GetJobAsync(Guid id);
        Task<IEnumerable<Models.DownloadJob>> GetAllJobsAsync();
        Task<IEnumerable<Models.DownloadJob>> GetJobsByStatusAsync(IEnumerable<Models.JobStatus> statuses, int limit = 100)
        {
            return GetAllJobsAsync().ContinueWith(t => 
                (IEnumerable<Models.DownloadJob>)System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.Take(
                        System.Linq.Enumerable.Where(t.Result, j => System.Linq.Enumerable.Contains(statuses, j.Status)), 
                        limit)));
        }
    }

    public interface ISettingsRepository
    {
        Task<Models.DownloadProcessingSettings> LoadAsync();
        Task SaveAsync(Models.DownloadProcessingSettings settings);
    }

    public interface IMediaUrlRefresher
    {
        Task<bool> RefreshJobUrlAsync(Models.DownloadJob job, CancellationToken cancellationToken = default);
    }

    public interface IExtractorEngine
    {
        Task<List<Models.DownloadJob>> AnalyzeUrlAsync(string url);
    }

    public interface IDownloadEngine
    {
        Task StartDownloadAsync(Models.DownloadJob job, IProgress<Models.DownloadJob> progress, CancellationToken cancellationToken);
    }
    
    public interface IMediaProcessor
    {
        Task<string> MergeMediaAsync(string videoPath, string audioPath, string outputPath, CancellationToken cancellationToken);
    }
}
