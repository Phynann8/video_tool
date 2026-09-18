using System;
using System.Collections.Generic;
using Start.Infrastructure.Models.YtDlp;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class YtDlpExtractorEngineTests
    {
        [Fact]
        public void ParseYtDlpMetadata_CleanSingleLineJson_ParsesSuccessfully()
        {
            string json = "{\"id\": \"video123\", \"title\": \"Sample Video\", \"duration\": 120.5, \"formats\": [{\"format_id\": \"137\", \"width\": 1920, \"height\": 1080}]}";

            var metadata = YtDlpExtractorEngine.ParseYtDlpMetadata(json);

            Assert.NotNull(metadata);
            Assert.Equal("video123", metadata.Id);
            Assert.Equal("Sample Video", metadata.Title);
            Assert.Equal(120.5, metadata.Duration);
            Assert.Single(metadata.Formats);
            Assert.Equal("137", metadata.Formats[0].FormatId);
            Assert.Equal(1080, metadata.Formats[0].Height);
        }

        [Fact]
        public void ParseYtDlpMetadata_WithTrailingProbeLine_ParsesSuccessfullyWithoutCrashing()
        {
            // Exact scenario reported: line 1 is video metadata, line 2 is rogue [PROBE] output
            string rawOutput = "{\"id\": \"XVY-7tJNYGI\", \"title\": \"Test Song\", \"duration\": 244.0, \"formats\": [{\"format_id\": \"251\", \"acodec\": \"opus\"}]}\n" +
                               "[PROBE] {\"sys.prefix\": \"C:\\\\Python312\", \"frozen\": true, \"cryptography\": \"ERROR\"}\n";

            var metadata = YtDlpExtractorEngine.ParseYtDlpMetadata(rawOutput);

            Assert.NotNull(metadata);
            Assert.Equal("XVY-7tJNYGI", metadata.Id);
            Assert.Equal("Test Song", metadata.Title);
            Assert.Equal(244.0, metadata.Duration);
            Assert.Single(metadata.Formats);
            Assert.Equal("251", metadata.Formats[0].FormatId);
        }

        [Fact]
        public void ParseYtDlpMetadata_WithLeadingWarningLines_ParsesSuccessfully()
        {
            // Leading warning lines from yt-dlp or python libraries
            string rawOutput = "WARNING: [youtube] Some non-fatal warning occurred\n" +
                               "DEBUG: Initializing cipher\n" +
                               "{\"id\": \"vid999\", \"title\": \"Clean Video\", \"formats\": []}\n" +
                               "[info] Finished metadata extraction";

            var metadata = YtDlpExtractorEngine.ParseYtDlpMetadata(rawOutput);

            Assert.NotNull(metadata);
            Assert.Equal("vid999", metadata.Id);
            Assert.Equal("Clean Video", metadata.Title);
        }

        [Fact]
        public void ParseYtDlpMetadata_MultiLineJsonWithTrailingText_ParsesSuccessfully()
        {
            string rawOutput = "{\n" +
                               "  \"id\": \"multi_line_id\",\n" +
                               "  \"title\": \"Multi-line Title\",\n" +
                               "  \"formats\": []\n" +
                               "}\n" +
                               "Additional text encountered after finished reading JSON content: [";

            var metadata = YtDlpExtractorEngine.ParseYtDlpMetadata(rawOutput);

            Assert.NotNull(metadata);
            Assert.Equal("multi_line_id", metadata.Id);
            Assert.Equal("Multi-line Title", metadata.Title);
        }

        [Fact]
        public void ParseYtDlpMetadata_PlaylistType_ParsesEntries()
        {
            string rawOutput = "{\"_type\": \"playlist\", \"id\": \"pl123\", \"title\": \"My Playlist\", \"entries\": [{\"id\": \"ep1\", \"title\": \"Episode 1\"}, {\"id\": \"ep2\", \"title\": \"Episode 2\"}]}\n" +
                               "[PROBE] {\"details\": \"ok\"}";

            var metadata = YtDlpExtractorEngine.ParseYtDlpMetadata(rawOutput);

            Assert.NotNull(metadata);
            Assert.Equal("playlist", metadata.Type);
            Assert.Equal("pl123", metadata.Id);
            Assert.Equal(2, metadata.Entries.Count);
            Assert.Equal("ep1", metadata.Entries[0].Id);
            Assert.Equal("ep2", metadata.Entries[1].Id);
        }

        [Fact]
        public void ParseYtDlpMetadata_EmptyOrWhitespace_ThrowsException()
        {
            Assert.Throws<Exception>(() => YtDlpExtractorEngine.ParseYtDlpMetadata(""));
            Assert.Throws<Exception>(() => YtDlpExtractorEngine.ParseYtDlpMetadata("   \r\n  "));
        }

        [Fact]
        public void ParseYtDlpMetadata_NoValidJson_ThrowsException()
        {
            string invalid = "ERROR: Failed to connect to server.\nProcess exited.";
            Assert.Throws<Exception>(() => YtDlpExtractorEngine.ParseYtDlpMetadata(invalid));
        }
    }
}
