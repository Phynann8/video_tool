using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests
{
    public class SystemIntegrationTests
    {
        [Fact]
        public void PowerManagementService_TracksSleepPreventionStateCorrectly()
        {
            var service = new PowerManagementService();
            Assert.False(service.IsSleepPrevented);

            service.PreventSleep();
            Assert.True(service.IsSleepPrevented);

            service.PreventSleep();
            Assert.True(service.IsSleepPrevented);

            service.AllowSleep();
            Assert.True(service.IsSleepPrevented);

            service.AllowSleep();
            Assert.False(service.IsSleepPrevented);

            // Extra calls should not underflow below 0
            service.AllowSleep();
            Assert.False(service.IsSleepPrevented);
        }

        [Fact]
        public void NativeMessagingProtocol_EncodesAndDecodesFramedMessagesCorrectly()
        {
            var payload = new
            {
                action = "download",
                url = "https://example.com/stream.mp4",
                pageTitle = "Test Stream",
                cookies = "session=12345"
            };

            string json = JsonSerializer.Serialize(payload);
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(json);

            // Encode with 4-byte little-endian length prefix
            byte[] lengthBuffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(lengthBuffer, utf8Bytes.Length);

            using var ms = new MemoryStream();
            ms.Write(lengthBuffer);
            ms.Write(utf8Bytes);

            // Now decode stream as Native Messaging Host does
            ms.Position = 0;
            byte[] readLengthBuffer = new byte[4];
            int read = ms.Read(readLengthBuffer, 0, 4);
            Assert.Equal(4, read);

            int decodedLength = BinaryPrimitives.ReadInt32LittleEndian(readLengthBuffer);
            Assert.Equal(utf8Bytes.Length, decodedLength);

            byte[] readBody = new byte[decodedLength];
            int bodyRead = ms.Read(readBody, 0, decodedLength);
            Assert.Equal(decodedLength, bodyRead);

            string readJson = Encoding.UTF8.GetString(readBody);
            Assert.Equal(json, readJson);
        }

        [Fact]
        public void DownloadJob_HoldsBrowserSessionMetadata()
        {
            var job = new DownloadJob
            {
                Url = "https://cdn.example.com/video.mp4",
                Title = "Protected CDN Video",
                Cookies = "auth_token=secret123; user_id=456",
                Referer = "https://example.com/watch",
                UserAgent = "Mozilla/5.0 TestBrowser"
            };

            Assert.Equal("auth_token=secret123; user_id=456", job.Cookies);
            Assert.Equal("https://example.com/watch", job.Referer);
            Assert.Equal("Mozilla/5.0 TestBrowser", job.UserAgent);
        }
    }
}
