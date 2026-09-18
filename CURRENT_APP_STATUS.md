# Universal Media Downloader: Current Status

**Updated:** 2026-09-17
**Application:** Universal Media Downloader
**Platform:** Windows WPF desktop application
**Framework:** C# / .NET 9 (`net9.0-windows`)
**Architecture:** Clean Architecture with Core, Application, Infrastructure, and UI layers

## Current Purpose

Universal Media Downloader accepts media URLs, extracts metadata and available streams, lets the user choose episodes and quality, queues downloads, reports progress, and stores download history locally.

## What Works Now

### User Interface

- WPF desktop interface.
- Platform selection and URL entry.
- Episode/playlist display and selection.
- Select-all and select-none controls.
- Video quality selection when metadata provides multiple streams.
- Download queue and history views.
- Folder selection for download destinations.
- Progress, speed, ETA, status, and cancellation display.

### Extraction

- yt-dlp-backed metadata extraction for supported yt-dlp platforms.
- yt-dlp plugin deployment support.
- Custom DramaBox yt-dlp extractor at:
  `Tools/yt-dlp-plugins/yt_dlp_plugins/extractor/dramabox.py`
- DramaBox plugin discovery from the published executable path:
  `out_publish/yt-dlp-plugins/dramabox/yt_dlp_plugins/`
- Existing C# extractors for DramaBox, Iflix, and KissKH remain in the repository.
- DramaBox URLs are currently routed through `YtDlpExtractorEngine`, allowing the Python plugin to handle them.
- DramaBox plugin includes:
  - URL and book ID parsing.
  - `__NEXT_DATA__` HTML fallback.
  - API token bootstrap support.
  - RSA-SHA256 signing implemented without third-party Python crypto dependencies.
  - Episode pagination.
  - CDN and quality URL normalization.

### Downloading

- yt-dlp handles complex platform downloads, cookies, format selection, HLS/DASH, retries, and ffmpeg orchestration.
- Direct HTTP(S) downloads use `HttpDownloadEngine`.
- Direct HTTP downloads now:
  - Probe for content length and byte-range support.
  - Download supported files using parallel 4 MB ranges.
  - Use up to 8 concurrent workers.
  - Retry failed segments independently.
  - Write temporary part files.
  - Assemble parts in order.
  - Verify final file length before replacing the destination file.
  - Fall back to sequential downloading when range requests are unsupported.
- ffmpeg is bundled for media merging and post-processing.
- Download jobs are queued and can be cancelled.

### Persistence

- SQLite stores download jobs and history under the user's local application data directory.
- Repository and database bootstrap services are registered through dependency injection.

### Windows Integration Completed

- Native clipboard URL monitoring is implemented while the main WPF window is open.
- The monitor uses `AddClipboardFormatListener` and `WM_CLIPBOARDUPDATE`.
- Copied HTTP(S) URLs populate the download URL field and open the Add Download view.
- Clipboard monitoring does not automatically start a download.
- Clipboard monitoring is cleaned up when the window closes.

## Confirmed Additions

These additions have been explicitly discussed and accepted for the application:

- yt-dlp extractor plugin architecture for site-specific scrapers.
- DramaBox Python plugin deployment beside the bundled yt-dlp executable.
- Routing DramaBox URL analysis through yt-dlp.
- Segmented direct HTTP downloading with range requests.
- Clipboard URL monitoring.

## Completed During This Work

- Created `Tools/yt-dlp-plugins/yt_dlp_plugins/extractor/dramabox.py`.
- Created plugin installation documentation at `Tools/yt-dlp-plugins/README.md`.
- Added plugin staging to `Start.UI/Start.UI.csproj` and `SetupDependencies.ps1`.
- Corrected published plugin layout to the named package path required by standalone yt-dlp.
- Updated DramaBox URL routing in `Start.App/Services/DownloadService.cs`.
- Added `Start.UI/Services/ClipboardUrlMonitor.cs`.
- Connected clipboard URL events through `MainWindow` and `MainViewModel`.
- Added range-capable segmented downloading to `Start.Infrastructure/Services/HttpDownloadEngine.cs`.
- Rebuilt the full solution successfully after the completed changes.

## Remaining Work

### High Priority

- Add automated tests for:
  - Range capability probing.
  - Partial-content validation.
  - Segment retry behavior.
  - Ordered assembly and file-length verification.
  - Sequential fallback.
  - Clipboard URL detection.
