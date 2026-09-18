using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Services
{
    public class RoutingDownloadEngine : IDownloadEngine
    {
        private readonly YtDlpDownloadEngine _ytDlpDownloadEngine;
        private readonly HttpDownloadEngine _httpDownloadEngine;

        public RoutingDownloadEngine(
            YtDlpDownloadEngine ytDlpDownloadEngine,
            HttpDownloadEngine httpDownloadEngine)
        {
            _ytDlpDownloadEngine = ytDlpDownloadEngine;
            _httpDownloadEngine = httpDownloadEngine;
        }

        public async Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
        {
            if (string.Equals(job.SourcePlatform, "DramaBox", StringComparison.OrdinalIgnoreCase))
            {
                // If this DramaBox episode has no URL (locked episode), try resolving it via API
                if (string.IsNullOrEmpty(job.Url) || !job.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"DEBUG: Resolving locked episode URL for: {job.Title}");
                    job.Status = JobStatus.Processing;
                    job.Speed = "Resolving...";
                    progress.Report(job);

                    bool resolved = await TryResolveLockedEpisodeUrlAsync(job);
                    if (!resolved)
                    {
                        throw new InvalidOperationException(
                            $"Could not resolve download URL for locked episode: {job.Title}. " +
                            "The mobile API did not return CDN data for this episode.");
                    }
                    Console.WriteLine($"DEBUG: Resolved URL for {job.Title}");
                }

                await _httpDownloadEngine.StartDownloadAsync(job, progress, cancellationToken);
                return;
            }

            await _ytDlpDownloadEngine.StartDownloadAsync(job, progress, cancellationToken);
        }

        /// <summary>
        /// Attempts to re-fetch CDN data for a locked episode using the mobile API.
        /// The bookId and chapterId are stored in the job's DownloadMetadata during analysis.
        /// </summary>
        private async Task<bool> TryResolveLockedEpisodeUrlAsync(DownloadJob job)
        {
            try
            {
                if (string.IsNullOrEmpty(job.DownloadMetadata)) return false;

                var metadata = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.DownloadMetadata);
                if (metadata == null) return false;

                string bookId = metadata.GetValueOrDefault("__BookId", "");
                string chapterId = metadata.GetValueOrDefault("__ChapterId", "");
                
                if (string.IsNullOrEmpty(bookId) || string.IsNullOrEmpty(chapterId))
                    return false;

                var apiClient = new DramaboxClient();
                await apiClient.EnsureInitializedAsync();

                // Fetch episodes from the API — the batch/load endpoint returns CDN data
                var (episodes, _) = await apiClient.GetEpisodesWithBookNameAsync(bookId, autoUnlock: false);
                if (episodes == null) return false;

                // Find our specific episode
                var episode = episodes.FirstOrDefault(e => e.ChapterId == chapterId);
                if (episode == null) return false;

                // Extract CDN URL
                var defaultCdn = episode.CdnList?.FirstOrDefault(c => c.IsDefault == 1)
                                 ?? episode.CdnList?.FirstOrDefault();

                if (defaultCdn?.VideoPathList != null && defaultCdn.VideoPathList.Any())
                {
                    var bestVideo = defaultCdn.VideoPathList
                        .OrderByDescending(v => v.Quality)
                        .FirstOrDefault();
                    
                    if (bestVideo != null)
                    {
                        string cdnDomain = defaultCdn.CdnDomain?.TrimEnd('/') ?? "";
                        job.Url = MakeAbsoluteUrl(cdnDomain, bestVideo.VideoPath);
                        
                        // Update video stream info
                        if (!job.AvailableVideoStreams.Any())
                        {
                            job.AvailableVideoStreams.Add(new VideoStreamInfo
                            {
                                Id = $"{chapterId}_{bestVideo.Quality}p",
                                Resolution = $"{bestVideo.Quality}p",
                                Extension = "mp4",
                                Codec = "H.264"
                            });
                            job.SelectedVideoStream = job.AvailableVideoStreams.First();
                        }
                        
                        // Also update the metadata map with the resolved URLs
                        var videoMap = new Dictionary<string, string>(metadata);
                        foreach (var vp in defaultCdn.VideoPathList)
                        {
                            videoMap[$"{vp.Quality}p"] = MakeAbsoluteUrl(cdnDomain, vp.VideoPath);
                        }
                        job.DownloadMetadata = JsonConvert.SerializeObject(videoMap);
                        
                        return !string.IsNullOrEmpty(job.Url) && job.Url.StartsWith("http");
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Failed to resolve locked episode: {ex.Message}");
                return false;
            }
        }

        private static string MakeAbsoluteUrl(string cdnDomain, string videoPath)
        {
            if (string.IsNullOrWhiteSpace(videoPath)) return string.Empty;
            if (videoPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return videoPath;
            if (videoPath.StartsWith("//")) return "https:" + videoPath;
            if (string.IsNullOrWhiteSpace(cdnDomain)) return videoPath;

            string domain = cdnDomain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? cdnDomain : "https://" + cdnDomain;
            return domain.TrimEnd('/') + "/" + videoPath.TrimStart('/');
        }
    }
}
