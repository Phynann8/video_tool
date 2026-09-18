using System.Net;
using System.Net.Http.Headers;
using Start.Core.Models;
using Start.Infrastructure.Services;
using Xunit;

namespace Downloader.Tests;

public sealed class HttpDownloadEngineTests
{
    [Fact]
    public async Task Downloads_range_supported_file_and_assembles_parts_in_order()
    {
        var bytes = CreatePayload(5 * 1024 * 1024 + 123);
        var handler = new RangeHttpMessageHandler(bytes);
        using var client = new HttpClient(handler);
        var engine = new HttpDownloadEngine(client);
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var job = CreateJob(directory);

        try
        {
            await engine.StartDownloadAsync(job, new Progress<DownloadJob>(), CancellationToken.None);

            var output = Path.Combine(directory, "range-test.mp4");
            Assert.Equal(bytes, await File.ReadAllBytesAsync(output));
            Assert.True(handler.RangeRequestCount >= 2);
            Assert.Equal(JobStatus.Completed, job.Status);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Retries_a_transient_range_failure()
    {
        var bytes = CreatePayload(5 * 1024 * 1024 + 123);
        var handler = new RangeHttpMessageHandler(bytes) { FailFirstRangeRequest = true };
        using var client = new HttpClient(handler);
        var engine = new HttpDownloadEngine(client);
        var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var job = CreateJob(directory);

        try
        {
            await engine.StartDownloadAsync(job, new Progress<DownloadJob>(), CancellationToken.None);

            var output = Path.Combine(directory, "range-test.mp4");
            Assert.Equal(bytes, await File.ReadAllBytesAsync(output));
            Assert.True(handler.RangeRequestCount >= 3);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static DownloadJob CreateJob(string directory) => new()
    {
        Url = "https://test.invalid/media.mp4",
        Title = "range-test",
        SavePath = directory,
        SelectedVideoStream = new VideoStreamInfo { Resolution = "1080p" }
    };

    private static byte[] CreatePayload(int length)
    {
        var bytes = new byte[length];
        for (var index = 0; index < bytes.Length; index++)
            bytes[index] = (byte)(index % 251);
        return bytes;
    }

    private sealed class RangeHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        private int _failed;
        private int _rangeRequestCount;

        public RangeHttpMessageHandler(byte[] bytes) => _bytes = bytes;
        public bool FailFirstRangeRequest { get; init; }
        public int RangeRequestCount => Volatile.Read(ref _rangeRequestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Head)
            {
                var head = new HttpResponseMessage(HttpStatusCode.OK);
                head.Headers.AcceptRanges.Add("bytes");
                head.Content = new ByteArrayContent(Array.Empty<byte>());
                head.Content.Headers.ContentLength = _bytes.Length;
                return Task.FromResult(head);
            }

            var range = request.Headers.Range?.Ranges.SingleOrDefault();
            if (range?.From is null || range.To is null)
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable));

            Interlocked.Increment(ref _rangeRequestCount);
            if (FailFirstRangeRequest && Interlocked.Exchange(ref _failed, 1) == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            var start = checked((int)range.From.Value);
            var end = checked((int)range.To.Value);
            var content = new ByteArrayContent(_bytes[start..(end + 1)]);
            content.Headers.ContentRange = new ContentRangeHeaderValue(start, end, _bytes.Length);
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = content };
            return Task.FromResult(response);
        }
    }
}
