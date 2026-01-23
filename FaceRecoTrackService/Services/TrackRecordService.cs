using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.Repositories;

namespace FaceRecoTrackService.Services
{
    public class TrackRecordService
    {
        private readonly PgTrackRepository _trackRepository;
        private readonly PgCameraMappingRepository _cameraMappingRepository;
        private readonly CameraRoomConfig _roomConfig;

        public TrackRecordService(
            PgTrackRepository trackRepository,
            PgCameraMappingRepository cameraMappingRepository,
            CameraRoomConfig roomConfig)
        {
            _trackRepository = trackRepository;
            _cameraMappingRepository = cameraMappingRepository;
            _roomConfig = roomConfig;
        }

        public async Task HandleTrackAsync(
            Guid personId,
            string snapCameraIp,
            DateTime snapTimeUtc,
            string? fallbackLocation,
            CancellationToken cancellationToken)
        {
            var latest = await _trackRepository.GetLatestTrackAsync(personId, cancellationToken);
            if (latest != null && string.Equals(latest.SnapCameraIp, snapCameraIp, StringComparison.OrdinalIgnoreCase))
            {
                // 同一房间（摄像头）只保留首次抓拍
                return;
            }

            if (latest != null)
            {
                await _trackRepository.UpdateTrackEndTimeAsync(latest.Id, snapTimeUtc, cancellationToken);
            }

            var mapping = await _cameraMappingRepository.GetMappingAsync(snapCameraIp, cancellationToken);
            var recordCameraIp = mapping?.RecordCameraIp ?? snapCameraIp;
            var location = mapping?.RoomName ?? "";
            if (string.IsNullOrWhiteSpace(location))
            {
                if (_roomConfig.RoomMapping.TryGetValue(snapCameraIp, out var mappedRoom))
                    location = mappedRoom;
                else
                    location = fallbackLocation ?? "";
            }

            var record = new TrackRecord
            {
                PersonId = personId,
                SnapTimeUtc = snapTimeUtc,
                SnapLocation = location,
                SnapCameraIp = snapCameraIp,
                RecordCameraIp = recordCameraIp,
                RecordStartTimeUtc = snapTimeUtc,
                RecordEndTimeUtc = null,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _trackRepository.InsertTrackAsync(record, cancellationToken);
        }
    }
}
