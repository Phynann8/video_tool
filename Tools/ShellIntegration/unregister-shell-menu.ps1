# PowerShell Script to unregister Windows Explorer context menu for Universal Media Downloader
$fileKey = "HKCU:\Software\Classes\*\shell\UniversalMediaDownloader"
if (Test-Path $fileKey) {
    Remove-Item -Path $fileKey -Recurse -Force
    Write-Host "[OK] Windows Explorer Context Menu removed." -ForegroundColor Green
} else {
    Write-Host "Context menu was not registered." -ForegroundColor Yellow
}
