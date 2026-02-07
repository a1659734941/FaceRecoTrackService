using System.ComponentModel.DataAnnotations;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>
    /// 人脸合规检测请求类
    /// </summary>
    public class FaceCheckRequest
    {
        /// <summary>
        /// Base64编码的人脸图像
        /// </summary>
        [Required(ErrorMessage = "Base64图像不能为空")]
        [MaxLength(1048576, ErrorMessage = "Base64图像大小不能超过1MB")]
        public string Base64Image { get; set; } = "";
    }
}
