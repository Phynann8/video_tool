import requests
from pathlib import Path
from requests.auth import HTTPBasicAuth
from sync_jira import load_env

c = load_env(Path(__file__).parent / ".env")
auth = HTTPBasicAuth(c["JIRA_EMAIL"], c["JIRA_API_TOKEN"])
headers = {"Accept": "application/json", "Content-Type": "application/json"}
base_url = c["JIRA_BASE_URL"].rstrip("/")

target_issues = ["UMD-17", "UMD-20"]

for issue in target_issues:
    # Check transitions available
    trans_res = requests.get(
        f"{base_url}/rest/api/3/issue/{issue}/transitions",
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
        f"{base_url}/rest/api/3/issue/{issue}/transitions",
        auth=auth,
        headers=headers,
        json={"transition": {"id": trans_id}}
    )
    if res.status_code in (200, 204):
        print(f"[OK] Marked {issue} as Done (transition {trans_id})")
    else:
        print(f"[WARN] {issue}: status {res.status_code} {res.text}")

print("Updated completed tasks in Jira.")
