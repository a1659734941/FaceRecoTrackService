namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceRegisterRequest
    {
        public string Base64Image { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Ip { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsTest { get; set; }
    }
}
