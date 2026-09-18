import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(__file__).parent / ".env")
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")

sprint1_issues = [f"UMD-{i}" for i in range(5, 16)]

for issue in sprint1_issues:
    res = requests.post(
        f"{base_url}/rest/api/3/issue/{issue}/transitions",
        auth=auth,
        headers=headers,
        json={"transition": {"id": "31"}}
    )
    if res.status_code in (200, 204):
        print(f"[OK] Marked {issue} as Done")
    else:
        print(f"[WARN] {issue}: {res.status_code}")

print("\nSprint 1 issues marked as Done!")
