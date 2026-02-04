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
        [ProducesResponseType(typeof(ApiResponse<TrackQueryResult>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTrack(
            [FromRoute] Guid id,
            [FromQuery] int pageNum = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            if (pageNum <= 0 || pageSize <= 0)
                return Ok(ApiResponse<TrackQueryResult>.Fail(400, "pageNum/pageSize必须为正整数"));

            try
            {
                var result = await _trackQueryService.GetTracksAsync(id, pageNum, pageSize, cancellationToken);
                return Ok(ApiResponse<TrackQueryResult>.Ok(result, "查询guid轨迹成功"));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse<TrackQueryResult>.Fail(400, $"查询失败：{ex.Message}"));
            }
        }
    }
}
