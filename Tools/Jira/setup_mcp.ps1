# PowerShell Script to configure Jira MCP Server in Antigravity
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$envFile = Join-Path $scriptDir ".env"
$globalMcpConfig = "$HOME\.gemini\config\mcp_config.json"

if (-not (Test-Path $envFile)) {
    Write-Host "[!] Could not find $envFile" -ForegroundColor Yellow
    Write-Host "Please copy '$scriptDir\.env.example' to '$envFile' and fill in your Jira credentials first." -ForegroundColor Cyan
    exit 1
}

# Parse .env
$config = @{}
Get-Content $envFile | ForEach-Object {
    $line = $_.Trim()
    if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
        $parts = $line.Split("=", 2)
        $config[$parts[0].Trim()] = $parts[1].Trim().Trim('"').Trim("'")
    }
}

$jiraUrl = $config["JIRA_BASE_URL"]
$jiraEmail = $config["JIRA_EMAIL"]
$jiraToken = $config["JIRA_API_TOKEN"]

if (-not $jiraUrl -or -not $jiraEmail -or -not $jiraToken) {
    Write-Host "[!] Incomplete credentials in $envFile. Please provide JIRA_BASE_URL, JIRA_EMAIL, and JIRA_API_TOKEN." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $globalMcpConfig)) {
    Write-Host "[!] Could not find $globalMcpConfig" -ForegroundColor Red
    exit 1
}

$mcpJson = Get-Content $globalMcpConfig -Raw | ConvertFrom-Json

# Configure Jira MCP server entry
$mcpJson.mcpServers | Add-Member -NotePropertyName "jira" -NotePropertyValue ([PSCustomObject]@{
    command = "cmd.exe"
    args = @("/c", "npx", "-y", "@softspark/jira-mcp", "serve")
    env = [PSCustomObject]@{
        JIRA_URL = $jiraUrl
        JIRA_EMAIL = $jiraEmail
        JIRA_API_TOKEN = $jiraToken
    }
}) -Force

$mcpJson | ConvertTo-Json -Depth 10 | Set-Content $globalMcpConfig -Encoding UTF8

Write-Host "[OK] Successfully registered Jira MCP Server in Antigravity configuration!" -ForegroundColor Green
Write-Host "Config file: $globalMcpConfig"
Write-Host "Jira Instance: $jiraUrl ($jiraEmail)"
Write-Host "`nRestart Antigravity or reopen the workspace to activate the Jira AI tools." -ForegroundColor Cyan
