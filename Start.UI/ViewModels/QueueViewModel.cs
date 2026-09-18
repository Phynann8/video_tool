using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Start.App.Services;
using Start.Core.Models;

namespace Start.UI.ViewModels
{
    public partial class QueueViewModel : ObservableObject
    {
        private readonly IDownloadService _downloadService;
        private readonly DispatcherTimer _timer;
        private bool _isLoading;
        public event EventHandler? BackRequested;

        [ObservableProperty]
        private ObservableCollection<DownloadJob> _jobs = new();

        [ObservableProperty]
        private int _downloadingCount;

        [ObservableProperty]
        private int _queuedCount;

        [ObservableProperty]
        private int _pausedCount;

        [ObservableProperty]
        private int _failedCount;

        [ObservableProperty]
        private bool _hasJobs;

        [ObservableProperty]
        private bool _isAllPaused;

        [ObservableProperty]
        private string _pauseAllButtonText = "Pause All";

        [ObservableProperty]
        private string _pauseAllIconKind = "Pause";

        [ObservableProperty]
        private string _summaryText = "Queue Empty";

        public QueueViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;
            
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += async (s, e) => await LoadJobs();
        }

        public async Task LoadJobs()
        {
            if (_isLoading) return;
            _isLoading = true;

            try
            {
                var allJobs = await _downloadService.GetHistoryAsync();
                
                // Active jobs: Pending, Queued, Downloading, Processing, Paused, Failed
                var activeList = allJobs.Where(j => 
                    j.Status == JobStatus.PendingAnalysis ||
                    j.Status == JobStatus.Queued || 
                    j.Status == JobStatus.Downloading || 
                    j.Status == JobStatus.Processing ||
                    j.Status == JobStatus.Paused ||
                    j.Status == JobStatus.Failed)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList();

                // 1. Remove jobs no longer in activeList
                var activeIds = new HashSet<Guid>(activeList.Select(j => j.Id));
                for (int i = Jobs.Count - 1; i >= 0; i--)
                {
                    if (!activeIds.Contains(Jobs[i].Id))
                    {
                        Jobs.RemoveAt(i);
                    }
                }

                // 2. Update existing jobs or insert new ones maintaining order
                for (int i = 0; i < activeList.Count; i++)
                {
                    var incoming = activeList[i];
                    var existing = Jobs.FirstOrDefault(j => j.Id == incoming.Id);

                    if (existing != null)
                    {
                        existing.Status = incoming.Status;
                        existing.Progress = incoming.Progress;
                        existing.Speed = incoming.Speed;
                        existing.Eta = incoming.Eta;
                        existing.ErrorMessage = incoming.ErrorMessage;
                        existing.SavePath = incoming.SavePath;
                        existing.ThumbnailUrl = incoming.ThumbnailUrl;

                        // Maintain list position if needed
                        int currentIndex = Jobs.IndexOf(existing);
                        if (currentIndex != i && i < Jobs.Count)
                        {
                            Jobs.Move(currentIndex, i);
                        }
                    }
                    else
                    {
                        if (i <= Jobs.Count)
                        {
                            Jobs.Insert(i, incoming);
                        }
                        else
                        {
                            Jobs.Add(incoming);
                        }
                    }
                }

                // 3. Update summary metrics
                DownloadingCount = activeList.Count(j => j.Status == JobStatus.Downloading);
                QueuedCount = activeList.Count(j => j.Status == JobStatus.Queued || j.Status == JobStatus.PendingAnalysis || j.Status == JobStatus.Processing);
                PausedCount = activeList.Count(j => j.Status == JobStatus.Paused);
                FailedCount = activeList.Count(j => j.Status == JobStatus.Failed);
                HasJobs = activeList.Count > 0;

                IsAllPaused = PausedCount > 0 && DownloadingCount == 0 && QueuedCount == 0;
                PauseAllButtonText = IsAllPaused ? "Resume All" : "Pause All";
                PauseAllIconKind = IsAllPaused ? "Play" : "Pause";

                if (activeList.Count == 0)
                {
                    SummaryText = "Queue is empty";
                }
                else if (IsAllPaused)
                {
                    SummaryText = $"All Paused ({PausedCount} items)";
                }
                else
                {
                    var parts = new List<string>();
                    if (DownloadingCount > 0) parts.Add($"{DownloadingCount} Downloading");
                    if (QueuedCount > 0) parts.Add($"{QueuedCount} Queued");
                    if (PausedCount > 0) parts.Add($"{PausedCount} Paused");
                    if (FailedCount > 0) parts.Add($"{FailedCount} Failed");
                    SummaryText = string.Join(" • ", parts);
                }
            }
            finally
            {
                _isLoading = false;
            }
        }
        
        public void StartAutoRefresh()
        {
            _timer.Start();
            _ = LoadJobs();
        }

        public void StopAutoRefresh() => _timer.Stop();

        [RelayCommand]
        private void Back()
        {
            StopAutoRefresh();
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task TogglePauseAll()
        {
            if (IsAllPaused)
            {
                await ResumeAll();
            }
            else
            {
                await PauseAll();
            }
        }

        [RelayCommand]
        private async Task PauseAll()
        {
            await _downloadService.PauseAllJobsAsync();
            await LoadJobs();
        }

        [RelayCommand]
        private async Task ResumeAll()
        {
            await _downloadService.ResumeAllJobsAsync();
            await LoadJobs();
        }

        [RelayCommand]
        private async Task RetryAll()
        {
            await _downloadService.RetryAllJobsAsync();
            await LoadJobs();
        }

        [RelayCommand]
        private async Task DeleteAll()
        {
            await _downloadService.DeleteAllQueueJobsAsync();
            Jobs.Clear();
            await LoadJobs();
        }

        [RelayCommand]
        private async Task PauseJob(DownloadJob? job)
        {
            if (job == null) return;
            await _downloadService.PauseJobAsync(job.Id);
            await LoadJobs();
        }

        [RelayCommand]
        private async Task ResumeJob(DownloadJob? job)
        {
            if (job == null) return;
            await _downloadService.ResumeJobAsync(job.Id);
            await LoadJobs();
        }

        [RelayCommand]
        private async Task RetryJob(DownloadJob? job)
        {
            if (job == null) return;
            await _downloadService.RetryJobAsync(job.Id);
            await LoadJobs();
        }

        [RelayCommand]
        private async Task DeleteJob(DownloadJob? job)
        {
            if (job == null) return;
            await _downloadService.DeleteJobAsync(job.Id);
            Jobs.Remove(job);
            await LoadJobs();
        }

        [RelayCommand]
        private async Task CancelJob(DownloadJob? job)
        {
            await DeleteJob(job);
        }
    }
}
