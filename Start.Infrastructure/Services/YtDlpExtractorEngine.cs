using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;
using Start.Infrastructure.Helpers;
using Start.Infrastructure.Models.YtDlp;

namespace Start.Infrastructure.Services
{
    public class YtDlpExtractorEngine : IExtractorEngine
    {
        private const string YtDlpExecutable = "yt-dlp.exe";
        private const string DefaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36";
        private readonly ISettingsRepository? _settingsRepository;
        private readonly DownloadProcessingSettings? _processingSettings;

        public YtDlpExtractorEngine(DownloadProcessingSettings? processingSettings = null)
        {
            _processingSettings = processingSettings;
        }

        public YtDlpExtractorEngine(ISettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;
        }

        public async Task<List<DownloadJob>> AnalyzeUrlAsync(string url)
        {
            // Determine if we should use --no-playlist
            // Individual Iflix episode URLs: /play/albumId/episodeId (two path segments after /play/)
            // Series URLs: /play/albumId-SeriesName (one path segment after /play/)
            // We only want --no-playlist for individual episode URLs
            var playlistArg = "";
            if (url.Contains("iflix.com") && url.Contains("/play/"))
            {
                // Check if this is an individual episode URL by looking for a second path segment
                var playPath = Regex.Match(url, @"/play/([^/?#]+)(/[^/?#]+)?");
                bool isIndividualEpisode = playPath.Success && playPath.Groups[2].Success && !string.IsNullOrEmpty(playPath.Groups[2].Value);
                if (isIndividualEpisode)
                {
                    playlistArg = "--no-playlist";
                }
            }
            
            var authArgs = BuildAuthArguments(url);
            var arguments = $"-J --no-warnings --ignore-errors --user-agent \"{DefaultUserAgent}\" {playlistArg} {authArgs} \"{url}\"".Trim();
            var resultJobs = new List<DownloadJob>();
            
            try 
            {
                var jsonOutput = await ProcessRunner.RunProcessAsync(YtDlpExecutable, arguments, CancellationToken.None, allowNonZeroExitWithOutput: true);
                var metadata = ParseYtDlpMetadata(jsonOutput);

                if (metadata == null) throw new Exception("Failed to parse yt-dlp output.");

                if (metadata.Type == "playlist" || metadata.Type == "multi_video")
                {
                    // Playlist logic: parse each entry
                    var playlistId = Guid.NewGuid().ToString(); // Grouping ID
                    int index = 1;
                    foreach (var entry in metadata.Entries)
                    {
                        if (entry == null)
                        {
                            // Entry was skipped by yt-dlp (e.g. Tencent pay limit / VIP / DRM protected)
                            resultJobs.Add(new DownloadJob
                            {
                                Id = Guid.NewGuid(),
                                Url = url,
                                Title = $"Episode {index:D2} (Locked / VIP / DRM)",
                                Status = JobStatus.PendingAnalysis,
                                SourcePlatform = DetectSourcePlatform(url),
                                PlaylistId = playlistId,
                                EpisodeNumber = index,
                                IsLocked = true,
                                IsSelected = false
                            });
                            index++;
                            continue;
                        }
                        
                        var job = ParseMetadataToJob(entry, entry.Url ?? url, playlistId);
                        if (job.EpisodeNumber <= 0)
                        {
                            job.EpisodeNumber = index;
                        }
                        resultJobs.Add(job);
                        index++;
                    }
                }
                else
                {
                    // Single video logic
                    var job = ParseMetadataToJob(metadata, url, null);
                    resultJobs.Add(job);
                }

                return resultJobs;
            }
            catch (Exception ex)
            {
                return new List<DownloadJob> 
                { 
                    new DownloadJob 
                    { 
                        Url = url, 
                        Status = JobStatus.AnalysisFailed, 
                        ErrorMessage = ex.Message 
                    } 
                };
            }
        }

