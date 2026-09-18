using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class IqiyiIntegrationTests
    {
        [Fact]
        public void DownloadProcessingSettings_HasDefaultIqiyiCredentialsConfigured()
        {
            var settings = new DownloadProcessingSettings();

            Assert.NotNull(settings.IqiyiAccounts);
            Assert.Equal(3, settings.IqiyiAccounts.Count);

            var primary = settings.IqiyiAccounts.FirstOrDefault(a => a.Label == "Primary");
            Assert.NotNull(primary);
            Assert.Equal("pongsawat_lee@hotmail.com", primary.Email);
            Assert.Equal("Lee5929354!@#", primary.Password);
            Assert.True(primary.IsActive);

            var backup1 = settings.IqiyiAccounts.FirstOrDefault(a => a.Label == "Primary Backup");
            Assert.NotNull(backup1);
            Assert.Equal("dannydaemon666@yahoo.co.uk", backup1.Email);
            Assert.Equal("cucumber666", backup1.Password);
            Assert.True(backup1.IsActive);

            var backup2 = settings.IqiyiAccounts.FirstOrDefault(a => a.Label == "Secondary Backup");
            Assert.NotNull(backup2);
            Assert.Equal("kero_aum@hotmail.com", backup2.Email);
            Assert.Equal("sichul13102", backup2.Password);
            Assert.True(backup2.IsActive);
        }

        [Theory]
        [InlineData("https://www.iq.com/play/spring-of-the-blade-episode-1-198yzaqjce8?lang=en_us", "iQiyi")]
        [InlineData("https://www.iqiyi.com/v_19rrojlavg.html", "iQiyi")]
        [InlineData("https://kisskh.co/Drama/The-First-Frost?id=8316", "KissKH")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "YouTube")]
        public void YtDlpExtractorEngine_DetectsPlatformCorrectly(string url, string expectedPlatform)
        {
            var extractor = new YtDlpExtractorEngine();
            var method = typeof(YtDlpExtractorEngine).GetMethod("DetectSourcePlatform", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Assert.NotNull(method);
            var result = method.Invoke(null, new object[] { url }) as string;
            Assert.Equal(expectedPlatform, result);
        }

        [Fact]
        public void YtDlpExtractorEngine_BuildAuthArguments_UsesActivePrimaryAccount()
        {
            var settings = new DownloadProcessingSettings();
            var extractor = new YtDlpExtractorEngine(settings);

            var method = typeof(YtDlpExtractorEngine).GetMethod("BuildAuthArguments",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var args = method.Invoke(extractor, new object[] { "https://www.iq.com/play/198yzaqjce8" }) as string;

            Assert.NotNull(args);
            Assert.Contains("pongsawat_lee@hotmail.com", args);
            Assert.Contains("Lee5929354!@#", args);
        }

        [Fact]
        public void YtDlpExtractorEngine_BuildAuthArguments_FallsBackToActiveBackup_WhenPrimaryDisabled()
        {
            var settings = new DownloadProcessingSettings();
            settings.IqiyiAccounts[0].IsActive = false; // Disable primary

            var extractor = new YtDlpExtractorEngine(settings);
            var method = typeof(YtDlpExtractorEngine).GetMethod("BuildAuthArguments",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(method);
            var args = method.Invoke(extractor, new object[] { "https://www.iq.com/play/198yzaqjce8" }) as string;

            Assert.NotNull(args);
            Assert.Contains("dannydaemon666@yahoo.co.uk", args);
            Assert.Contains("cucumber666", args);
        }
    }
}
