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
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务添加人脸摄像头
                var result = await _cameraService.AddFaceCameraAsync(request, cancellationToken);
                // 返回添加成功的结果
                return Ok(ApiResponse<FaceCameraResponse>.Ok(result, "新增成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>新增录像摄像头</summary>
        [HttpPost("addRecord")]
        [ProducesResponseType(typeof(ApiResponse<RecordCameraResponse>), 200)]
        public async Task<IActionResult> AddRecordCamera([FromBody] AddRecordCameraRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<RecordCameraResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务添加录像摄像头
                var result = await _cameraService.AddRecordCameraAsync(request, cancellationToken);
                // 返回添加成功的结果
                return Ok(ApiResponse<RecordCameraResponse>.Ok(result, "新增成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
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

        /// <summary>绑定人脸与录像摄像头（用 id，一个人脸摄像头可以绑定多个录像摄像头）</summary>
        [HttpPost("addBind")]
        [ProducesResponseType(typeof(ApiResponse<long>), 200)]
        public async Task<IActionResult> BindCameras([FromBody] BindCamerasRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<long>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务绑定摄像头
                var mappingId = await _cameraService.BindCamerasAsync(request, cancellationToken);
                // 返回绑定成功的结果
                return Ok(ApiResponse<long>.Ok(mappingId, "绑定成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<long>.Fail(400, ex.Message));
            }
            catch (System.InvalidOperationException ex)
            {
                // 处理冲突错误（如重复绑定）
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
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<bool>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务更新绑定关系
                var ok = await _cameraService.UpdateBindingAsync(mappingId, request, cancellationToken);
                // 返回更新结果
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "修改成功" : "未变更"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
            catch (System.InvalidOperationException ex)
            {
                // 处理冲突错误
                return Ok(ApiResponse<bool>.Fail(409, ex.Message));
            }
        }

        /// <summary>强行绑定：若该人脸或录像已绑定则先解除再绑</summary>
        [HttpPost("addBindForce")]
        [ProducesResponseType(typeof(ApiResponse<long>), 200)]
        public async Task<IActionResult> ForceBindCameras([FromBody] ForceBindCamerasRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<long>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务强行绑定摄像头
                var mappingId = await _cameraService.ForceBindCamerasAsync(request, cancellationToken);
                // 返回绑定成功的结果
                return Ok(ApiResponse<long>.Ok(mappingId, "绑定成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
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
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务更新人脸摄像头信息
                var result = await _cameraService.UpdateFaceCameraAsync(id, ip, request, cancellationToken);
                // 检查是否找到摄像头
                if (result == null)
                    return Ok(ApiResponse<FaceCameraResponse>.Fail(404, "未找到该人脸摄像头"));
                // 返回更新成功的结果
                return Ok(ApiResponse<FaceCameraResponse>.Ok(result, "修改成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<FaceCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>修改录像摄像头（query: id 或 ip 二选一）</summary>
        [HttpPut("changeRecord")]
        [ProducesResponseType(typeof(ApiResponse<RecordCameraResponse>), 200)]
        public async Task<IActionResult> UpdateRecordCamera([FromQuery] long? id, [FromQuery] string? ip, [FromBody] UpdateRecordCameraRequest request, CancellationToken cancellationToken)
        {
            // 验证请求参数是否合法
            if (!ModelState.IsValid)
            {
                // 收集所有验证错误信息
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                // 返回400错误和错误信息
                return Ok(ApiResponse<RecordCameraResponse>.Fail(400, string.Join(", ", errors)));
            }
            
            try
            {
                // 调用摄像头服务更新录像摄像头信息
                var result = await _cameraService.UpdateRecordCameraAsync(id, ip, request, cancellationToken);
                // 检查是否找到摄像头
                if (result == null)
                    return Ok(ApiResponse<RecordCameraResponse>.Fail(404, "未找到该录像摄像头"));
                // 返回更新成功的结果
                return Ok(ApiResponse<RecordCameraResponse>.Ok(result, "修改成功"));
            }
            catch (System.ArgumentException ex)
            {
                // 处理参数错误
                return Ok(ApiResponse<RecordCameraResponse>.Fail(400, ex.Message));
            }
        }

        /// <summary>解绑指定的一对人脸和录像摄像头</summary>
        [HttpDelete("unbindPair")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> UnbindSpecificPair([FromQuery] long faceCameraId, [FromQuery] long recordCameraId, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.UnbindSpecificPairAsync(faceCameraId, recordCameraId, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "解绑成功" : "未找到绑定关系"));
            }
            catch (System.ArgumentException ex)
            {

                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
        }

        /// <summary>解绑一个人脸摄像头绑定的所有录像摄像头</summary>
        [HttpDelete("unbindAllFromFace/{faceCameraId:long}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> UnbindAllFromFaceCamera(long faceCameraId, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.UnbindAllFromFaceCameraAsync(faceCameraId, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "解绑成功" : "未找到绑定关系"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
        }

        /// <summary>解绑一个录像摄像头绑定的所有人脸摄像头</summary>
        [HttpDelete("unbindAllFromRecord/{recordCameraId:long}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        public async Task<IActionResult> UnbindAllFromRecordCamera(long recordCameraId, CancellationToken cancellationToken)
        {
            try
            {
                var ok = await _cameraService.UnbindAllFromRecordCameraAsync(recordCameraId, cancellationToken);
                return Ok(ApiResponse<bool>.Ok(ok, ok ? "解绑成功" : "未找到绑定关系"));
            }
            catch (System.ArgumentException ex)
            {
                return Ok(ApiResponse<bool>.Fail(400, ex.Message));
            }
        }
    }
}
