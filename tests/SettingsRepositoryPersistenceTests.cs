using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Start.Core.Models;
using Start.Infrastructure.Data;
using Xunit;

namespace Downloader.Tests
{
    public class SettingsRepositoryPersistenceTests : IDisposable
    {
        private readonly string _testDbPath;

        public SettingsRepositoryPersistenceTests()
        {
            _testDbPath = Path.Combine(Path.GetTempPath(), $"settings_repo_test_{Guid.NewGuid():N}.db");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
            }
            catch { }
        }

        [Fact]
        public async Task SqliteSettingsRepository_LoadsDefaultSettingsWhenEmpty()
        {
            var repo = new SqliteSettingsRepository(_testDbPath);
            var settings = await repo.LoadAsync();

            Assert.NotNull(settings);
            Assert.Equal(3, settings.IqiyiAccounts.Count);
            Assert.Equal("Primary", settings.IqiyiAccounts[0].Label);
            Assert.Equal("Primary Backup", settings.IqiyiAccounts[1].Label);
            Assert.Equal("Secondary Backup", settings.IqiyiAccounts[2].Label);
            Assert.Equal(16, settings.MaxSegmentWorkers);
            Assert.Equal(3, settings.MaxConcurrentDownloads);
            Assert.True(settings.EnableClipboardMonitoring);
        }

        [Fact]
        public async Task SqliteSettingsRepository_SavesAndPersistsAllConfigurations()
        {
            var repo = new SqliteSettingsRepository(_testDbPath);
            var settings = new DownloadProcessingSettings
            {
                DefaultDownloadPath = @"C:\Downloads\TestMedia",
                MaxSegmentWorkers = 28,
                MinSegmentSizeBytes = 8 * 1024 * 1024,
                MaxConcurrentDownloads = 6,
                EnableClipboardMonitoring = false,
                ExtractAudio = true,
                AudioFormat = "flac",
                EmbedMetadata = false,
                EmbedThumbnail = false,
                IqiyiAccounts = new()
                {
                    new IqiyiAccountCredential { Label = "Primary", Email = "custom_vip@iq.com", Password = "secret_password", IsActive = true },
                    new IqiyiAccountCredential { Label = "Primary Backup", Email = "custom_backup@iq.com", Password = "backup_pass", IsActive = false },
                    new IqiyiAccountCredential { Label = "Secondary Backup", Email = "third_backup@iq.com", Password = "third_pass", IsActive = true }
                }
            };

            await repo.SaveAsync(settings);

            // Re-read with a fresh repository instance
            var reloadedRepo = new SqliteSettingsRepository(_testDbPath);
            var loaded = await reloadedRepo.LoadAsync();

            Assert.Equal(@"C:\Downloads\TestMedia", loaded.DefaultDownloadPath);
            Assert.Equal(28, loaded.MaxSegmentWorkers);
            Assert.Equal(8 * 1024 * 1024, loaded.MinSegmentSizeBytes);
            Assert.Equal(6, loaded.MaxConcurrentDownloads);
            Assert.False(loaded.EnableClipboardMonitoring);
            Assert.True(loaded.ExtractAudio);
            Assert.Equal("flac", loaded.AudioFormat);
            Assert.False(loaded.EmbedMetadata);
            Assert.False(loaded.EmbedThumbnail);

            Assert.Equal(3, loaded.IqiyiAccounts.Count);
            Assert.Equal("custom_vip@iq.com", loaded.IqiyiAccounts[0].Email);
            Assert.True(loaded.IqiyiAccounts[0].IsActive);
            Assert.Equal("custom_backup@iq.com", loaded.IqiyiAccounts[1].Email);
            Assert.False(loaded.IqiyiAccounts[1].IsActive);
            Assert.Equal("third_backup@iq.com", loaded.IqiyiAccounts[2].Email);
            Assert.True(loaded.IqiyiAccounts[2].IsActive);
        }
    }
}
