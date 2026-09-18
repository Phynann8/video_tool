using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class DramaBoxExtractorTests
    {
        [Theory]
        [InlineData("https://www.dramaboxdb.com/drama/42000009462/When-Lies-Speak-Again-DUBBED", "42000009462")]
        [InlineData("https://www.dramabox.com/drama/42000009462/When-Lies-Speak-Again-DUBBED", "42000009462")]
        [InlineData("https://www.dramaboxdb.com/movie/42000009462", "42000009462")]
        [InlineData("https://www.dramaboxdb.com/movie/42000009462/when-lies-speak-again-dubbed", "42000009462")]
        [InlineData("https://www.dramabox.com/video/42000009462_When-Lies-Speak-Again/700607239_Episode-1", "42000009462")]
        [InlineData("https://www.dramabox.com/book/42000009462", "42000009462")]
        [InlineData("42000009462", "42000009462")]
        public void ExtractBookId_ParsesVariousUrlFormats(string url, string expectedId)
        {
            var result = DramaBoxExtractorEngine.ExtractBookId(url);
            Assert.Equal(expectedId, result);
        }

        [Theory]
        [InlineData("https://www.dramabox.com/drama/42000009462/When-Lies-Speak-Again-DUBBED", "When Lies Speak Again Dubbed")]
        [InlineData("https://www.dramabox.com/video/42000009462_When-Lies-Speak-Again", "When Lies Speak Again")]
        public void ExtractTitleFromUrl_ExtractsNormalizedTitle(string url, string expectedTitle)
        {
            var title = DramaBoxExtractorEngine.ExtractTitleFromUrl(url);
            Assert.Equal(expectedTitle, title);
        }

        [Theory]
        [InlineData("https://cdn.example.com", "/video/ep1.mp4", "https://cdn.example.com/video/ep1.mp4")]
        [InlineData("cdn.example.com", "video/ep1.mp4", "https://cdn.example.com/video/ep1.mp4")]
        [InlineData("", "https://direct.example.com/ep1.mp4", "https://direct.example.com/ep1.mp4")]
        [InlineData("cdn.example.com", "//direct.example.com/ep1.mp4", "https://direct.example.com/ep1.mp4")]
        public void MakeAbsoluteUrl_BuildsCorrectHttpsUrl(string domain, string path, string expected)
        {
            var url = DramaBoxExtractorEngine.MakeAbsoluteUrl(domain, path);
            Assert.Equal(expected, url);
        }

        [Fact]
        public void DramaBoxEpisode_DeserializesWebScraperFormat()
        {
            // Web scraper __NEXT_DATA__ format uses "id", "name", "index", "unlock", "mp4"
            string json = @"
            {
                ""id"": ""700607239"",
                ""name"": ""Episode 1"",
                ""index"": 0,
                ""unlock"": true,
                ""mp4"": ""https://hwvideoseo.dramaboxdb.com/sample.mp4""
            }";

            var episode = JsonConvert.DeserializeObject<DramaBoxEpisode>(json);
            Assert.NotNull(episode);
            Assert.Equal("700607239", episode.ChapterId);
            Assert.Equal("Episode 1", episode.ChapterName);
            Assert.Equal(0, episode.ChapterIndex);
            Assert.Equal(0, episode.IsCharge); // unlock: true -> IsCharge: 0
            Assert.Equal("https://hwvideoseo.dramaboxdb.com/sample.mp4", episode.Mp4);
        }

        [Fact]
        public void DramaBoxEpisode_DeserializesMobileApiFormat()
        {
            // Mobile API format uses "chapterId", "chapterName", "chapterIndex", "isCharge", "cdnList"
            string json = @"
            {
                ""chapterId"": ""1001"",
                ""chapterName"": ""Chapter 1"",
                ""chapterIndex"": 1,
                ""isCharge"": 1,
                ""cdnList"": [
                    {
                        ""cdnDomain"": ""https://video.dramaboxdb.com"",
                        ""isDefault"": 1,
                        ""videoPathList"": [
                            { ""quality"": 720, ""videoPath"": ""/720p.mp4"" }
                        ]
                    }
                ]
            }";

            var episode = JsonConvert.DeserializeObject<DramaBoxEpisode>(json);
            Assert.NotNull(episode);
            Assert.Equal("1001", episode.ChapterId);
            Assert.Equal("Chapter 1", episode.ChapterName);
            Assert.Equal(1, episode.ChapterIndex);
            Assert.Equal(1, episode.IsCharge);
            Assert.Single(episode.CdnList);
            Assert.Equal("https://video.dramaboxdb.com", episode.CdnList[0].CdnDomain);
        }

        [Fact]
        public async Task DramaboxClient_ReportsUninitializedGracefullyWithoutCrashing()
        {
            var client = new DramaboxClient();
            // Client without token should indicate uninitialized
            Assert.False(client.IsInitialized);

            // PostAsync without initialization should return safe error object instead of throwing
            var result = await client.PostAsync("/test/endpoint", new { test = 1 });
            Assert.NotNull(result);
            Assert.False(result["success"]?.Value<bool>() ?? true);
            Assert.Equal(403, result["code"]?.Value<int>());
        }

        [Fact]
        public async Task DramaBoxExtractorEngine_AnalyzeUrlAsync_FallsBackAndExtractsFromDramaboxDbUrl()
        {
            // dramaboxdb.com/drama/... returns 404 upstream, but our engine must automatically
            // fall back to candidates and extract the episode list without failing.
            var engine = new DramaBoxExtractorEngine();
            var jobs = await engine.AnalyzeUrlAsync("https://www.dramaboxdb.com/drama/42000009462/When-Lies-Speak-Again-DUBBED");

            Assert.NotNull(jobs);
            Assert.NotEmpty(jobs);
            Assert.All(jobs, j => Assert.Equal("DramaBox", j.SourcePlatform));
            
            // Should have unlocked episodes with playable media URLs
            var unlockedWithUrls = jobs.Where(j => !string.IsNullOrEmpty(j.Url) && j.Url.StartsWith("http")).ToList();
            Assert.NotEmpty(unlockedWithUrls);
            Assert.Contains(unlockedWithUrls, j => j.Url.Contains(".mp4"));
        }
    }
}
