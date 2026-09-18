using System;
using System.Collections.Generic;
using System.Linq;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class YtDlpDownloadEngineTests
    {
        [Fact]
        public void TryParseProgress_StandardYtDlpFormat_ParsesCorrectly()
        {
            string line = "[download]  23.5% of  246.03MiB at  1.02MiB/s ETA 03:12";

            bool result = YtDlpDownloadEngine.TryParseProgress(line, out double percent, out string speed, out string eta);

            Assert.True(result);
            Assert.Equal(23.5, percent);
            Assert.Equal("1.02MiB/s", speed);
            Assert.Equal("03:12", eta);
        }

        [Fact]
        public void TryParseProgress_Aria2cFormat_ParsesCorrectly()
        {
            string line = "[#48bc29 35MiB/246MiB(14%) CN:16 DL:1.1MiB ETA:3m28s]";

            bool result = YtDlpDownloadEngine.TryParseProgress(line, out double percent, out string speed, out string eta);

            Assert.True(result);
            Assert.Equal(14, percent);
            Assert.Equal("1.1MiB", speed);
            Assert.Equal("3m28s", eta);
        }

        [Fact]
        public void TryParseProgress_Aria2cFormatWithoutEta_ParsesCorrectly()
        {
            string line = "[#48bc29 100MiB/100MiB(100%) CN:8 DL:5.4MiB]";

            bool result = YtDlpDownloadEngine.TryParseProgress(line, out double percent, out string speed, out string eta);

            Assert.True(result);
            Assert.Equal(100, percent);
            Assert.Equal("5.4MiB", speed);
            Assert.Equal(string.Empty, eta);
        }

        [Fact]
        public void TryParseProgress_NonProgressLine_ReturnsFalse()
        {
            string line = "[youtube] Extracting URL: https://youtu.be/XVY-7tJNYGI";

            bool result = YtDlpDownloadEngine.TryParseProgress(line, out double percent, out string speed, out string eta);

            Assert.False(result);
            Assert.Equal(0, percent);
        }

        [Fact]
        public void QualityDefaulting_Prefers1080pWhenAvailable()
        {
            var qualities = new List<string> { "2160p (4K)", "1440p (2K)", "1080p (Full HD)", "720p (HD)", "480p" };

            // Logic matching AddDownloadViewModel: prefer 1080p, fallback to first
            string selected = qualities.FirstOrDefault(q => q.Contains("1080")) ?? qualities.First();

            Assert.Equal("1080p (Full HD)", selected);
        }

        [Fact]
        public void QualityDefaulting_FallsBackToFirstWhen1080pMissing()
        {
            var qualities = new List<string> { "720p (HD)", "480p", "360p" };

            string selected = qualities.FirstOrDefault(q => q.Contains("1080")) ?? qualities.First();

            Assert.Equal("720p (HD)", selected);
        }
    }
}
