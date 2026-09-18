using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Start.App.Services;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Data;
using Xunit;

namespace Downloader.Tests
{
    public class QueueAndHistoryOptimizationTests : IDisposable
    {
        private readonly string _testDbPath;

        public QueueAndHistoryOptimizationTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"test_downloader_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath)) File.Delete(_testDbPath);
            }
            catch { }
        }

        [Fact]
        public async Task SqliteDownloadRepository_GetJobsByStatusAsync_FiltersAndLimitsCorrectly()
        {
            var bootstrap = new DatabaseBootstrap(_testDbPath);
            await bootstrap.SetupAsync();

            var repo = new SqliteDownloadRepository(_testDbPath);

            // Add 10 active jobs and 10 completed jobs
            for (int i = 0; i < 10; i++)
            {
                await repo.AddJobAsync(new DownloadJob
                {
                    Title = $"Active Job {i}",
                    Status = JobStatus.Downloading,
                    Url = $"https://test.com/active/{i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(i)
                });

                await repo.AddJobAsync(new DownloadJob
                {
                    Title = $"Completed Job {i}",
                    Status = JobStatus.Completed,
                    Url = $"https://test.com/completed/{i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(i)
                });
            }

            // Query active jobs with limit 5
            var activeJobs = (await repo.GetJobsByStatusAsync(new[] { JobStatus.Downloading }, limit: 5)).ToList();
            Assert.Equal(5, activeJobs.Count);
            Assert.All(activeJobs, j => Assert.Equal(JobStatus.Downloading, j.Status));

            // Query completed jobs with limit 20
            var completedJobs = (await repo.GetJobsByStatusAsync(new[] { JobStatus.Completed }, limit: 20)).ToList();
            Assert.Equal(10, completedJobs.Count);
            Assert.All(completedJobs, j => Assert.Equal(JobStatus.Completed, j.Status));
        }

        [Fact]
        public async Task DownloadService_GetActiveAndHistoryJobs_SeparatesCorrectly()
        {
            var bootstrap = new DatabaseBootstrap(_testDbPath);
            await bootstrap.SetupAsync();

            var repo = new SqliteDownloadRepository(_testDbPath);
            await repo.AddJobAsync(new DownloadJob { Title = "Active 1", Status = JobStatus.Downloading, Url = "u1" });
            await repo.AddJobAsync(new DownloadJob { Title = "Active 2", Status = JobStatus.Queued, Url = "u2" });
            await repo.AddJobAsync(new DownloadJob { Title = "Completed 1", Status = JobStatus.Completed, Url = "u3" });
            await repo.AddJobAsync(new DownloadJob { Title = "Failed 1", Status = JobStatus.Failed, Url = "u4" });

            var service = new DownloadService(
                Enumerable.Empty<IExtractorEngine>(),
                repo,
                new FakeQueueManager());

            var active = (await service.GetActiveJobsAsync()).ToList();
            var history = (await service.GetCompletedHistoryJobsAsync()).ToList();

            Assert.Equal(2, active.Count);
            Assert.Contains(active, j => j.Title == "Active 1");
            Assert.Contains(active, j => j.Title == "Active 2");

            Assert.Equal(2, history.Count);
            Assert.Contains(history, j => j.Title == "Completed 1");
            Assert.Contains(history, j => j.Title == "Failed 1");
        }

        [Fact]
        public async Task DownloadService_GetJobAsync_ReturnsSingleJob()
        {
            var bootstrap = new DatabaseBootstrap(_testDbPath);
            await bootstrap.SetupAsync();

            var repo = new SqliteDownloadRepository(_testDbPath);
            var jobId = Guid.NewGuid();
            await repo.AddJobAsync(new DownloadJob { Id = jobId, Title = "Target Job", Status = JobStatus.Queued, Url = "uTarget" });

            var service = new DownloadService(
                Enumerable.Empty<IExtractorEngine>(),
                repo,
                new FakeQueueManager());

            var fetched = await service.GetJobAsync(jobId);
            Assert.NotNull(fetched);
            Assert.Equal(jobId, fetched.Id);
            Assert.Equal("Target Job", fetched.Title);
        }

        private class FakeQueueManager : IQueueManager
        {
            public event EventHandler<DownloadJob>? JobUpdated;
            public event EventHandler<DownloadJob>? JobCompleted;
            public event EventHandler<DownloadJob>? JobFailed;
            public int ActiveJobCount => 0;
            public void Enqueue(DownloadJob job) { }
            public Task CancelJob(Guid jobId) => Task.CompletedTask;
            public Task PauseJob(Guid jobId) => Task.CompletedTask;
            public Task ResumeJob(Guid jobId) => Task.CompletedTask;
            public Task PauseAll() => Task.CompletedTask;
            public Task ResumeAll() => Task.CompletedTask;
        }
    }
}
