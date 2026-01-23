using System;

namespace FaceRecoTrackService.Core.Models
{
    public class TrackRecord
    {
        public long Id { get; set; }
        public Guid PersonId { get; set; }
        public DateTime SnapTimeUtc { get; set; }
        public string SnapLocation { get; set; } = "";
        public string SnapCameraIp { get; set; } = "";
        public string RecordCameraIp { get; set; } = "";
        public DateTime RecordStartTimeUtc { get; set; }
        public DateTime? RecordEndTimeUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
