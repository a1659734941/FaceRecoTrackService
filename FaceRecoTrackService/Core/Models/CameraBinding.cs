namespace FaceRecoTrackService.Core.Models
{
    /// <summary>
    /// 人脸摄像头与录像摄像头绑定关系
    /// </summary>
    public class CameraBinding
    {
        public long Id { get; set; }
        public long FaceCameraId { get; set; }
        public long RecordCameraId { get; set; }
    }
}
