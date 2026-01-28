using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using FaceRecoTrackService.Core.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceRecoTrackService.Core.Algorithms
{
    /// <summary>
    /// 人脸特征提取与比对服务
    /// </summary>
    public class FaceFeatureService : IDisposable
    {
        private const int InputChannels = 3;

        private readonly InferenceSession _onnxSession;
        private readonly FaceRecognitionOptions _config;
        private readonly int _inputWidth;
        private readonly int _inputHeight;
        private readonly string _inputName;
        private bool _disposed;

        public FaceFeatureService(FaceRecognitionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.FaceNetModelPath))
                throw new ArgumentNullException(nameof(options.FaceNetModelPath), "模型路径不能为空");

            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
            if (options.OnnxIntraOpNumThreads > 0)
                sessionOptions.IntraOpNumThreads = options.OnnxIntraOpNumThreads;
            if (options.OnnxInterOpNumThreads > 0)
                sessionOptions.InterOpNumThreads = options.OnnxInterOpNumThreads;

            _onnxSession = new InferenceSession(options.FaceNetModelPath, sessionOptions);
            _config = options;
            _inputWidth = Math.Max(1, options.FeatureInputWidth);
            _inputHeight = Math.Max(1, options.FeatureInputHeight);
            _inputName = _onnxSession.InputMetadata.Keys.FirstOrDefault() ?? "input";
        }

        /// <summary>
        /// 提取人脸图像的特征向量
        /// </summary>
        public float[] ExtractFeatures(string imagePath)
        {
            using var image = LoadImageAsRgb(imagePath);
            if (image.IsEmpty)
                throw new InvalidOperationException($"图片加载失败：{imagePath}");

            float[] inputData = PreprocessImage(image);
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, InputChannels, _inputHeight, _inputWidth });

            using var results = _onnxSession.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            });

            return results.First().AsTensor<float>().ToArray();
        }

        /// <summary>
        /// 计算两个特征向量的余弦相似度
        /// </summary>
        public float CalculateSimilarity(float[] feat1, float[] feat2)
        {
            if (feat1 == null || feat2 == null || feat1.Length != feat2.Length)
                throw new ArgumentException("特征向量无效");

            var norm1 = NormalizeVector(feat1);
            var norm2 = NormalizeVector(feat2);

            float dotProduct = 0;
            for (int i = 0; i < norm1.Length; i++)
                dotProduct += norm1[i] * norm2[i];

            return dotProduct;
        }

        /// <summary>
        /// 从流提取人脸特征（避免临时文件）
        /// </summary>
        public float[] ExtractFeaturesFromStream(Stream imageStream)
        {
            byte[] imageBytes = new byte[imageStream.Length];
            imageStream.Read(imageBytes, 0, imageBytes.Length);

            using var mat = new Mat();
            CvInvoke.Imdecode(imageBytes, ImreadModes.ColorBgr, mat);
            if (mat.IsEmpty)
                throw new InvalidOperationException("从流加载图像失败");

            using var rgbImage = new Mat();
            CvInvoke.CvtColor(mat, rgbImage, ColorConversion.Bgr2Rgb);

            float[] inputData = PreprocessImage(rgbImage);
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, InputChannels, _inputHeight, _inputWidth });

            using var results = _onnxSession.Run(new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
            });

            return results.First().AsTensor<float>().ToArray();
        }

        private Mat LoadImageAsRgb(string imagePath)
        {
            var image = CvInvoke.Imread(imagePath, ImreadModes.ColorBgr);
            if (image.IsEmpty) return image;

            var rgbImage = new Mat();
            switch (image.NumberOfChannels)
            {
                case 1:
                    CvInvoke.CvtColor(image, rgbImage, ColorConversion.Gray2Rgb);
                    break;
                case 4:
                    CvInvoke.CvtColor(image, rgbImage, ColorConversion.Bgra2Rgb);
                    break;
                default:
                    CvInvoke.CvtColor(image, rgbImage, ColorConversion.Bgr2Rgb);
                    break;
            }
            image.Dispose();
            return rgbImage;
        }

        private float[] PreprocessImage(Mat rgbImage)
        {
            using var resized = new Mat();
            CvInvoke.Resize(
                rgbImage,
                resized,
                new System.Drawing.Size(_inputWidth, _inputHeight),
                interpolation: Inter.Linear);

            if (_config.EnableHistogramEqualization)
            {
                using var gray = new Mat();
                CvInvoke.CvtColor(resized, gray, ColorConversion.Rgb2Gray);
                using var equalized = new Mat();
                CvInvoke.EqualizeHist(gray, equalized);
                CvInvoke.CvtColor(equalized, resized, ColorConversion.Gray2Rgb);
            }

            using var continuous = resized.IsContinuous ? resized : resized.Clone();

            int totalBytes = _inputWidth * _inputHeight * InputChannels;
            byte[] bytes = new byte[totalBytes];
            Marshal.Copy(continuous.DataPointer, bytes, 0, totalBytes);

            return NormalizeToTensor(bytes);
        }

        private float[] NormalizeToTensor(byte[] imageBytes)
        {
            int pixelCount = _inputWidth * _inputHeight;
            var normalized = new float[InputChannels * pixelCount];

            for (int i = 0; i < pixelCount; i++)
            {
                normalized[i] = (imageBytes[i * 3] / 255f - 0.5f) / 0.5f;
                normalized[pixelCount + i] = (imageBytes[i * 3 + 1] / 255f - 0.5f) / 0.5f;
                normalized[pixelCount * 2 + i] = (imageBytes[i * 3 + 2] / 255f - 0.5f) / 0.5f;
            }

            return normalized;
        }

        public static float[] ResizeVector(float[] vector, int targetSize)
        {
            if (vector == null) throw new ArgumentNullException(nameof(vector));
            if (targetSize <= 0 || vector.Length == targetSize) return vector;

            var resized = new float[targetSize];
            int copyCount = Math.Min(vector.Length, targetSize);
            Array.Copy(vector, resized, copyCount);
            return resized;
        }

        private float[] NormalizeVector(float[] vector)
        {
            float norm = (float)Math.Sqrt(vector.Sum(v => v * v));
            return norm < 1e-6 ? vector : vector.Select(v => v / norm).ToArray();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
                _onnxSession?.Dispose();
            _disposed = true;
        }

        ~FaceFeatureService() => Dispose(false);
    }
}
