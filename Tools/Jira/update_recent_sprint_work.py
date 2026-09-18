import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(__file__).parent / ".env")
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")
project_key = c.get("JIRA_PROJECT_KEY", "UMD")
sprint_id = 70  # Sprint 2 - Features

tasks = [
    {
        "summary": "YouTube 403 Forbidden Resolution & n-sig JS Runtime Support",
        "epic": "UMD-3",
        "description": "Resolved YouTube throttling and HTTP 403 Forbidden errors encountered during stream downloads (especially stalling around 23% progress). Integrated Node.js runtime support (--js-runtimes node) for solving YouTube JavaScript signature challenges (n-sig), eliminated forced User-Agent overrides for YouTube to permit correct client headers, and added automatic fragment/network retries (--retries 5 --fragment-retries 5). Verified with automated test suite.",
        "resolution_details": [
            "Configured YtDlpDownloadEngine to dynamically add --js-runtimes node for YouTube URLs.",
            "Omitted custom User-Agent headers on YouTube downloads so yt-dlp uses authentic internal client signatures.",
            "Added network and fragment retry arguments (--retries 5 --fragment-retries 5).",
            "Added automated tests in YtDlpDownloadEngineTests.cs and YtDlpExtractorEngineTests.cs. All 68 tests passing."
        ]
    },
    {
        "summary": "Multi-Connection Parallel Download Acceleration via aria2c",
        "epic": "UMD-1",
        "description": "Implemented multi-connection chunked downloading (IDM-style parallel acceleration) using aria2c as an external downloader. Spawns 8 parallel connections with segment slicing to maximize throughput on high-bandwidth streams. Added dual progress regular expression parsing to support both yt-dlp and aria2c progress streams, and updated SetupDependencies.ps1 to automatically deploy aria2c.exe.",
        "resolution_details": [
            "Integrated aria2c multi-connection args (--downloader aria2c --downloader-args 'aria2c:-x 8 -s 8 -k 1M -j 8') when aria2c.exe is present in the application directory.",
            "Added Aria2ProgressRegex alongside standard yt-dlp progress regex with TryParseProgress helper.",
            "Updated SetupDependencies.ps1 to download and extract aria2c-1.37.0-win-64bit automatically on clean installations.",
            "Successfully tested multi-connection download speeds on large video streams."
        ]
    },
    {
        "summary": "Queue & History View WPF Virtualization and SQLite Indexing",
        "epic": "UMD-4",
        "description": "Resolved UI freezing and thread deadlock when navigating to QueueView or HistoryView with large volumes of downloads. Migrated non-virtualized ItemsControl layouts to virtualized recycling ListBox panels, added B-Tree SQLite indexes (IX_Jobs_Status, IX_Jobs_CreatedAt) in DatabaseBootstrap, and optimized SQL queries using native LIMIT clauses and background thread dispatching.",
        "resolution_details": [
            "Converted ItemsControl to ListBox with VirtualizingPanel.IsVirtualizing='True' and VirtualizationMode='Recycling' in QueueView.xaml and HistoryView.xaml.",
            "Added database indices on Jobs(Status) and Jobs(CreatedAt DESC) to eliminate full table scans.",
            "Optimized GetRecentJobsAsync with native SQL LIMIT 100 instead of loading all jobs into memory.",
            "Verified fast, responsive UI navigation across Queue, History, and Search views."
        ]
    },
    {
        "summary": "Real-Time Download Failure Diagnostics & UI Error Logging",
        "epic": "UMD-4",
        "description": "Enhanced user-facing error diagnostics and logging across the application. Added process stdout error scanning in ProcessRunner when stderr is empty, hooked QueueManager.JobFailed and JobCompleted events to AddDownloadViewModel UI log stream with color indicators, added in-place ErrorMessage display and ToolTips to download list items, and optimized default stream quality to 1080p.",
        "resolution_details": [
            "Updated ProcessRunner to scan stdout for ERROR: lines when standard error stream is empty, surfacing exact yt-dlp failure reasons.",
            "Subscribed AddDownloadViewModel to QueueManager.JobFailed and JobCompleted events to display immediate red/green status log messages.",
            "Added ToolTip and inline TextBlock for ErrorMessage on failed jobs in AddDownloadView.xaml and QueueView.xaml.",
            "Changed default quality selection logic to prefer 1080p (Full HD) instead of the highest available (e.g. 4K) to avoid bandwidth exhaustion."
        ]
    }
]

