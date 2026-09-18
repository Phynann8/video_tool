import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(__file__).parent / ".env")
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")

issue_key = "UMD-19"

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
                        "text": "Completed investigation and resolution for DramaBox 403 and 404 upstream responses:"
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
                                        "text": "HTTP 403 (API Strategy): Upstream sapi.dramaboxdb.com is protected by Akamai EdgeSuite WAF (errors.edgesuite.net), blocking non-mobile TLS handshakes. Resolved by removing crash-inducing dummy fallback tokens, catching HTTP 403 gracefully, and providing actionable diagnostic warnings while falling back to web scraping."
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
                                        "text": "HTTP 404 (Scraper Strategy): dramabox.com uses /drama/{id}/... routes whereas dramaboxdb.com uses /movie/{id}/... routes. Submitting dramaboxdb.com/drama/... previously 404'd. Resolved by implementing smart multi-candidate URL fallback chains in both dramabox.py and DramaBoxExtractorEngine.cs."
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
                                        "text": "HTML chapter parsing fixed in dramabox.py (supports item['index'] and item['unlock']), verified live downloads and candidate fallback. 39 unit/integration tests passing."
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
