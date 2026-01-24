namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceCompareResponse
    {
        public bool IsSamePerson { get; set; }
        public float Similarity { get; set; }
        public string FaceImageBase64_1 { get; set; } = "";
        public string FaceImageBase64_2 { get; set; } = "";
    }
}
