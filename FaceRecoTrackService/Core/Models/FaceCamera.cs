namespace FaceRecoTrackService.Core.Models
{
    /// <summary>
    /// 人脸/抓拍摄像头
    /// </summary>
    public class FaceCamera
    {
        public long Id { get; set; }
        public string CameraIp { get; set; } = "";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
