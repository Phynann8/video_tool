using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Helpers;

namespace Start.Infrastructure.Services
{
    public class YtDlpDownloadEngine : IDownloadEngine
    {
        private const string YtDlpExecutable = "yt-dlp.exe";
        private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
        private static readonly Regex ProgressRegex = new Regex(
            @"\[download\]\s+(\d+(?:\.\d+)?)%.*?(?:at\s+([^\s]+)\s+)?ETA\s+([^\s]+)",
            RegexOptions.Compiled);
        private static readonly Regex Aria2cProgressRegex = new Regex(
            @"\[#\w+\s+[^()]*\((\d+(?:\.\d+)?)%\).*?DL:([^\s\]]+)(?:\s+ETA:([^\s\]]+))?",
            RegexOptions.Compiled);

        public async Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
        {
            var formatArg = "-f \"bestvideo+bestaudio/best\"";
            var postProcessArg = "--merge-output-format mp4";

            // If it's a direct stream URL (m3u8, mp4, etc.), we can't use custom format IDs like "shd-0"
            bool isDirectStream = job.Url.Contains(".m3u8") || job.Url.Contains(".mp4") || job.Url.Contains(".m4a") || job.Url.Contains(".ts");
            bool isIflixOrDramaBox = job.Url.Contains("iflix.com") || job.Url.Contains("wetvinfo.com") || job.SourcePlatform == "DramaBox";

            if (isDirectStream || isIflixOrDramaBox)
            {
                // For direct streams or Iflix/DramaBox, "best" is the most reliable
                formatArg = "-f \"best\"";
            }
            else if (job.IsAudioOnly || (job.SelectedVideoStream == null && job.SelectedAudioStream != null))
            {
                var audioId = job.SelectedAudioStream?.Id ?? "bestaudio";
                formatArg = $"-f \"{audioId}\" -x --audio-format mp3";
                postProcessArg = string.Empty;
            }
            else if (job.SelectedVideoStream != null)
            {
                if (job.SelectedAudioStream != null)
                {
                    // Separate video + audio streams (e.g. YouTube) — merge them
                    formatArg = $"-f \"{job.SelectedVideoStream.Id}+{job.SelectedAudioStream.Id}\"";
                }
                else if (job.AvailableAudioStreams != null && job.AvailableAudioStreams.Count > 0)
                {
                    // Audio streams exist but none selected — use bestaudio
                    formatArg = $"-f \"{job.SelectedVideoStream.Id}+bestaudio\"";
                }
                else
                {
                    // No separate audio streams at all (combined format) — use video format alone
                    formatArg = $"-f \"{job.SelectedVideoStream.Id}\"";
                }
            }

            Directory.CreateDirectory(job.SavePath);
            var sanitizedTitle = SanitizeFilename(job.Title);
            var outputTemplate = Path.Combine(job.SavePath, $"{sanitizedTitle}.%(ext)s");

            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            var arguments = $"{formatArg} {postProcessArg} --user-agent \"{DefaultUserAgent}\" --ffmpeg-location \"{baseDir}\" -o \"{outputTemplate}\" --no-warnings --no-mtime \"{job.Url}\"".Trim();

            Console.WriteLine($"[YtDlpDownloadEngine] Starting download: {job.Title}");
            Console.WriteLine($"[YtDlpDownloadEngine] URL: {job.Url}");
            Console.WriteLine($"[YtDlpDownloadEngine] Args: {arguments}");

            await ProcessRunner.RunProcessAsync(YtDlpExecutable, arguments, cancellationToken, (line) =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;

                // Parse progress
                if (TryParseProgress(line, out double percent, out string speed, out string eta))
                {
                    job.Progress = percent;
                    job.Speed = speed;
                    job.Eta = eta;
                    job.Status = JobStatus.Downloading;
                    progress.Report(job);
                }
            });
            
            job.Progress = 100;
            job.Status = JobStatus.Completed;
            progress.Report(job);
        }

        public static bool TryParseProgress(string line, out double percent, out string speed, out string eta)
        {
            percent = 0;
            speed = string.Empty;
            eta = string.Empty;

            if (string.IsNullOrWhiteSpace(line))
                return false;

            // 1. Aria2c format: [#48bc29 35MiB/246MiB(14%) CN:16 DL:1.1MiB ETA:3m28s]
            var ariaMatch = Aria2cProgressRegex.Match(line);
            if (ariaMatch.Success)
            {
                if (double.TryParse(ariaMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out percent))
                {
                    speed = ariaMatch.Groups[2].Success ? ariaMatch.Groups[2].Value : string.Empty;
                    eta = ariaMatch.Groups[3].Success ? ariaMatch.Groups[3].Value : string.Empty;
                    return true;
                }
            }

            // 2. yt-dlp standard format: [download]  23.5% of  246.03MiB at  1.02MiB/s ETA 03:12
            var ytMatch = ProgressRegex.Match(line);
            if (ytMatch.Success)
            {
                if (double.TryParse(ytMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out percent))
                {
                    speed = ytMatch.Groups[2].Success ? ytMatch.Groups[2].Value : string.Empty;
                    eta = ytMatch.Groups[3].Success ? ytMatch.Groups[3].Value : string.Empty;
                    return true;
                }
            }

            return false;
        }

        private string SanitizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return "download";
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", filename.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return sanitized;
        }
    }
}
