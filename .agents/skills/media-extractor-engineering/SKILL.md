---
name: media-extractor-engineering
description: >-
  Workflows and guidelines for researching, writing, and testing media extraction engines,
  yt-dlp Python plugins, video stream analyzers, and segmented downloaders for Universal Media Downloader.
---

# Media Extractor & Video Downloader Engineering Guide

This skill guides the engineering of platform-specific scrapers, `yt-dlp` plugins, API reverse-engineering, and download engines for **Universal Media Downloader**.

---

## 1. Extractor Architecture

Universal Media Downloader supports two extractor pathways:

1. **`yt-dlp` Extractor Plugins (Recommended for complex platforms)**:
   - Located at: `Tools/yt-dlp-plugins/yt_dlp_plugins/extractor/`
   - Example: `dramabox.py`
   - Deployed alongside `yt-dlp.exe` in `out_publish/yt-dlp-plugins/`
   - Routed via `YtDlpExtractorEngine` in `Start.Infrastructure`.
2. **C# Native Extractors (Direct HTTP/JSON APIs)**:
   - Located at: `Start.Infrastructure/Services/*ExtractorEngine.cs`
   - Implement `IExtractorEngine` interface from `Start.Core.Interfaces`.
   - Examples: `DramaBoxExtractorEngine`, `IflixExtractorEngine`, `KissKhExtractorEngine`.

---

## 2. Reverse-Engineering Workflow for New Video Platforms

When adding or fixing video platform support (e.g. Netshort, DramaBox, Iflix, KissKH):

### Step 1: Web & API Reconnaissance
- Extract embedded state from HTML: Look for `<script id="__NEXT_DATA__">`, `window.__INITIAL_STATE__`, or inline JSON objects.
- Inspect network requests using browser DevTools or Puppeteer:
  - Identify video stream manifests: HLS (`.m3u8`) or DASH (`.mpd`).
  - Check request headers: `Referer`, `Origin`, `User-Agent`, and custom auth headers (`Authorization`, `x-token`, `x-sign`).
  - Identify cryptographic signatures (e.g. RSA-SHA256, HMAC-SHA256, MD5/AES).

### Step 2: Standalone Prototype Script
Always test extraction in an isolated Python or C# script in the root or `tests/` before integrating:
- Python: `test_<platform>.py`
- Validate episode pagination, title extraction, stream resolution matching, and audio/video sync.

### Step 3: Integrating into `yt-dlp-plugins`
When writing a `yt-dlp` plugin (`yt_dlp_plugins/extractor/<platform>.py`):
- Inherit from `InfoExtractor`.
- Define `_VALID_URL` regex accurately matching the platform's URL schema.
- Implement `_real_extract(self, url)`:
  - Parse web page or make direct JSON API calls using `self._download_webpage` or `self._download_json`.
  - Extract stream formats with `self._extract_m3u8_formats` or build format dictionaries (`url`, `ext`, `quality`, `height`, `http_headers`).
  - Return an entry dictionary with `id`, `title`, `formats`, `thumbnail`, and playlist entries.

---

## 3. High-Speed Segmented Downloading (`HttpDownloadEngine`)

For direct MP4 / video chunk downloads, `Start.Infrastructure.Services.HttpDownloadEngine` handles multi-threaded range requests:

- **Range Check**: Probes the endpoint with `HEAD` request to check `Accept-Ranges: bytes` and `Content-Length`.
- **Chunk Slicing**: Splits files into parallel 4 MB segments across up to 8 concurrent workers.
- **Fault-Tolerance**: Each worker writes `.part` files; failed segments retry independently.
- **Assembly & Verification**: Verifies final file length against `Content-Length` before committing the file.
- **Fallback**: Automatically falls back to sequential streaming if HTTP range requests are not accepted.

---

## 4. Media Post-Processing with FFmpeg

- Managed by `Start.Infrastructure.Services.FfmpegMediaProcessor`.
- Capabilities:
  - Combining separate video and audio streams into an `.mp4` container.
  - Converting `.ts` chunks or `.m3u8` streams to consolidated `.mp4`.
  - Subtitle embedding and stream tagging.

---

## 5. Verification Commands

```powershell
# Test a standalone scraper script
python test_kisskh.py

# Test yt-dlp plugin discovery and extraction
& "Tools/yt-dlp/yt-dlp.exe" --verbose "https://example.com/video/url"

# Build and verify solution
dotnet build UniversalMediaDownloader.sln
```
