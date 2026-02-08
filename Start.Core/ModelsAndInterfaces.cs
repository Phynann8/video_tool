using System;
using System.Collections.Generic;

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
        Cancelled
    }

    public class DownloadJob
    {
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
        
        public VideoStreamInfo? SelectedVideoStream { get; set; }
        public AudioStreamInfo? SelectedAudioStream { get; set; }
        public List<VideoStreamInfo> AvailableVideoStreams { get; set; } = new();
        public List<AudioStreamInfo> AvailableAudioStreams { get; set; } = new();
        public bool IsAudioOnly { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? CompletedAt { get; set; }
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
}

namespace Start.Core.Interfaces
{
    public interface IDownloadRepository
    {
        Task AddJobAsync(Models.DownloadJob job);
        Task UpdateJobAsync(Models.DownloadJob job);
        Task<Models.DownloadJob?> GetJobAsync(Guid id);
        Task<IEnumerable<Models.DownloadJob>> GetAllJobsAsync();
    }

    public interface IExtractorEngine
    {
        Task<Models.DownloadJob> AnalyzeUrlAsync(string url);
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
