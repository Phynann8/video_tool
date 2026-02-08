using Newtonsoft.Json;

namespace Start.Infrastructure.Models.YtDlp
{
    public class YtDlpMetadata
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; } = string.Empty;

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("formats")]
        public List<YtDlpFormat> Formats { get; set; } = new();
    }

    public class YtDlpFormat
    {
        [JsonProperty("format_id")]
        public string FormatId { get; set; } = string.Empty;

        [JsonProperty("ext")]
        public string Extension { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int? Width { get; set; }

        [JsonProperty("height")]
        public int? Height { get; set; }

        [JsonProperty("acodec")]
        public string AudioCodec { get; set; } = string.Empty;

        [JsonProperty("vcodec")]
        public string VideoCodec { get; set; } = string.Empty;

        [JsonProperty("filesize")]
        public long? FileSize { get; set; }
        
        [JsonProperty("filesize_approx")]
        public long? FileSizeApprox { get; set; }

        [JsonProperty("tbr")]
        public double? Bitrate { get; set; }
    }
}
