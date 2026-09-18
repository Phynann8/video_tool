using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Services
{
    public class IflixExtractorEngine : IExtractorEngine
    {
        private readonly HttpClient _httpClient;
        private readonly IExtractorEngine _ytDlpEngine;

        public IflixExtractorEngine() : this(new HttpClient(), new YtDlpExtractorEngine())
        {
        }

        public IflixExtractorEngine(HttpClient httpClient, IExtractorEngine ytDlpEngine)
        {
            _httpClient = httpClient;
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
            }
            _ytDlpEngine = ytDlpEngine;
        }

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            // For individual episode URLs (has both album ID and episode ID in path),
            // pass directly to yt-dlp — it will use --no-playlist automatically
            if (!url.Contains("/album/"))
            {
                return await _ytDlpEngine.AnalyzeUrlAsync(url);
            }

            // For /album/ URLs, convert to the /play/ series URL that yt-dlp's
            // IflixSeries extractor understands. This extractor natively paginates
            // and discovers ALL episodes (not just the first 20 in the HTML).
            var seriesPlayUrl = await ConvertAlbumToSeriesPlayUrl(url);

            Console.WriteLine($"DEBUG: Converted album URL to series play URL: {seriesPlayUrl}");

            // yt-dlp IflixSeries extractor will return a playlist with all episodes
            var allJobs = await _ytDlpEngine.AnalyzeUrlAsync(seriesPlayUrl);

            // Filter out failed analyses
            var successfulJobs = allJobs
                .Where(j => j.Status != JobStatus.AnalysisFailed)
                .ToList();

            if (!successfulJobs.Any())
            {
                throw new Exception("No episodes could be extracted from this album.");
            }

            // Sort by title (EP01, EP02, etc.)
            return successfulJobs.OrderBy(j => j.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Converts an /album/ URL to a /play/ series URL.
        /// Album URL:  https://www.iflix.com/en/album/2n6ypc2x23zbeh5
        /// Series URL: https://www.iflix.com/en/play/2n6ypc2x23zbeh5-SeriesName
        /// The series play URL triggers yt-dlp's IflixSeries extractor which finds all episodes.
        /// </summary>
        private async Task<string> ConvertAlbumToSeriesPlayUrl(string albumUrl)
        {
            // Extract album ID from URL
            // e.g. https://www.iflix.com/en/album/2n6ypc2x23zbeh5 -> 2n6ypc2x23zbeh5
            var albumIdMatch = Regex.Match(albumUrl, @"/album/([^/?#]+)");
            if (!albumIdMatch.Success)
            {
                throw new Exception($"Could not extract album ID from URL: {albumUrl}");
            }
            var albumId = albumIdMatch.Groups[1].Value;

            // Extract the language prefix (e.g. /en/)
            var langMatch = Regex.Match(albumUrl, @"iflix\.com(/[^/]+)/album/");
            var langPrefix = langMatch.Success ? langMatch.Groups[1].Value : "/en";

            // Fetch the album page to find the series play link
            var response = await _httpClient.GetStringAsync(albumUrl);

            // Look for the series play link (the generic one without an episode ID)
            // Pattern: /en/play/2n6ypc2x23zbeh5-The_Street_Vendor's_Secret_Identity
            // This is the "Play" button link, which has the album ID followed by a dash and name
            var playLinkPattern = $@"href=""({langPrefix}/play/{Regex.Escape(albumId)}-[^""]+)""";
            var playMatch = Regex.Match(response, playLinkPattern);

            if (playMatch.Success)
            {
                var path = playMatch.Groups[1].Value;
                return $"https://www.iflix.com{path}";
            }

            // Fallback: try a broader pattern
            var fallbackPattern = $@"href=""([^""]*?/play/{Regex.Escape(albumId)}-[^""]+)""";
            playMatch = Regex.Match(response, fallbackPattern);

            if (playMatch.Success)
            {
                var path = playMatch.Groups[1].Value;
                return path.StartsWith("http") ? path : $"https://www.iflix.com{path}";
            }

            // Last resort: construct the play URL with just the album ID
            // yt-dlp may still recognize it
            return $"https://www.iflix.com{langPrefix}/play/{albumId}";
        }
    }
}

