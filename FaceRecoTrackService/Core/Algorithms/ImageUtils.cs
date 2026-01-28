using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;
using Serilog;

namespace FaceRecoTrackService.Core.Algorithms
{
    /// <summary>
    /// 图像通用处理工具
    /// </summary>
    public static class ImageUtils
    {
        /// <summary>
        /// 加载图像
        /// </summary>
        public static SKImage LoadImage(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("图像文件不存在", path);

            using var stream = File.OpenRead(path);
            var image = SKImage.FromEncodedData(stream);
            if (image == null)
                throw new InvalidOperationException($"图像解码失败: {path}");
            return image;
        }

        /// <summary>
        /// 从字节数组加载图像
        /// </summary>
        public static SKImage LoadImage(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("图像数据为空", nameof(imageBytes));

            using var data = SKData.CreateCopy(imageBytes);
            var image = SKImage.FromEncodedData(data);
            if (image == null)
                throw new InvalidOperationException("图像解码失败：无法生成图像对象");
            return image;
        }

        /// <summary>
        /// 保存图像到指定路径
        /// </summary>
        public static void SaveImage(SKImage image, string savePath)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            try
            {
                string dir = Path.GetDirectoryName(savePath) ?? "";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                using var outputStream = File.OpenWrite(savePath);
                image.Encode(SKEncodedImageFormat.Jpeg, 95).SaveTo(outputStream);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存图像失败");
            }
        }

        /// <summary>
        /// 保存图像字节到指定路径
        /// </summary>
        public static void SaveImage(byte[] imageBytes, string savePath)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                throw new ArgumentException("图像数据为空", nameof(imageBytes));

            try
            {
                string dir = Path.GetDirectoryName(savePath) ?? "";
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllBytes(savePath, imageBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存图像字节失败");
            }
        }

        /// <summary>
        /// 绘制检测框并返回结果图像
        /// </summary>
        public static SKImage DrawDetectionResults(SKImage originalImage, List<ObjectDetection> results)
        {
            using var surface = SKSurface.Create(new SKImageInfo(originalImage.Width, originalImage.Height));
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(originalImage, 0, 0);

            using var paint = new SKPaint
            {
                Color = SKColors.Red,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2
            };

            foreach (var result in results)
            {
                canvas.DrawRect(result.BoundingBox, paint);
            }

            return surface.Snapshot();
        }
    }
}
