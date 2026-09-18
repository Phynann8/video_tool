using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Start.App.Services;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Data;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class DynamicSegmentationTests
    {
        [Fact]
        public void DynamicSegment_TrySplit_SplitsUnfinishedBytesAccurately()
        {
            long minSplit = 1024 * 1024; // 1 MB
            var segment = new DynamicSegment(0, 10 * 1024 * 1024 - 1, "test.part");
            segment.CurrentPosition = 2 * 1024 * 1024; // 2 MB already read, 8 MB remaining

            var stolen = segment.TrySplit(minSplit, "parts");

            Assert.NotNull(stolen);
            // Stolen start must immediately succeed segment end
            Assert.Equal(segment.EndOffset + 1, stolen.StartOffset);
            // Stolen end must equal the original end
            Assert.Equal(10 * 1024 * 1024 - 1, stolen.EndOffset);
            // Segment start must remain 0
            Assert.Equal(0, segment.StartOffset);
            // Verify sum of remaining bytes is preserved
            Assert.Equal(8 * 1024 * 1024, segment.RemainingBytes + stolen.RemainingBytes);
        }

        [Fact]
        public void DynamicSegment_TrySplit_RejectsTooSmallRemainingBytes()
        {
            long minSplit = 2 * 1024 * 1024;
            // Only 2 MB total remaining, less than minSplit * 2 (4 MB)
            var segment = new DynamicSegment(0, 2 * 1024 * 1024 - 1, "test.part");
            var stolen = segment.TrySplit(minSplit, "parts");

            Assert.Null(stolen);
            Assert.Equal(2 * 1024 * 1024 - 1, segment.EndOffset);
        }

        [Fact]
        public void DynamicSegment_TrySplit_RejectsCompletedSegment()
        {
            var segment = new DynamicSegment(0, 1000, "test.part")
            {
                IsCompleted = true
            };

            var stolen = segment.TrySplit(100, "parts");
            Assert.Null(stolen);
        }

        [Fact]
        public void DynamicSegments_MultipleSplits_MaintainStrictContinuityWithoutGaps()
        {
            long totalSize = 50 * 1024 * 1024; // 50 MB
            var segments = new List<DynamicSegment>
            {
                new DynamicSegment(0, 25 * 1024 * 1024 - 1, "part1.part"),
                new DynamicSegment(25 * 1024 * 1024, totalSize - 1, "part2.part")
            };

            // Simulate worker 1 stealing from segment 0
            var stolen0 = segments[0].TrySplit(2 * 1024 * 1024, "parts");
            Assert.NotNull(stolen0);
            segments.Add(stolen0);

            // Simulate worker 2 stealing from segment 1
            var stolen1 = segments[1].TrySplit(2 * 1024 * 1024, "parts");
            Assert.NotNull(stolen1);
            segments.Add(stolen1);

            // Sort by StartOffset
            var ordered = segments.OrderBy(s => s.StartOffset).ToList();

            Assert.Equal(0, ordered[0].StartOffset);
            for (int i = 1; i < ordered.Count; i++)
            {
                Assert.Equal(ordered[i - 1].EndOffset + 1, ordered[i].StartOffset);
            }
            Assert.Equal(totalSize - 1, ordered.Last().EndOffset);
        }

        [Fact]
        public async Task HttpDownloadEngine_DownloadsAndAssemblesByteRangesCorrectly()
        {
            // Create a fake test payload of 5 MB
            int dataSize = 5 * 1024 * 1024;
            byte[] sourceData = new byte[dataSize];
            new Random(42).NextBytes(sourceData);

            var mockHandler = new MockRangeHttpHandler(sourceData);
            var httpClient = new HttpClient(mockHandler);
            var engine = new HttpDownloadEngine(httpClient);

            var tempDir = Path.Combine(Path.GetTempPath(), "umd_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var job = new DownloadJob
                {
                    Id = Guid.NewGuid(),
                    Url = "https://mock.cdn/video.mp4",
                    Title = "mock_test_video",
                    SavePath = tempDir
                };

                var progress = new Progress<DownloadJob>();
                await engine.StartDownloadAsync(job, progress, CancellationToken.None);

                string expectedFile = Path.Combine(tempDir, "mock_test_video.mp4");
                Assert.True(File.Exists(expectedFile), "Downloaded file must exist.");

                byte[] downloadedData = await File.ReadAllBytesAsync(expectedFile);
                Assert.Equal(sourceData.Length, downloadedData.Length);
                Assert.Equal(sourceData, downloadedData);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task HttpDownloadEngine_ResumesInterruptedDownloadFromExistingPartsAccurately()
        {
            // 6 MB total data
            int totalSize = 6 * 1024 * 1024;
            byte[] sourceData = new byte[totalSize];
            new Random(1337).NextBytes(sourceData);

            var mockHandler = new MockRangeHttpHandler(sourceData);
            var httpClient = new HttpClient(mockHandler);
            var engine = new HttpDownloadEngine(httpClient);

            var tempDir = Path.Combine(Path.GetTempPath(), "umd_resume_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var job = new DownloadJob
                {
                    Id = Guid.NewGuid(),
                    Url = "https://mock.cdn/video.mp4",
                    Title = "resumed_video",
                    SavePath = tempDir
                };

                string targetFile = Path.Combine(tempDir, "resumed_video.mp4");
                string partsDir = targetFile + ".parts";
                Directory.CreateDirectory(partsDir);

                // Simulate an existing interrupted download: first 2 MB already written to part 0
                int existingBytes = 2 * 1024 * 1024;
                string existingPart = Path.Combine(partsDir, "000000000000.part");
                await File.WriteAllBytesAsync(existingPart, sourceData.Take(existingBytes).ToArray());

                var progress = new Progress<DownloadJob>();
                await engine.StartDownloadAsync(job, progress, CancellationToken.None);

                Assert.True(File.Exists(targetFile), "Resumed download must assemble the final output file.");
                byte[] downloadedData = await File.ReadAllBytesAsync(targetFile);
                Assert.Equal(sourceData.Length, downloadedData.Length);
                Assert.Equal(sourceData, downloadedData);

                // Verify that byte 0 was NOT requested over HTTP because it already existed on disk
                Assert.True(mockHandler.RequestedRanges.Count > 0, "Missing ranges must be requested.");
                Assert.All(mockHandler.RequestedRanges, r => Assert.True(r.From >= existingBytes, 
                    $"Request range from {r.From} should not re-download existing bytes (< {existingBytes})."));

                // Verify parts folder was cleaned up after successful assembly
                Assert.False(Directory.Exists(partsDir), "Parts directory must be removed after successful assembly.");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public async Task QueueManager_PauseAndResume_TransitionsStateCorrectly()
        {
            var repo = new TestDownloadRepository();
            var engine = new TestDownloadEngine();
            var queue = new QueueManager(engine, repo);

            var job = new DownloadJob
            {
                Id = Guid.NewGuid(),
                Title = "Test Pause Resume",
                Status = JobStatus.Queued
            };
            await repo.AddJobAsync(job);

            queue.Enqueue(job);

            // Wait for engine to start running
            await engine.StartedSignal.Task;

            // Pause the job
            await queue.PauseJob(job.Id);

            // Wait brief moment for cancellation to process and repository to update
            for (int i = 0; i < 20; i++)
            {
                var current = await repo.GetJobAsync(job.Id);
                if (current?.Status == JobStatus.Paused) break;
                await Task.Delay(50);
            }

            var pausedJob = await repo.GetJobAsync(job.Id);
            Assert.NotNull(pausedJob);
            Assert.Equal(JobStatus.Paused, pausedJob.Status);

            // Now resume the job
            await queue.ResumeJob(job.Id);

            var resumedJob = await repo.GetJobAsync(job.Id);
            Assert.NotNull(resumedJob);
            Assert.True(resumedJob.Status == JobStatus.Queued || resumedJob.Status == JobStatus.Downloading);
        }

        [Fact]
        public void HttpDownloadEngine_UsesConfiguredWorkerCountAndSegmentSize()
        {
            var settings = new DownloadProcessingSettings
            {
                MaxSegmentWorkers = 8,
                MinSegmentSizeBytes = 1024 * 1024,
                DefaultDownloadPath = @"C:\CustomDownloads"
            };

            var engine = new HttpDownloadEngine(settings);

            Assert.Equal(8, engine.MaxSegmentWorkers);
            Assert.Equal(1024 * 1024, engine.MinSegmentSizeBytes);
        }

        [Fact]
        public async Task SettingsPersistence_SavesAndLoadsFullConfigurationAccurately()
        {
            var tempDb = Path.Combine(Path.GetTempPath(), "test_settings_" + Guid.NewGuid().ToString("N") + ".db");
            var bootstrap = new DatabaseBootstrap(tempDb);
            await bootstrap.SetupAsync();

            try
            {
                var repo = new SqliteSettingsRepository(tempDb);
                var original = new DownloadProcessingSettings
                {
                    DefaultDownloadPath = @"D:\Media\Downloads",
                    MaxSegmentWorkers = 24,
                    MinSegmentSizeBytes = 4 * 1024 * 1024,
                    MaxConcurrentDownloads = 5,
                    EnableClipboardMonitoring = false,
                    ExtractAudio = true,
                    AudioFormat = "opus",
                    EmbedMetadata = true,
                    EmbedThumbnail = true
                };

                await repo.SaveAsync(original);

                var loaded = await repo.LoadAsync();

                Assert.NotNull(loaded);
                Assert.Equal(original.DefaultDownloadPath, loaded.DefaultDownloadPath);
                Assert.Equal(original.MaxSegmentWorkers, loaded.MaxSegmentWorkers);
                Assert.Equal(original.MinSegmentSizeBytes, loaded.MinSegmentSizeBytes);
                Assert.Equal(original.MaxConcurrentDownloads, loaded.MaxConcurrentDownloads);
                Assert.Equal(original.EnableClipboardMonitoring, loaded.EnableClipboardMonitoring);
                Assert.Equal(original.ExtractAudio, loaded.ExtractAudio);
                Assert.Equal(original.AudioFormat, loaded.AudioFormat);
                Assert.Equal(original.EmbedMetadata, loaded.EmbedMetadata);
                Assert.Equal(original.EmbedThumbnail, loaded.EmbedThumbnail);
            }
            finally
            {
                if (File.Exists(tempDb))
                {
                    try { File.Delete(tempDb); } catch { }
                }
            }
        }

        private sealed class TestDownloadRepository : IDownloadRepository
        {
            private readonly Dictionary<Guid, DownloadJob> _jobs = new();
            public Task AddJobAsync(DownloadJob job) { _jobs[job.Id] = job; return Task.CompletedTask; }
            public Task UpdateJobAsync(DownloadJob job) { _jobs[job.Id] = job; return Task.CompletedTask; }
            public Task<DownloadJob?> GetJobAsync(Guid id) => Task.FromResult(_jobs.TryGetValue(id, out var j) ? j : null);
            public Task<IEnumerable<DownloadJob>> GetAllJobsAsync() => Task.FromResult<IEnumerable<DownloadJob>>(_jobs.Values.ToList());
        }

        private sealed class TestDownloadEngine : IDownloadEngine
        {
            public TaskCompletionSource<bool> StartedSignal { get; } = new();

            public async Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
            {
                job.Status = JobStatus.Downloading;
                StartedSignal.TrySetResult(true);
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        private sealed class MockRangeHttpHandler : HttpMessageHandler
        {
            private readonly byte[] _data;
            public List<(long From, long To)> RequestedRanges { get; } = new();

            public MockRangeHttpHandler(byte[] data)
            {
                _data = data;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.Method == HttpMethod.Head)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK);
                    response.Headers.AcceptRanges.Add("bytes");
                    response.Content = new ByteArrayContent(Array.Empty<byte>());
                    response.Content.Headers.ContentLength = _data.Length;
                    return Task.FromResult(response);
                }

                if (request.Method == HttpMethod.Get)
                {
                    if (request.Headers.Range != null && request.Headers.Range.Ranges.Count > 0)
                    {
                        var range = request.Headers.Range.Ranges.First();
                        long from = range.From ?? 0;
                        long to = range.To ?? (_data.Length - 1);
                        to = Math.Min(to, _data.Length - 1);
                        int count = (int)(to - from + 1);

                        lock (RequestedRanges)
                        {
                            RequestedRanges.Add((from, to));
                        }

                        byte[] slice = new byte[count];
                        Array.Copy(_data, from, slice, 0, count);

                        var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
                        response.Content = new ByteArrayContent(slice);
                        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, _data.Length);
                        response.Content.Headers.ContentLength = count;
                        return Task.FromResult(response);
                    }
                    else
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Content = new ByteArrayContent(_data);
                        response.Content.Headers.ContentLength = _data.Length;
                        return Task.FromResult(response);
                    }
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MethodNotAllowed));
            }
        }
    }
}
