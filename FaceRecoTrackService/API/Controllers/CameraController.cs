using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Services;
using FaceRecoTrackService.Utils;
using Microsoft.AspNetCore.Mvc;

namespace FaceRecoTrackService.API.Controllers
{
    /// <summary>
    /// 摄像头管理：人脸摄像头、录像摄像头、绑定关系
    /// </summary>
    [ApiController]
    [Route("api/camera")]
    public class CameraController : ControllerBase
    {
        private readonly CameraService _cameraService;

        public CameraController(CameraService cameraService)
        {
            _cameraService = cameraService;
        }

        /// <summary>新增人脸摄像头</summary>
        [HttpPost("addFace")]
        [ProducesResponseType(typeof(ApiResponse<FaceCameraResponse>), 200)]
        public async Task<IActionResult> AddFaceCamera([FromBody] AddFaceCameraRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cameraService.AddFaceCameraAsync(request, cancellationToken);
                return Ok(ApiResponse<FaceCameraResponse>.Ok(result, "新增成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>新增录像摄像头</summary>
        [HttpPost("addRecord")]
        [ProducesResponseType(typeof(ApiResponse<RecordCameraResponse>), 200)]
        public async Task<IActionResult> AddRecordCamera([FromBody] AddRecordCameraRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cameraService.AddRecordCameraAsync(request, cancellationToken);
                return Ok(ApiResponse<RecordCameraResponse>.Ok(result, "新增成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<RecordCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>查询人脸摄像头（返回所有人脸摄像头的 id、ip、介绍）</summary>
        [HttpGet("queryFace")]
        [ProducesResponseType(typeof(ApiResponse<List<FaceCameraResponse>>), 200)]
        public async Task<IActionResult> ListFaceCameras(CancellationToken cancellationToken)
        {
            var list = await _cameraService.ListFaceCamerasAsync(cancellationToken);
            return Ok(ApiResponse<List<FaceCameraResponse>>.Ok(list, "查询成功"));
        }

        /// <summary>查询录像摄像头（返回所有录像摄像头的 id、ip、名称）</summary>
        [HttpGet("queryRecord")]
        [ProducesResponseType(typeof(ApiResponse<List<RecordCameraResponse>>), 200)]
        public async Task<IActionResult> ListRecordCameras(CancellationToken cancellationToken)
        {
            var list = await _cameraService.ListRecordCamerasAsync(cancellationToken);
            return Ok(ApiResponse<List<RecordCameraResponse>>.Ok(list, "查询成功"));
        }

        /// <summary>绑定人脸与录像摄像头（用 id，不可重复绑定同一人脸或同一录像）</summary>
        [HttpPost("addBind")]
        [ProducesResponseType(typeof(ApiResponse<long>), 200)]
        public async Task<IActionResult> BindCameras([FromBody] BindCamerasRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var mappingId = await _cameraService.BindCamerasAsync(request, cancellationToken);
                return Ok(ApiResponse<long>.Ok(mappingId, "绑定成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<long>.Fail(400, ex.Message));
            }
            catch (System.InvalidOperationException ex)
            {
                return Ok(ApiResponse<long>.Fail(409, ex.Message));
            }
        }

        /// <summary>查询已绑定的摄像头（返回两个摄像头 IP 及录像位置名称）</summary>
        [HttpGet("queryBindings")]
        [ProducesResponseType(typeof(ApiResponse<List<BoundCameraItem>>), 200)]
        public async Task<IActionResult> ListBindings(CancellationToken cancellationToken)
        {
            var list = await _cameraService.ListBindingsAsync(cancellationToken);
            return Ok(ApiResponse<List<BoundCameraItem>>.Ok(list, "查询成功"));
        }

        /// <summary>修改已绑定：换绑（传 newRecordCameraId）或解绑（unbind=true）</summary>
        [HttpPut("changeBinding/{mappingId:long}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> UpdateBinding(long mappingId, [FromBody] UpdateBindingRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.UpdateBindingAsync(mappingId, request, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "修改成功" : "未变更"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
            catch (System.InvalidOperationException ex)
            {
                return Ok(ApiResponse<bool>.Fail(409, ex.Message));
            }
        }

        /// <summary>强行绑定：若该人脸或录像已绑定则先解除再绑</summary>
        [HttpPost("addBindForce")]
        [ProducesResponseType(typeof(ApiResponse<long>), 200)]
        public async Task<IActionResult> ForceBindCameras([FromBody] ForceBindCamerasRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var mappingId = await _cameraService.ForceBindCamerasAsync(request, cancellationToken);
                return Ok(ApiResponse<long>.Ok(mappingId, "绑定成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<long>.Fail(400, ex.Message));
            }
        }

        /// <summary>删除人脸摄像头（query: id 或 ip 二选一）</summary>
        [HttpDelete("deleteFace")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> DeleteFaceCamera([FromQuery] long? id, [FromQuery] string? ip, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.DeleteFaceCameraAsync(id, ip, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "删除成功" : "未找到记录"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
        }

        /// <summary>删除录像摄像头（query: id 或 ip 二选一）</summary>
        [HttpDelete("deleteRecord")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> DeleteRecordCamera([FromQuery] long? id, [FromQuery] string? ip, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.DeleteRecordCameraAsync(id, ip, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "删除成功" : "未找到记录"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
        }

        /// <summary>修改人脸摄像头（query: id 或 ip 二选一）</summary>
        [HttpPut("changeFace")]
        [ProducesResponseType(typeof(ApiResponse<FaceCameraResponse>), 200)]
        public async Task<IActionResult> UpdateFaceCamera([FromQuery] long? id, [FromQuery] string? ip, [FromBody] UpdateFaceCameraRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cameraService.UpdateFaceCameraAsync(id, ip, request, cancellationToken);
                if (result == null)
                    return Ok(ApiResponse<FaceCameraResponse>.Fail(404, "未找到该人脸摄像头"));
                return Ok(ApiResponse<FaceCameraResponse>.Ok(result, "修改成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>修改录像摄像头（query: id 或 ip 二选一）</summary>
        [HttpPut("changeRecord")]
        [ProducesResponseType(typeof(ApiResponse<RecordCameraResponse>), 200)]
        public async Task<IActionResult> UpdateRecordCamera([FromQuery] long? id, [FromQuery] string? ip, [FromBody] UpdateRecordCameraRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _cameraService.UpdateRecordCameraAsync(id, ip, request, cancellationToken);
                if (result == null)
                    return Ok(ApiResponse<RecordCameraResponse>.Fail(404, "未找到该录像摄像头"));
                return Ok(ApiResponse<RecordCameraResponse>.Ok(result, "修改成功"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<RecordCameraResponse>.Fail(400, ex.Message));
            }
        }
    }
}
