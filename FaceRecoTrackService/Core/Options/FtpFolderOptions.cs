namespace FaceRecoTrackService.Core.Options
{
    public class FtpFolderOptions
    {
        public string Path { get; set; } = "";
        public bool IncludeSubdirectories { get; set; } = true;
        public string[] FilePatterns { get; set; } = new[] { "*.jpg", "*.jpeg", "*.png" };
        public string DefaultCameraName { get; set; } = "unknown";
        public string DefaultLocation { get; set; } = "unknown";
    }
}
