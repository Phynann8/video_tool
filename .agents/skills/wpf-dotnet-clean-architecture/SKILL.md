---
name: wpf-dotnet-clean-architecture
description: >-
  Expert guidance for developing, refactoring, and maintaining the Universal Media Downloader
  C# .NET 9 WPF desktop application. Covers Clean Architecture layer separation, MVVM patterns,
  WPF XAML styling, Dispatcher UI thread synchronization, and Dapper/SQLite persistence.
---

# WPF & .NET 9 Clean Architecture Guide

This skill guides development and maintenance of the **Universal Media Downloader** C# .NET 9 WPF application.

## 1. Solution & Architecture Boundaries

The solution is structured into four distinct layers following Clean Architecture principles:

```
Start.UI (WPF Views, ViewModels, Clipboard Monitor, Resource Dictionaries)
   └── Start.App (Use Cases, DownloadService, QueueManager, Application Orchestration)
         └── Start.Infrastructure (SQLite/Dapper, HttpDownloadEngine, YtDlp, Ffmpeg)
               └── Start.Core (Domain Entities: DownloadJob, PlaylistInfo, VideoStream; Interfaces)
```

### Dependency Rules:
1. **`Start.Core`**:
   - Zero external framework dependencies.
   - Contains pure domain entities (`DownloadJob`, `PlaylistInfo`, `VideoStream`, `DownloadStatus`).
   - Defines interfaces (`IDownloadEngine`, `IExtractorEngine`, `IDownloadRepository`, `IQueueManager`).
2. **`Start.Infrastructure`**:
   - Implements interfaces from `Start.Core`.
   - Manages external tools: `yt-dlp`, `ffmpeg`, HTTP multi-segment streaming.
   - Manages SQLite persistence via `Microsoft.Data.Sqlite` and `Dapper`.
3. **`Start.App`**:
   - Implements application workflows (`DownloadService`, `QueueManager`).
   - Routes downloads to appropriate engines (`RoutingDownloadEngine`).
4. **`Start.UI`**:
   - Presentation layer only.
   - Contains XAML Views, ViewModels, Converters, and Controls.
   - Configures Dependency Injection in `App.xaml.cs`.

---

## 2. WPF & MVVM Best Practices

### A. ViewModel Guidelines
- All ViewModels must implement `INotifyPropertyChanged`.
- Prefer `SetField<T>(ref field, value)` helper pattern to raise property change notifications cleanly.
- Collections bound to UI list controls (`ItemsControl`, `ListView`, `DataGrid`) must use `ObservableCollection<T>`.
- Always expose user interactions via `ICommand` (e.g. `RelayCommand`).

### B. UI Thread & Dispatcher Safety
WPF elements can only be modified from the Dispatcher thread. Background tasks, downloader progress callbacks, and worker events must marshal updates to the UI thread:

```csharp
// Use async dispatcher invoke when notifying UI from background threads
await Application.Current.Dispatcher.InvokeAsync(() =>
{
    job.Progress = newProgress;
    job.Speed = speedText;
});
```

### C. Resource Cleanup
- Always clean up native event listeners (such as `WM_CLIPBOARDUPDATE` in `ClipboardUrlMonitor`) when windows or services close.
- Unsubscribe from ViewModel events to avoid memory leaks.

---

## 3. Asynchronous Programming & Cancellation

- **Propagate Cancellation Tokens**: Always pass `CancellationToken` from UI through `QueueManager` -> `DownloadService` -> `HttpDownloadEngine` / `YtDlpDownloadEngine`.
- **Never block the UI thread**: Avoid `.Result` or `.Wait()`. Always use `async` / `await`.
- **Resource handling**: Wrap `HttpClient`, `SqliteConnection`, and file streams in `using` declarations.

---

## 4. SQLite Persistence & Schema Migrations

- Database path: `%LOCALAPPDATA%\UniversalMediaDownloader\media_downloader.db`
- Data access is handled through `SqliteDownloadRepository` using `Dapper`.
- If modifying core entities like `DownloadJob`, update both:
  1. `Start.Infrastructure/Data/DatabaseBootstrap.cs` (table creation queries).
  2. `Start.Infrastructure/Data/SqliteDownloadRepository.cs` (CRUD queries and JSON serialization).

---

## 5. Build & Verification Commands

```powershell
# Build entire solution
dotnet build UniversalMediaDownloader.sln

# Run the WPF Application
dotnet run --project Start.UI/Start.UI.csproj

# Run test suites
dotnet test tests/test_api_client.csproj
```
