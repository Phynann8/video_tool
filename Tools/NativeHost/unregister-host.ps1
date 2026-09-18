# PowerShell Script to unregister Universal Media Downloader Native Messaging Host
$hostName = "com.universalmediadownloader.nativehost"

$chromeKey = "HKCU:\Software\Google\Chrome\NativeMessagingHosts\$hostName"
if (Test-Path $chromeKey) {
    Remove-Item -Path $chromeKey -Recurse -Force
    Write-Host "[OK] Unregistered Chrome Native Messaging Host." -ForegroundColor Green
}

$edgeKey = "HKCU:\Software\Microsoft\Edge\NativeMessagingHosts\$hostName"
if (Test-Path $edgeKey) {
    Remove-Item -Path $edgeKey -Recurse -Force
    Write-Host "[OK] Unregistered Edge Native Messaging Host." -ForegroundColor Green
}

Write-Host "Unregistration complete." -ForegroundColor Cyan
