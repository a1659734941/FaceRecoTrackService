using System;

namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceInfoResponse
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = "";
        public string Ip { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsTest { get; set; }
        public string? ImageBase64 { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
