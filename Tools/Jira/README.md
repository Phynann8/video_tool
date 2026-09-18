# Universal Media Downloader - Jira Integration & Scrum Management

This directory contains everything you need to connect your Jira instance to both your **IDE** and your **AI Assistant**, as well as import the complete Scrum backlog so no unfinished work is lost.

---

## 1. IDE Extension (Atlassian: Jira and Bitbucket)

The official **Atlassian: Jira and Bitbucket** extension (`atlassian.atlascode`) is **already installed** in your IDE environment!

### How to Activate It:
1. Look at your IDE's left sidebar and click on the **Atlassian icon** (or press `Ctrl + Shift + P` and type `Atlassian: Open Settings`).
2. Click **Authenticate with Jira Cloud**.
3. A browser window will open asking to authorize your Atlassian account. Click **Accept**.
4. Once authorized, your **Jira Issues, Scrum Boards, and Active Sprints** will appear directly in your editor sidebar. You can view, transition (To Do -> In Progress -> Done), and create tasks without leaving your code.

---

## 2. AI Agent MCP Extension (Let the AI Manage Jira for You)

You can connect the Jira MCP Server so the AI assistant can query, update, create, and transition Jira issues directly in chat:

### Step 1: Create an Atlassian API Token
1. Go to: [https://id.atlassian.com/manage-profile/security/api-tokens](https://id.atlassian.com/manage-profile/security/api-tokens)
2. Click **Create API token**, label it `UMD`, and copy the token.

### Step 2: Configure Credentials
Copy `Tools/Jira/.env.example` to `Tools/Jira/.env` (which is git-ignored):
```env
JIRA_BASE_URL=https://your-domain.atlassian.net
JIRA_EMAIL=your-email@example.com
JIRA_API_TOKEN=your_token_here
JIRA_PROJECT_KEY=UMD
```

### Step 3: Run the MCP Setup Script
Run in PowerShell:
```powershell
powershell -ExecutionPolicy Bypass -File "Tools\Jira\setup_mcp.ps1"
```
This registers the Jira MCP Server in your global `~/.gemini/config/mcp_config.json`.
After restarting the IDE/session, the AI will have native Jira tools available in chat!

---

## 3. Populating your Scrum Backlog (Epics, Sprints, & Tasks)

Choose either option to populate all 25+ project tasks:

### Option A: Automated REST API Sync (Recommended)
Run:
```bash
python Tools/Jira/sync_jira.py
```
This script connects to your Jira instance, creates the Scrum board, establishes **Sprint 1 (Foundation & Parity)** and **Sprint 2 (Resiliency & Features)**, and creates all Epics, Stories, and Tasks with descriptions.

### Option B: 1-Click CSV Import
1. In Jira, go to **Settings** -> **System** -> **External System Import** -> **CSV**.
2. Select `Tools/Jira/jira_import_tasks.csv`.
3. Choose your project (**UMD**) and click **Begin Import**.

---

## Backlog Structure Summary

### Epics
1. **Networking & Download Acceleration Engine:** Dynamic segmentation, work stealing, connection pooling.
2. **Browser & Windows System Integration:** Extension, Native Host, Named Pipes, Taskbar, Tray, Power APIs.
3. **Media Extractors & Platform Scrapers:** DramaBox, KissKH, Iflix, yt-dlp plugin deployment.
4. **UI Polish & Queue Management:** Settings persistence, pause/resume, bandwidth control.

### Sprints
- **Sprint 1 - Foundation & Parity (Completed):**
  - HTTP Range Capability Probing
  - Dynamic File Segmentation (up to 32 streams)
  - Dynamic Work-Stealing Algorithm
  - SocketsHttpHandler Keep-Alive Connection Pooling
  - Offset-Indexed Local Assembly
  - Manifest V3 Browser Extension & Native Messaging Host
  - Windows Named Pipe IPC Bridge
  - Windows Taskbar Progress & System Tray Integration
  - Win32 Power Management (Sleep Prevention)
- **Sprint 2 - Resiliency & Features (Current / Up Next):**
  - Make Worker Count and Segment Size Configurable in Settings
  - Persistent Resumable Downloads Across Restarts (manifest in SQLite)
  - Bandwidth Limiter / Speed Capping
  - Add Pause and Resume Controls in Queue View
  - Settings Persistence (download path, audio extraction preferences)
  - Investigate Upstream DramaBox 403/404 Responses
  - Full Integration Tests for KissKH and Iflix
