using System.Collections.Generic;

namespace FaceRecoTrackService.Core.Options
{
    public class CameraRoomConfig
    {
        public Dictionary<string, string> RoomMapping { get; set; } = new();
    }
}
