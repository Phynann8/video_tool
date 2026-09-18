import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(__file__).parent / ".env")
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")

mapping = {
    "UMD-1": ["UMD-5", "UMD-6", "UMD-7", "UMD-8", "UMD-9", "UMD-16", "UMD-17", "UMD-18"],
    "UMD-2": ["UMD-10", "UMD-11", "UMD-12", "UMD-13", "UMD-14", "UMD-15", "UMD-22", "UMD-23"],
    "UMD-3": ["UMD-19", "UMD-24"],
    "UMD-4": ["UMD-20", "UMD-21"]
}

for epic, issues in mapping.items():
    for issue in issues:
        res = requests.put(
            f"{base_url}/rest/api/3/issue/{issue}",
            auth=auth,
            headers=headers,
            json={"fields": {"parent": {"key": epic}}}
        )
        if res.status_code in (200, 204):
            print(f"[OK] Linked {issue} -> {epic}")
        else:
            print(f"[WARN] {issue} -> {epic}: {res.status_code}")

print("\nDone linking epics!")
