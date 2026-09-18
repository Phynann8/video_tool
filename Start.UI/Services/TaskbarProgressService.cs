using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Shell;
using Start.App.Services;
using Start.Core.Models;

namespace Start.UI.Services
{
    public interface ITaskbarProgressService
    {
        void Attach(Window window);
        void SetProgress(double value, TaskbarItemProgressState state);
        void Clear();
    }

    public sealed class TaskbarProgressService : ITaskbarProgressService, IDisposable
    {
        private readonly IQueueManager _queueManager;
        private readonly ConcurrentDictionary<Guid, double> _activeProgress = new();
        private Window? _window;
        private TaskbarItemInfo? _taskbarInfo;

        public TaskbarProgressService(IQueueManager queueManager)
        {
            _queueManager = queueManager;
            _queueManager.JobUpdated += OnJobUpdated;
            _queueManager.JobCompleted += OnJobCompleted;
            _queueManager.JobFailed += OnJobFailed;
        }

        public void Attach(Window window)
        {
            _window = window;
            _taskbarInfo = window.TaskbarItemInfo ?? new TaskbarItemInfo();
            window.TaskbarItemInfo = _taskbarInfo;
        }

        public void SetProgress(double value, TaskbarItemProgressState state)
        {
            if (_window == null || _taskbarInfo == null) return;

            _window.Dispatcher.InvokeAsync(() =>
            {
                _taskbarInfo.ProgressValue = Math.Clamp(value, 0.0, 1.0);
                _taskbarInfo.ProgressState = state;
            });
        }

        public void Clear()
        {
            _activeProgress.Clear();
            if (_window == null || _taskbarInfo == null) return;

            _window.Dispatcher.InvokeAsync(() =>
            {
                _taskbarInfo.ProgressValue = 0;
                _taskbarInfo.ProgressState = TaskbarItemProgressState.None;
            });
        }

        private void OnJobUpdated(object? sender, DownloadJob job)
        {
            if (job.Status == JobStatus.Downloading || job.Status == JobStatus.Processing)
            {
                _activeProgress[job.Id] = job.Progress / 100.0;
                UpdateTaskbar();
            }
        }

        private void OnJobCompleted(object? sender, DownloadJob job)
        {
            _activeProgress.TryRemove(job.Id, out _);
            UpdateTaskbar();
        }

        private void OnJobFailed(object? sender, DownloadJob job)
        {
            _activeProgress.TryRemove(job.Id, out _);
            if (_window != null && _taskbarInfo != null)
            {
                _window.Dispatcher.InvokeAsync(() =>
                {
                    _taskbarInfo.ProgressState = TaskbarItemProgressState.Error;
                });
            }
        }

        private void UpdateTaskbar()
        {
            if (_window == null || _taskbarInfo == null) return;

            if (_activeProgress.IsEmpty)
            {
                _window.Dispatcher.InvokeAsync(() =>
                {
                    _taskbarInfo.ProgressValue = 0;
                    _taskbarInfo.ProgressState = TaskbarItemProgressState.None;
                });
                return;
            }

            double total = 0;
            int count = 0;
            foreach (var val in _activeProgress.Values)
            {
                total += val;
                count++;
            }

            double avg = count > 0 ? total / count : 0;

            _window.Dispatcher.InvokeAsync(() =>
            {
                _taskbarInfo.ProgressValue = Math.Clamp(avg, 0.0, 1.0);
                _taskbarInfo.ProgressState = TaskbarItemProgressState.Normal;
            });
        }

        public void Dispose()
        {
            _queueManager.JobUpdated -= OnJobUpdated;
            _queueManager.JobCompleted -= OnJobCompleted;
            _queueManager.JobFailed -= OnJobFailed;
            Clear();
        }
    }
}
