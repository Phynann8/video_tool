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
            vm.NavigateToQueueRequested -= OnNavigateToQueue;
            vm.NavigateToQueueRequested += OnNavigateToQueue;
            vm.NavigateToHistoryRequested -= OnNavigateToHistory;
            vm.NavigateToHistoryRequested += OnNavigateToHistory;
            vm.NavigateToSettingsRequested -= OnNavigateToSettings;
            vm.NavigateToSettingsRequested += OnNavigateToSettings;
            CurrentView = new AddDownloadView { DataContext = vm };
        }

        private void OnNavigateToQueue(object? sender, EventArgs e) => NavigateToQueue();
        private void OnNavigateToHistory(object? sender, EventArgs e) => NavigateToHistory();
        private void OnNavigateToSettings(object? sender, EventArgs e) => NavigateToSettings();

        public void SetClipboardUrl(string url)
        {
            if (CurrentView is not AddDownloadView)
                NavigateToAddDownload();

            if (CurrentView is AddDownloadView currentView &&
                currentView.DataContext is AddDownloadViewModel viewModel)
            {
                viewModel.Url = url;
            }
        }

        public void SetInterceptedDownload(string url, string? cookies = null, string? referer = null, string? userAgent = null, string? title = null)
        {
            if (CurrentView is not AddDownloadView)
                NavigateToAddDownload();

            if (CurrentView is AddDownloadView currentView &&
                currentView.DataContext is AddDownloadViewModel viewModel)
            {
                viewModel.Url = url;
                if (!string.IsNullOrWhiteSpace(title) && viewModel.CurrentDramaTitle == "No drama loaded")
                {
                    viewModel.CurrentDramaTitle = title;
                }
            }
        }

        [RelayCommand]
        public void NavigateToQueue()
        {
            var vm = _serviceProvider.GetRequiredService<QueueViewModel>();
            vm.BackRequested -= OnQueueBackRequested;
            vm.BackRequested += OnQueueBackRequested;
            vm.StartAutoRefresh();
            
            CurrentView = new QueueView { DataContext = vm };
            _ = vm.LoadJobs();
        }

        private void OnQueueBackRequested(object? sender, EventArgs e)
        {
            NavigateToAddDownload();
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
            vm.BackRequested -= OnSettingsBackRequested;
            vm.BackRequested += OnSettingsBackRequested;
            CurrentView = new SettingsView { DataContext = vm };
        }

        private void OnSettingsBackRequested(object? sender, EventArgs e)
        {
            NavigateToAddDownload();
        }
    }
}
