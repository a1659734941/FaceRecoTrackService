using System;
using System.Collections.Generic;
using System.IO;
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

        private static readonly ThreadLocal<Dictionary<string, FaceFeatureService>> _threadLocalInstances = 
            new ThreadLocal<Dictionary<string, FaceFeatureService>>(() => new Dictionary<string, FaceFeatureService>());

        private FaceFeatureService(FaceRecognitionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.FaceNetModelPath))
                throw new ArgumentNullException(nameof(options.FaceNetModelPath), "模型路径不能为空");

            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
            
            // Set conservative graph optimization level to avoid compatibility issues
            sessionOptions.GraphOptimizationLevel = Microsoft.ML.OnnxRuntime.GraphOptimizationLevel.ORT_ENABLE_BASIC;
            
            // Set conservative thread counts to avoid resource competition
            sessionOptions.IntraOpNumThreads = 4;
            sessionOptions.InterOpNumThreads = 2;
            
            // Enable memory optimization
            sessionOptions.EnableMemoryPattern = true;
            // Disable optimized model file to avoid compatibility issues
            // sessionOptions.OptimizedModelFilePath = Path.Combine(Path.GetDirectoryName(options.FaceNetModelPath) ?? string.Empty, $"{Path.GetFileNameWithoutExtension(options.FaceNetModelPath)}.optimized.onnx");
            
            // Try to enable CUDA if available (commented out for now, can be enabled if GPU is available)
            // try
            // {
            //     sessionOptions.AppendExecutionProvider_CUDA();
            // }
            // catch
            // {
            //     // CUDA not available, fall back to CPU
            // }

            try
            {
                // Ensure model path exists and is accessible
                if (!File.Exists(options.FaceNetModelPath))
                {
                    // Try case-insensitive path resolution
                    string directory = Path.GetDirectoryName(options.FaceNetModelPath) ?? string.Empty;
                    string fileName = Path.GetFileName(options.FaceNetModelPath);
                    if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(fileName))
                    {
                        var files = Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly);
                        var matchingFile = files.FirstOrDefault(f => string.Equals(Path.GetFileName(f), fileName, StringComparison.OrdinalIgnoreCase));
                        if (matchingFile != null)
                        {
                            _onnxSession = new InferenceSession(matchingFile, sessionOptions);
                        }
                        else
                        {
                            throw new FileNotFoundException($"模型文件不存在: {options.FaceNetModelPath}", options.FaceNetModelPath);
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException($"模型文件不存在: {options.FaceNetModelPath}", options.FaceNetModelPath);
                    }
                }
                else
                {
                    _onnxSession = new InferenceSession(options.FaceNetModelPath, sessionOptions);
                }
                
                _config = options;
                _inputWidth = Math.Max(1, options.FeatureInputWidth);
                _inputHeight = Math.Max(1, options.FeatureInputHeight);
                _inputName = _onnxSession.InputMetadata.Keys.FirstOrDefault() ?? "input";
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"初始化人脸特征提取模型失败: {ex.Message}", ex);
            }
        }

        public static FaceFeatureService GetInstance(FaceRecognitionOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrEmpty(options.FaceNetModelPath))
                throw new ArgumentNullException(nameof(options.FaceNetModelPath), "模型路径不能为空");

            string key = options.FaceNetModelPath;
            var instances = _threadLocalInstances.Value;
            FaceFeatureService instance;
            if (instances != null && instances.TryGetValue(key, out instance))
            {
                return instance;
            }
            instance = new FaceFeatureService(options);
            if (instances != null)
            {
                instances[key] = instance;
            }
            return instance;
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
            var inputTensor = new DenseTensor<float>(inputData, new[] { 1, _inputHeight, _inputWidth, InputChannels });

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
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                imageStream.CopyTo(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            using var mat = new Mat();
            try
            {
                CvInvoke.Imdecode(imageBytes, ImreadModes.ColorBgr, mat);
                if (mat.IsEmpty)
                    throw new InvalidOperationException("从流加载图像失败");

                using var rgbImage = new Mat();
                CvInvoke.CvtColor(mat, rgbImage, ColorConversion.Bgr2Rgb);

                float[] inputData = PreprocessImage(rgbImage);
                var inputTensor = new DenseTensor<float>(inputData, new[] { 1, _inputHeight, _inputWidth, InputChannels });

                using var results = _onnxSession.Run(new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(_inputName, inputTensor)
                });

                return results.First().AsTensor<float>().ToArray();
            }
            catch
            {
                mat.Dispose();
                throw;
            }
        }

        private Mat LoadImageAsRgb(string imagePath)
        {
            var image = CvInvoke.Imread(imagePath, ImreadModes.ColorBgr);
            if (image.IsEmpty)
            {
                return image;
            }

            var rgbImage = new Mat();
            try
            {
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
                return rgbImage;
            }
            catch
            {
                rgbImage.Dispose();
                throw;
            }
            finally
            {
                image.Dispose();
            }
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
