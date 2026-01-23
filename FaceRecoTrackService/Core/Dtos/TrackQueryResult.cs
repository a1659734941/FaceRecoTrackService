using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FaceRecoTrackService.Core.Dtos
{
    public class TrackQueryItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("抓拍时间")]
        public string SnapTime { get; set; } = "";

        [JsonPropertyName("抓拍地点")]
        public string SnapLocation { get; set; } = "";

        [JsonPropertyName("抓拍摄像头")]
        public string SnapCamera { get; set; } = "";

        [JsonPropertyName("录像摄像头")]
        public string RecordCamera { get; set; } = "";

        [JsonPropertyName("录像开始时间")]
        public string RecordStartTime { get; set; } = "";

        [JsonPropertyName("录像结束时间")]
        public string RecordEndTime { get; set; } = "";
    }

    public class TrackQueryResult
    {
        [JsonPropertyName("list")]
        public List<TrackQueryItem> List { get; set; } = new();

        [JsonPropertyName("pagesize")]
        public int Pagesize { get; set; }

        [JsonPropertyName("pagenum")]
        public int Pagenum { get; set; }

        [JsonPropertyName("total")]
        public long Total { get; set; }
    }
}
