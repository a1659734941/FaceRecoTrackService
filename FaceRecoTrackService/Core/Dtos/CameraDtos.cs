using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>新增人脸摄像头请求</summary>
    public class AddFaceCameraRequest
    {
        /// <summary>
        /// 摄像头IP地址
        /// </summary>
        [JsonPropertyName("cameraIp")]
        [Required(ErrorMessage = "摄像头IP不能为空")]
        [MaxLength(50, ErrorMessage = "摄像头IP长度不能超过50个字符")]
        public string CameraIp { get; set; } = "";

        /// <summary>
        /// 摄像头描述信息
        /// </summary>
        [JsonPropertyName("description")]
        [MaxLength(500, ErrorMessage = "描述长度不能超过500个字符")]
        public string? Description { get; set; }
    }

    /// <summary>新增录像摄像头请求</summary>
    public class AddRecordCameraRequest
    {
        /// <summary>
        /// 摄像头IP地址
        /// </summary>
        [JsonPropertyName("cameraIp")]
        [Required(ErrorMessage = "摄像头IP不能为空")]
        [MaxLength(50, ErrorMessage = "摄像头IP长度不能超过50个字符")]
        public string CameraIp { get; set; } = "";

        /// <summary>
        /// 摄像头位置名称
        /// </summary>
        [JsonPropertyName("locationName")]
        [Required(ErrorMessage = "位置名称不能为空")]
        [MaxLength(100, ErrorMessage = "位置名称长度不能超过100个字符")]
        public string LocationName { get; set; } = "";
    }

    /// <summary>绑定人脸与录像摄像头请求（用 id 绑定，不可重复绑定）</summary>
    public class BindCamerasRequest
    {
        /// <summary>
        /// 人脸摄像头ID
        /// </summary>
        [JsonPropertyName("faceCameraId")]
        [Required(ErrorMessage = "人脸摄像头ID不能为空")]
        [Range(1, long.MaxValue, ErrorMessage = "人脸摄像头ID必须为正整数")]
        public long FaceCameraId { get; set; }

        /// <summary>
        /// 录像摄像头ID
        /// </summary>
        [JsonPropertyName("recordCameraId")]
        [Required(ErrorMessage = "录像摄像头ID不能为空")]
        [Range(1, long.MaxValue, ErrorMessage = "录像摄像头ID必须为正整数")]
        public long RecordCameraId { get; set; }
    }

    /// <summary>已绑定项：两个摄像头 IP + 录像位置名称</summary>
    public class BoundCameraItem
    {
        /// <summary>
        /// 绑定关系ID
        /// </summary>
        [JsonPropertyName("mappingId")]
        public long MappingId { get; set; }

        /// <summary>
        /// 人脸摄像头ID
        /// </summary>
        [JsonPropertyName("faceCameraId")]
        public long FaceCameraId { get; set; }

        /// <summary>
        /// 人脸摄像头IP
        /// </summary>
        [JsonPropertyName("faceCameraIp")]
        public string FaceCameraIp { get; set; } = "";

        /// <summary>
        /// 录像摄像头ID
        /// </summary>
        [JsonPropertyName("recordCameraId")]
        public long RecordCameraId { get; set; }

        /// <summary>
        /// 录像摄像头IP
        /// </summary>
        [JsonPropertyName("recordCameraIp")]
        public string RecordCameraIp { get; set; } = "";

        /// <summary>
        /// 录像摄像头位置名称
        /// </summary>
        [JsonPropertyName("locationName")]
        public string LocationName { get; set; } = "";
    }

    /// <summary>修改绑定：换绑或解绑</summary>
    public class UpdateBindingRequest
    {
        /// <summary>
        /// 新录像摄像头ID（用于换绑）
        /// </summary>
        [JsonPropertyName("newRecordCameraId")]
        [Range(1, long.MaxValue, ErrorMessage = "新录像摄像头ID必须为正整数")]
        public long? NewRecordCameraId { get; set; }

        /// <summary>
        /// 是否解绑
        /// </summary>
        [JsonPropertyName("unbind")]
        public bool Unbind { get; set; }
    }

    /// <summary>强行绑定：若已绑定则先解绑再绑</summary>
    public class ForceBindCamerasRequest
    {
        /// <summary>
        /// 人脸摄像头ID
        /// </summary>
        [JsonPropertyName("faceCameraId")]
        [Required(ErrorMessage = "人脸摄像头ID不能为空")]
        [Range(1, long.MaxValue, ErrorMessage = "人脸摄像头ID必须为正整数")]
        public long FaceCameraId { get; set; }

        /// <summary>
        /// 录像摄像头ID
        /// </summary>
        [JsonPropertyName("recordCameraId")]
        [Required(ErrorMessage = "录像摄像头ID不能为空")]
        [Range(1, long.MaxValue, ErrorMessage = "录像摄像头ID必须为正整数")]
        public long RecordCameraId { get; set; }
    }

    /// <summary>人脸摄像头信息（含 id）</summary>
    public class FaceCameraResponse
    {
        /// <summary>
        /// 人脸摄像头ID
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// 人脸摄像头IP
        /// </summary>
        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        /// <summary>
        /// 人脸摄像头描述
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>录像摄像头信息（含 id）</summary>
    public class RecordCameraResponse
    {
        /// <summary>
        /// 录像摄像头ID
        /// </summary>
        [JsonPropertyName("id")]
        public long Id { get; set; }

        /// <summary>
        /// 录像摄像头IP
        /// </summary>
        [JsonPropertyName("cameraIp")]
        public string CameraIp { get; set; } = "";

        /// <summary>
        /// 录像摄像头位置名称
        /// </summary>
        [JsonPropertyName("locationName")]
        public string LocationName { get; set; } = "";

        /// <summary>
        /// 创建时间
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>修改人脸摄像头请求</summary>
    public class UpdateFaceCameraRequest
    {
        /// <summary>
        /// 新的摄像头IP（可选）
        /// </summary>
        [JsonPropertyName("cameraIp")]
        [MaxLength(50, ErrorMessage = "摄像头IP长度不能超过50个字符")]
        public string? CameraIp { get; set; }

        /// <summary>
        /// 新的摄像头描述（可选）
        /// </summary>
        [JsonPropertyName("description")]
        [MaxLength(500, ErrorMessage = "描述长度不能超过500个字符")]
        public string? Description { get; set; }
    }

    /// <summary>修改录像摄像头请求</summary>
    public class UpdateRecordCameraRequest
    {
        /// <summary>
        /// 新的摄像头IP（可选）
        /// </summary>
        [JsonPropertyName("cameraIp")]
        [MaxLength(50, ErrorMessage = "摄像头IP长度不能超过50个字符")]
        public string? CameraIp { get; set; }

        /// <summary>
        /// 新的位置名称（可选）
        /// </summary>
        [JsonPropertyName("locationName")]
        [MaxLength(100, ErrorMessage = "位置名称长度不能超过100个字符")]
        public string? LocationName { get; set; }
    }
}
