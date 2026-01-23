namespace FaceRecoTrackService.Core.Options
{
    public class FaceRecognitionOptions
    {
        public string YoloModelPath { get; set; } = "";
        public string FaceNetModelPath { get; set; } = "";
        public float DetectionConfidence { get; set; } = 0.45f;
        public float IouThreshold { get; set; } = 0.45f;
        public int FaceExpandRatio { get; set; } = 20;
        public double BaseSharpnessThreshold { get; set; } = 35.0;
        public double SizeThresholdCoefficient { get; set; } = 0.0002;
        public int VectorSize { get; set; } = 512;
        public bool EnableDebugSaveFaces { get; set; } = false;
        public string DebugSaveDir { get; set; } = "snapshots/registrations";
    }
}
