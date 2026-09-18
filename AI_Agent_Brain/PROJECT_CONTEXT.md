# Project Context

## What The App Does
Universal Media Downloader is a comprehensive desktop application for downloading video and audio content from multiple online platforms. It provides a user-friendly WPF interface for managing download queues, selecting quality options, and monitoring progress. The application supports batch downloads from playlists, automatic format detection, and post-processing features like file joining and conversion.

Key features:
- Multi-platform support (YouTube, DramaBox, and extensible provider system)
- Quality selection (video resolution, audio bitrate)
- Playlist/episode management
- Concurrent download queuing
- Progress tracking with speed/ETA
- SQLite-based persistence
- Clean Architecture for maintainability

## Data Flow
1. **Input**: User provides URLs (single videos or playlists)
2. **Analysis**: ExtractorEngine analyzes URL and fetches metadata (title, duration, available streams)
3. **Selection**: User chooses quality options from available video/audio streams
4. **Queue**: DownloadJob added to QueueManager for processing
5. **Download**: DownloadEngine routes to appropriate handler (yt-dlp or HTTP)
6. **Processing**: MediaProcessor uses ffmpeg for joining/merging if needed
7. **Output**: Files saved to user-specified locations with metadata

## Important Files
| Path | Purpose |
|------|---------|
| `Start.Core/ModelsAndInterfaces.cs` | Core domain models (DownloadJob, PlaylistInfo, StreamInfo) |
| `Start.UI/App.xaml.cs` | WPF application entry point and DI configuration |
| `Start.App/Services/DownloadService.cs` | Main orchestration service |
| `Start.App/Services/QueueManager.cs` | Download queue management |
| `Start.Infrastructure/Services/YtDlpExtractorEngine.cs` | YouTube metadata extraction |
| `Start.Infrastructure/Services/DramaBoxExtractorEngine.cs` | DramaBox platform support (dual extraction: HTML scraping + API) |
| `Start.Infrastructure/Services/DramaboxClient.cs` | DramaBox API client with RSA authentication |
| `Start.Infrastructure/Services/DramaboxCrypto.cs` | Cryptographic utilities for API signing |
| `Start.Infrastructure/Data/SqliteDownloadRepository.cs` | SQLite persistence layer |
| `Start.UI/ViewModels/MainViewModel.cs` | Main UI logic |
| `UniversalMediaDownloader.sln` | Visual Studio solution file |

## DramaBox Integration Details

### **Dual Extraction Strategy:**
1. **Primary Method**: HTML scraping from DramaBox pages
   - Extracts `__NEXT_DATA__` JSON from page scripts
   - Parses episode metadata, CDN information, and video URLs
   - Handles both free and locked content detection

2. **Fallback Method**: Official API calls
   - Uses RSA-signed authentication with device simulation
   - Calls `/drama-box/chapterv2/batch/load` endpoint
   - Automatically handles pagination by looping through sequential `index` batches of 6 until `chapterCount` is reached
   - Bypasses website restrictions for locked content

### **Authentication System:**
- **RSA Signing**: Uses private key for request signing
- **Device Simulation**: Generates realistic device IDs and Android IDs
- **Token Management**: Obtains and manages API tokens
- **Request Signing**: Signs all API requests with timestamp + body + device info

### **Video Quality Handling:**
- Supports multiple resolutions (720p, 1080p, etc.)
- CDN routing with multiple domain support
- Quality preference ordering (highest first)
- Fallback to MP4 URLs when CDN unavailable

### **Error Recovery:**
- Graceful fallback from scraping to API
- Retry mechanisms for failed requests
- Alternative CDN selection
- Comprehensive error logging

## Feature Status
### Done:
- Core download functionality for YouTube and DramaBox
- WPF UI with download queue management
- SQLite persistence for download history
- Quality selection and stream management
- Playlist support with episode selection
- Progress tracking and cancellation
- Basic error handling and retry logic
- DramaBox dual extraction (scraping + API)
- RSA-signed authentication for DramaBox API
- End-to-end download verification for DramaBox API fallback
- Fully-automated API pagination handling to fetch all episodes (unrestricted batch limits)
- Deprecation and removal of external decryption proxy dependency

### In Progress:
- Enhanced error recovery mechanisms
- Additional platform providers
- Advanced post-processing options

### Planned:
- Batch export/import of download lists
- Download scheduling
- Proxy support
- Advanced filtering and search
- Cloud storage integration

### Known Issues:
- yt-dlp dependency requires manual installation
- Some platforms may have anti-bot measures
- Large playlist processing can be memory-intensive
- UI responsiveness during concurrent downloads
- **Missing HTTP Range header support for resumable downloads** (recommended for production)

## Decisions
### Architecture Choice:
- **Decision**: Clean Architecture (Onion Architecture)
- **Reason**: Separation of concerns, testability, and maintainability for a complex media processing application
- **Tradeoff**: More boilerplate code vs. long-term maintainability

### Tech Stack:
- **Decision**: C# .NET 8 with WPF
- **Reason**: Mature ecosystem, strong tooling, cross-platform potential
- **Tradeoff**: Windows-only deployment vs. development productivity

### Database:
- **Decision**: SQLite with local file storage
- **Reason**: No server requirements, ACID compliance, good for desktop apps
- **Tradeoff**: Single-user only vs. simplicity

### External Tools:
- **Decision**: yt-dlp for extraction, ffmpeg for processing
- **Reason**: Industry standard tools with comprehensive format support
- **Tradeoff**: External dependencies vs. comprehensive media handling

### DramaBox Integration:
- **Decision**: Dual extraction (scraping + API) with RSA authentication
- **Reason**: Maximum compatibility and access to both free and premium content
- **Tradeoff**: Complexity vs. reliability and feature completeness

### Dependency Injection:
- **Decision**: Microsoft.Extensions.DependencyInjection
- **Reason**: Built-in .NET support, familiar patterns
- **Tradeoff**: Runtime configuration vs. compile-time safety