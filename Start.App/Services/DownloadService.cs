using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.App.Services
{
    public interface IDownloadService
    {
        Task<List<DownloadJob>> AnalyzeUrlAsync(string url);
        Task StartDownloadAsync(Guid jobId, VideoStreamInfo? videoStream, AudioStreamInfo? audioStream, string savePath);
        Task<IEnumerable<DownloadJob>> GetHistoryAsync();
        Task<IEnumerable<DownloadJob>> GetActiveJobsAsync(int limit = 100);
        Task<IEnumerable<DownloadJob>> GetCompletedHistoryJobsAsync(int limit = 100);
        Task<DownloadJob?> GetJobAsync(Guid id);
        Task RetryJobAsync(Guid jobId);
        Task RetryAllJobsAsync();
        Task CancelJobAsync(Guid jobId);
        Task DeleteJobAsync(Guid jobId);
        Task DeleteAllQueueJobsAsync();
        Task PauseJobAsync(Guid jobId);
        Task ResumeJobAsync(Guid jobId);
        Task PauseAllJobsAsync();
        Task ResumeAllJobsAsync();
    }

    public class DownloadService : IDownloadService
    {
        private readonly IEnumerable<IExtractorEngine> _extractors;
        private readonly IDownloadRepository _repository;
        private readonly IQueueManager _queueManager;

        public DownloadService(
            IEnumerable<IExtractorEngine> extractors, 
            IDownloadRepository repository, 
            IQueueManager queueManager)
        {
            _extractors = extractors;
            _repository = repository;
            _queueManager = queueManager;
        }

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            // Route to the correct extractor based on URL
            var extractor = ResolveExtractor(url);
            var jobs = await extractor.AnalyzeUrlAsync(url);
            foreach (var job in jobs)
            {
                await _repository.AddJobAsync(job);
            }
            return jobs;
        }

        public async Task StartDownloadAsync(Guid jobId, VideoStreamInfo? videoStream, AudioStreamInfo? audioStream, string savePath)
        {
            var job = await _repository.GetJobAsync(jobId);
            if (job == null) throw new ArgumentException("Job not found");

            await _queueManager.CancelJob(jobId);

            job.SelectedVideoStream = videoStream;
            job.SelectedAudioStream = audioStream;
            job.IsAudioOnly = videoStream == null && audioStream != null;
            job.SavePath = savePath;
            job.Status = JobStatus.Queued;
            job.Speed = string.Empty;
            job.Eta = "Queued";
            
            await _repository.UpdateJobAsync(job);
            
            _queueManager.Enqueue(job);
        }
        
        public async Task<IEnumerable<DownloadJob>> GetHistoryAsync()
        {
            return await _repository.GetAllJobsAsync();
        }

        public async Task<IEnumerable<DownloadJob>> GetActiveJobsAsync(int limit = 100)
        {
            var activeStatuses = new[]
            {
                JobStatus.PendingAnalysis,
                JobStatus.Queued,
                JobStatus.Downloading,
                JobStatus.Processing,
                JobStatus.Paused
            };
            return await _repository.GetJobsByStatusAsync(activeStatuses, limit);
        }

        public async Task<IEnumerable<DownloadJob>> GetCompletedHistoryJobsAsync(int limit = 100)
        {
            var historyStatuses = new[]
            {
                JobStatus.Completed,
                JobStatus.Failed,
                JobStatus.Cancelled
            };
            return await _repository.GetJobsByStatusAsync(historyStatuses, limit);
        }

        public async Task<DownloadJob?> GetJobAsync(Guid id)
        {
            return await _repository.GetJobAsync(id);
        }

        public async Task RetryJobAsync(Guid jobId)
        {
             var job = await _repository.GetJobAsync(jobId);
             if(job != null)
             {
                 await StartDownloadAsync(jobId, job.SelectedVideoStream, job.SelectedAudioStream, job.SavePath);
             }
        }

        public async Task RetryAllJobsAsync()
        {
            var allJobs = await _repository.GetAllJobsAsync();
            var retriable = allJobs.Where(j => j.Status != JobStatus.Completed).ToList();

            foreach (var job in retriable)
            {
                await StartDownloadAsync(job.Id, job.SelectedVideoStream, job.SelectedAudioStream, job.SavePath);
            }
        }

        public async Task CancelJobAsync(Guid jobId)
        {
            await _queueManager.CancelJob(jobId);
            
            var job = await _repository.GetJobAsync(jobId);
            if(job != null && job.Status != JobStatus.Completed && job.Status != JobStatus.Failed)
            {
                job.Status = JobStatus.Cancelled;
                await _repository.UpdateJobAsync(job);
            }
        }

        public async Task DeleteJobAsync(Guid jobId)
        {
            await _queueManager.CancelJob(jobId);
            await _repository.DeleteJobAsync(jobId);
        }

        public async Task DeleteAllQueueJobsAsync()
        {
            await _queueManager.CancelAll();
            var allJobs = await _repository.GetAllJobsAsync();
            var queueJobs = allJobs.Where(j => 
                j.Status == JobStatus.Queued || 
                j.Status == JobStatus.Downloading || 
                j.Status == JobStatus.Processing || 
                j.Status == JobStatus.PendingAnalysis || 
                j.Status == JobStatus.Paused ||
                j.Status == JobStatus.Cancelled ||
                j.Status == JobStatus.Failed).Select(j => j.Id).ToList();

            await _repository.DeleteJobsAsync(queueJobs);
        }

        public async Task PauseJobAsync(Guid jobId) => await _queueManager.PauseJob(jobId);
        public async Task ResumeJobAsync(Guid jobId) => await _queueManager.ResumeJob(jobId);
        public async Task PauseAllJobsAsync() => await _queueManager.PauseAll();
        public async Task ResumeAllJobsAsync() => await _queueManager.ResumeAll();

        private IExtractorEngine ResolveExtractor(string url)
        {
            // DramaBox URL detection
            if (url.Contains("dramabox.com", StringComparison.OrdinalIgnoreCase) ||
                url.Contains("dramabox", StringComparison.OrdinalIgnoreCase))
            {
                var dramaBoxExtractor = _extractors
                    .FirstOrDefault(e => e.GetType().Name.Contains("DramaBox", StringComparison.OrdinalIgnoreCase));
                if (dramaBoxExtractor != null) return dramaBoxExtractor;
            }

            if (url.Contains("iflix.com", StringComparison.OrdinalIgnoreCase))
            {
                var iflixExtractor = _extractors
                    .FirstOrDefault(e => e.GetType().Name.Contains("Iflix", StringComparison.OrdinalIgnoreCase));
                if (iflixExtractor != null) return iflixExtractor;
            }

            if (url.Contains("kisskh.co", StringComparison.OrdinalIgnoreCase))
            {
                var kissKhExtractor = _extractors
                    .FirstOrDefault(e => e.GetType().Name.Contains("KissKh", StringComparison.OrdinalIgnoreCase));
                if (kissKhExtractor != null) return kissKhExtractor;
            }

            // Default: use the first extractor (yt-dlp) for everything else
            return _extractors.First();
        }
    }
}

