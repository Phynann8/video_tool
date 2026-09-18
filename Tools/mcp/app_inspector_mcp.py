"""
Universal Media Downloader - App & SQLite Inspector MCP Server
Provides real-time inspection tools for the application database, download jobs, and runtime environment.
"""

import os
import json
import sqlite3
from pathlib import Path
from typing import Optional, Dict, Any, List
from mcp.server.mcpserver import MCPServer

server = MCPServer("UniversalMediaDownloader-Inspector")

def get_db_path() -> Path:
    local_app_data = os.environ.get("LOCALAPPDATA", "")
    if local_app_data:
        db_path = Path(local_app_data) / "UniversalMediaDownloader" / "media_downloader.db"
        if db_path.exists():
            return db_path
    # Fallback to local project search if not in LocalAppData
    return Path(local_app_data) / "UniversalMediaDownloader" / "media_downloader.db"

@server.tool()
def check_app_environment() -> Dict[str, Any]:
    """Check existence and status of core dependencies: SQLite DB, yt-dlp, ffmpeg, and plugins."""
    db_path = get_db_path()
    project_root = Path(__file__).resolve().parents[2]
    
    ytdlp_candidates = [
        project_root / "out_publish" / "yt-dlp.exe",
        project_root / "Tools" / "yt-dlp" / "yt-dlp.exe",
    ]
    ffmpeg_candidates = [
        project_root / "out_publish" / "ffmpeg.exe",
        project_root / "Tools" / "ffmpeg" / "ffmpeg.exe",
    ]
    plugins_dir = project_root / "Tools" / "yt-dlp-plugins" / "yt_dlp_plugins" / "extractor"
    
    return {
        "database": {
            "path": str(db_path),
            "exists": db_path.exists(),
            "size_bytes": db_path.stat().st_size if db_path.exists() else 0
        },
        "yt_dlp": {
            str(p): p.exists() for p in ytdlp_candidates
        },
        "ffmpeg": {
            str(p): p.exists() for p in ffmpeg_candidates
        },
        "custom_plugins": [f.name for f in plugins_dir.glob("*.py")] if plugins_dir.exists() else []
    }

@server.tool()
def get_database_schema() -> Dict[str, Any]:
    """Inspect all table schemas and column definitions in media_downloader.db."""
    db_path = get_db_path()
    if not db_path.exists():
        return {"error": f"Database not found at {db_path}"}

    with sqlite3.connect(db_path) as conn:
        cursor = conn.cursor()
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';")
        tables = [row[0] for row in cursor.fetchall()]
        schema = {}
        for table in tables:
            cursor.execute(f"PRAGMA table_info({table});")
            columns = [
                {"cid": col[0], "name": col[1], "type": col[2], "notnull": bool(col[3]), "pk": bool(col[5])}
                for col in cursor.fetchall()
            ]
            cursor.execute(f"SELECT COUNT(*) FROM {table};")
            count = cursor.fetchone()[0]
            schema[table] = {"columns": columns, "row_count": count}
        return {"tables": schema}

@server.tool()
def list_recent_jobs(limit: int = 10) -> List[Dict[str, Any]]:
    """List the most recent download jobs from the database with decoded JSON metadata."""
    db_path = get_db_path()
    if not db_path.exists():
        return [{"error": f"Database not found at {db_path}"}]

    with sqlite3.connect(db_path) as conn:
        conn.row_factory = sqlite3.Row
        cursor = conn.cursor()
        cursor.execute(
            "SELECT Id, Url, Status, CreatedAt, JsonData FROM Jobs ORDER BY CreatedAt DESC LIMIT ?",
            (limit,)
        )
        rows = cursor.fetchall()
        jobs = []
        for row in rows:
            job_dict = {
                "id": row["Id"],
                "url": row["Url"],
                "status": row["Status"],
                "created_at": row["CreatedAt"],
            }
            try:
                job_dict["data"] = json.loads(row["JsonData"]) if row["JsonData"] else {}
            except Exception:
                job_dict["raw_data"] = row["JsonData"]
            jobs.append(job_dict)
        return jobs

@server.tool()
def query_database(sql: str) -> Dict[str, Any]:
    """Execute a safe read-only (SELECT) query on media_downloader.db."""
    sql_trimmed = sql.strip().upper()
    if not sql_trimmed.startswith("SELECT") and not sql_trimmed.startswith("PRAGMA"):
        return {"error": "Only SELECT and PRAGMA queries are allowed via this tool."}

    db_path = get_db_path()
    if not db_path.exists():
        return {"error": f"Database not found at {db_path}"}

    with sqlite3.connect(db_path) as conn:
        conn.row_factory = sqlite3.Row
        cursor = conn.cursor()
        cursor.execute(sql)
        rows = cursor.fetchall()
        return {
            "query": sql,
            "row_count": len(rows),
            "rows": [dict(r) for r in rows[:100]]
        }

if __name__ == "__main__":
    server.run("stdio")
