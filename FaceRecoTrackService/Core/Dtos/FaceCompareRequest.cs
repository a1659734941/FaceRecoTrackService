namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceCompareRequest
    {
        public string Base64Image1 { get; set; } = "";
        public string Base64Image2 { get; set; } = "";
    }
}
