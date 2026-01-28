using System;
using System.Collections.Generic;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Dnn;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Serilog;
using SkiaSharp;
using FaceRecoTrackService.Core.Options;

namespace FaceRecoTrackService.Core.Algorithms
{
    /// <summary>
    /// 人脸检测与预处理处理器
    /// </summary>
    public class FaceDetector : IDisposable
    {
        private readonly Net _net;
        private readonly FaceRecognitionOptions _config;
        private readonly System.Drawing.Size _inputSize = new System.Drawing.Size(640, 640);
        private bool _disposed;

        public FaceDetector(FaceRecognitionOptions config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            if (string.IsNullOrWhiteSpace(_config.YoloModelPath))
                throw new ArgumentException("YoloModelPath配置不能为空", nameof(config));
            
            if (!System.IO.File.Exists(_config.YoloModelPath))
                throw new FileNotFoundException($"模型文件不存在: {_config.YoloModelPath}", _config.YoloModelPath);
            
            try
            {
                _net = DnnInvoke.ReadNetFromONNX(_config.YoloModelPath);
                _net.SetPreferableBackend(Emgu.CV.Dnn.Backend.Default);
                _net.SetPreferableTarget(Target.Cpu);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载ONNX模型失败: {_config.YoloModelPath}", ex);
            }
        }

        /// <summary>
        /// 检测图像中的人脸并返回结果
        /// </summary>
        public List<ObjectDetection> DetectFaces(SKImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            using var mat = ToMat(image);
            if (mat.IsEmpty)
                throw new InvalidOperationException("图像解析失败：OpenCV Mat 为空");
            using var blob = DnnInvoke.BlobFromImage(
                mat,
                1.0 / 255.0,
                _inputSize,
                new MCvScalar(),
                swapRB: true,
                crop: false);
            _net.SetInput(blob);

            using var outputs = new VectorOfMat();
            _net.Forward(outputs, _net.UnconnectedOutLayersNames);
            if (outputs.Size == 0)
                return new List<ObjectDetection>();
            if (outputs[0] == null || outputs[0].IsEmpty)
                return new List<ObjectDetection>();

            var detections = ParseDetections(outputs[0], mat.Size, _inputSize, _config.DetectionConfidence);
            return ApplyNms(detections, _config.DetectionConfidence, _config.IouThreshold);
        }

        /// <summary>
        /// 裁剪并筛选清晰的人脸
        /// </summary>
        public Dictionary<int, SKImage> CropAndFilterSharpFaces(SKImage originalImage, List<ObjectDetection> detectionResults)
        {
            var validFaces = new Dictionary<int, SKImage>();

            for (int i = 0; i < detectionResults.Count; i++)
            {
                var face = detectionResults[i];
                var bbox = face.BoundingBox;
                var (croppedFace, x, y, width, height) = CropFace(originalImage, bbox);

                if (croppedFace == null)
                {
                    Log.Warning("人脸 {Index} 裁剪失败，跳过", i);
                    continue;
                }

                if (!SharpnessEvaluator.IsSharp(croppedFace, width, height, _config))
                {
                    Log.Warning("人脸 {Index} 模糊，跳过", i);
                    continue;
                }

                validFaces[i] = croppedFace;
            }

            return validFaces;
        }

        /// <summary>
        /// 裁剪并筛选清晰的人脸（附带清晰度统计）
        /// </summary>
        public Dictionary<int, SKImage> CropAndFilterSharpFaces(
            SKImage originalImage,
            List<ObjectDetection> detectionResults,
            out double maxSharpness,
            out double maxThreshold,
            out int evaluatedCount)
        {
            maxSharpness = 0;
            maxThreshold = 0;
            evaluatedCount = 0;

            var validFaces = new Dictionary<int, SKImage>();
            for (int i = 0; i < detectionResults.Count; i++)
            {
                var face = detectionResults[i];
                var bbox = face.BoundingBox;
                var (croppedFace, x, y, width, height) = CropFace(originalImage, bbox);

                if (croppedFace == null)
                {
                    Log.Warning("人脸 {Index} 裁剪失败，跳过", i);
                    continue;
                }

                evaluatedCount++;
                var isSharp = SharpnessEvaluator.IsSharp(croppedFace, width, height, _config, out var actualSharpness);
                var threshold = SharpnessEvaluator.GetDynamicThreshold(width, height, _config);
                if (actualSharpness > maxSharpness)
                {
                    maxSharpness = actualSharpness;
                    maxThreshold = threshold;
                }

                if (!isSharp)
                {
                    Log.Warning("人脸 {Index} 模糊，跳过", i);
                    continue;
                }

                validFaces[i] = croppedFace;
            }

            return validFaces;
        }

        /// <summary>
        /// 裁剪人脸区域（带扩展处理）
        /// </summary>
        private (SKImage? croppedFace, int x, int y, int width, int height) CropFace(SKImage originalImage, SKRectI bbox)
        {
            int x = (int)Math.Round((double)bbox.Left);
            int y = (int)Math.Round((double)bbox.Top);
            int width = (int)Math.Round((double)bbox.Width);
            int height = (int)Math.Round((double)bbox.Height);

            x = Math.Max(0, x - _config.FaceExpandRatio);
            y = Math.Max(0, y - _config.FaceExpandRatio);
            width = Math.Min(originalImage.Width - x, width + 2 * _config.FaceExpandRatio);
            height = Math.Min(originalImage.Height - y, height + 2 * _config.FaceExpandRatio);

            int right = x + width;
            int bottom = y + height;
            right = Math.Min(right, originalImage.Width);
            bottom = Math.Min(bottom, originalImage.Height);
            int validWidth = right - x;
            int validHeight = bottom - y;
            if (validWidth <= 0 || validHeight <= 0)
            {
                Log.Warning("人脸有效区域为空，跳过");
            }

            var faceRect = new SKRectI(x, y, right, bottom);
            return (originalImage.Subset(faceRect), x, y, validWidth, validHeight);
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
            {
                _net?.Dispose();
            }
            _disposed = true;
        }

