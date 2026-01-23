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

        /// <summary>
        /// 初始化人脸接口控制器
        /// </summary>
        /// <param name="registrationService">人脸注册服务</param>
        /// <param name="deletionService">人脸删除服务</param>
        /// <param name="queryService">人脸查询服务</param>
        public FaceController(
            FaceRegistrationService registrationService,
            FaceDeletionService deletionService,
            FaceQueryService queryService)
        {
            _registrationService = registrationService;
            _deletionService = deletionService;
            _queryService = queryService;
        }

        /// <summary>
        /// 注册人脸信息（写入PG与Qdrant）
        /// </summary>
        /// <param name="request">人脸注册请求，包含base64图片与用户名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回注册成功的人脸唯一ID</returns>
        [HttpPost("register")]
        public async Task<ApiResponse<FaceRegisterResponse>> Register([FromBody] FaceRegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _registrationService.RegisterAsync(request, cancellationToken);
                return ApiResponse<FaceRegisterResponse>.Ok(result, "注册成功");
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<FaceRegisterResponse>.Fail(ErrorCodes.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse<FaceRegisterResponse>.Fail(ErrorCodes.InternalError, $"注册失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取PG库已注册人脸数量
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回PG库中人脸数量</returns>
        [HttpGet("count")]
        public async Task<ApiResponse<long>> Count(CancellationToken cancellationToken)
        {
            try
            {
                var count = await _queryService.GetCountAsync(cancellationToken);
                return ApiResponse<long>.Ok(count, "查询成功");
            }
            catch (Exception ex)
            {
                return ApiResponse<long>.Fail(ErrorCodes.InternalError, $"查询失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 获取Qdrant库已注册人脸数量
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回Qdrant集合中的向量数量</returns>
        [HttpGet("qdrant/count")]
        public async Task<ApiResponse<long>> QdrantCount(CancellationToken cancellationToken)
        {
            try
            {
                var count = await _queryService.GetQdrantCountAsync(cancellationToken);
                return ApiResponse<long>.Ok(count, "查询成功");
            }
            catch (Exception ex)
            {
                return ApiResponse<long>.Fail(ErrorCodes.InternalError, $"查询失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据人脸ID获取人脸信息
        /// </summary>
        /// <param name="id">人脸唯一ID（GUID）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回人脸基础信息，不包含图片Base64</returns>
        [HttpGet("getfaceinfo/{id:guid}")]
        public async Task<ApiResponse<FaceInfoResponse>> GetFaceInfo([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _queryService.GetByIdAsync(id, cancellationToken);
                if (result == null)
                    return ApiResponse<FaceInfoResponse>.Fail(ErrorCodes.NotFound, "未找到该人脸信息");

                return ApiResponse<FaceInfoResponse>.Ok(result, "查询成功");
            }
            catch (Exception ex)
            {
                return ApiResponse<FaceInfoResponse>.Fail(ErrorCodes.InternalError, $"查询失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 根据人脸ID删除人脸信息
        /// </summary>
        /// <param name="id">人脸唯一ID（GUID）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>返回删除结果提示</returns>
        [HttpDelete("deletefaceinfo/{id:guid}")]
        public async Task<ApiResponse<string>> DeleteFaceInfo([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _deletionService.DeleteAsync(id, cancellationToken);
                if (!deleted)
                    return ApiResponse<string>.Fail(ErrorCodes.NotFound, "未找到该人脸信息");

                return ApiResponse<string>.Ok("删除成功", "删除成功");
            }
            catch (ArgumentException ex)
            {
                return ApiResponse<string>.Fail(ErrorCodes.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                return ApiResponse<string>.Fail(ErrorCodes.InternalError, $"删除失败：{ex.Message}");
            }
        }
    }
}