- Test segmented downloading against a controlled local HTTP range server.
- Add better cleanup for interrupted assembly and cancellation.
- [COMPLETED - UMD-17] Resumable state across application restarts: existing .part files are scanned on start/resume, missing byte ranges computed, and partial downloads preserved on disk.
- [COMPLETED - UMD-20] Pause & Resume controls in Queue View: per-item Pause/Resume/Retry buttons and top-bar Pause All / Resume All.
- [COMPLETED - UMD-16] Configurable worker count (1-32) and segment slice size (1-16MB) in HttpDownloadEngine.
- [COMPLETED - UMD-21] Settings persistence across app sessions in SQLite.
- [COMPLETED - UMD-22] Native clipboard monitor setting toggle and top-bar controls.
- [COMPLETED - UMD-24] Full integration tests for KissKH and Iflix extractors with mock HTTP and CDN validation.
- Add optional checksum/hash verification when the server provides a trusted checksum.

### Windows System Integration

Not implemented yet:

- Explorer shell extension or Explorer context-menu registration.
- Windows Task Scheduler integration for scheduled downloads.
- Dial-up/RAS connection management.
- Shutdown, sleep, or hibernate actions through Windows power APIs.
- System-tray/background mode for clipboard monitoring.
- User-facing settings to enable or disable clipboard monitoring.

These features should be added with explicit user controls and confirmation. Shutdown and power actions must never run silently.

### Download and Networking

- Add proxy configuration to the UI and direct HTTP engine.
- Add configurable bandwidth limits and connection limits.
- Improve progress throttling for highly concurrent segmented downloads.
- Preserve authentication headers/cookies when direct URLs require them.
- Handle expiring signed CDN URLs during long segmented downloads.
- Add better HTTP status diagnostics for blocked or expired URLs.

### yt-dlp and Platform Support

- Refresh the bundled yt-dlp executable regularly.
- Add live integration tests for plugin discovery without relying on changing external sites.
- Investigate current DramaBox upstream 403/404 responses; local RSA signing and plugin loading work, but the upstream service currently rejects the tested API/page requests.
- Verify which platform names shown in the UI have working extractor implementations. The UI lists several future platforms that are not yet separate providers.

### UI and Product Polish

- Persist settings such as default download path.
- Add a clear clipboard-monitor status indicator.
- Add scheduled-download controls after the scheduling workflow is defined.
- Improve error messages and recovery actions.
- Add pause/resume controls for queued and active jobs.
- Add archive/deduplication support for completed downloads.

## Current Architecture

```text
WPF UI
  -> ViewModels
  -> DownloadService
  -> IExtractorEngine
       -> YtDlpExtractorEngine
            -> yt-dlp.exe + yt-dlp plugins
       -> Legacy platform extractors
  -> QueueManager
  -> IDownloadEngine
       -> YtDlpDownloadEngine
            -> yt-dlp.exe + ffmpeg
       -> RoutingDownloadEngine
            -> HttpDownloadEngine
                 -> range-capable direct HTTP downloader
  -> SQLite repository
```

## Verification Status

- Full solution build: passed.
- yt-dlp plugin discovery from published output: passed.
- DramaBox RSA signer self-test: passed.
- Offline plugin URL and helper validation: passed.
- Live DramaBox extraction: blocked by upstream HTTP 403/404 responses.
- Segmented downloader against a controlled range server: not yet tested.
- Windows clipboard listener: compiled and integrated; manual desktop verification remains recommended.

### Windows System & Browser Integration Completed

- Native clipboard URL monitoring is implemented while the main WPF window is open (`WM_CLIPBOARDUPDATE`).
- Windows Taskbar progress integration via `ITaskbarProgressService` (`TaskbarItemInfo`) displaying real-time download percentage and error states on the Windows taskbar.
- System Tray integration via `ISystemTrayService` (`Shell_NotifyIcon`) supporting minimize to tray, notification balloons on completion, and restore on click.
- Win32 Power Management via `IPowerManagementService` (`SetThreadExecutionState`) preventing Windows sleep and network suspension during active download queues.
- Chromium / Edge Browser Interception Layer:
  - WebExtension (Manifest V3) for context menu and popup media capture.
  - Native Messaging Host (`UniversalMediaDownloader.NativeHost`) adhering to Chromium binary length-prefix protocol.
  - Windows Named Pipe IPC (`UniversalMediaDownloaderPipe`) transferring intercepted URLs, page titles, and session cookies directly to the running application.
- Explorer Shell Context Menu installer/uninstaller scripts (`Tools/ShellIntegration/register-shell-menu.ps1`).

## Confirmed Additions

These additions have been explicitly discussed and accepted for the application:

- yt-dlp extractor plugin architecture for site-specific scrapers.
- DramaBox Python plugin deployment beside the bundled yt-dlp executable.
- Routing DramaBox URL analysis through yt-dlp.
- Segmented direct HTTP downloading with range requests.
- Clipboard URL monitoring.
- Native Taskbar Progress, System Tray, and Power Management integration.
- Browser extension interception with Native Messaging Host and Named Pipe IPC.

## Completed During This Work

