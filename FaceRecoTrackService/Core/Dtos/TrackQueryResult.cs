using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FaceRecoTrackService.Core.Dtos
{
    public class TrackQueryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("snapTime")]
        public string SnapTime { get; set; } = "";

        [JsonPropertyName("snapLocation")]
        public string SnapLocation { get; set; } = "";

        [JsonPropertyName("snapCamera")]
        public string SnapCamera { get; set; } = "";

        [JsonPropertyName("recordCamera")]
        public string RecordCamera { get; set; } = "";

        [JsonPropertyName("recordStartTime")]
        public string RecordStartTime { get; set; } = "";

        [JsonPropertyName("recordEndTime")]
        public string RecordEndTime { get; set; } = "";
    }

    public class TrackQueryResult
    {
        [JsonPropertyName("list")]
        public List<TrackQueryItem> List { get; set; } = new();

        [JsonPropertyName("pageSize")]
        public int Pagesize { get; set; }

        [JsonPropertyName("pageNum")]
        public int Pagenum { get; set; }

        [JsonPropertyName("total")]
        public long Total { get; set; }
    }
}
