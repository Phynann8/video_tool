# PowerShell Script to register Windows Explorer context menu for Universal Media Downloader
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$appExe = Resolve-Path (Join-Path $scriptDir "..\..\Start.UI\bin\Debug\net9.0-windows\Start.UI.exe") -ErrorAction SilentlyContinue

if (-not $appExe) {
    $appExe = Resolve-Path (Join-Path $scriptDir "..\..\out_publish\Start.UI.exe") -ErrorAction SilentlyContinue
}

if (-not $appExe) {
    Write-Host "Could not find Start.UI.exe. Please build or publish the project first." -ForegroundColor Red
    exit 1
}

$exePath = $appExe.Path
$commandStr = "`"$exePath`" `"%1`""

# Register under HKCU:\Software\Classes\*\shell\UniversalMediaDownloader
$fileKey = "HKCU:\Software\Classes\*\shell\UniversalMediaDownloader"
if (-not (Test-Path $fileKey)) { New-Item -Path $fileKey -Force | Out-Null }
Set-ItemProperty -Path $fileKey -Name "(Default)" -Value "Download with Universal Media Downloader"
Set-ItemProperty -Path $fileKey -Name "Icon" -Value "`"$exePath`""

$cmdKey = "$fileKey\command"
if (-not (Test-Path $cmdKey)) { New-Item -Path $cmdKey -Force | Out-Null }
Set-ItemProperty -Path $cmdKey -Name "(Default)" -Value $commandStr

Write-Host "[OK] Windows Explorer Context Menu registered successfully!" -ForegroundColor Green
Write-Host "Target executable: $exePath"
