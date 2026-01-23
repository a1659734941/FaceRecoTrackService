namespace FaceRecoTrackService.Utils.QdrantUtil
{
    /// <summary>
    /// Qdrant连接配置
    /// </summary>
    public class QdrantConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 6334;
        public string CollectionName { get; set; } = "face_collection";
        public bool UseHttps { get; set; } = false;
        public string ApiKey { get; set; } = "";
        public bool RecreateOnVectorSizeMismatch { get; set; } = false;
    }
}
