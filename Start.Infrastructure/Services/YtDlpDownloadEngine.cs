using System;
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
        private static readonly Regex ProgressRegex = new Regex(
            @"\[download\]\s+(\d+(?:\.\d+)?)%.*?(?:at\s+([^\s]+)\s+)?ETA\s+(\d{2}:\d{2}(?::\d{2})?)",
            RegexOptions.Compiled);

        public async Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
        {
            var formatArg = "-f \"bestvideo+bestaudio/best\"";
            var postProcessArg = "--merge-output-format mp4";

            if (job.IsAudioOnly || (job.SelectedVideoStream == null && job.SelectedAudioStream != null))
            {
                var audioId = job.SelectedAudioStream?.Id ?? "bestaudio";
                formatArg = $"-f \"{audioId}\" -x --audio-format mp3";
                postProcessArg = string.Empty;
            }
            else if (job.SelectedVideoStream != null)
            {
                var audioId = job.SelectedAudioStream?.Id ?? "bestaudio";
                formatArg = $"-f \"{job.SelectedVideoStream.Id}+{audioId}\"";
            }

            var outputTemplate = $"{job.SavePath}\\%(title)s.%(ext)s";

            var arguments = $"{formatArg} {postProcessArg} -o \"{outputTemplate}\" --no-warnings --no-mtime \"{job.Url}\"".Trim();

            await ProcessRunner.RunProcessAsync(YtDlpExecutable, arguments, cancellationToken, (line) =>
            {
                if (string.IsNullOrWhiteSpace(line)) return;

                // Parse progress
                var match = ProgressRegex.Match(line);
                if (match.Success)
                {
                    if (double.TryParse(match.Groups[1].Value, out double percent))
                    {
                        job.Progress = percent;
                        job.Speed = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
                        job.Eta = match.Groups[3].Value;
                        job.Status = JobStatus.Downloading;
                        progress.Report(job);
                    }
                }
            });
            
            job.Status = JobStatus.Processing; // Or completed if no post-processing
            progress.Report(job);
        }
    }
}
