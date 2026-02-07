using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Services;
using FaceRecoTrackService.Utils;
using Microsoft.AspNetCore.Mvc;

namespace FaceRecoTrackService.API.Controllers
{
    /// <summary>
    /// 人脸相关接口
    /// </summary>
    [ApiController]
    [Route("api/face")]
    public class FaceController : ControllerBase
    {
        private readonly FaceRegistrationService _registrationService;
        private readonly FaceDeletionService _deletionService;
        private readonly FaceQueryService _queryService;
        private readonly FaceVerificationService _verificationService;

        /// <summary>
        /// 初始化人脸接口控制器
        /// </summary>
        /// <param name="registrationService">人脸注册服务</param>
        /// <param name="deletionService">人脸删除服务</param>
        /// <param name="queryService">人脸查询服务</param>
        public FaceController(
            FaceRegistrationService registrationService,
            FaceDeletionService deletionService,
            FaceQueryService queryService,
            FaceVerificationService verificationService)
        {
            _registrationService = registrationService;
            _deletionService = deletionService;
            _queryService = queryService;
            _verificationService = verificationService;
        }

        /// <summary>
        /// 注册人脸信息（写入PG与Qdrant）
        /// </summary>
        /// <param name="request">人脸注册请求，包含base64图片与用户名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回注册成功的人脸唯一ID</returns>
        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<FaceRegisterResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Register([FromBody] FaceRegisterRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<FaceRegisterResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用注册服务进行人脸注册
                var result = await _registrationService.RegisterAsync(request, cancellationToken);
                // 返回注册成功的结果
                return Ok(ApiResponse<FaceRegisterResponse>.Ok(result, "注册成功"));
            }
            catch (ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<FaceRegisterResponse>.Fail(400, ex.Message));
            }
            catch (Exception ex)
            {
                // 处理其他错误
                return Ok(ApiResponse<FaceRegisterResponse>.Fail(400, $"注册失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 获取PG库已注册人脸数量和完整信息列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回PG库中人脸数量和完整信息列表</returns>
        [HttpGet("count")]
        [ProducesResponseType(typeof(ApiResponse<FaceCountResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Count(CancellationToken cancellationToken)
        {
            try
            {
                var count = await _queryService.GetCountAsync(cancellationToken);
                var faces = await _queryService.GetAllFacesAsync(cancellationToken);
                var response = new FaceCountResponse
                {
                    Count = count,
                    Faces = faces as List<FaceInfoResponse> ?? faces.ToList()
                };
                return Ok(ApiResponse<FaceCountResponse>.Ok(response, "查询成功"));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<FaceCountResponse>.Fail(400, $"查询失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 获取Qdrant库已注册人脸数量
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回Qdrant集合中的向量数量</returns>
        [HttpGet("qdrant/count")]
        [ProducesResponseType(typeof(ApiResponse<long>), StatusCodes.Status200OK)]
        public async Task<IActionResult> QdrantCount(CancellationToken cancellationToken)
        {
            try
            {
                var count = await _queryService.GetQdrantCountAsync(cancellationToken);
                return Ok(ApiResponse<long>.Ok(count, "查询成功"));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<long>.Fail(400, $"查询失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 根据人脸ID获取人脸信息
        /// </summary>
        /// <param name="id">人脸唯一ID（GUID）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回人脸基础信息，不包含图片Base64</returns>
        [HttpGet("getfaceinfo/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<FaceInfoResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFaceInfo([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _queryService.GetByIdAsync(id, cancellationToken);
                if (result == null)
                    return Ok(ApiResponse<FaceInfoResponse>.Fail(400, "未找到该人脸信息"));

                return Ok(ApiResponse<FaceInfoResponse>.Ok(result, "查询成功"));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<FaceInfoResponse>.Fail(400, $"查询失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 根据人脸ID删除人脸信息
        /// </summary>
        /// <param name="id">人脸唯一ID（GUID）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回删除结果提示</returns>
        [HttpDelete("deletefaceinfo/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
        public async Task<IActionResult> DeleteFaceInfo([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _deletionService.DeleteAsync(id, cancellationToken);
                if (!deleted)
                    return Ok(ApiResponse<string>.Fail(400, "未找到该人脸信息"));

                return Ok(ApiResponse<string>.Ok("删除成功", "删除成功"));
            }
            catch (ArgumentException ex)
            {
                return Ok(ApiResponse<string>.Fail(400, ex.Message));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<string>.Fail(400, $"删除失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 人脸对比（返回相似度与裁剪后的人脸Base64）
        /// </summary>
        [HttpPost("compare")]
        [ProducesResponseType(typeof(ApiResponse<FaceCompareResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Compare([FromBody] FaceCompareRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<FaceCompareResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用验证服务进行人脸对比
                var result = await _verificationService.CompareAsync(request, cancellationToken);
                // 返回对比结果
                return Ok(ApiResponse<FaceCompareResponse>.Ok(result, "对比完成"));
            }
            catch (ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<FaceCompareResponse>.Fail(400, ex.Message));
            }
            catch (Exception ex)
            {
                // 处理其他错误
                return Ok(ApiResponse<FaceCompareResponse>.Fail(400, $"对比失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 人脸合规检测（是否人脸、是否合规）
        /// </summary>
        [HttpPost("check")]
        [ProducesResponseType(typeof(ApiResponse<FaceCheckResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Check([FromBody] FaceCheckRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<FaceCheckResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用验证服务进行人脸合规检测
                var result = await _verificationService.CheckAsync(request, cancellationToken);
                // 返回检测结果
                return Ok(ApiResponse<FaceCheckResponse>.Ok(result, "检测完成"));
            }
            catch (ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<FaceCheckResponse>.Fail(400, ex.Message));
            }
            catch (Exception ex)
            {
                // 处理其他错误
                return Ok(ApiResponse<FaceCheckResponse>.Fail(400, $"检测失败：{ex.Message}"));
            }
        }

        /// <summary>
        /// 电脑人脸识别
        /// </summary>
        /// <param name="request">电脑人脸识别请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>电脑人脸识别响应</returns>
        [HttpPost("computer/recognize")]
        [ProducesResponseType(typeof(ApiResponse<ComputerFaceRecognitionResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> ComputerRecognize([FromBody] ComputerFaceRecognitionRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<ComputerFaceRecognitionResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用验证服务进行电脑人脸识别
                var result = await _verificationService.ComputerRecognizeAsync(request, cancellationToken);
                // 返回识别结果
                return Ok(ApiResponse<ComputerFaceRecognitionResponse>.Ok(result, "识别完成"));
            }
            catch (ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<ComputerFaceRecognitionResponse>.Fail(400, ex.Message));
            }
            catch (Exception ex)
            {
                // 处理其他错误
                return Ok(ApiResponse<ComputerFaceRecognitionResponse>.Fail(400, $"识别失败：{ex.Message}"));
            }
        }
    }
}
