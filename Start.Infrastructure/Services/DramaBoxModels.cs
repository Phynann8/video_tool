using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Start.Infrastructure.Services
{
    public class DramaBoxEpisode
    {
        [JsonProperty("chapterId")] public string ChapterId { get; set; } = string.Empty;
        [JsonProperty("id")] public string Id { set { if (string.IsNullOrEmpty(ChapterId)) ChapterId = value; } }
        [JsonProperty("chapterIndex")] public int ChapterIndex { get; set; }
        [JsonProperty("index")] public int Index { set { ChapterIndex = value; } }
        [JsonProperty("isCharge")] public int IsCharge { get; set; }
        [JsonProperty("unlock")] public object Unlock { set { 
            if (value is bool b) IsCharge = b ? 0 : 1;
            else if (value is long l) IsCharge = l == 1 ? 0 : 1;
            else if (value is int i) IsCharge = i == 1 ? 0 : 1;
            else if (value is JValue jv) {
                if (jv.Type == JTokenType.Boolean) IsCharge = jv.Value<bool>() ? 0 : 1;
                else if (jv.Type == JTokenType.Integer) IsCharge = jv.Value<int>() == 1 ? 0 : 1;
            }
        } }
        [JsonProperty("chapterName")] public string ChapterName { get; set; } = string.Empty;
        [JsonProperty("name")] public string Name { set { if (string.IsNullOrEmpty(ChapterName)) ChapterName = value; } }
        [JsonProperty("cdnList")] public List<DramaBoxCdn> CdnList { get; set; } = new();
        [JsonProperty("mp4")] public string Mp4 { get; set; } = string.Empty;
        [JsonProperty("chapterImg")] public string ChapterImg { get; set; } = string.Empty;
        [JsonProperty("cover")] public string Cover { set { if (string.IsNullOrEmpty(ChapterImg)) ChapterImg = value; } }
        [JsonProperty("chargeChapter")] public bool ChargeChapter { get; set; }
    }

    public class DramaBoxCdn
    {
        [JsonProperty("cdnDomain")] public string CdnDomain { get; set; } = string.Empty;
        [JsonProperty("isDefault")] public int IsDefault { get; set; }
        [JsonProperty("videoPathList")] public List<DramaBoxVideoPath> VideoPathList { get; set; } = new();
    }

    public class DramaBoxVideoPath
    {
        [JsonProperty("quality")] public int Quality { get; set; }
        [JsonProperty("videoPath")] public string VideoPath { get; set; } = string.Empty;
        [JsonProperty("isDefault")] public int IsDefault { get; set; }
    }
}
