using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Core.Models;
using FaceRecoTrackService.Infrastructure.Repositories;

namespace FaceRecoTrackService.Services
{
    public class CameraService
    {
        private readonly PgFaceCameraRepository _faceRepo;
        private readonly PgRecordCameraRepository _recordRepo;
        private readonly PgCameraMappingRepository _mappingRepo;

        public CameraService(
            PgFaceCameraRepository faceRepo,
            PgRecordCameraRepository recordRepo,
            PgCameraMappingRepository mappingRepo)
        {
            _faceRepo = faceRepo;
            _recordRepo = recordRepo;
            _mappingRepo = mappingRepo;
        }

        public async Task<FaceCameraResponse> AddFaceCameraAsync(AddFaceCameraRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CameraIp))
                throw new ArgumentException("摄像头IP不能为空");
            var entity = await _faceRepo.AddAsync(request.CameraIp.Trim(), request.Description?.Trim(), cancellationToken);
            return ToFaceCameraResponse(entity);
        }

        public async Task<RecordCameraResponse> AddRecordCameraAsync(AddRecordCameraRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.CameraIp))
                throw new ArgumentException("摄像头IP不能为空");
            if (string.IsNullOrWhiteSpace(request.LocationName))
                throw new ArgumentException("位置名称不能为空");
            var entity = await _recordRepo.AddAsync(request.CameraIp.Trim(), request.LocationName.Trim(), cancellationToken);
            return ToRecordCameraResponse(entity);
        }

        public async Task<List<FaceCameraResponse>> ListFaceCamerasAsync(CancellationToken cancellationToken)
        {
            var list = await _faceRepo.ListAllAsync(cancellationToken);
            var result = new List<FaceCameraResponse>();
            foreach (var e in list)
                result.Add(ToFaceCameraResponse(e));
            return result;
        }

        public async Task<List<RecordCameraResponse>> ListRecordCamerasAsync(CancellationToken cancellationToken)
        {
            var list = await _recordRepo.ListAllAsync(cancellationToken);
            var result = new List<RecordCameraResponse>();
            foreach (var e in list)
                result.Add(ToRecordCameraResponse(e));
            return result;
        }

        /// <summary>绑定人脸与录像摄像头（用 id），允许一个人脸摄像头绑定多个录像摄像头，但一个录像摄像头只能绑定一个人脸摄像头。</summary>
        public async Task<long> BindCamerasAsync(BindCamerasRequest request, CancellationToken cancellationToken)
        {
            var face = await _faceRepo.GetByIdAsync(request.FaceCameraId, cancellationToken);
            if (face == null)
                throw new ArgumentException("人脸摄像头不存在");
            var record = await _recordRepo.GetByIdAsync(request.RecordCameraId, cancellationToken);
            if (record == null)
                throw new ArgumentException("录像摄像头不存在");

            

            var id = await _mappingRepo.BindAsync(request.FaceCameraId, request.RecordCameraId, cancellationToken);
            return id;
        }

        public async Task<List<BoundCameraItem>> ListBindingsAsync(CancellationToken cancellationToken)
        {
            var rows = await _mappingRepo.ListBindingsAsync(cancellationToken);
            var list = new List<BoundCameraItem>();
            foreach (var r in rows)
                list.Add(new BoundCameraItem
                {
                    MappingId = r.Id,
                    FaceCameraId = r.FaceCameraId,
                    FaceCameraIp = r.FaceCameraIp,
                    RecordCameraId = r.RecordCameraId,
                    RecordCameraIp = r.RecordCameraIp,
                    LocationName = r.LocationName
                });
            return list;
        }

        /// <summary>修改已绑定：换绑（传 newRecordCameraId）或解绑（unbind=true）。</summary>
        public async Task<bool> UpdateBindingAsync(long mappingId, UpdateBindingRequest request, CancellationToken cancellationToken)
        {
            var binding = await _mappingRepo.GetBindingByMappingIdAsync(mappingId, cancellationToken);
            if (binding == null)
                throw new ArgumentException("绑定记录不存在");

            if (request.Unbind)
            {
                return await _mappingRepo.UnbindByMappingIdAsync(mappingId, cancellationToken);
            }

            if (request.NewRecordCameraId.HasValue)
            {
                var record = await _recordRepo.GetByIdAsync(request.NewRecordCameraId.Value, cancellationToken);
                if (record == null)
                    throw new ArgumentException("目标录像摄像头不存在");
                return await _mappingRepo.UpdateRecordCameraIdAsync(mappingId, request.NewRecordCameraId.Value, cancellationToken);
            }

            return false;
        }

        /// <summary>强行绑定：若该录像已绑定则先解除再绑，保留人脸摄像头的其他绑定关系。</summary>
        public async Task<long> ForceBindCamerasAsync(ForceBindCamerasRequest request, CancellationToken cancellationToken)
        {
            var face = await _faceRepo.GetByIdAsync(request.FaceCameraId, cancellationToken);
            if (face == null)
                throw new ArgumentException("人脸摄像头不存在");
            var record = await _recordRepo.GetByIdAsync(request.RecordCameraId, cancellationToken);
            if (record == null)
                throw new ArgumentException("录像摄像头不存在");

            

            var id = await _mappingRepo.BindAsync(request.FaceCameraId, request.RecordCameraId, cancellationToken);
            return id;
        }

        public async Task<bool> DeleteFaceCameraAsync(long? id, string? ip, CancellationToken cancellationToken)
        {
            if (id.HasValue)
                return await _faceRepo.DeleteByIdAsync(id.Value, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ip))
                return await _faceRepo.DeleteByIpAsync(ip.Trim(), cancellationToken);
            throw new ArgumentException("请提供 id 或 ip");
        }

        public async Task<bool> DeleteRecordCameraAsync(long? id, string? ip, CancellationToken cancellationToken)
        {
            if (id.HasValue)
                return await _recordRepo.DeleteByIdAsync(id.Value, cancellationToken);
            if (!string.IsNullOrWhiteSpace(ip))
                return await _recordRepo.DeleteByIpAsync(ip.Trim(), cancellationToken);
            throw new ArgumentException("请提供 id 或 ip");
        }

        public async Task<FaceCameraResponse?> UpdateFaceCameraAsync(long? id, string? ip, UpdateFaceCameraRequest request, CancellationToken cancellationToken)
        {
            FaceCamera? entity;
            if (id.HasValue)
                entity = await _faceRepo.GetByIdAsync(id.Value, cancellationToken);
            else if (!string.IsNullOrWhiteSpace(ip))
                entity = await _faceRepo.GetByIpAsync(ip!.Trim(), cancellationToken);
            else
                throw new ArgumentException("请提供 id 或 ip");

            if (entity == null) return null;

            await (id.HasValue
                ? _faceRepo.UpdateByIdAsync(id.Value, request.CameraIp?.Trim(), request.Description?.Trim(), cancellationToken)
                : _faceRepo.UpdateByIpAsync(ip!.Trim(), request.CameraIp?.Trim(), request.Description?.Trim(), cancellationToken));
            var updatedEntity = await _faceRepo.GetByIdAsync(entity.Id, cancellationToken);
            return updatedEntity != null ? ToFaceCameraResponse(updatedEntity) : ToFaceCameraResponse(entity);
        }

        public async Task<RecordCameraResponse?> UpdateRecordCameraAsync(long? id, string? ip, UpdateRecordCameraRequest request, CancellationToken cancellationToken)
        {
            RecordCamera? entity;
            if (id.HasValue)
                entity = await _recordRepo.GetByIdAsync(id.Value, cancellationToken);
            else if (!string.IsNullOrWhiteSpace(ip))
                entity = await _recordRepo.GetByIpAsync(ip!.Trim(), cancellationToken);
            else
                throw new ArgumentException("请提供 id 或 ip");

            if (entity == null) return null;

            await (id.HasValue
                ? _recordRepo.UpdateByIdAsync(id.Value, request.CameraIp?.Trim(), request.LocationName?.Trim(), cancellationToken)
                : _recordRepo.UpdateByIpAsync(ip!.Trim(), request.CameraIp?.Trim(), request.LocationName?.Trim(), cancellationToken));
            var updatedEntity = await _recordRepo.GetByIdAsync(entity.Id, cancellationToken);
            return updatedEntity != null ? ToRecordCameraResponse(updatedEntity) : ToRecordCameraResponse(entity);
        }

        private static FaceCameraResponse ToFaceCameraResponse(FaceCamera e)
        {
            return new FaceCameraResponse { Id = e.Id, CameraIp = e.CameraIp, Description = e.Description, CreatedAt = e.CreatedAt };
        }

        private static RecordCameraResponse ToRecordCameraResponse(RecordCamera e)
        {
            return new RecordCameraResponse { Id = e.Id, CameraIp = e.CameraIp, LocationName = e.LocationName, CreatedAt = e.CreatedAt };
        }
    }
}
