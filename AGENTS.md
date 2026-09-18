# Project Overview: Universal Media Downloader

Universal Media Downloader is a professional Windows desktop application built with **C# / .NET 9** and **WPF** using Clean Architecture. It provides high-speed media downloading, yt-dlp plugin support, direct segmented HTTP downloading, and SQLite history persistence.

---

## 🏗️ Architecture Layers
- **`Start.Core`**: Domain models (`DownloadJob`, `PlaylistInfo`, `VideoStream`), status enums, and core abstractions. Zero external dependencies.
- **`Start.Infrastructure`**: Implementation layer for SQLite persistence (Dapper), external process execution (`yt-dlp`, `ffmpeg`), and `HttpDownloadEngine` (multi-threaded parallel chunk downloader).
- **`Start.App`**: Business logic, use cases, `DownloadService`, and `QueueManager`.
- **`Start.UI`**: WPF presentation layer, XAML views, ViewModels (MVVM), styling, and `ClipboardUrlMonitor` (`WM_CLIPBOARDUPDATE`).
- **`Tools/yt-dlp-plugins`**: Custom Python extractors for platforms like DramaBox, integrated into yt-dlp.

---

## 🛠️ Key Workflows & Commands
- **Build Solution**: `dotnet build UniversalMediaDownloader.sln`
- **Run Application**: `dotnet run --project Start.UI/Start.UI.csproj`
- **Run Tests**: `dotnet test tests/test_api_client.csproj`
- **Publish**: `dotnet publish Start.UI/Start.UI.csproj -c Release -r win-x64 --self-contained`

---

## 📐 Coding Rules
1. **Clean Architecture Isolation**: Never introduce dependencies from `Start.Core` to outer layers.
2. **MVVM & WPF Threading**: All UI property changes must notify via `INotifyPropertyChanged`. Never update UI properties from background threads without `Application.Current.Dispatcher.InvokeAsync`.
3. **Database Consistency**: SQLite database resides at `%LOCALAPPDATA%\UniversalMediaDownloader\media_downloader.db`. Always update `DatabaseBootstrap.cs` and `SqliteDownloadRepository.cs` in sync when modifying schema.
4. **Cancellation**: Propagate `CancellationToken` throughout download pipelines to ensure clean cancellation.

---

## 🧠 Custom Skills Available
- [wpf-dotnet-clean-architecture](file:///.agents/skills/wpf-dotnet-clean-architecture/SKILL.md): WPF MVVM, .NET 9 patterns, XAML UX, and DI setup.
- [media-extractor-engineering](file:///.agents/skills/media-extractor-engineering/SKILL.md): Reverse-engineering video sites, yt-dlp plugin dev, HLS/DASH streams, and segmented downloading.
