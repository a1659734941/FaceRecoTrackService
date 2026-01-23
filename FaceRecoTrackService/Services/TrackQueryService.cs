using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Infrastructure.Repositories;

namespace FaceRecoTrackService.Services
{
    public class TrackQueryService
    {
        private readonly PgTrackRepository _trackRepository;

        public TrackQueryService(PgTrackRepository trackRepository)
        {
            _trackRepository = trackRepository;
        }

        public async Task<TrackQueryResult> GetTracksAsync(
            Guid personId,
            int pageNum,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var (items, total) = await _trackRepository.GetTracksByPersonAsync(personId, pageNum, pageSize, cancellationToken);

            var list = items.Select(item => new TrackQueryItem
            {
                Id = item.PersonId.ToString(),
                SnapTime = item.SnapTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                SnapLocation = item.SnapLocation,
                SnapCamera = item.SnapCameraIp,
                RecordCamera = item.RecordCameraIp,
                RecordStartTime = item.RecordStartTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                RecordEndTime = (item.RecordEndTimeUtc ?? DateTime.UtcNow).ToLocalTime()
                    .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            }).ToList();

            return new TrackQueryResult
            {
                List = list,
                Pagesize = pageSize,
                Pagenum = pageNum,
                Total = total
            };
        }
    }
}
