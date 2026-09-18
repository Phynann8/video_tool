# AGENTS.md

## Project Overview
Universal Media Downloader is a professional C# .NET 8 WPF desktop application for downloading media from various platforms. It uses Clean Architecture with decoupled provider system supporting YouTube, DramaBox, and other video platforms with automated file joining and high-speed downloads.

## Architecture Map
- `Start.Core/`: Core models, interfaces, and business logic (DownloadJob, PlaylistInfo, etc.)
- `Start.Infrastructure/`: Data access (SQLite), external tools (yt-dlp, ffmpeg), and infrastructure services
- `Start.App/`: Application services (DownloadService, QueueManager) and dependency injection setup
- `Start.UI/`: WPF UI with ViewModels and Views
- `tests/`: Unit tests for core components
- `AI_Agent_Brain/`: AI agent context and documentation

## Main Workflows
- Build solution: `dotnet build UniversalMediaDownloader.sln`
- Run application: `dotnet run --project Start.UI/Start.UI.csproj`
- Run tests: `dotnet test tests/test_api_client.csproj`
- Publish: `dotnet publish Start.UI/Start.UI.csproj -c Release -r win-x64 --self-contained`

## Coding Rules
- Follow Clean Architecture: Core has no dependencies, Infrastructure depends on Core, App/UI depend on both
- Use dependency injection for all services
- Implement INotifyPropertyChanged for ViewModels
- Keep changes scoped to requested features
- Prefer existing patterns over new abstractions
- Use async/await for I/O operations

## Important Concepts
- **DownloadJob**: Core entity with status, progress, streams, metadata
- **ExtractorEngine**: Platform-specific metadata extraction (yt-dlp, DramaBox)
- **DownloadEngine**: Handles actual downloading with routing based on URL
- **MediaProcessor**: Post-processing with ffmpeg (joining, conversion)
- **QueueManager**: Manages concurrent downloads with prioritization
- **DramaBox Integration**: Dual extraction (HTML scraping + API fallback) with RSA-signed authentication

## Common Pitfalls
- Do not modify core models without updating database schema
- External tools (yt-dlp, ffmpeg) must be available in PATH or configured paths
- WPF UI updates must be on main thread; use Dispatcher for background operations
- SQLite database is in %LOCALAPPDATA%/UniversalMediaDownloader/
- Generated files in bin/ and obj/ should not be committed

## Testing Expectations
- Run `dotnet test` after any core logic changes
- UI changes require manual testing as WPF is hard to unit test
- Integration tests should verify end-to-end download flows
- Mock external dependencies (yt-dlp, ffmpeg) for unit tests

## Current Project Status
- Last known working state: Functional media downloader with DramaBox (dual extraction & API fallback verified) and YouTube support
- Known broken areas: None currently identified
- Next planned task: Expand platform support and improve error handling