- Fixed verbatim string escaping in `NetShortExtractorEngine.cs`.
- Added `IPowerManagementService` and `PowerManagementService` using Win32 `SetThreadExecutionState`.
- Connected `QueueManager` to prevent system sleep while downloads are running.
- Added `TaskbarProgressService` updating `MainWindow.TaskbarItemInfo`.
- Added `SystemTrayService` implementing native Win32 tray icon and completion balloon notifications.
- Added `NativePipeServerService` to listen on `\\.\pipe\UniversalMediaDownloaderPipe`.
- Built `Tools/NativeHost/UniversalMediaDownloader.NativeHost` (.NET 9 console app) for Chromium Native Messaging.
- Built `Tools/BrowserExtension` (Manifest V3 WebExtension with cookies and context menu interception).
- Created automated registration and unregistration scripts for Chrome/Edge and Explorer context menus.
- Added browser session metadata (`Cookies`, `Referer`, `UserAgent`) to `DownloadJob` and wired them into `HttpDownloadEngine`.
- Rebuilt the full solution and verified with passing unit tests.

## Remaining Work

### High Priority

- Add automated tests for:
  - Range capability probing.
  - Partial-content validation.
  - Segment retry behavior.
  - Ordered assembly and file-length verification.
  - Sequential fallback.
  - Clipboard URL detection.
- Test segmented downloading against a controlled local HTTP range server.
- Add better cleanup for interrupted assembly and cancellation.
- [COMPLETED - UMD-17] Resumable state across application restarts: existing .part files are scanned on start/resume, missing byte ranges computed, and partial downloads preserved on disk.
- [COMPLETED - UMD-20] Pause & Resume controls in Queue View: per-item Pause/Resume/Retry buttons and top-bar Pause All / Resume All.
- [COMPLETED - UMD-16] Configurable worker count (1-32) and segment slice size (1-16MB) in HttpDownloadEngine.
- [COMPLETED - UMD-21] Settings persistence across app sessions in SQLite.
- [COMPLETED - UMD-22] Native clipboard monitor setting toggle and top-bar controls.
- [COMPLETED - UMD-24] Full integration tests for KissKH and Iflix extractors with mock HTTP and CDN validation.
- Add optional checksum/hash verification when the server provides a trusted checksum.

### Windows System Integration
- Dial-up/RAS connection management (optional / legacy).
- In-process COM DLL shell extension (superseded by modern registry command verbs and MSIX sparse packages).

### Download and Networking

- Add proxy configuration to the UI and direct HTTP engine.
- Add configurable bandwidth limits and connection limits.
- Improve progress throttling for highly concurrent segmented downloads.
- Handle expiring signed CDN URLs during long segmented downloads.
- Add better HTTP status diagnostics for blocked or expired URLs.

### yt-dlp and Platform Support

- Refresh the bundled yt-dlp executable regularly.
- Add live integration tests for plugin discovery without relying on changing external sites.
- Investigate current DramaBox upstream 403/404 responses; local RSA signing and plugin loading work, but the upstream service currently rejects the tested API/page requests.
- Verify which platform names shown in the UI have working extractor implementations. The UI lists several future platforms that are not yet separate providers.

### UI and Product Polish

- Persist settings such as default download path.
- Add a clear clipboard-monitor status indicator.
- Add scheduled-download controls after the scheduling workflow is defined.
- Improve error messages and recovery actions.
- Add pause/resume controls for queued and active jobs.
- Add archive/deduplication support for completed downloads.

## Current Architecture

```text
Browser Extension (Manifest V3)
  -> Native Messaging Host (UniversalMediaDownloader.NativeHost)
       -> Named Pipe IPC (\\.\pipe\UniversalMediaDownloaderPipe)
            -> WPF UI (Start.UI)
                 -> ViewModels (MainViewModel / AddDownloadViewModel)
                 -> TaskbarProgressService (TaskbarItemInfo)
                 -> SystemTrayService (Shell_NotifyIcon)
                 -> DownloadService
                 -> IExtractorEngine
                      -> YtDlpExtractorEngine (yt-dlp.exe + plugins)
                      -> Legacy platform extractors
                 -> QueueManager (with IPowerManagementService sleep prevention)
                 -> IDownloadEngine
                      -> YtDlpDownloadEngine (yt-dlp.exe + ffmpeg)
                      -> RoutingDownloadEngine
                            -> HttpDownloadEngine (dynamic segmentation + work-stealing + pooled socket re-use)
                 -> SQLite repository
```

## Verification Status

- Full solution build: passed (0 errors, 0 warnings).
- Unit tests: 10 passed (including dynamic range partitioning, work stealing, gap-free assembly, power management, and native messaging framing).
- yt-dlp plugin discovery from published output: passed.
- DramaBox RSA signer self-test: passed.
- Offline plugin URL and helper validation: passed.
- Windows clipboard listener: compiled and integrated.
- Native messaging protocol: framed binary tests passed.
- Dynamic byte range downloader: verified with in-memory HTTP byte server.