        private static Mat ToMat(SKImage image)
        {
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            if (data == null)
                throw new InvalidOperationException("图像编码失败：无法生成PNG数据");
            var mat = new Mat();
            CvInvoke.Imdecode(data.ToArray(), ImreadModes.ColorBgr, mat);
            return mat;
        }

        private static List<ObjectDetection> ApplyNms(
            List<ObjectDetection> detections,
            float scoreThreshold,
            float nmsThreshold)
        {
            if (detections == null || detections.Count == 0)
                return detections ?? new List<ObjectDetection>();

            var boxes = new Rectangle[detections.Count];
            var scores = new float[detections.Count];
            for (int i = 0; i < detections.Count; i++)
            {
                var rect = detections[i].BoundingBox;
                boxes[i] = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
                scores[i] = detections[i].Confidence;
            }

            var indices = DnnInvoke.NMSBoxes(
                boxes,
                scores,
                Math.Max(scoreThreshold, 0f),
                nmsThreshold);

            var results = new List<ObjectDetection>();
            if (indices == null || indices.Length == 0)
                return results;

            foreach (var idx in indices)
            {
                if (idx >= 0 && idx < detections.Count)
                    results.Add(detections[idx]);
            }

            return results;
        }

        private static List<ObjectDetection> ParseDetections(
            Mat output,
            System.Drawing.Size originalSize,
            System.Drawing.Size inputSize,
            float confidenceThreshold)
        {
            var results = new List<ObjectDetection>();
            if (output == null || output.IsEmpty)
                return results;

            var total = (int)output.Total;
            if (total <= 0)
                return results;

            var data = new float[total];
            output.CopyTo(data);

            int dims = output.Dims;
            int[] sizes = output.SizeOfDimension;

            float scaleX = originalSize.Width / (float)inputSize.Width;
            float scaleY = originalSize.Height / (float)inputSize.Height;

            if (dims == 3 && sizes.Length >= 3)
            {
                int dim1 = sizes[1];
                int dim2 = sizes[2];
                bool channelsFirst = dim1 < dim2;
                int attrs = channelsFirst ? dim1 : dim2;
                int boxes = channelsFirst ? dim2 : dim1;

                for (int i = 0; i < boxes; i++)
                {
                    float x = channelsFirst ? data[0 * boxes + i] : data[i * attrs + 0];
                    float y = channelsFirst ? data[1 * boxes + i] : data[i * attrs + 1];
                    float w = channelsFirst ? data[2 * boxes + i] : data[i * attrs + 2];
                    float h = channelsFirst ? data[3 * boxes + i] : data[i * attrs + 3];
                    float obj = channelsFirst ? data[4 * boxes + i] : data[i * attrs + 4];

                    float score = ComputeScore(data, channelsFirst, boxes, attrs, i, obj);
                    if (score < confidenceThreshold)
                        continue;

                    AddBox(results, x, y, w, h, score, scaleX, scaleY, originalSize);
                }

                return results;
            }

            if (output.Rows > 0 && output.Cols > 0)
            {
                int boxes = output.Rows;
                int attrs = output.Cols;
                for (int i = 0; i < boxes; i++)
                {
                    int offset = i * attrs;
                    float x = data[offset];
                    float y = data[offset + 1];
                    float w = data[offset + 2];
                    float h = data[offset + 3];
                    float obj = data[offset + 4];

                    float score = ComputeScore(data, false, boxes, attrs, i, obj);
                    if (score < confidenceThreshold)
                        continue;

                    AddBox(results, x, y, w, h, score, scaleX, scaleY, originalSize);
                }
            }

            return results;
        }

        private static float ComputeScore(
            float[] data,
            bool channelsFirst,
            int boxes,
            int attrs,
            int index,
            float obj)
        {
            if (attrs <= 5)
                return obj;

            float maxClass = 0f;
            for (int c = 5; c < attrs; c++)
            {
                float value = channelsFirst ? data[c * boxes + index] : data[index * attrs + c];
                if (value > maxClass)
                    maxClass = value;
            }

            float scoreWithObj = obj * maxClass;
            float score = Math.Max(obj, Math.Max(maxClass, scoreWithObj));
            return score;
        }

        private static void AddBox(
            List<ObjectDetection> results,
            float x,
            float y,
            float w,
            float h,
            float score,
            float scaleX,
            float scaleY,
            System.Drawing.Size originalSize)
        {
            float left = (x - w / 2f) * scaleX;
            float top = (y - h / 2f) * scaleY;
            float width = w * scaleX;
            float height = h * scaleY;

            int ix = ClampToInt(left, 0, originalSize.Width - 1);
            int iy = ClampToInt(top, 0, originalSize.Height - 1);
            int iw = ClampToInt(width, 1, originalSize.Width - ix);
            int ih = ClampToInt(height, 1, originalSize.Height - iy);

            results.Add(new ObjectDetection
            {
                BoundingBox = new SKRectI(ix, iy, ix + iw, iy + ih),
                Confidence = score
            });
        }

        private static int ClampToInt(float value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return (int)Math.Round(value);
        }
    }
}
