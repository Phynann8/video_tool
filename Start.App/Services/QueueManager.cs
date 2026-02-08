using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.App.Services
{
    public interface IQueueManager
    {
        void Enqueue(DownloadJob job);
        Task CancelJob(Guid jobId);
    }

    public class QueueManager : IQueueManager
    {
        private readonly IDownloadEngine _downloader;
        private readonly IDownloadRepository _repository;
        private readonly SemaphoreSlim _semaphore;
        private readonly Dictionary<Guid, CancellationTokenSource> _activeJobs = new();
        private const int MaxConcurrentDownloads = 3;

        public QueueManager(IDownloadEngine downloader, IDownloadRepository repository)
        {
            _downloader = downloader;
            _repository = repository;
            _semaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
        }

        public void Enqueue(DownloadJob job)
        {
            // Fire and forget the processing task
            _ = ProcessJobAsync(job);
        }

        private async Task ProcessJobAsync(DownloadJob job)
        {
            await _semaphore.WaitAsync();
            var cts = new CancellationTokenSource();
            _activeJobs[job.Id] = cts;

            try
            {
                // Re-verify status just in case
                if (job.Status == JobStatus.Cancelled) return;

                var progress = new Progress<DownloadJob>(async (updatedJob) => 
                {
                     // Update DB throttled
                     if (updatedJob.Progress % 5 == 0) 
                         await _repository.UpdateJobAsync(updatedJob);
                });

                await _downloader.StartDownloadAsync(job, progress, cts.Token);
                
                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.Now;
                await _repository.UpdateJobAsync(job);
            }
            catch (OperationCanceledException)
            {
                job.Status = JobStatus.Cancelled;
                await _repository.UpdateJobAsync(job);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await _repository.UpdateJobAsync(job);
            }
            finally
            {
                _activeJobs.Remove(job.Id);
                cts.Dispose();
                _semaphore.Release();
            }
        }

        public Task CancelJob(Guid jobId)
        {
            if (_activeJobs.TryGetValue(jobId, out var cts))
            {
                cts.Cancel();
            }
            return Task.CompletedTask;
        }
    }
}
