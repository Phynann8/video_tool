using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Helpers;
using Start.Infrastructure.Models.YtDlp;

namespace Start.Infrastructure.Services
{
    public class YtDlpExtractorEngine : IExtractorEngine
    {
        private const string YtDlpExecutable = "yt-dlp.exe"; // Assumes in PATH or working dir

        public async Task<DownloadJob> AnalyzeUrlAsync(string url)
        {
            // Command: yt-dlp -J --flat-playlist --no-warnings <url>
            // -J: Dump JSON
            // --flat-playlist: If it's a playlist, don't list all videos fully, just basic info (for now handling single video focus)
            
            var arguments = $"-J --no-warnings \"{url}\"";
            
            try 
            {
                var jsonOutput = await ProcessRunner.RunProcessAsync(YtDlpExecutable, arguments, CancellationToken.None);
                var metadata = JsonConvert.DeserializeObject<YtDlpMetadata>(jsonOutput);

                if (metadata == null) throw new Exception("Failed to parse yt-dlp output.");

                var videoStreams = metadata.Formats
                    .Where(f =>
                        !string.Equals(f.VideoCodec, "none", StringComparison.OrdinalIgnoreCase) &&
                        f.Height.HasValue)
                    .Select(f => new VideoStreamInfo
                    {
                        Id = f.FormatId,
                        Resolution = $"{f.Width ?? 0}x{f.Height ?? 0}",
                        Extension = f.Extension,
                        SizeBytes = f.FileSize ?? f.FileSizeApprox ?? 0,
                        Codec = f.VideoCodec
                    })
                    .OrderByDescending(v => GetVerticalResolution(v.Resolution))
                    .ThenByDescending(v => v.SizeBytes)
                    .ToList();

                var audioStreams = metadata.Formats
                    .Where(f =>
                        string.Equals(f.VideoCodec, "none", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(f.AudioCodec, "none", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new AudioStreamInfo
                    {
                        Id = f.FormatId,
                        Bitrate = f.Bitrate.HasValue ? $"{Math.Round(f.Bitrate.Value)} kbps" : "Unknown",
                        Extension = f.Extension,
                        SizeBytes = f.FileSize ?? f.FileSizeApprox ?? 0,
                        Codec = f.AudioCodec
                    })
                    .OrderByDescending(a => GetBitrate(a.Bitrate))
                    .ThenByDescending(a => a.SizeBytes)
                    .ToList();

                var job = new DownloadJob
                {
                    Id = Guid.NewGuid(),
                    Url = url,
                    Title = metadata.Title,
                    ThumbnailUrl = metadata.Thumbnail,
                    Duration = TimeSpan.FromSeconds(metadata.Duration),
                    Status = JobStatus.PendingAnalysis,
                    SourcePlatform = "Detected",
                    AvailableVideoStreams = videoStreams,
                    AvailableAudioStreams = audioStreams,
                    SelectedVideoStream = videoStreams.FirstOrDefault(),
                    SelectedAudioStream = audioStreams.FirstOrDefault()
                };
                
                return job;
            }
            catch (Exception ex)
            {
                // Return a job in failed state or rethrow? 
                // Interface says Task<DownloadJob>, typically we return the job with error info or throw.
                // Creating a failed job object might be better for UI handling.
                return new DownloadJob 
                { 
                    Url = url, 
                    Status = JobStatus.AnalysisFailed, 
                    ErrorMessage = ex.Message 
                };
            }
        }

        private static int GetVerticalResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution)) return 0;
            var parts = resolution.Split('x');
            if (parts.Length != 2) return 0;
            return int.TryParse(parts[1], out var height) ? height : 0;
        }

        private static double GetBitrate(string bitrateLabel)
        {
            if (string.IsNullOrWhiteSpace(bitrateLabel)) return 0;
            var value = bitrateLabel.Split(' ')[0];
            return double.TryParse(value, out var bitrate) ? bitrate : 0;
        }
    }
}
