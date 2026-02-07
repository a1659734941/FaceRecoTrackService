using System;
using System.ComponentModel.DataAnnotations;

namespace FaceRecoTrackService.Core.Dtos
{
    /// <summary>
    /// 电脑人脸识别请求类
    /// </summary>
    public class ComputerFaceRecognitionRequest
    {
        /// <summary>
        /// Base64编码的人脸图像
        /// </summary>
        [Required(ErrorMessage = "Base64图像不能为空")]
        [MaxLength(1048576, ErrorMessage = "Base64图像大小不能超过1MB")]
        public string Base64Image { get; set; } = "";
        
        /// <summary>
        /// 识别阈值（可选，默认值由服务配置）
        /// </summary>
        [Range(0, 1, ErrorMessage = "识别阈值必须在0到1之间")]
        public double? Threshold { get; set; }
    }

    /// <summary>
    /// 电脑人脸识别响应类
    /// </summary>
    public class ComputerFaceRecognitionResponse
    {
        /// <summary>
        /// 是否识别成功
        /// </summary>
        public bool Recognized { get; set; }
        
        /// <summary>
        /// 识别到的人脸ID（如果识别成功）
        /// </summary>
        public Guid? FaceId { get; set; }
        
        /// <summary>
        /// 识别到的用户名（如果识别成功）
        /// </summary>
        public string? UserName { get; set; }
        
        /// <summary>
        /// 相似度分数（如果识别成功）
        /// </summary>
        public double? Similarity { get; set; }
        
        /// <summary>
        /// 识别消息
        /// </summary>
        public string Message { get; set; } = "";
        
        /// <summary>
        /// 裁剪后的人脸图像Base64（可选）
        /// </summary>
        public string? CroppedFaceBase64 { get; set; }
    }
}