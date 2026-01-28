using System;

namespace FaceRecoTrackService.Core.Models
{
    public class FaceVectorRecord
    {
        public Guid Id { get; set; }
        public string UserName { get; set; } = "";
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}
