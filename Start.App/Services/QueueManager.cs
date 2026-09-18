using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
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
        Task CancelAll();
        Task PauseJob(Guid jobId);
        Task ResumeJob(Guid jobId);
        Task PauseAll();
        Task ResumeAll();
        event EventHandler<DownloadJob>? JobUpdated;
        event EventHandler<DownloadJob>? JobCompleted;
        event EventHandler<DownloadJob>? JobFailed;
        int ActiveJobCount { get; }
    }

    public class QueueManager : IQueueManager
    {
        private readonly IDownloadEngine _downloader;
        private readonly IDownloadRepository _repository;
        private readonly IPowerManagementService? _powerService;
        private readonly IEnumerable<IMediaUrlRefresher> _urlRefreshers;
        private readonly SemaphoreSlim _semaphore;
        private readonly Dictionary<Guid, CancellationTokenSource> _activeJobs = new();
        private readonly HashSet<Guid> _pausedJobs = new();
        private const int MaxConcurrentDownloads = 3;
        private const int MaxTransientAttempts = 3;

        public event EventHandler<DownloadJob>? JobUpdated;
        public event EventHandler<DownloadJob>? JobCompleted;
        public event EventHandler<DownloadJob>? JobFailed;

        public int ActiveJobCount
        {
            get
            {
                lock (_activeJobs)
                {
                    return _activeJobs.Count;
                }
            }
        }

        public QueueManager(
            IDownloadEngine downloader,
            IDownloadRepository repository,
            IPowerManagementService? powerService = null,
            IEnumerable<IMediaUrlRefresher>? urlRefreshers = null)
        {
            _downloader = downloader;
            _repository = repository;
            _powerService = powerService;
            _urlRefreshers = urlRefreshers ?? Enumerable.Empty<IMediaUrlRefresher>();
            _semaphore = new SemaphoreSlim(MaxConcurrentDownloads, MaxConcurrentDownloads);
        }

        public void Enqueue(DownloadJob job)
        {
            _ = ProcessJobAsync(job);
        }

        private async Task ProcessJobAsync(DownloadJob job)
        {
            await _semaphore.WaitAsync();
            var cts = new CancellationTokenSource();
            lock (_activeJobs)
            {
                _activeJobs[job.Id] = cts;
            }

            _powerService?.PreventSleep();

            try
            {
                if (job.Status == JobStatus.Cancelled) return;

                job.Status = JobStatus.Downloading;
                await _repository.UpdateJobAsync(job);
                JobUpdated?.Invoke(this, job);

                var progress = new Progress<DownloadJob>(async (updatedJob) =>
                {
                    await _repository.UpdateJobAsync(updatedJob);
                    JobUpdated?.Invoke(this, updatedJob);
                });

                await StartDownloadWithRetryAsync(job, progress, cts.Token);

                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                job.Progress = 100;
                await _repository.UpdateJobAsync(job);
                JobCompleted?.Invoke(this, job);
            }
            catch (OperationCanceledException)
            {
                bool wasPaused;
                lock (_pausedJobs)
                {
                    wasPaused = _pausedJobs.Remove(job.Id);
                }

                if (wasPaused)
                {
                    job.Status = JobStatus.Paused;
                    job.Speed = string.Empty;
                    job.Eta = "Paused";
                }
                else
                {
                    job.Status = JobStatus.Cancelled;
                    job.Speed = string.Empty;
                    job.Eta = string.Empty;
                }

                await _repository.UpdateJobAsync(job);
                JobUpdated?.Invoke(this, job);
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                await _repository.UpdateJobAsync(job);
                JobFailed?.Invoke(this, job);
            }
            finally
            {
                lock (_activeJobs)
                {
                    _activeJobs.Remove(job.Id);
                }
                cts.Dispose();
                _powerService?.AllowSleep();
                _semaphore.Release();
            }
        }

        private async Task StartDownloadWithRetryAsync(
            DownloadJob job,
            IProgress<DownloadJob> progress,
            CancellationToken cancellationToken)
        {
            // 1. Proactive JIT check: if the signed URL is already marked expired, refresh it before initiating download
            if (job.IsUrlExpired)
            {
                await TryRefreshJobUrlAsync(job, cancellationToken);
            }

            Exception? lastException = null;

            for (var attempt = 1; attempt <= MaxTransientAttempts; attempt++)
            {
                try
                {
                    await _downloader.StartDownloadAsync(job, progress, cancellationToken);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.Message.Contains("403"))
                {
                    lastException = ex;
                    bool refreshed = await TryRefreshJobUrlAsync(job, cancellationToken);
                    if (!refreshed)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt == MaxTransientAttempts)
                    {
                        throw;
                    }
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }
        }

        private async Task<bool> TryRefreshJobUrlAsync(DownloadJob job, CancellationToken cancellationToken)
        {
            foreach (var refresher in _urlRefreshers)
            {
                try
                {
                    if (await refresher.RefreshJobUrlAsync(job, cancellationToken))
                    {
                        await _repository.UpdateJobAsync(job);
                        JobUpdated?.Invoke(this, job);
                        return true;
                    }
                }
                catch
                {
                    // Try next refresher
                }
            }
            return false;
        }

        public Task CancelJob(Guid jobId)
        {
            lock (_activeJobs)
            {
                if (_activeJobs.TryGetValue(jobId, out var cts))
                {
                    cts.Cancel();
                }
            }
            return Task.CompletedTask;
        }

        public async Task CancelAll()
        {
            List<Guid> activeIds;
            lock (_activeJobs)
            {
                activeIds = _activeJobs.Keys.ToList();
            }

            foreach (var id in activeIds)
            {
                await CancelJob(id);
            }

            var allJobs = await _repository.GetAllJobsAsync();
            var nonCompletedJobs = allJobs.Where(j => 
                j.Status == JobStatus.Queued || 
                j.Status == JobStatus.Downloading || 
                j.Status == JobStatus.Processing || 
                j.Status == JobStatus.PendingAnalysis ||
                j.Status == JobStatus.Paused).ToList();

            foreach (var job in nonCompletedJobs)
            {
                job.Status = JobStatus.Cancelled;
                job.Speed = string.Empty;
                job.Eta = string.Empty;
                await _repository.UpdateJobAsync(job);
                JobUpdated?.Invoke(this, job);
            }
        }

        public Task PauseJob(Guid jobId)
        {
            lock (_pausedJobs)
            {
                _pausedJobs.Add(jobId);
            }

            lock (_activeJobs)
            {
                if (_activeJobs.TryGetValue(jobId, out var cts))
                {
                    cts.Cancel();
                }
            }
            return Task.CompletedTask;
        }

        public async Task ResumeJob(Guid jobId)
        {
            var job = await _repository.GetJobAsync(jobId);
            if (job != null && job.Status == JobStatus.Paused)
            {
                job.Status = JobStatus.Queued;
                await _repository.UpdateJobAsync(job);
                Enqueue(job);
            }
        }

        public async Task PauseAll()
        {
            List<Guid> activeIds;
            lock (_activeJobs)
            {
                activeIds = _activeJobs.Keys.ToList();
            }

            foreach (var id in activeIds)
            {
                await PauseJob(id);
            }

            var allJobs = await _repository.GetAllJobsAsync();
            foreach (var job in allJobs.Where(j => j.Status == JobStatus.Queued || j.Status == JobStatus.Processing || j.Status == JobStatus.PendingAnalysis))
            {
                await PauseJob(job.Id);
            }
        }

        public async Task ResumeAll()
        {
            var allJobs = await _repository.GetAllJobsAsync();
            var pausedJobs = allJobs.Where(j => j.Status == JobStatus.Paused).OrderBy(j => j.CreatedAt).ToList();
            foreach (var job in pausedJobs)
            {
                await ResumeJob(job.Id);
            }
        }
    }
}
