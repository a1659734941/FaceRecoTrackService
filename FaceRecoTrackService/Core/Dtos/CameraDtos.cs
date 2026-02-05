using System.Text.Json.Serialization;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>新增人脸摄像头请求</summary>
    public class AddFaceCameraRequest
    {
        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>新增录像摄像头请求</summary>
    public class AddRecordCameraRequest
    {
        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        [JsonPropertyName("locationName")]
        public string LocationName { get; set; } = "";
    }

    /// <summary>绑定人脸与录像摄像头请求（用 id 绑定，不可重复绑定）</summary>
    public class BindCamerasRequest
    {
        [JsonPropertyName("faceCameraId")]
        public long FaceCameraId { get; set; }

        [JsonPropertyName("recordCameraId")]
        public long RecordCameraId { get; set; }
    }

    /// <summary>已绑定项：两个摄像头 IP + 录像位置名称</summary>
    public class BoundCameraItem
    {
        [JsonPropertyName("mappingId")]
        public long MappingId { get; set; }

        [JsonPropertyName("faceCameraId")]
        public long FaceCameraId { get; set; }

        [JsonPropertyName("faceCameraIp")]
        public string FaceCameraIp { get; set; } = "";

        [JsonPropertyName("recordCameraId")]
        public long RecordCameraId { get; set; }

        [JsonPropertyName("recordCameraIp")]
        public string RecordCameraIp { get; set; } = "";

        [JsonPropertyName("locationName")]
        public string LocationName { get; set; } = "";
    }

    /// <summary>修改绑定：换绑或解绑</summary>
    public class UpdateBindingRequest
    {
        [JsonPropertyName("newRecordCameraId")]
        public long? NewRecordCameraId { get; set; }

        [JsonPropertyName("unbind")]
        public bool Unbind { get; set; }
    }

    /// <summary>强行绑定：若已绑定则先解绑再绑</summary>
    public class ForceBindCamerasRequest
    {
        [JsonPropertyName("faceCameraId")]
        public long FaceCameraId { get; set; }

        [JsonPropertyName("recordCameraId")]
        public long RecordCameraId { get; set; }
    }

    /// <summary>人脸摄像头信息（含 id）</summary>
    public class FaceCameraResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>录像摄像头信息（含 id）</summary>
    public class RecordCameraResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        [JsonPropertyName("locationName")]
        public string LocationName { get; set; } = "";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>修改人脸摄像头请求</summary>
    public class UpdateFaceCameraRequest
    {
        [JsonPropertyName("cameraIp")]
        public string? CameraIp { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    /// <summary>修改录像摄像头请求</summary>
    public class UpdateRecordCameraRequest
    {
        [JsonPropertyName("cameraIp")]
        public string? CameraIp { get; set; }

        [JsonPropertyName("locationName")]
        public string? LocationName { get; set; }
    }
}
