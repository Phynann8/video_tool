# PowerShell Script to register Universal Media Downloader Native Messaging Host for Chrome and Edge
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$manifestPath = Join-Path $scriptDir "com.universalmediadownloader.nativehost.json"
$exePath = Join-Path $scriptDir "bin\Debug\net9.0\UniversalMediaDownloader.NativeHost.exe"

if (-not (Test-Path $exePath)) {
    Write-Host "Publishing/Building Native Host first..." -ForegroundColor Yellow
    dotnet build (Join-Path $scriptDir "UniversalMediaDownloader.NativeHost.csproj") -c Debug
}

# Update JSON manifest with absolute path to exe
$resolvedExePath = (Resolve-Path $exePath).Path
$manifestContent = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifestContent.path = $resolvedExePath
$manifestContent | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding UTF8

$hostName = "com.universalmediadownloader.nativehost"
$resolvedManifestPath = (Resolve-Path $manifestPath).Path

# Register for Google Chrome
$chromeKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$hostName"
if (-not (Test-Path $chromeKey)) {
    New-Item -Path $chromeKey -Force | Out-Null
}
Set-ItemProperty -Path $chromeKey -Name "(Default)" -Value $resolvedManifestPath
Write-Host "[OK] Registered Chrome Native Messaging Host: $chromeKey" -ForegroundColor Green

# Register for Microsoft Edge
$edgeKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"
if (-not (Test-Path $edgeKey)) {
    New-Item -Path $edgeKey -Force | Out-Null
}
Set-ItemProperty -Path $edgeKey -Name "(Default)" -Value $resolvedManifestPath
Write-Host "[OK] Registered Edge Native Messaging Host: $edgeKey" -ForegroundColor Green

Write-Host "`nNative Messaging Host successfully registered!" -ForegroundColor Cyan
Write-Host "Manifest location: $resolvedManifestPath"
Write-Host "Executable location: $resolvedExePath"
