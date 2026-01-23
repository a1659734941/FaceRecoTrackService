using System;

namespace FaceRecoTrackService.Utils
{
    public static class Base64Helper
    {
        public static byte[] DecodeImage(string base64Image)
        {
            if (string.IsNullOrWhiteSpace(base64Image))
                throw new ArgumentException("base64Image不能为空", nameof(base64Image));

            var trimmed = base64Image.Trim();
            var commaIndex = trimmed.IndexOf(',');
            if (trimmed.StartsWith("data:image", StringComparison.OrdinalIgnoreCase) && commaIndex > 0)
                trimmed = trimmed[(commaIndex + 1)..];

            try
            {
                return Convert.FromBase64String(trimmed);
            }
            catch (FormatException)
            {
                throw new ArgumentException("base64Image格式不正确");
            }
        }
    }
}
