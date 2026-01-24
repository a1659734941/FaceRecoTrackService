namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceCheckResponse
    {
        public bool IsFace { get; set; }
        public bool IsCompliant { get; set; }
        public string? FaceImageBase64 { get; set; }
        public string Reason { get; set; } = "";
    }
}
