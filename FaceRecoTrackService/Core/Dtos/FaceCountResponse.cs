using System;
using System.Collections.Generic;

namespace FaceRecoTrackService.Core.Dtos
{
    public class FaceCountResponse
    {
        public long Count { get; set; }
        public List<FaceInfoResponse> Faces { get; set; } = new List<FaceInfoResponse>();
    }
}
