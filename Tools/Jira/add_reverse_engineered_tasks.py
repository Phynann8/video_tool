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

new_tasks = [
    {
        "summary": "Widevine L3 DRM Key Extraction & Decryption Pipeline (KeyDive Integration)",
        "epic": "UMD-3",
        "description": "Reverse engineering of KRUT DL identified integrated Widevine L3 Content Decryption Module (CDM) capabilities (KeyDive). Implement an automated Widevine L3 CDM keybox loader and OEMCrypto license interceptor to decrypt protected 1080p DRM streams across supported streaming platforms."
    },
    {
        "summary": "VIP Episode Bypass & Fallback Stream Resolver Integration",
        "epic": "UMD-3",
        "description": "Decompilation revealed secondary stream resolver endpoints (e.g. regexd.com / bypass_matches) utilized by competitors when ad-reward unlocks fail. Implement a resilient fallback resolver pipeline for locked episodes to obtain direct, non-watermarked MP4/M3U8 URLs."
    },
    {
        "summary": "Live Platform Catalog & Trending Drama Browser Grid",
        "epic": "UMD-4",
        "description": "Competitor tools (KRUT DL / Epic Box) feature an in-app visual catalog that queries platform theater APIs (/drama-box/he001/theater). Implement a responsive card grid with HD poster artwork (using _try_hd_url parameter filtering), rank badges, and 1-click loading without requiring users to copy-paste URLs from browsers."
    },
    {
        "summary": "Direct Stream Link Interceptor & Batch Export Tool",
        "epic": "UMD-4",
        "description": "Decompiled tools feature an 'API Intercept' dialog allowing users to scrape and inspect all direct playable video URLs across all episodes. Add a dialog/panel to export direct CDN URLs, durations, and formats to TXT, clipboard, or external tools."
    },
    {
        "summary": "Automatic TS to MP4 Remuxer & HD Artwork Bundler",
        "epic": "UMD-1",
        "description": "Provide a post-download automated lossless remuxer (-c copy) that converts raw MPEG-TS (.ts) segments into playable MP4 containers, and saves full-resolution uncompressed poster art (cover.jpg) in the output drama folder."
    }
]

created_keys = []

for task in new_tasks:
    print(f"\nCreating To-Do task: {task['summary']}...")
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
    requests.put(
        f"{base_url}/rest/api/3/issue/{issue_key}",
        auth=auth,
        headers=headers,
        json={"fields": {"parent": {"key": task["epic"]}}}
    )

    # Move to Sprint 2 (Sprint ID 70)
    requests.post(
        f"{base_url}/rest/agile/1.0/sprint/{sprint_id}/issue",
        auth=auth,
        headers=headers,
        json={"issues": [issue_key]}
    )
    print(f"[OK] Assigned {issue_key} to Sprint ID {sprint_id} (Status: To Do)")

print(f"\nAll reverse-engineered tasks successfully created in Jira: {created_keys}")
