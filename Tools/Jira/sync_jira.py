#!/usr/bin/env python3
"""
Jira Cloud Automation Script for Universal Media Downloader
Synchronizes the Scrum Backlog, Epics, Sprints, and Tasks via Jira REST API.
"""

import os
import sys
import json
import csv
from pathlib import Path
import requests
from requests.auth import HTTPBasicAuth

def load_env(env_file):
    config = {}
    if not env_file.exists():
        return config
    with open(env_file, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, val = line.split("=", 1)
            config[key.strip()] = val.strip().strip("\"'")
    return config

def main():
    script_dir = Path(__file__).parent
    env_file = script_dir / ".env"
    if not env_file.exists():
        env_file = script_dir.parent.parent / ".env"

    config = load_env(env_file)
    base_url = config.get("JIRA_BASE_URL") or os.getenv("JIRA_BASE_URL")
    email = config.get("JIRA_EMAIL") or os.getenv("JIRA_EMAIL")
    token = config.get("JIRA_API_TOKEN") or os.getenv("JIRA_API_TOKEN")
    project_key = config.get("JIRA_PROJECT_KEY") or os.getenv("JIRA_PROJECT_KEY", "UMD")

    print("=" * 60)
    print("  Universal Media Downloader -> Jira Scrum Sync Tool")
    print("=" * 60)

    if not base_url or not email or not token:
        print("\n[!] Missing credentials.")
        print(f"Please copy '{script_dir / '.env.example'}' to '{script_dir / '.env'}'")
        print("and configure your Jira Domain, Email, and API Token.\n")
        print("Or set environment variables: JIRA_BASE_URL, JIRA_EMAIL, JIRA_API_TOKEN")
        sys.exit(1)

    base_url = base_url.rstrip("/")
    auth = HTTPBasicAuth(email, token)
    headers = {
        "Accept": "application/json",
        "Content-Type": "application/json"
    }

    # 1. Test Connection
    print(f"Connecting to Jira at {base_url} as {email}...")
    try:
        res = requests.get(f"{base_url}/rest/api/3/myself", auth=auth, headers=headers, timeout=15)
        if res.status_code != 200:
            print(f"[ERROR] Authentication failed (HTTP {res.status_code}): {res.text}")
            sys.exit(1)
        user_data = res.json()
        print(f"[OK] Authenticated as: {user_data.get('displayName')} ({user_data.get('emailAddress', email)})\n")
    except Exception as ex:
        print(f"[ERROR] Could not connect to Jira: {ex}")
        sys.exit(1)

    # 2. Check Project
    print(f"Verifying project '{project_key}'...")
    proj_res = requests.get(f"{base_url}/rest/api/3/project/{project_key}", auth=auth, headers=headers)
    if proj_res.status_code == 404:
        print(f"[!] Project '{project_key}' does not exist.")
        print(f"Please create a Software / Scrum project with key '{project_key}' in Jira first, or update JIRA_PROJECT_KEY in .env.")
        sys.exit(1)
    elif proj_res.status_code != 200:
        print(f"[ERROR] Failed to fetch project: {proj_res.text}")
        sys.exit(1)
    
    proj_data = proj_res.json()
    print(f"[OK] Found project: {proj_data.get('name')} (Key: {project_key})")

    # 3. Locate Scrum Board
    board_res = requests.get(f"{base_url}/rest/agile/1.0/board?projectKeyOrId={project_key}", auth=auth, headers=headers)
    board_id = None
    if board_res.status_code == 200:
        boards = board_res.json().get("values", [])
        if boards:
            board_id = boards[0]["id"]
            print(f"[OK] Using Board: {boards[0]['name']} (ID: {board_id})")

    # 4. Create Sprints if board exists
    sprint_ids = {}
    if board_id:
        existing_sprints_res = requests.get(f"{base_url}/rest/agile/1.0/board/{board_id}/sprint", auth=auth, headers=headers)
        existing_sprints = {}
        if existing_sprints_res.status_code == 200:
            for sp in existing_sprints_res.json().get("values", []):
                existing_sprints[sp["name"]] = sp["id"]

        desired_sprints = [
            "Sprint 1 - Foundation & Parity",
            "Sprint 2 - Resiliency & Features"
        ]

        for s_name in desired_sprints:
            if s_name in existing_sprints:
                sprint_ids[s_name] = existing_sprints[s_name]
                print(f"[INFO] Sprint exists: {s_name} (ID: {sprint_ids[s_name]})")
            else:
                s_payload = {
                    "name": s_name,
                    "originBoardId": board_id
                }
                s_create = requests.post(f"{base_url}/rest/agile/1.0/sprint", auth=auth, headers=headers, json=s_payload)
                if s_create.status_code in (200, 201):
                    new_sp = s_create.json()
                    sprint_ids[s_name] = new_sp["id"]
                    print(f"[OK] Created Sprint: {s_name} (ID: {new_sp['id']})")
                else:
                    print(f"[WARN] Could not create sprint '{s_name}': {s_create.text}")

    # 5. Read CSV and Create Tasks
    csv_file = script_dir / "jira_import_tasks.csv"
    if not csv_file.exists():
        print(f"[ERROR] CSV file not found: {csv_file}")
        sys.exit(1)

    print(f"\nImporting tasks from {csv_file.name}...")
    with open(csv_file, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        created_count = 0
        for row in reader:
            summary = row["Summary"]
            issue_type = row["Issue Type"]
            desc = row["Description"]
            priority = row.get("Priority", "Medium")
            sprint_name = row.get("Sprint")

            payload = {
                "fields": {
                    "project": {"key": project_key},
                    "summary": summary,
                    "description": {
                        "type": "doc",
                        "version": 1,
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [{"type": "text", "text": desc}]
                            }
                        ]
                    },
                    "issuetype": {"name": issue_type if issue_type in ["Task", "Story", "Epic"] else "Task"}
                }
            }

            create_res = requests.post(f"{base_url}/rest/api/3/issue", auth=auth, headers=headers, json=payload)
            if create_res.status_code in (200, 201):
                issue_info = create_res.json()
                issue_key = issue_info["key"]
                print(f"  [+] Created {issue_type} {issue_key}: {summary}")
                created_count += 1

                # Link to sprint if applicable
                if sprint_name and sprint_name in sprint_ids:
                    sp_id = sprint_ids[sprint_name]
                    sp_link_payload = {"issues": [issue_key]}
                    requests.post(f"{base_url}/rest/agile/1.0/sprint/{sp_id}/issue", auth=auth, headers=headers, json=sp_link_payload)
            else:
                print(f"  [-] Failed to create {summary}: {create_res.text}")

    print("\n" + "=" * 60)
    print(f"  Sync Completed! {created_count} issues synced to Jira.")
    print(f"  Scrum Board: {base_url}/jira/software/projects/{project_key}/boards")
    print("=" * 60 + "\n")

if __name__ == "__main__":
    main()
