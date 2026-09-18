using System.Collections.ObjectModel;
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
        public event EventHandler? BackRequested;

        [ObservableProperty]
        private ObservableCollection<DownloadJob> _jobs = new();

        public QueueViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;
            
            // Simple polling for UI updates
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += async (s, e) => await LoadJobs();
        }

        public async Task LoadJobs()
        {
             var allJobs = await _downloadService.GetHistoryAsync();
             
             // Filter for active jobs (Pending, Queued, Downloading, Processing)
             var activeJobs = allJobs.Where(j => 
                j.Status == JobStatus.PendingAnalysis ||
                j.Status == JobStatus.Queued || 
                j.Status == JobStatus.Downloading || 
                j.Status == JobStatus.Processing)
                .OrderByDescending(j => j.CreatedAt);

             Jobs.Clear();
             foreach(var job in activeJobs)
             {
                 Jobs.Add(job);
             }
        }
        
        public void StartAutoRefresh() => _timer.Start();
        public void StopAutoRefresh() => _timer.Stop();

        [RelayCommand]
        private void Back()
        {
            StopAutoRefresh();
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task CancelJob(DownloadJob job)
        {
            if (job == null) return;
            await _downloadService.CancelJobAsync(job.Id);
            await LoadJobs();
        }
        
        [RelayCommand]
        private async Task RetryJob(DownloadJob job)
        {
            if (job == null) return;
            await _downloadService.RetryJobAsync(job.Id);
            await LoadJobs();
        }
    }
}
