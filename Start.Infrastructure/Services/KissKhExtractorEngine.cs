using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;
using Newtonsoft.Json.Linq;

namespace Start.Infrastructure.Services
{
    public class KissKhExtractorEngine : IExtractorEngine
    {
        private readonly HttpClient _httpClient;
        private const string BaseDomain = "https://kisskh.co";
        private const string VideoBaseUrl = "https://hls03.videodelivery2.site";

        public KissKhExtractorEngine(HttpClient? httpClient = null)
        {
            _httpClient = httpClient ?? new HttpClient();
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
            }
        }

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            // URL Format: https://kisskh.co/Drama/The-First-Frost?id=8316
            var dramaIdMatch = Regex.Match(url, @"id=(\d+)");
            if (!dramaIdMatch.Success)
            {
                throw new Exception("Could not find Drama ID in the URL. Please make sure it contains '?id=XXXX'.");
            }

            string dramaId = dramaIdMatch.Groups[1].Value;
            string dramaSlug = url.Split('/').Last().Split('?').First();

            // 1. Fetch Drama Metadata to get the episode list
            // API: https://kisskh.co/api/DramaList/Drama/8316?isq=false
            string apiUrl = $"{BaseDomain}/api/DramaList/Drama/{dramaId}?isq=false";
            
            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Add("Referer", url);
            request.Headers.Add("Origin", BaseDomain);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch drama metadata from KissKH API. Status: {response.StatusCode}");
            }

            string jsonContent = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(jsonContent);
            
            var episodes = data["episodes"] as JArray;
            if (episodes == null || !episodes.Any())
            {
                throw new Exception("No episodes found for this drama.");
            }

            string dramaTitle = data["title"]?.ToString() ?? dramaSlug;
            string thumbnailUrl = data["thumbnail"]?.ToString();

            // 2. Generate Jobs
            var jobs = new List<DownloadJob>();
            foreach (var ep in episodes)
            {
                int epNum = ep["number"]?.Value<int>() ?? 0;
                if (epNum == 0) continue;

                // Pattern: https://hls03.videodelivery2.site/8316/The-First-Frost.Ep1.mp4
                string videoUrl = $"{VideoBaseUrl}/{dramaId}/{dramaSlug}.Ep{epNum}.mp4";

                var job = new DownloadJob
                {
                    Id = Guid.NewGuid(),
                    Url = videoUrl,
                    Title = $"{dramaTitle} - Episode {epNum}",
                    ThumbnailUrl = thumbnailUrl,
                    Status = JobStatus.PendingAnalysis,
                    SourcePlatform = "KissKH",
                    AvailableVideoStreams = new List<VideoStreamInfo>
                    {
                        new VideoStreamInfo
                        {
                            Id = "original",
                            Resolution = "1080P/720P (Direct)",
                            Extension = "mp4"
                        }
                    }
                };
                
                job.SelectedVideoStream = job.AvailableVideoStreams.First();
                jobs.Add(job);
            }

            return jobs.OrderBy(j => j.Title).ToList();
        }
    }
}
