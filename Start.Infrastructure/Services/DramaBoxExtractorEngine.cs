using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Services
{
    public class DramaBoxExtractorEngine : IExtractorEngine, IMediaUrlRefresher
    {
        private static readonly HttpClient _httpClient = CreateHttpClient();

        private readonly DramaboxClient _apiClient = new DramaboxClient();

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            // Extract Book ID
            string bookId = ExtractBookId(url);
            if (string.IsNullOrEmpty(bookId))
            {
                return new List<DownloadJob>();
            }

            // Combined strategy:
            // 1. HTML scraper gets the COMPLETE episode list (all episodes)
            // 2. Mobile API provides CDN links for as many episodes as it can
            // 3. Episodes missing CDN links store metadata for download-time resolution
            
            List<DownloadJob>? scraperJobs = null;
            List<DownloadJob>? apiJobs = null;

            // Step 1: Get full episode list from HTML scraper
            try
            {
                scraperJobs = await AnalyzeWithScraperAsync(url);
                if (scraperJobs != null)
                    Console.WriteLine($"DEBUG: HTML scraper found {scraperJobs.Count} episodes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: HTML scraper error: {ex.Message}");
            }

            // Step 2: Get CDN-enriched data from mobile API
            try
            {
                apiJobs = await AnalyzeWithApiClientAsync(url);
                if (apiJobs != null)
                {
                    int withUrls = apiJobs.Count(j => !string.IsNullOrEmpty(j.Url) && j.Url.StartsWith("http"));
                    Console.WriteLine($"DEBUG: Mobile API found {apiJobs.Count} episodes ({withUrls} with CDN URLs)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Mobile API error: {ex.Message}");
            }

            // Step 3: Merge results — prefer API jobs (they have CDN URLs), fill gaps from scraper
            if (apiJobs != null && apiJobs.Any() && scraperJobs != null && scraperJobs.Any())
            {
                // Build lookup from API results by episode number
                var apiLookup = apiJobs
                    .Where(j => !string.IsNullOrEmpty(j.Url) && j.Url.StartsWith("http"))
                    .ToDictionary(j => j.EpisodeNumber, j => j);

                // For each scraper job, check if we have a better API version
                foreach (var job in scraperJobs)
                {
                    if (apiLookup.TryGetValue(job.EpisodeNumber, out var apiJob))
                    {
                        // Merge API data into scraper job
                        job.Url = apiJob.Url;
                        job.AvailableVideoStreams = apiJob.AvailableVideoStreams;
                        job.SelectedVideoStream = apiJob.SelectedVideoStream;
                        job.DownloadMetadata = apiJob.DownloadMetadata;
                        job.IsLocked = false; // We have a URL, so it's effectively unlocked
                    }
                }

                int mergedWithUrl = scraperJobs.Count(j => !string.IsNullOrEmpty(j.Url) && j.Url.StartsWith("http"));
                Console.WriteLine($"DEBUG: Merged result: {scraperJobs.Count} episodes, {mergedWithUrl} with download URLs");
                return scraperJobs;
            }

            // If only API results available
            if (apiJobs != null && apiJobs.Any() && apiJobs.First().Status != JobStatus.AnalysisFailed)
                return apiJobs;

            // If only scraper results available
            if (scraperJobs != null && scraperJobs.Any())
                return scraperJobs;

            return new List<DownloadJob>();
        }



        private async Task<List<DownloadJob>?> AnalyzeWithScraperAsync(string url)
        {
            string bookId = ExtractBookId(url);
            if (string.IsNullOrEmpty(bookId)) return null;

            var candidates = new List<string> { url };
            candidates.Add($"https://www.dramabox.com/drama/{bookId}/drama");
            candidates.Add($"https://www.dramaboxdb.com/movie/{bookId}");
            candidates.Add($"https://www.dramabox.com/movie/{bookId}");

            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candUrl in candidates)
            {
                if (!seenUrls.Add(candUrl)) continue;

                try
                {
                    var response = await _httpClient.GetAsync(candUrl);
                    if (!response.IsSuccessStatusCode) continue;

                    string html = await response.Content.ReadAsStringAsync();
                    var match = Regex.Match(html, @"<script id=""__NEXT_DATA__"" type=""application/json"">(.*?)</script>", RegexOptions.Singleline);
                    
                    if (!match.Success) continue;

                    var nextData = JObject.Parse(match.Groups[1].Value);
                    var pageProps = nextData["props"]?["pageProps"];
                    
                    if (pageProps == null) continue;

                    var detailInfo = pageProps["detailInfo"] ?? pageProps["bookInfo"];
                    var bookInfo = detailInfo?["bookInfo"] ?? detailInfo;

                    var dramaDetail = new DramaBoxDetail
                    {
                        BookId = bookInfo?["bookId"]?.ToString() ?? bookId,
                        BookName = bookInfo?["bookName"]?.ToString() ?? bookInfo?["name"]?.ToString() ?? pageProps["bookName"]?.ToString() ?? ExtractTitleFromUrl(url),
                        CoverWap = bookInfo?["coverWap"]?.ToString() ?? string.Empty,
                        ChapterCount = bookInfo?["chapterCount"]?.ToObject<int>() ?? 0,
                        Introduction = bookInfo?["introduction"]?.ToString() ?? string.Empty
                    };

                    var chapterList = detailInfo?["chapterList"] ?? pageProps["chapterList"];
                    if (chapterList == null || !chapterList.Any())
                    {
                        chapterList = pageProps.SelectTokens("$..chapterList").FirstOrDefault() 
                                    ?? pageProps.SelectTokens("$..chapters").FirstOrDefault();
                    }

                    if (chapterList == null || !chapterList.Any()) continue;

                    var episodes = chapterList.ToObject<List<DramaBoxEpisode>>() ?? new List<DramaBoxEpisode>();
                    if (episodes.Any())
                    {
                        return BuildJobs(dramaDetail, episodes, url);
                    }
                }
                catch
                {
                    // Try next candidate
                }
            }

            return null;
        }

        private async Task<List<DownloadJob>?> AnalyzeWithApiClientAsync(string url)
        {
            string bookId = ExtractBookId(url);
            if (string.IsNullOrEmpty(bookId)) return null;

            try
            {
                // Ensure the client is initialized with the Master Token/Device ID
                await _apiClient.EnsureInitializedAsync();

                // We enable autoUnlock to simulate ad-watching for premium episodes
                var (episodes, bookName) = await _apiClient.GetEpisodesWithBookNameAsync(bookId, autoUnlock: false);
                if (episodes == null || !episodes.Any())
                {
                    return null;
                }

                var dramaTitle = !string.IsNullOrWhiteSpace(bookName)
                    ? bookName
                    : ExtractTitleFromUrl(url);

                var dramaDetail = new DramaBoxDetail
                {
                    BookId = bookId,
                    BookName = dramaTitle,
                };

                return BuildJobs(dramaDetail, episodes, url);
            }
            catch (Exception ex)
            {
                return new List<DownloadJob>
                {
                    new DownloadJob { Status = JobStatus.AnalysisFailed, ErrorMessage = $"API Fallback failed: {ex.Message}" }
                };
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }


        private static List<DownloadJob> BuildJobs(DramaBoxDetail? detail, List<DramaBoxEpisode> episodes, string sourceUrl)
        {
            var resultJobs = new List<DownloadJob>();
            string playlistId = Guid.NewGuid().ToString();
            string folderName = (detail?.BookName ?? "DramaBoxDownload").ReplaceInvalidPathChars();

            foreach (var item in episodes.OrderBy(e => e.ChapterIndex).Select((episode, index) => new { episode, index }))
            {
                var episode = item.episode;
                var displayNumber = item.index + 1;
                var episodeName = !string.IsNullOrWhiteSpace(episode.ChapterName)
                    ? episode.ChapterName
                    : $"Episode {displayNumber}";

                var job = new DownloadJob
                {
                    Title = $"{detail?.BookName ?? "Drama"} - {episodeName}",
                    ThumbnailUrl = episode.ChapterImg ?? detail?.CoverWap ?? string.Empty,
                    SourcePlatform = "DramaBox",
                    Status = JobStatus.Queued,
                    PlaylistId = playlistId,
                    EpisodeNumber = displayNumber,
                    IsLocked = episode.IsCharge == 1 || episode.ChargeChapter,
                    IsSelected = episode.IsCharge == 0 && !episode.ChargeChapter,
                    // SavePath carries the drama sub-folder name (relative); the download
                    // engine will combine it with the user's chosen base directory.
                    SavePath = folderName
                };

                var defaultCdn = episode.CdnList?.FirstOrDefault(c => c.IsDefault == 1)
                                 ?? episode.CdnList?.FirstOrDefault();

                if (defaultCdn?.VideoPathList != null && defaultCdn.VideoPathList.Any())
                {
                    var sortedVideos = defaultCdn.VideoPathList.OrderByDescending(v => v.Quality).ToList();
                    string cdnDomain = defaultCdn.CdnDomain?.TrimEnd('/') ?? string.Empty;

                    foreach (var videoPath in sortedVideos)
                    {
                        job.AvailableVideoStreams.Add(new VideoStreamInfo
                        {
                            Id = $"{episode.ChapterId}_{videoPath.Quality}p",
                            Resolution = $"{videoPath.Quality}p",
                            Extension = "mp4",
                            Codec = videoPath.Quality >= 1080 ? "H.264 (HD)" : "H.264",
                        });
                    }

                    var bestVideo = sortedVideos.FirstOrDefault();
                    if (bestVideo != null)
                    {
                        job.Url = MakeAbsoluteUrl(cdnDomain, bestVideo.VideoPath);
                        job.SelectedVideoStream = job.AvailableVideoStreams
                            .FirstOrDefault(s => s.Resolution == $"{bestVideo.Quality}p");
                    }
                }
                else if (!string.IsNullOrEmpty(episode.Mp4))
                {
                    // Fallback to web-based MP4 URL
                    job.Url = MakeAbsoluteUrl(string.Empty, episode.Mp4);
                    job.AvailableVideoStreams.Add(new VideoStreamInfo
                    {
                        Id = $"{episode.ChapterId}_720p",
                        Resolution = "720p",
                        Extension = "mp4",
                        Codec = "H.264",
                    });
                    job.SelectedVideoStream = job.AvailableVideoStreams.FirstOrDefault();
                }

                var videoMap = new Dictionary<string, string>();
                if (defaultCdn?.VideoPathList != null && defaultCdn.VideoPathList.Any())
                {
                    string cdnDomain = defaultCdn.CdnDomain?.TrimEnd('/') ?? string.Empty;
                    foreach (var vp in defaultCdn.VideoPathList)
                    {
                        videoMap[$"{vp.Quality}p"] = MakeAbsoluteUrl(cdnDomain, vp.VideoPath);
                    }
                }
                else if (!string.IsNullOrEmpty(episode.Mp4))
                {
                    videoMap["720p"] = MakeAbsoluteUrl(string.Empty, episode.Mp4);
                }
                
                // Store BookId and ChapterId in the metadata for unlocking logic later
                videoMap["__BookId"] = detail?.BookId ?? string.Empty;
                videoMap["__ChapterId"] = episode.ChapterId;

                job.DownloadMetadata = JsonConvert.SerializeObject(videoMap);
                resultJobs.Add(job);
            }

            return resultJobs;
        }

        public static string ExtractTitleFromUrl(string url)
        {
            var match = Regex.Match(
                url,
                @"(?:dramabox(?:db)?\.com)/(?:drama|movie)/\d+/([^/?#]+)|(?:dramabox(?:db)?\.com)/video/\d+_([^/?#]+)",
                RegexOptions.IgnoreCase);

            var rawTitle = match.Success
                ? (match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
                : "Drama";

            var normalized = rawTitle.Replace('-', ' ').Replace('_', ' ').Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return "Drama";
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
        }

        /// <summary>
        /// Combines a CDN domain with a video path, ensuring the result is always an absolute https URL.
        /// Handles: already-absolute URLs, protocol-relative URLs (//cdn...), and relative paths (/drm/...).
        /// If the video is encrypted (*.encrypt.mp4), it routes the URL through the Sansekai decryption proxy.
        /// </summary>
        public static string MakeAbsoluteUrl(string cdnDomain, string videoPath)
        {
            if (string.IsNullOrWhiteSpace(videoPath)) return string.Empty;

            // If it's already an absolute URL, return it directly
            if (videoPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return videoPath;

            // Handle protocol-relative URLs
            if (videoPath.StartsWith("//"))
                return "https:" + videoPath;

            if (string.IsNullOrWhiteSpace(cdnDomain)) return videoPath;

            string domain = cdnDomain.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? cdnDomain
                : "https://" + cdnDomain;

            return domain.TrimEnd('/') + "/" + videoPath.TrimStart('/');
        }

        public static string ExtractBookId(string url)
        {
            // Aggressive regex to find the numeric ID in any Dramabox-like URL
            var match = Regex.Match(
                url,
                @"(?:drama|movie|video|book|play|gifted/film).*?[=/](\d+)",
                RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            if (Regex.IsMatch(url.Trim(), @"^\d+$"))
                return url.Trim();

            return string.Empty;
        }

        public static DateTime? TryParseUrlExpiry(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var match = Regex.Match(url, @"[?&](?:Expires|exp|token_exp)=(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && long.TryParse(match.Groups[1].Value, out var epoch))
            {
                return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }
            return null;
        }

        public async Task<bool> RefreshJobUrlAsync(DownloadJob job, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(job.Url) && string.IsNullOrWhiteSpace(job.OriginalPageUrl))
                return false;

            try
            {
                var targetUrl = !string.IsNullOrWhiteSpace(job.OriginalPageUrl) ? job.OriginalPageUrl : job.Url;
                var jobs = await AnalyzeUrlAsync(targetUrl);
                var match = jobs.FirstOrDefault(j => j.EpisodeNumber == job.EpisodeNumber || j.Title == job.Title) ?? jobs.FirstOrDefault();
                if (match != null && !string.IsNullOrWhiteSpace(match.Url) && match.Url != job.Url)
                {
                    job.Url = match.Url;
                    job.UrlExpiresAt = match.UrlExpiresAt;
                    return true;
                }
            }
            catch
            {
                // Fallback
            }
            return false;
        }
    }

    internal static class StringExtensions
    {
        public static string ReplaceInvalidPathChars(this string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
        }
    }

    public class DramaBoxDetail
    {
        [JsonProperty("bookId")] public string BookId { get; set; } = string.Empty;
        [JsonProperty("bookName")] public string BookName { get; set; } = string.Empty;
        [JsonProperty("name")] public string Name { set { if (string.IsNullOrEmpty(BookName)) BookName = value; } }
        [JsonProperty("coverWap")] public string CoverWap { get; set; } = string.Empty;
        [JsonProperty("cover")] public string Cover { set { if (string.IsNullOrEmpty(CoverWap)) CoverWap = value; } }
        [JsonProperty("chapterCount")] public int ChapterCount { get; set; }
        [JsonProperty("introduction")] public string Introduction { get; set; } = string.Empty;
    }

}
