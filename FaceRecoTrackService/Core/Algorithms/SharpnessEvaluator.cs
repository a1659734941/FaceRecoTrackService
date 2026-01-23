using System;
using System.IO;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using SkiaSharp;
using FaceRecoTrackService.Core.Options;

namespace FaceRecoTrackService.Core.Algorithms
{
    /// <summary>
    /// 人脸清晰度评估工具
    /// </summary>
    public static class SharpnessEvaluator
    {
        /// <summary>
        /// 判断人脸是否清晰（基于拉普拉斯方差）
        /// </summary>
        public static bool IsSharp(SKImage faceImage, int width, int height, FaceRecognitionOptions config, out double actualSharpness)
        {
            actualSharpness = 0;
            try
            {
                using var stream = new MemoryStream();
                faceImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
                byte[] imageBytes = stream.ToArray();

                using var mat = new Mat();
                CvInvoke.Imdecode(imageBytes, ImreadModes.ColorBgr, mat);
                if (mat.IsEmpty) return false;

                using var gray = new Mat();
                CvInvoke.CvtColor(mat, gray, ColorConversion.Bgr2Gray);

                using var laplacian = new Mat();
                CvInvoke.Laplacian(gray, laplacian, DepthType.Cv64F);

                using var mean = new Mat();
                using var stdDev = new Mat();
                CvInvoke.MeanStdDev(laplacian, mean, stdDev);

                if (!stdDev.IsEmpty)
                {
                    double[] stdDevArray = new double[1];
                    Marshal.Copy(stdDev.DataPointer, stdDevArray, 0, 1);
                    actualSharpness = stdDevArray[0];
                }

                return actualSharpness > GetDynamicThreshold(width, height, config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"清晰度评估失败：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取动态清晰度阈值（基于人脸尺寸）
        /// </summary>
        public static double GetDynamicThreshold(int width, int height, FaceRecognitionOptions config)
        {
            double faceArea = width * height;
            double dynamicThreshold = config.BaseSharpnessThreshold - (faceArea * config.SizeThresholdCoefficient);
            return Math.Max(dynamicThreshold, 1.0);
        }

        public static bool IsSharp(SKImage faceImage, int width, int height, FaceRecognitionOptions config)
        {
            return IsSharp(faceImage, width, height, config, out _);
        }
    }
}
