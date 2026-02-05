namespace FaceRecoTrackService.Core.Models
{
    /// <summary>
    /// 录像摄像头
    /// </summary>
    public class RecordCamera
    {
        public long Id { get; set; }
        public string CameraIp { get; set; } = "";
        public string LocationName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}
