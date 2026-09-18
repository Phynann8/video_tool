using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Start.App.Services;
using Start.Core.Models;
using System.Linq;

namespace Start.UI.ViewModels
{
    public partial class HistoryViewModel : ObservableObject
    {
        private readonly IDownloadService _downloadService;
        public event EventHandler? BackRequested;

        [ObservableProperty]
        private ObservableCollection<DownloadJob> _jobs = new();

        public HistoryViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;
        }

        [RelayCommand]
        private void Back()
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        public async Task LoadHistory()
        {
             var allJobs = await _downloadService.GetHistoryAsync();
             
             // Filter for completed/cancelled/failed
             var historyJobs = allJobs.Where(j => 
                j.Status == JobStatus.Completed || 
                j.Status == JobStatus.Failed || 
                j.Status == JobStatus.Cancelled)
                .OrderByDescending(j => j.CreatedAt);

             Jobs.Clear();
             foreach(var job in historyJobs)
             {
                 Jobs.Add(job);
             }
        }
    }
}
