# Reverse Engineering & Decompilation Insights

## Summary of Reference Tools Analyzed
1. **KRUT DL VERSION 2.2** (Python / PyQt5 decompiled binary via `pyinstxtractor`)
2. **Epic Box** (Electron / Node / Python wrapper)
3. **test_app / dramabox-rest-api-node** (Node.js Express upstream proxy)
4. **test_app / DramaBoxGratis** (Next.js / TypeScript client)

---

## 1. Widevine L3 DRM Key Extraction & Decryption Pipeline (`KeyDive`)
* **Jira Task**: `UMD-34`
* **Source in Reference**: `KRUT DL VERSION 2.2.exe_extracted/KeyDive` (`keydive/core.py`, `keydive/drm/cdm.py`, `keydive/drm/keybox.py`, `keydive/adb/remote.py`, `keydive/drm/protocol/license_pb2.py`).
* **Mechanism**:
  * Automates extraction of Widevine L3 device keys and Content Decryption Module (CDM) binaries (`client_id.bin`, `private_key.pem`).
  * Supports hooking via ADB on Android emulators or loading physical keybox blobs.
  * Captures DRM license challenge from streaming services, signs the request via CDM, exchanges with license servers (`license_pb2`), and derives the AES-128 content keys (`KID:KEY`).
  * Feeds the extracted keys to FFmpeg (`-c:v copy -c:a copy -decryption_key <key>`) or mp4decrypt to produce unencrypted MP4 files.
* **Our Advantage & Plan**:
  * Allows downloading premium 1080p DRM-protected streams on short drama platforms (ShortMax, ReelShort, iQiyi DRM streams).

---

## 2. VIP Episode Bypass & Fallback Stream Resolver
* **Jira Task**: `UMD-35`
* **Source in Reference**: `Dramabox.js` (`getStreamUrl`), `KRUT DL` (`_start_bypass`, `bypass_matches.txt`).
* **Mechanism**:
  * In addition to official ad-reward watch simulations (`/drama-box/chapterv2/watch/reward`), reference tools utilize external resolver proxies (such as `https://regexd.com/base.php?ajax=1&bookId=${bookId}&lang=${lang}&episode=${episode}`).
  * If official tokens/ad-rewards fail or return 403/404, the fallback proxy resolves the direct unwatermarked CDN URLs (`mp4` and `m3u8`) without requiring user coins or VIP status.
* **Our Advantage & Plan**:
  * Implement secondary resolver fallback in `DramaBoxExtractorEngine` whenever primary ad-reward returns empty or locked streams.

---

## 3. In-App VIP Cookie & Session Vault UI
* **Jira Task**: `UMD-26`
* **Source in Reference**: `KRUT DL` (`CookiesDialog`, `_save`, `_clear`, `_refresh_status`).
* **Mechanism**:
  * Provides a user-friendly dialog for pasting browser session cookies (e.g. from Chrome DevTools or extension export).
  * Automatically injects `Cookie: ...` into HTTP request headers for authenticated VIP access, enabling 1080p streams without rate limiting.
* **Our Advantage & Plan**:
  * Expand our settings and dialogs to include a "VIP Cookie Vault" for DramaBox, NetShort, and KissKH.

---

## 4. Live Platform Catalog & Trending Drama Browser Grid
* **Jira Task**: `UMD-36`
* **Source in Reference**: `KRUT DL` (`_browse_platform`, `_populate_browse`, `_reflow_browse_grid`, `_try_hd_url`).
* **Mechanism**:
  * Calls `/drama-box/he001/theater` with categories (`rank`, `latest`, `vip`).
  * Renders a responsive visual card grid with drama posters, titles, and episode counts.
  * Clicking a card immediately navigates to its episode list without forcing users to open a web browser and copy URLs.
  * Uses `_try_hd_url` to strip thumbnail resizing/compression parameters to download full-resolution artwork.
* **Our Advantage & Plan**:
  * Connect our `AddDownloadView` trending section to live API theater queries for 1-click drama downloads.

---

## 5. Direct Stream Link Interceptor & Batch Export Tool
* **Jira Task**: `UMD-37`
* **Source in Reference**: `KRUT DL` (`_start_scrape_links`, `_show_scrape_results`).
* **Mechanism**:
  * An "API Intercept" tool that resolves all direct media URLs across all episodes of a series.
  * Displays them in a tabular dialog with 1-click "Export Links to TXT", "Copy All URLs to Clipboard", and format export for external downloaders (IDM, aria2, curl).
* **Our Advantage & Plan**:
  * Provides advanced users with raw stream URLs for external automation, scripting, or batch IDM exports.

---

## 6. Automatic TS to MP4 Remuxer & HD Artwork Bundler
* **Jira Task**: `UMD-38`
* **Source in Reference**: `KRUT DL` (`_on_convert_toggled`, `_save_thumbnail`, `auto_convert`).
* **Mechanism**:
  * For raw MPEG-TS chunked downloads, automatically invokes lossless FFmpeg stream copy (`ffmpeg -i input.ts -c copy output.mp4`) post-download.
  * Fetches the highest resolution poster artwork and saves it as `cover.jpg` inside the drama folder alongside the downloaded episodes.