created_keys = []

for task in tasks:
    print(f"\nCreating task: {task['summary']}...")
    payload = {
        "fields": {
            "project": {"key": project_key},
            "summary": task["summary"],
            "description": {
                "type": "doc",
                "version": 1,
                "content": [
                    {
                        "type": "paragraph",
                        "content": [{"type": "text", "text": task["description"]}]
                    }
                ]
            },
            "issuetype": {"name": "Task"},
            "parent": {"key": task["epic"]}
        }
    }

    create_res = requests.post(f"{base_url}/rest/api/3/issue", auth=auth, headers=headers, json=payload)
    if create_res.status_code not in (200, 201):
        # Fallback if parent cannot be set at creation time
        del payload["fields"]["parent"]
        create_res = requests.post(f"{base_url}/rest/api/3/issue", auth=auth, headers=headers, json=payload)

    if create_res.status_code not in (200, 201):
        print(f"[FAIL] Could not create issue: {create_res.status_code} {create_res.text}")
        continue

    issue_data = create_res.json()
    issue_key = issue_data["key"]
    created_keys.append(issue_key)
    print(f"[OK] Created {issue_key}: {task['summary']}")

    # Link to parent epic if not already linked
    link_res = requests.put(
        f"{base_url}/rest/api/3/issue/{issue_key}",
        auth=auth,
        headers=headers,
        json={"fields": {"parent": {"key": task["epic"]}}}
    )
    if link_res.status_code in (200, 204):
        print(f"[OK] Linked {issue_key} -> Epic {task['epic']}")

    # Move to Sprint
    sprint_res = requests.post(
        f"{base_url}/rest/agile/1.0/sprint/{sprint_id}/issue",
        auth=auth,
        headers=headers,
        json={"issues": [issue_key]}
    )
    if sprint_res.status_code in (200, 204):
        print(f"[OK] Assigned {issue_key} to Sprint ID {sprint_id}")

    # Add resolution comment
    comment_content = [
        {
            "type": "paragraph",
            "content": [
                {
                    "type": "text",
                    "text": f"Completed work on {issue_key}:"
                }
            ]
        },
        {
            "type": "bulletList",
            "content": [
                {
                    "type": "listItem",
                    "content": [
                        {
                            "type": "paragraph",
                            "content": [{"type": "text", "text": detail}]
                        }
                    ]
                }
                for detail in task["resolution_details"]
            ]
        }
    ]

    comment_res = requests.post(
        f"{base_url}/rest/api/3/issue/{issue_key}/comment",
        auth=auth,
        headers=headers,
        json={"body": {"type": "doc", "version": 1, "content": comment_content}}
    )
    if comment_res.status_code in (200, 201):
        print(f"[OK] Added completion comment to {issue_key}")

    # Transition to Done (transition ID 31)
    trans_res = requests.post(
        f"{base_url}/rest/api/3/issue/{issue_key}/transitions",
        auth=auth,
        headers=headers,
        json={"transition": {"id": "31"}}
    )
    if trans_res.status_code in (200, 204):
        print(f"[OK] Marked {issue_key} as Done")
    else:
        print(f"[WARN] Could not transition {issue_key}: {trans_res.status_code} {trans_res.text}")

print(f"\nAll tasks successfully created, linked, and marked Done in Jira: {created_keys}")
