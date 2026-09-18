using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Services
{
    public sealed class DynamicSegment
    {
        public long StartOffset { get; }
        public long EndOffset { get; private set; }
        public string TempFilePath { get; }
        public long CurrentPosition { get; set; }
        public bool IsCompleted { get; set; }
        public long TotalBytes => EndOffset - StartOffset + 1;
        public long RemainingBytes => IsCompleted ? 0 : Math.Max(0, EndOffset - CurrentPosition + 1);

        public DynamicSegment(long startOffset, long endOffset, string tempFilePath)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            TempFilePath = tempFilePath;
            CurrentPosition = startOffset;
        }

        public DynamicSegment? TrySplit(long minSplitSize, string tempDir)
        {
            if (IsCompleted) return null;
            long remaining = RemainingBytes;
            if (remaining < minSplitSize * 2) return null;

            long splitBytes = remaining / 2;
            long newSegmentStart = EndOffset - splitBytes + 1;
            long newSegmentEnd = EndOffset;

            EndOffset = newSegmentStart - 1;

            string stolenTempFile = Path.Combine(tempDir, $"{Guid.NewGuid():N}.part");
            return new DynamicSegment(newSegmentStart, newSegmentEnd, stolenTempFile);
        }
    }

    /// <summary>
    /// Downloads MP4 files directly via HttpClient.
    /// Used for DramaBox episodes where the API provides direct download URLs.
    /// </summary>
    public class HttpDownloadEngine : IDownloadEngine
    {
        private readonly HttpClient _httpClient;
        public int MaxSegmentWorkers { get; set; } = 16;
        public long MinSegmentSizeBytes { get; set; } = 2 * 1024 * 1024;

        public HttpDownloadEngine()
        {
            _httpClient = CreateHttpClient();
        }

        public HttpDownloadEngine(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpDownloadEngine(DownloadProcessingSettings settings)
        {
            _httpClient = CreateHttpClient();
            MaxSegmentWorkers = settings.MaxSegmentWorkers;
            MinSegmentSizeBytes = settings.MinSegmentSizeBytes;
        }

        public async Task StartDownloadAsync(DownloadJob job, IProgress<DownloadJob> progress, CancellationToken cancellationToken)
        {
            // Resolve the actual download URL from the saved video map + selected quality
            string downloadUrl = ResolveDownloadUrl(job);
            if (string.IsNullOrEmpty(downloadUrl))
            {
                throw new InvalidOperationException("No download URL available for this episode.");
            }

            // Build output path: {downloadFolder}\{EpisodeTitle}.mp4
            string safeTitle = SanitizeFileName(job.Title);
            string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

            // job.SavePath set by extractor = drama folder name; if it's a full path use that as base
            string outputDir;
            if (!string.IsNullOrWhiteSpace(job.SavePath) && !job.SavePath.StartsWith("{") && !job.SavePath.StartsWith("["))
            {
                // Could be just a folder name (from DramaBox extractor) or a full path from Settings
                if (Path.IsPathRooted(job.SavePath))
                {
                    outputDir = job.SavePath;
                }
                else
                {
                    // Treat as drama sub-folder name inside MyVideos
                    outputDir = Path.Combine(baseDir, SanitizeFileName(job.SavePath));
                }
            }
            else
            {
                outputDir = baseDir;
            }

            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, $"{safeTitle}.mp4");

            job.Status = JobStatus.Downloading;
            job.SavePath = outputDir;
            progress.Report(job);

            string partsDir = outputPath + ".parts";

            // 1. Probe for range support and total file size via HEAD request
            long totalBytes = -1;
            bool supportsRange = false;

            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                ApplyHeaders(headRequest, job);
                using var headResponse = await _httpClient.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (headResponse.IsSuccessStatusCode)
                {
                    totalBytes = headResponse.Content.Headers.ContentLength ?? -1;
                    supportsRange = headResponse.Headers.AcceptRanges.Contains("bytes") || headResponse.Headers.Contains("Accept-Ranges");
                }
            }
            catch
            {
                // Fall back to direct GET
            }

            // 2. Multi-segment range download with resume support
            if (supportsRange && totalBytes > 0)
            {
                Directory.CreateDirectory(partsDir);

                // Detect existing parts for resume support
                var existingParts = Directory.GetFiles(partsDir, "*.part")
                    .Select(f =>
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (long.TryParse(name, out long start))
                        {
                            var len = new FileInfo(f).Length;
                            return new { FilePath = f, Start = start, Length = len, End = start + len - 1 };
                        }
                        return null;
                    })
                    .Where(p => p != null && p.Length > 0)
                    .Select(p => p!)
                    .OrderBy(p => p.Start)
                    .ToList();

                var missingRanges = new List<(long From, long To)>();
                long cur = 0;
                foreach (var ep in existingParts)
                {
                    if (ep.Start > cur)
                    {
                        missingRanges.Add((cur, ep.Start - 1));
                    }
                    cur = Math.Max(cur, ep.End + 1);
                }
                if (cur < totalBytes)
                {
                    missingRanges.Add((cur, totalBytes - 1));
                }

                // Partition missing ranges into segments
                var segmentsToDownload = new List<(long From, long To)>();
                foreach (var range in missingRanges)
                {
                    long rangeLen = range.To - range.From + 1;
                    if (rangeLen <= 0) continue;

                    int chunks = 1;
                    if (rangeLen >= MinSegmentSizeBytes)
                    {
                        chunks = Math.Min(MaxSegmentWorkers, Math.Max(2, (int)(rangeLen / MinSegmentSizeBytes)));
                    }
                    long chunkSize = (long)Math.Ceiling((double)rangeLen / chunks);

                    for (int i = 0; i < chunks; i++)
                    {
                        long sFrom = range.From + i * chunkSize;
                        long sTo = Math.Min(range.To, sFrom + chunkSize - 1);
                        if (sFrom <= sTo)
                        {
                            segmentsToDownload.Add((sFrom, sTo));
                        }
                    }
                }

                long downloadedBytes = existingParts.Sum(p => p.Length);
                var startTime = DateTime.Now;
                var lockObj = new object();

                using var semaphore = new SemaphoreSlim(MaxSegmentWorkers, MaxSegmentWorkers);
                var downloadTasks = segmentsToDownload.Select(async seg =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        string partPath = Path.Combine(partsDir, $"{seg.From:D12}.part");
                        int maxRetries = 3;
                        for (int attempt = 1; attempt <= maxRetries; attempt++)
                        {
                            try
                            {
                                using var rangeReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                                ApplyHeaders(rangeReq, job);
                                rangeReq.Headers.Range = new RangeHeaderValue(seg.From, seg.To);

                                using var rangeResp = await _httpClient.SendAsync(rangeReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                                rangeResp.EnsureSuccessStatusCode();

                                using var stream = await rangeResp.Content.ReadAsStreamAsync(cancellationToken);
                                using var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);
                                var buf = new byte[65536];
                                int read;
                                while ((read = await stream.ReadAsync(buf, 0, buf.Length, cancellationToken)) > 0)
                                {
                                    await fs.WriteAsync(buf, 0, read, cancellationToken);
                                    lock (lockObj)
                                    {
                                        downloadedBytes += read;
                                        double elapsed = (DateTime.Now - startTime).TotalSeconds;
                                        double spd = elapsed > 0 ? downloadedBytes / elapsed : 0;
                                        job.Progress = (double)downloadedBytes / totalBytes * 100d;
                                        job.Speed = $"{FormatSpeed(spd)} | {FormatBytes(downloadedBytes)}/{FormatBytes(totalBytes)}";
                                        long rem = totalBytes - downloadedBytes;
                                        job.Eta = FormatEta(spd > 0 ? rem / spd : 0);
                                    }
                                }
                                return;
                            }
                            catch when (attempt < maxRetries && !cancellationToken.IsCancellationRequested)
                            {
                                await Task.Delay(100 * attempt, cancellationToken);
                            }
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(downloadTasks);

                // Assemble parts in order
                var allPartFiles = Directory.GetFiles(partsDir, "*.part")
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                using (var finalOut = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true))
                {
                    foreach (var p in allPartFiles)
                    {
                        using var pf = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
                        await pf.CopyToAsync(finalOut, cancellationToken);
                    }
                }

                try
                {
                    Directory.Delete(partsDir, true);
                }
                catch { }

                job.Progress = 100;
                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTime.Now;
                job.Eta = "Done";
                progress.Report(job);
                return;
            }

            // 3. Fallback: single GET stream download
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            ApplyHeaders(request, job);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            totalBytes = response.Content.Headers.ContentLength ?? -1;
            long directDownloaded = 0;
            var directStart = DateTime.Now;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true);

            var buffer = new byte[65536];
            int bytesRead;
            var lastUpdate = DateTime.Now;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                directDownloaded += bytesRead;

                if ((DateTime.Now - lastUpdate).TotalMilliseconds > 500)
                {
                    lastUpdate = DateTime.Now;
                    double elapsed = (DateTime.Now - directStart).TotalSeconds;
                    double speedBytesPerSec = elapsed > 0 ? directDownloaded / elapsed : 0;

                    if (totalBytes > 0)
                    {
                        job.Progress = (double)directDownloaded / totalBytes * 100d;
                        long remaining = totalBytes - directDownloaded;
                        double etaSecs = speedBytesPerSec > 0 ? remaining / speedBytesPerSec : 0;
                        job.Speed = $"{FormatSpeed(speedBytesPerSec)} | {FormatBytes(directDownloaded)}/{FormatBytes(totalBytes)}";
                        job.Eta = FormatEta(etaSecs);
                    }
                    else
                    {
                        job.Speed = $"{FormatSpeed(speedBytesPerSec)} | {FormatBytes(directDownloaded)} downloaded";
                    }
                    progress.Report(job);
                }
            }

            job.Progress = 100;
            job.Status = JobStatus.Completed;
            job.CompletedAt = DateTime.Now;
            job.Eta = "Done";
            progress.Report(job);
        }

        private static void ApplyHeaders(HttpRequestMessage request, DownloadJob job)
        {
            if (!string.IsNullOrWhiteSpace(job.UserAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", job.UserAgent);
            }
            if (!string.IsNullOrWhiteSpace(job.Referer))
            {
                request.Headers.TryAddWithoutValidation("Referer", job.Referer);
            }
            if (!string.IsNullOrWhiteSpace(job.Cookies))
            {
                request.Headers.TryAddWithoutValidation("Cookie", job.Cookies);
            }
        }

        private string ResolveDownloadUrl(DownloadJob job)
        {
            if (!string.IsNullOrEmpty(job.Url) && job.Url.StartsWith("http"))
            {
                if (job.SelectedVideoStream != null && !string.IsNullOrEmpty(job.DownloadMetadata))
                {
                    try
                    {
                        var videoMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(job.DownloadMetadata);
                        if (videoMap != null && videoMap.TryGetValue(job.SelectedVideoStream.Resolution, out var url))
                        {
                            return url;
                        }
                    }
                    catch
                    {
                        // SavePath is not JSON
                    }
                }
                return job.Url;
            }

            return string.Empty;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024d:0.#} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024d * 1024d):0.#} MB";
            return $"{bytes / (1024d * 1024d * 1024d):0.#} GB";
        }

        private static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024) return $"{bytesPerSec:0} B/s";
            if (bytesPerSec < 1024 * 1024) return $"{bytesPerSec / 1024d:0.#} KB/s";
            return $"{bytesPerSec / (1024d * 1024d):0.##} MB/s";
        }

        private static string FormatEta(double seconds)
        {
            if (seconds <= 0) return "--:--";
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
                : ts.Minutes > 0
                    ? $"{ts.Minutes}m {ts.Seconds:D2}s"
                    : $"{ts.Seconds}s";
        }

        private static HttpClient CreateHttpClient()
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("UniversalMediaDownloader/1.0");
            return client;
        }
    }
}
