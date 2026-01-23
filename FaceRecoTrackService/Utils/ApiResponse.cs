using System.Text.Json.Serialization;

namespace FaceRecoTrackService.Utils
{
    public class ApiResponse<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("msg")]
        public string Msg { get; set; } = "";

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        public static ApiResponse<T> Ok(T? data, string msg = "查询成功")
        {
            return new ApiResponse<T> { Code = 200, Msg = msg, Data = data };
        }

        public static ApiResponse<T> Fail(int code, string msg)
        {
            return new ApiResponse<T> { Code = code, Msg = msg, Data = default };
        }
    }
}
