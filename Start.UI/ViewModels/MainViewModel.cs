using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Start.UI.Views;

namespace Start.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private object? _currentView;

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            // Default to AddDownload
            NavigateToAddDownload();
        }

        [RelayCommand]
        public void NavigateToAddDownload()
        {
            var vm = _serviceProvider.GetRequiredService<AddDownloadViewModel>();
            
            // Unsubscribe to avoid leaks if transient (though weak events preferred, simple unsubscribe ok for now)
            vm.OnDownloadStarted -= OnDownloadStartedHandler;
            vm.OnDownloadStarted += OnDownloadStartedHandler;
            
            CurrentView = new AddDownloadView { DataContext = vm };
        }

        private void OnDownloadStartedHandler(object? sender, EventArgs e)
        {
             NavigateToQueue();
        }

        [RelayCommand]
        public void NavigateToQueue()
        {
            var vm = _serviceProvider.GetRequiredService<QueueViewModel>();
            vm.StartAutoRefresh(); 
            // Note: StopAutoRefresh logic needed when navigating away if singleton? 
            // If Transient, simple GC might not stop timer if it holds ref? 
            // For MVP transient is okay, but timer needs disposal. 
            // Better: LoadJobs call once, and refresh button. Or keep it simple.
            
            CurrentView = new QueueView { DataContext = vm };
            _ = vm.LoadJobs(); // Fire and forget (intentionally not awaited)
        }

        [RelayCommand]
        public void NavigateToHistory()
        {
            var vm = _serviceProvider.GetRequiredService<HistoryViewModel>();
            _ = vm.LoadHistory();
            CurrentView = new HistoryView { DataContext = vm };
        }

        [RelayCommand]
        public void NavigateToSettings()
        {
            var vm = _serviceProvider.GetRequiredService<SettingsViewModel>();
            CurrentView = new SettingsView { DataContext = vm };
        }
    }
}
