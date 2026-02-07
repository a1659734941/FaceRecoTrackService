using System.ComponentModel.DataAnnotations;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>
    /// 人脸对比请求类
    /// </summary>
    public class FaceCompareRequest
    {
        /// <summary>
        /// 第一张Base64编码的人脸图像
        /// </summary>
        [Required(ErrorMessage = "第一张Base64图像不能为空")]
        [MaxLength(1048576, ErrorMessage = "第一张Base64图像大小不能超过1MB")]
        public string Base64Image1 { get; set; } = "";
        
        /// <summary>
        /// 第二张Base64编码的人脸图像
        /// </summary>
        [Required(ErrorMessage = "第二张Base64图像不能为空")]
        [MaxLength(1048576, ErrorMessage = "第二张Base64图像大小不能超过1MB")]
        public string Base64Image2 { get; set; } = "";
    }
}
