using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.App.Services
{
    public interface IDownloadService
    {
        Task<DownloadJob> AnalyzeUrlAsync(string url);
        Task StartDownloadAsync(Guid jobId, VideoStreamInfo? videoStream, AudioStreamInfo? audioStream, string savePath);
        Task<IEnumerable<DownloadJob>> GetHistoryAsync();
        Task RetryJobAsync(Guid jobId);
        Task CancelJobAsync(Guid jobId); // Placeholder for cancellation logic
    }

    public class DownloadService : IDownloadService
    {
        private readonly IExtractorEngine _extractor;
        private readonly IDownloadRepository _repository;
        private readonly IQueueManager _queueManager;

        public DownloadService(
            IExtractorEngine extractor, 
            IDownloadRepository repository, 
            IQueueManager queueManager)
        {
            _extractor = extractor;
            _repository = repository;
            _queueManager = queueManager;
        }

        public async Task<DownloadJob> AnalyzeUrlAsync(string url)
        {
            var job = await _extractor.AnalyzeUrlAsync(url);
            await _repository.AddJobAsync(job);
            return job;
        }

        public async Task StartDownloadAsync(Guid jobId, VideoStreamInfo? videoStream, AudioStreamInfo? audioStream, string savePath)
        {
            var job = await _repository.GetJobAsync(jobId);
            if (job == null) throw new ArgumentException("Job not found");

            job.SelectedVideoStream = videoStream;
            job.SelectedAudioStream = audioStream;
            job.IsAudioOnly = videoStream == null && audioStream != null;
            job.SavePath = savePath;
            job.Status = JobStatus.Queued;
            
            await _repository.UpdateJobAsync(job);
            
            _queueManager.Enqueue(job);
        }
        
        public async Task<IEnumerable<DownloadJob>> GetHistoryAsync()
        {
            return await _repository.GetAllJobsAsync();
        }

        public async Task RetryJobAsync(Guid jobId)
        {
             var job = await _repository.GetJobAsync(jobId);
             if(job != null)
             {
                 await StartDownloadAsync(jobId, job.SelectedVideoStream, job.SelectedAudioStream, job.SavePath);
             }
        }

        public async Task CancelJobAsync(Guid jobId)
        {
            await _queueManager.CancelJob(jobId);
            
            // Also update DB state if not active
            var job = await _repository.GetJobAsync(jobId);
            if(job != null && job.Status != JobStatus.Completed && job.Status != JobStatus.Failed)
            {
                job.Status = JobStatus.Cancelled;
                await _repository.UpdateJobAsync(job);
            }
        }
    }
}
