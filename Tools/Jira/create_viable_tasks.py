import os, requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(r"Tools\Jira\.env"))
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")
proj = c.get("JIRA_PROJECT_KEY", "UMD")

# Get sprint 2 if exists
board_res = requests.get(f"{base_url}/rest/agile/1.0/board?projectKeyOrId={proj}", auth=auth, headers=headers)
board_id = board_res.json().get("values", [{}])[0].get("id")
sprint_id = None
if board_id:
    sprints_res = requests.get(f"{base_url}/rest/agile/1.0/board/{board_id}/sprint", auth=auth, headers=headers)
    for sp in sprints_res.json().get("values", []):
        if "sprint 2" in sp["name"].lower():
            sprint_id = sp["id"]
            break

new_tasks = [
    {
        "summary": "JIT Signed URL Resolution for Expiring CDN Streams",
        "description": "Platforms like DramaBox and Cloudflare Stream issue short-lived HMAC-signed URLs (30-60s expiration). Defer URL extraction in QueueProcessor so that each queued item resolves its direct media link immediately before downloading starts, preventing 403 Forbidden timeouts on long episode queues."
    },
    {
        "summary": "Authenticated Session & Browser Cookie Vault Integration",
        "description": "Allow users to import cookies/session tokens from installed desktop browsers (Chrome, Edge, Brave, Firefox) or specify user session credentials in SettingsView. Forwards active cookies, User-Agent, and Authorization headers across HttpDownloadEngine and yt-dlp to allow downloading of user-purchased/VIP unlocked content."
    },
    {
        "summary": "Native HLS AES-128 Decryption Pipeline & Key Header Forwarding",
        "description": "Enhance the HLS stream download pipeline to explicitly parse #EXT-X-KEY with METHOD=AES-128, authenticate the key URI request using forwarded session headers, decrypt the 16-byte CBC/CTR segment payloads in memory, and stitch into MP4 via FFmpeg."
    },
    {
        "summary": "Visual Paywall Diagnostics & VIP Badge in Episode Selector",
        "description": "Detect when an episode is strictly paywalled on the backend server (empty manifest / is_locked flag). Display a distinct 'VIP Locked' badge with informative tooltip ('Episode locked upstream. Authenticated VIP credentials required to download') instead of showing generic failure errors."
    }
]

created_keys = []
for task in new_tasks:
    payload = {
        "fields": {
            "project": {"key": proj},
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
            "issuetype": {"name": "Task"}
        }
    }
    res = requests.post(f"{base_url}/rest/api/3/issue", auth=auth, headers=headers, json=payload)
    if res.status_code in (200, 201):
        key = res.json().get("key")
        created_keys.append(key)
        print(f"[OK] Created Task {key}: {task['summary']}")
        if sprint_id:
            requests.post(f"{base_url}/rest/agile/1.0/sprint/{sprint_id}/issue", auth=auth, headers=headers, json={"issues": [key]})
    else:
        print(f"[ERR] Failed to create {task['summary']}: {res.status_code} {res.text}")

print(f"\nSuccessfully created {len(created_keys)} new tasks: {', '.join(created_keys)}")