        private DownloadJob ParseMetadataToJob(YtDlpMetadata metadata, string originalUrl, string? playlistId)
        {
            var formats = metadata.Formats ?? new List<YtDlpFormat>();
            var videoStreams = formats
                .Where(f =>
                    f != null &&
                    !string.Equals(f.VideoCodec, "none", StringComparison.OrdinalIgnoreCase) &&
                    f.Height.HasValue)
                .Select(f => new VideoStreamInfo
                {
                    Id = f.FormatId,
                    Resolution = $"{f.Width ?? 0}x{f.Height ?? 0}",
                    Extension = f.Extension,
                    SizeBytes = f.FileSize ?? f.FileSizeApprox ?? 0,
                    Codec = f.VideoCodec
                })
                .OrderByDescending(v => GetVerticalResolution(v.Resolution))
                .ThenByDescending(v => v.SizeBytes)
                .ToList();

            var audioStreams = formats
                .Where(f =>
                    f != null &&
                    string.Equals(f.VideoCodec, "none", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(f.AudioCodec, "none", StringComparison.OrdinalIgnoreCase))
                .Select(f => new AudioStreamInfo
                {
                    Id = f.FormatId,
                    Bitrate = f.Bitrate.HasValue ? $"{Math.Round(f.Bitrate.Value)} kbps" : "Unknown",
                    Extension = f.Extension,
                    SizeBytes = f.FileSize ?? f.FileSizeApprox ?? 0,
                    Codec = f.AudioCodec
                })
                .OrderByDescending(a => GetBitrate(a.Bitrate))
                .ThenByDescending(a => a.SizeBytes)
                .ToList();

            int epNum = 0;
            var epMatch = Regex.Match(metadata.Title ?? string.Empty, @"(?:EP|Episode)\s*0*(\d+)", RegexOptions.IgnoreCase);
            if (epMatch.Success && int.TryParse(epMatch.Groups[1].Value, out int parsedEp))
            {
                epNum = parsedEp;
            }

            bool hasStreams = videoStreams.Count > 0;

            return new DownloadJob
            {
                Id = Guid.NewGuid(),
                Url = originalUrl,
                Title = metadata.Title ?? "Untitled",
                ThumbnailUrl = metadata.Thumbnail ?? string.Empty,
                Duration = TimeSpan.FromSeconds(metadata.Duration),
                Status = JobStatus.PendingAnalysis,
                SourcePlatform = DetectSourcePlatform(originalUrl),
                AvailableVideoStreams = videoStreams,
                AvailableAudioStreams = audioStreams,
                SelectedVideoStream = videoStreams.FirstOrDefault(),
                SelectedAudioStream = audioStreams.FirstOrDefault(),
                PlaylistId = playlistId,
                EpisodeNumber = epNum,
                IsLocked = !hasStreams,
                IsSelected = hasStreams
            };
        }

        private static int GetVerticalResolution(string resolution)
        {
            if (string.IsNullOrWhiteSpace(resolution)) return 0;
            var parts = resolution.Split('x');
            if (parts.Length != 2) return 0;
            return int.TryParse(parts[1], out var height) ? height : 0;
        }

        private static double GetBitrate(string bitrateLabel)
        {
            if (string.IsNullOrWhiteSpace(bitrateLabel)) return 0;
            var value = bitrateLabel.Split(' ')[0];
            return double.TryParse(value, out var bitrate) ? bitrate : 0;
        }

        public static YtDlpMetadata ParseYtDlpMetadata(string rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
                throw new Exception("yt-dlp output is empty.");

            // 1. Line-by-line search: yt-dlp -J outputs the full JSON on a single line.
            // This safely bypasses any extraneous output lines (such as [PROBE], warnings, or plugin logs).
            using (var reader = new StringReader(rawOutput))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        try
                        {
                            var meta = JsonConvert.DeserializeObject<YtDlpMetadata>(trimmed);
                            if (meta != null && (!string.IsNullOrEmpty(meta.Id) ||
                                                 !string.IsNullOrEmpty(meta.Type) ||
                                                 !string.IsNullOrEmpty(meta.Title) ||
                                                 (meta.Formats != null && meta.Formats.Count > 0) ||
                                                 (meta.Entries != null && meta.Entries.Count > 0)))
                            {
                                return meta;
                            }
                        }
                        catch
                        {
                            // Try next line if this line wasn't valid metadata JSON
                        }
                    }
                }
            }

            // 2. Direct deserialization attempt (for clean multi-line or standard single-line output)
            try
            {
                var meta = JsonConvert.DeserializeObject<YtDlpMetadata>(rawOutput);
                if (meta != null) return meta;
            }
            catch
            {
                // Fall through to boundary extraction
            }

            // 3. Fallback: Locate outermost JSON object boundaries '{' and '}'
            int firstBrace = rawOutput.IndexOf('{');
            int lastBrace = rawOutput.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                var candidate = rawOutput.Substring(firstBrace, lastBrace - firstBrace + 1);
                try
                {
                    var meta = JsonConvert.DeserializeObject<YtDlpMetadata>(candidate);
                    if (meta != null) return meta;
                }
                catch
                {
                    // Fall through to JsonTextReader
                }
            }

            // 4. Fallback: Use JsonTextReader with SupportMultipleContent to read first JSON object
            try
            {
                int startIndex = firstBrace >= 0 ? firstBrace : 0;
                using var strReader = new StringReader(rawOutput.Substring(startIndex));
                using var jsonReader = new JsonTextReader(strReader) { SupportMultipleContent = true };
                var serializer = JsonSerializer.CreateDefault();
                var meta = serializer.Deserialize<YtDlpMetadata>(jsonReader);
                if (meta != null) return meta;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse yt-dlp metadata JSON: {ex.Message}", ex);
            }

            throw new Exception("Failed to parse yt-dlp output: No valid metadata JSON found.");
        }

        private static string DetectSourcePlatform(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "Direct";
            if (url.Contains("iq.com", StringComparison.OrdinalIgnoreCase) || url.Contains("iqiyi.com", StringComparison.OrdinalIgnoreCase))
                return "iQiyi";
            if (url.Contains("kisskh.co", StringComparison.OrdinalIgnoreCase))
                return "KissKH";
            if (url.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) || url.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                return "YouTube";
            if (url.Contains("dramabox", StringComparison.OrdinalIgnoreCase))
                return "DramaBox";
            if (url.Contains("iflix.com", StringComparison.OrdinalIgnoreCase) || url.Contains("wetvinfo.com", StringComparison.OrdinalIgnoreCase))
                return "Iflix";

            return "Direct";
        }

        private string BuildAuthArguments(string url)
        {
            var platform = DetectSourcePlatform(url);
            var settings = _processingSettings;
            if (settings == null && _settingsRepository != null)
            {
                try
                {
                    settings = _settingsRepository.LoadAsync().GetAwaiter().GetResult();
                }
                catch { }
            }

            if (platform == "iQiyi" && settings?.IqiyiAccounts != null)
            {
                var account = settings.IqiyiAccounts.FirstOrDefault(a => a.IsActive);
                if (account != null && !string.IsNullOrEmpty(account.Email) && !string.IsNullOrEmpty(account.Password))
                {
                    return $"--username \"{account.Email}\" --password \"{account.Password}\"";
                }
            }

            return string.Empty;
        }
    }
}
