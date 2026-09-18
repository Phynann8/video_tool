using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class PlatformExtractorIntegrationTests
    {
        #region KissKH Extractor Tests

        [Fact]
        public async Task KissKhExtractor_SuccessfulAlbumExtraction_ParsesEpisodesAndBuildsCdnStreamUrls()
        {
            var mockHandler = new MockHttpMessageHandler((req) =>
            {
                Assert.Equal(HttpMethod.Get, req.Method);
                Assert.Equal("https://kisskh.co/api/DramaList/Drama/8316?isq=false", req.RequestUri?.ToString());
                Assert.Contains("kisskh.co", req.Headers.Referrer?.ToString());
                Assert.Equal("https://kisskh.co", req.Headers.GetValues("Origin").FirstOrDefault());

                string jsonResponse = @"{
                    ""title"": ""The First Frost"",
                    ""thumbnail"": ""https://static.kisskh.co/poster/8316.jpg"",
                    ""episodes"": [
                        { ""number"": 1, ""id"": 101 },
                        { ""number"": 2, ""id"": 102 },
                        { ""number"": 3, ""id"": 103 }
                    ]
                }";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var extractor = new KissKhExtractorEngine(httpClient);

            string albumUrl = "https://kisskh.co/Drama/The-First-Frost?id=8316";
            var jobs = await extractor.AnalyzeUrlAsync(albumUrl);

            Assert.NotNull(jobs);
            Assert.Equal(3, jobs.Count);

            // Verify metadata
            Assert.Equal("The First Frost - Episode 1", jobs[0].Title);
            Assert.Equal("https://hls03.videodelivery2.site/8316/The-First-Frost.Ep1.mp4", jobs[0].Url);
            Assert.Equal("https://static.kisskh.co/poster/8316.jpg", jobs[0].ThumbnailUrl);
            Assert.Equal("KissKH", jobs[0].SourcePlatform);
            Assert.NotNull(jobs[0].SelectedVideoStream);
            Assert.Equal("1080P/720P (Direct)", jobs[0].SelectedVideoStream?.Resolution);

            Assert.Equal("The First Frost - Episode 2", jobs[1].Title);
            Assert.Equal("https://hls03.videodelivery2.site/8316/The-First-Frost.Ep2.mp4", jobs[1].Url);

            Assert.Equal("The First Frost - Episode 3", jobs[2].Title);
            Assert.Equal("https://hls03.videodelivery2.site/8316/The-First-Frost.Ep3.mp4", jobs[2].Url);
        }

        [Fact]
        public async Task KissKhExtractor_MissingDramaId_ThrowsException()
        {
            var extractor = new KissKhExtractorEngine(new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))));

            string invalidUrl = "https://kisskh.co/Drama/The-First-Frost";
            var ex = await Assert.ThrowsAsync<Exception>(() => extractor.AnalyzeUrlAsync(invalidUrl));
            Assert.Contains("Could not find Drama ID", ex.Message);
        }

        [Fact]
        public async Task KissKhExtractor_ApiHttpError_ThrowsDescriptiveException()
        {
            var mockHandler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
            var extractor = new KissKhExtractorEngine(new HttpClient(mockHandler));

            string url = "https://kisskh.co/Drama/Non-Existent?id=9999";
            var ex = await Assert.ThrowsAsync<Exception>(() => extractor.AnalyzeUrlAsync(url));
            Assert.Contains("NotFound", ex.Message);
        }

        [Fact]
        public async Task KissKhExtractor_EmptyEpisodeList_ThrowsException()
        {
            var mockHandler = new MockHttpMessageHandler(_ =>
            {
                string jsonResponse = @"{
                    ""title"": ""Empty Drama"",
                    ""thumbnail"": """",
                    ""episodes"": []
                }";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json")
                };
            });

            var extractor = new KissKhExtractorEngine(new HttpClient(mockHandler));
            string url = "https://kisskh.co/Drama/Empty-Drama?id=1234";

            var ex = await Assert.ThrowsAsync<Exception>(() => extractor.AnalyzeUrlAsync(url));
            Assert.Contains("No episodes found", ex.Message);
        }

        #endregion

        #region Iflix Extractor Tests

        [Fact]
        public async Task IflixExtractor_AlbumUrl_ConvertsToSeriesPlayUrlAndExtractsAllEpisodes()
        {
            string capturedAlbumHtmlUrl = string.Empty;

            var mockHttp = new MockHttpMessageHandler(req =>
            {
                capturedAlbumHtmlUrl = req.RequestUri?.ToString() ?? string.Empty;

                string html = @"
                    <html>
                        <head><title>The Street Vendor</title></head>
                        <body>
                            <div class='play-btn'>
                                <a href=""/en/play/2n6ypc2x23zbeh5-The_Street_Vendor_Secret_Identity"">Play</a>
                            </div>
                        </body>
                    </html>";

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(html, System.Text.Encoding.UTF8, "text/html")
                };
            });

            string capturedYtDlpUrl = string.Empty;
            var mockEngine = new MockExtractorEngine(url =>
            {
                capturedYtDlpUrl = url;
                return Task.FromResult(new List<DownloadJob>
                {
                    new DownloadJob { Title = "The Street Vendor EP02", Url = "https://cdn.iflix.com/ep2.mp4", Status = JobStatus.PendingAnalysis },
                    new DownloadJob { Title = "The Street Vendor EP01", Url = "https://cdn.iflix.com/ep1.mp4", Status = JobStatus.PendingAnalysis },
                    new DownloadJob { Title = "Failed Entry", Url = "https://cdn.iflix.com/fail.mp4", Status = JobStatus.AnalysisFailed }
                });
            });

            var extractor = new IflixExtractorEngine(new HttpClient(mockHttp), mockEngine);

            string albumUrl = "https://www.iflix.com/en/album/2n6ypc2x23zbeh5";
            var jobs = await extractor.AnalyzeUrlAsync(albumUrl);

            // 1. Verify album page was fetched
            Assert.Equal("https://www.iflix.com/en/album/2n6ypc2x23zbeh5", capturedAlbumHtmlUrl);

            // 2. Verify album URL was converted to series play URL and passed to engine
            Assert.Equal("https://www.iflix.com/en/play/2n6ypc2x23zbeh5-The_Street_Vendor_Secret_Identity", capturedYtDlpUrl);

            // 3. Verify failed entry was filtered out and items are ordered by title
            Assert.Equal(2, jobs.Count);
            Assert.Equal("The Street Vendor EP01", jobs[0].Title);
            Assert.Equal("The Street Vendor EP02", jobs[1].Title);
        }

        [Fact]
        public async Task IflixExtractor_DirectEpisodeUrl_DelegatesDirectlyToEngineWithoutFetchingHtml()
        {
            bool httpWasCalled = false;
            var mockHttp = new MockHttpMessageHandler(_ =>
            {
                httpWasCalled = true;
                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            string capturedUrl = string.Empty;
            var mockEngine = new MockExtractorEngine(url =>
            {
                capturedUrl = url;
                return Task.FromResult(new List<DownloadJob>
                {
                    new DownloadJob { Title = "Episode Single", Url = url, Status = JobStatus.PendingAnalysis }
                });
            });

            var extractor = new IflixExtractorEngine(new HttpClient(mockHttp), mockEngine);

            string episodeUrl = "https://www.iflix.com/en/play/2n6ypc2x23zbeh5/ep1";
            var jobs = await extractor.AnalyzeUrlAsync(episodeUrl);

            // Should NOT have fetched HTML for non-album direct episode URL
            Assert.False(httpWasCalled, "Direct episode URLs should not fetch album HTML.");
            Assert.Equal(episodeUrl, capturedUrl);
            Assert.Single(jobs);
        }

        [Fact]
        public async Task IflixExtractor_InvalidAlbumUrl_ThrowsException()
        {
            var extractor = new IflixExtractorEngine(
                new HttpClient(new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
                new MockExtractorEngine(_ => Task.FromResult(new List<DownloadJob>())));

            string invalidAlbumUrl = "https://www.iflix.com/en/album/";
            var ex = await Assert.ThrowsAsync<Exception>(() => extractor.AnalyzeUrlAsync(invalidAlbumUrl));
            Assert.Contains("Could not extract album ID", ex.Message);
        }

        [Fact]
        public async Task IflixExtractor_NoSuccessfulEpisodes_ThrowsException()
        {
            var mockHttp = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@"<a href=""/en/play/album123-Title"">Play</a>", System.Text.Encoding.UTF8, "text/html")
            });

            var mockEngine = new MockExtractorEngine(_ => Task.FromResult(new List<DownloadJob>
            {
                new DownloadJob { Title = "Failed 1", Status = JobStatus.AnalysisFailed }
            }));

            var extractor = new IflixExtractorEngine(new HttpClient(mockHttp), mockEngine);

            string albumUrl = "https://www.iflix.com/en/album/album123";
            var ex = await Assert.ThrowsAsync<Exception>(() => extractor.AnalyzeUrlAsync(albumUrl));
            Assert.Contains("No episodes could be extracted", ex.Message);
        }

        #endregion

        #region Helper Mock Classes

        private sealed class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }

        private sealed class MockExtractorEngine : IExtractorEngine
        {
            private readonly Func<string, Task<List<DownloadJob>>> _analyzeFunc;

            public MockExtractorEngine(Func<string, Task<List<DownloadJob>>> analyzeFunc)
            {
                _analyzeFunc = analyzeFunc;
            }

            public Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
            {
                return _analyzeFunc(url);
            }
        }

        #endregion
    }
}
