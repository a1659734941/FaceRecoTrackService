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
        public QdrantEmbeddedOptions Embedded { get; set; } = new();
    }

    /// <summary>
    /// 内置 Qdrant 启动配置
    /// </summary>
    public class QdrantEmbeddedOptions
    {
        public bool Enabled { get; set; } = true;
        public string BinPath { get; set; } = @"qdrant\qdrant.exe";
        public string WorkingDirectory { get; set; } = "qdrant";
        public string Arguments { get; set; } = "";
        public int StartupTimeoutSeconds { get; set; } = 15;
        public int PortCheckTimeoutMs { get; set; } = 500;
    }
}
