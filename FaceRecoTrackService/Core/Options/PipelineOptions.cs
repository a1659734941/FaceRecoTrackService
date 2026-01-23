namespace FaceRecoTrackService.Core.Options
{
    public class PipelineOptions
    {
        public int PollIntervalMs { get; set; } = 2000;
        public int MinFaceCount { get; set; } = 1;
        public int TopK { get; set; } = 5;
        public float SimilarityThreshold { get; set; } = 0.87f;
        public float FallbackSimilarityThreshold { get; set; } = 0.78f;
        public string SnapshotSaveDir { get; set; } = "snapshots";
        public bool DeleteProcessedSnapshots { get; set; } = false;
    }
}
