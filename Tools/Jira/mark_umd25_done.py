import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(r"Tools\Jira\.env"))
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")

issue_key = "UMD-25"

# Add comment summarizing findings and resolution
comment_body = {
    "body": {
        "type": "doc",
        "version": 1,
        "content": [
            {
                "type": "paragraph",
                "content": [
                    {
                        "type": "text",
                        "text": "Completed implementation of UMD-25 (JIT Signed URL Resolution for Expiring CDN Streams):"
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
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Added OriginalPageUrl, UrlExpiresAt, and IsUrlExpired tracking to DownloadJob."
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "type": "listItem",
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Created IMediaUrlRefresher interface implemented by DramaBoxExtractorEngine, allowing on-demand re-extraction of signed CDN URLs."
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "type": "listItem",
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Updated QueueManager with proactive pre-download expiry checks and automatic 403 Forbidden link recovery."
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "type": "listItem",
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Added 6 unit/integration tests in JitSignedUrlResolutionTests.cs. All 45 tests passing."
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ]
    }
}

comment_res = requests.post(
    f"{base_url}/rest/api/3/issue/{issue_key}/comment",
    auth=auth,
    headers=headers,
    json=comment_body
)
if comment_res.status_code in (200, 201):
    print(f"[OK] Added resolution comment to {issue_key}")
else:
    print(f"[WARN] Failed to comment on {issue_key}: {comment_res.status_code} {comment_res.text}")

# Check transitions available
trans_res = requests.get(
    f"{base_url}/rest/api/3/issue/{issue_key}/transitions",
    auth=auth,
    headers=headers
)
if trans_res.status_code == 200:
    transitions = trans_res.json().get("transitions", [])
    done_trans = next((t for t in transitions if "done" in t["name"].lower()), None)
    trans_id = done_trans["id"] if done_trans else "31"
else:
    trans_id = "31"

res = requests.post(
    f"{base_url}/rest/api/3/issue/{issue_key}/transitions",
    auth=auth,
    headers=headers,
    json={"transition": {"id": trans_id}}
)
if res.status_code in (200, 204):
    print(f"[OK] Marked {issue_key} as Done (transition {trans_id})")
else:
    print(f"[WARN] {issue_key}: status {res.status_code} {res.text}")
