using SkiaSharp;

namespace FaceRecoTrackService.Core.Algorithms
{
    public sealed class ObjectDetection
    {
        public SKRectI BoundingBox { get; set; }
        public float Confidence { get; set; }
    }
}
