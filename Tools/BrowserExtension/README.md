# Universal Media Downloader - Browser Integration Extension

This browser extension bridges Google Chrome, Microsoft Edge, Brave, and other Chromium-based browsers with Universal Media Downloader using the Chrome Native Messaging API.

## Features
- **Right-Click Context Menu:** Right click any video, audio, or download link and choose **"Download with Universal Media Downloader"**.
- **One-Click Extension Popup:** Click the extension icon and hit **"Download This Page"** to extract the current page stream.
- **Session Cookie Interception:** Automatically grabs the current domain's cookies and User-Agent, ensuring restricted and signed CDN streaming links can be downloaded without authentication errors.
- **Automatic App Activation:** If the desktop application is running minimized in the system tray, it restores the window and loads the URL. If the app is closed, the Native Host starts the app automatically.

## Quick Installation

### Step 1: Register the Native Host
Open PowerShell and run:
```powershell
powershell -ExecutionPolicy Bypass -File ..\NativeHost\register-host.ps1
```
This registers `com.universalmediadownloader.nativehost` in your user registry (`HKCU:\Software\Google\Chrome\NativeMessagingHosts` and `HKCU:\Software\Microsoft\Edge\NativeMessagingHosts`).

### Step 2: Load the Extension into your Browser
1. Open your browser:
   - For **Google Chrome**: Navigate to `chrome://extensions`
   - For **Microsoft Edge**: Navigate to `edge://extensions`
2. Enable **Developer mode** (toggle in top-right or sidebar).
3. Click **Load unpacked** (or "Load unpacked extension").
4. Select this directory: `Tools/BrowserExtension`.
5. The extension is now installed and active!
