using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Services
{
    public sealed class NetShortExtractorEngine : IExtractorEngine
    {
        private static readonly Regex EpisodeUrl = new(
            @"^https?://(?:www\.)?netshort\.com/episode/[^/?#]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NextData = new(
            @"<script[^>]+id=[""']__NEXT_DATA__[""'][^>]*>(.*?)</script>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex NuxtData = new(
            @"window\.__NUXT__\s*=\s*(.*?);</script>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly string[] MediaKeys =
        {
            "playVoucher", "videoUrl", "video_url", "playUrl", "play_url", "url"
        };

        private readonly HttpClient _httpClient;

        public NetShortExtractorEngine()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36");
        }

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            if (!EpisodeUrl.IsMatch(url))
                return Failed(url, "NetShort extractor expects an episode URL.");

            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var root = ParsePageData(html);
                var mediaUrls = FindMediaUrls(root).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (mediaUrls.Count == 0)
                    return Failed(url, "NetShort page did not expose a playable media URL.");

                var title = ExtractTitle(url);
                return mediaUrls.Select((mediaUrl, index) => new DownloadJob
                {
                    Url = mediaUrl,
                    Title = mediaUrls.Count == 1 ? title : $"{title} - Part {index + 1}",
                    Status = JobStatus.PendingAnalysis,
                    SourcePlatform = "NetShort",
                    AvailableVideoStreams = new List<VideoStreamInfo>
                    {
                        new() { Id = "direct", Resolution = "Original", Extension = "mp4" }
                    },
                    SelectedVideoStream = new VideoStreamInfo
                    {
                        Id = "direct", Resolution = "Original", Extension = "mp4"
                    }
                }).ToList();
            }
            catch (Exception ex)
            {
                return Failed(url, $"NetShort extraction failed: {ex.Message}");
            }
        }

        private static JObject ParsePageData(string html)
        {
            var nextMatch = NextData.Match(html);
            if (nextMatch.Success)
                return JObject.Parse(nextMatch.Groups[1].Value);

            var nuxtMatch = NuxtData.Match(html);
            if (nuxtMatch.Success)
                return JObject.Parse(nuxtMatch.Groups[1].Value);

            throw new InvalidOperationException("No __NEXT_DATA__ or __NUXT__ payload was found.");
        }

        private static IEnumerable<string> FindMediaUrls(JToken token)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (MediaKeys.Contains(property.Name, StringComparer.OrdinalIgnoreCase) &&
                        property.Value.Type == JTokenType.String)
                    {
                        var value = property.Value.Value<string>();
                        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                            yield return value!;
                    }

                    foreach (var nested in FindMediaUrls(property.Value))
                        yield return nested;
                }
            }
            else if (token is JArray array)
            {
                foreach (var item in array)
                foreach (var nested in FindMediaUrls(item))
                    yield return nested;
            }
        }

        private static string ExtractTitle(string url)
        {
            var segment = url.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "NetShort";
            return Regex.Replace(segment, "-\\d+$", string.Empty)
                .Replace('-', ' ')
                .Trim()
                .Replace("  ", " ");
        }

        private static List<DownloadJob> Failed(string url, string message) => new()
        {
            new DownloadJob { Url = url, Status = JobStatus.AnalysisFailed, ErrorMessage = message }
        };
    }
}
