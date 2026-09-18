using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Start.App.Services;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class JitSignedUrlResolutionTests
    {
        [Theory]
        [InlineData("https://cdn.example.com/video.mp4?Expires=1789682400&Signature=abc", 1789682400)]
        [InlineData("https://cdn.example.com/video.mp4?exp=1789682400&key=xyz", 1789682400)]
        [InlineData("https://cdn.example.com/video.mp4?token_exp=1789682400", 1789682400)]
        public void TryParseUrlExpiry_ParsesEpochExpiryCorrectly(string url, long expectedEpoch)
        {
            var expiry = DramaBoxExtractorEngine.TryParseUrlExpiry(url);
            Assert.NotNull(expiry);
            var expectedUtc = DateTimeOffset.FromUnixTimeSeconds(expectedEpoch).UtcDateTime;
            Assert.Equal(expectedUtc, expiry.Value);
        }

        [Fact]
        public void DownloadJob_DetectsExpiredUrlProperly()
        {
            var activeJob = new DownloadJob
            {
                Url = "https://cdn.example.com/video.mp4",
                UrlExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };
            Assert.False(activeJob.IsUrlExpired);

            var expiredJob = new DownloadJob
            {
                Url = "https://cdn.example.com/video.mp4",
                UrlExpiresAt = DateTime.UtcNow.AddMinutes(-5)
            };
            Assert.True(expiredJob.IsUrlExpired);
        }

        [Fact]
        public async Task QueueManager_ProactivelyRefreshesExpiredSignedUrl_BeforeDownloadStarts()
        {
            var testRepo = new MockDownloadRepo();
            var mockDownloader = new MockDownloadEngine();
            var mockRefresher = new MockMediaUrlRefresher("https://fresh-cdn.example.com/new-signed-stream.mp4");

            var qm = new QueueManager(mockDownloader, testRepo, null, new[] { mockRefresher });

            var job = new DownloadJob
            {
                Id = Guid.NewGuid(),
                Title = "Expiring Episode",
                Url = "https://old-cdn.example.com/expired-stream.mp4",
                UrlExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Mark as already expired
                Status = JobStatus.Queued,
                SavePath = Path.GetTempPath()
            };

            await testRepo.AddJobAsync(job);

            var completedTcs = new TaskCompletionSource<DownloadJob>();
            qm.JobCompleted += (sender, completedJob) =>
            {
                if (completedJob.Id == job.Id) completedTcs.TrySetResult(completedJob);
            };

            qm.Enqueue(job);

            var result = await Task.WhenAny(completedTcs.Task, Task.Delay(5000));
            Assert.Same(completedTcs.Task, result);

            // Verify the refresher was called and URL was replaced with the fresh signed URL
            Assert.True(mockRefresher.WasCalled);
            Assert.Equal("https://fresh-cdn.example.com/new-signed-stream.mp4", mockDownloader.LastDownloadedUrl);
        }

        [Fact]
        public async Task QueueManager_RecoversFrom403Forbidden_ByRefreshingJitUrl()
        {
            var testRepo = new MockDownloadRepo();
            var failingDownloader = new FailOnceWith403Downloader("https://fresh-cdn.example.com/recovered-stream.mp4");
            var mockRefresher = new MockMediaUrlRefresher("https://fresh-cdn.example.com/recovered-stream.mp4");

            var qm = new QueueManager(failingDownloader, testRepo, null, new[] { mockRefresher });

            var job = new DownloadJob
            {
                Id = Guid.NewGuid(),
                Title = "403 Expired Episode",
                Url = "https://cdn.example.com/rejected-stream.mp4",
                Status = JobStatus.Queued,
                SavePath = Path.GetTempPath()
            };

            await testRepo.AddJobAsync(job);

            var completedTcs = new TaskCompletionSource<DownloadJob>();
            qm.JobCompleted += (sender, completedJob) =>
            {
                if (completedJob.Id == job.Id) completedTcs.TrySetResult(completedJob);
            };

            qm.Enqueue(job);

            var result = await Task.WhenAny(completedTcs.Task, Task.Delay(5000));
            Assert.Same(completedTcs.Task, result);

            // Verify it caught 403, refreshed the link, and completed with the fresh URL
            Assert.True(mockRefresher.WasCalled);
            Assert.Equal("https://fresh-cdn.example.com/recovered-stream.mp4", job.Url);
            Assert.Equal(JobStatus.Completed, job.Status);
        }

        private class MockDownloadRepo : IDownloadRepository
        {
            private readonly Dictionary<Guid, DownloadJob> _jobs = new();
            public Task AddJobAsync(DownloadJob job) { _jobs[job.Id] = job; return Task.CompletedTask; }
            public Task UpdateJobAsync(DownloadJob job) { _jobs[job.Id] = job; return Task.CompletedTask; }
            public Task<DownloadJob?> GetJobAsync(Guid id) => Task.FromResult(_jobs.GetValueOrDefault(id));
            public Task<IEnumerable<DownloadJob>> GetAllJobsAsync() => Task.FromResult<IEnumerable<DownloadJob>>(_jobs.Values);
        }

        private class MockMediaUrlRefresher : IMediaUrlRefresher
        {
            private readonly string _freshUrl;
            public bool WasCalled { get; private set; }

            public MockMediaUrlRefresher(string freshUrl) => _freshUrl = freshUrl;

            public Task<bool> RefreshJobUrlAsync(DownloadJob job, CancellationToken cancellationToken = default)
            {
                WasCalled = true;
                job.Url = _freshUrl;
                job.UrlExpiresAt = DateTime.UtcNow.AddMinutes(30);
                return Task.FromResult(true);
            }
        }

        private class MockDownloadEngine : IDownloadEngine
        {
            public string LastDownloadedUrl { get; private set; } = string.Empty;

            public Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
            {
                LastDownloadedUrl = job.Url;
                job.Progress = 100;
                progress.Report(job);
                return Task.CompletedTask;
            }
        }

        private class FailOnceWith403Downloader : IDownloadEngine
        {
            private readonly string _expectedFreshUrl;
            private bool _failedOnce = false;

            public FailOnceWith403Downloader(string expectedFreshUrl) => _expectedFreshUrl = expectedFreshUrl;

            public Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
            {
                if (!_failedOnce)
                {
                    _failedOnce = true;
                    throw new HttpRequestException("HTTP 403 Forbidden: signed URL expired", null, HttpStatusCode.Forbidden);
                }

                // Succeeded with fresh URL
                Assert.Equal(_expectedFreshUrl, job.Url);
                job.Progress = 100;
                progress.Report(job);
                return Task.CompletedTask;
            }
        }
    }
}
