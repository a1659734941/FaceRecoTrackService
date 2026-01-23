using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Services;
using FaceRecoTrackService.Utils;
using Microsoft.AspNetCore.Mvc;

namespace FaceRecoTrackService.API.Controllers
{
    [ApiController]
    [Route("api/track")]
    public class TrackController : ControllerBase
    {
        private readonly TrackQueryService _trackQueryService;

        public TrackController(TrackQueryService trackQueryService)
        {
            _trackQueryService = trackQueryService;
        }

        [HttpGet("{id:guid}")]
        public async Task<ApiResponse<TrackQueryResult>> GetTrack(
            [FromRoute] Guid id,
            [FromQuery] int pageNum = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (pageNum <= 0 || pageSize <= 0)
                return ApiResponse<TrackQueryResult>.Fail(ErrorCodes.BadRequest, "pageNum/pageSize必须为正整数");

            try
            {
                var result = await _trackQueryService.GetTracksAsync(id, pageNum, pageSize, cancellationToken);
                return ApiResponse<TrackQueryResult>.Ok(result, "查询guid轨迹成功");
            }
            catch (Exception ex)
            {
                return ApiResponse<TrackQueryResult>.Fail(ErrorCodes.InternalError, $"查询失败：{ex.Message}");
            }
        }
    }
}
