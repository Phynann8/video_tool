param (
    [string]$outDir = "Start.UI\bin\Debug\net9.0-windows"
)

# Ensure output directory exists
if (-not (Test-Path -Path $outDir)) {
    New-Item -ItemType Directory -Path $outDir | Out-Null
    Write-Host "Created directory $outDir"
}

# Download yt-dlp.exe
$ytDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe"
$ytDlpDest = Join-Path $outDir "yt-dlp.exe"

Write-Host "Downloading yt-dlp.exe..."
Invoke-WebRequest -Uri $ytDlpUrl -OutFile $ytDlpDest
Write-Host "yt-dlp.exe downloaded to $ytDlpDest"

# Download ffmpeg.exe (gyan.dev essentials build)
$ffmpegZipUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip"
$ffmpegZipDest = Join-Path $outDir "ffmpeg.zip"
$ffmpegExtractDir = Join-Path $outDir "ffmpeg-extracted"

Write-Host "Downloading ffmpeg..."
Invoke-WebRequest -Uri $ffmpegZipUrl -OutFile $ffmpegZipDest

Write-Host "Extracting ffmpeg..."
Expand-Archive -Path $ffmpegZipDest -DestinationPath $ffmpegExtractDir -Force

# The zip contains a folder like ffmpeg-7.1-essentials_build, we need to find the bin folder
$binPath = Get-ChildItem -Path $ffmpegExtractDir -Directory | Select-Object -First 1 | Join-Path -ChildPath "bin"

Write-Host "Copying ffmpeg.exe and ffprobe.exe..."
Copy-Item -Path (Join-Path $binPath "ffmpeg.exe") -Destination (Join-Path $outDir "ffmpeg.exe") -Force
Copy-Item -Path (Join-Path $binPath "ffprobe.exe") -Destination (Join-Path $outDir "ffprobe.exe") -Force

# Cleanup
Write-Host "Cleaning up temporary ffmpeg zip and extraction folder..."
Remove-Item -Path $ffmpegZipDest -Force
Remove-Item -Path $ffmpegExtractDir -Recurse -Force

Write-Host "Setup complete! Missing dependencies have been downloaded and placed in $outDir"
