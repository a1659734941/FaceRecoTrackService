using System.ComponentModel.DataAnnotations;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>
    /// 人脸注册请求类
    /// </summary>
    public class FaceRegisterRequest
    {
        /// <summary>
        /// Base64编码的人脸图像
        /// </summary>
        [Required(ErrorMessage = "Base64图像不能为空")]
        [MaxLength(1048576, ErrorMessage = "Base64图像大小不能超过1MB")]
        public string Base64Image { get; set; } = "";
        
        /// <summary>
        /// 用户名
        /// </summary>
        [Required(ErrorMessage = "用户名不能为空")]
        [MaxLength(100, ErrorMessage = "用户名长度不能超过100个字符")]
        public string UserName { get; set; } = "";
        
        /// <summary>
        /// 摄像头IP地址
        /// </summary>
        [MaxLength(50, ErrorMessage = "IP地址长度不能超过50个字符")]
        public string Ip { get; set; } = "";
        
        /// <summary>
        /// 人脸描述信息
        /// </summary>
        [MaxLength(500, ErrorMessage = "描述长度不能超过500个字符")]
        public string Description { get; set; } = "";
    }
}
