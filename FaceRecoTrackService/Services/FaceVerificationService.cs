using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Utils;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace FaceRecoTrackService.Services
{
    public class FaceVerificationService
    {
        private readonly FaceRecognitionOptions _faceOptions;
        private readonly PipelineOptions _pipelineOptions;

        public FaceVerificationService(
            FaceRecognitionOptions faceOptions,
            IOptions<PipelineOptions> pipelineOptions)
        {
            _faceOptions = faceOptions ?? throw new ArgumentNullException(nameof(faceOptions));
            _pipelineOptions = pipelineOptions?.Value ?? throw new ArgumentNullException(nameof(pipelineOptions));
        }

        public Task<FaceCompareResponse> CompareAsync(FaceCompareRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Base64Image1))
                throw new ArgumentException("base64Image1不能为空");
            if (string.IsNullOrWhiteSpace(request.Base64Image2))
                throw new ArgumentException("base64Image2不能为空");

            cancellationToken.ThrowIfCancellationRequested();

            var bytes1 = Base64Helper.DecodeImage(request.Base64Image1);
            var bytes2 = Base64Helper.DecodeImage(request.Base64Image2);

            using var image1 = ImageUtils.LoadImage(bytes1);
            using var image2 = ImageUtils.LoadImage(bytes2);
            using var detector = new FaceDetector(_faceOptions);
            using var featureExtractor = new FaceFeatureService(_faceOptions.FaceNetModelPath);

            using var face1 = ExtractBestFace(image1, detector, "图片1", out var faceBase64_1);
            using var face2 = ExtractBestFace(image2, detector, "图片2", out var faceBase64_2);

            var vector1 = ExtractVector(featureExtractor, face1);
            var vector2 = ExtractVector(featureExtractor, face2);
            var similarity = featureExtractor.CalculateSimilarity(vector1, vector2);

            return Task.FromResult(new FaceCompareResponse
            {
                IsSamePerson = similarity >= _pipelineOptions.SimilarityThreshold,
                Similarity = similarity,
                FaceImageBase64_1 = faceBase64_1,
                FaceImageBase64_2 = faceBase64_2
            });
        }

        public Task<FaceCheckResponse> CheckAsync(FaceCheckRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Base64Image))
                throw new ArgumentException("base64Image不能为空");

            cancellationToken.ThrowIfCancellationRequested();

            var bytes = Base64Helper.DecodeImage(request.Base64Image);
            using var image = ImageUtils.LoadImage(bytes);
            using var detector = new FaceDetector(_faceOptions);

            var detections = detector.DetectFaces(image);
            if (detections == null || detections.Count == 0)
            {
                return Task.FromResult(new FaceCheckResponse
                {
                    IsFace = false,
                    IsCompliant = false,
                    Reason = "未检测到人脸"
                });
            }

            if (detections.Count > 1)
            {
                var faceBase64 = TryGetFirstFaceBase64(image, detector, detections);
                return Task.FromResult(new FaceCheckResponse
                {
                    IsFace = true,
                    IsCompliant = false,
                    FaceImageBase64 = faceBase64,
                    Reason = "检测到多张人脸"
                });
            }

            var validFaces = detector.CropAndFilterSharpFaces(image, detections);
            if (validFaces == null || validFaces.Count == 0)
            {
                return Task.FromResult(new FaceCheckResponse
                {
                    IsFace = true,
                    IsCompliant = false,
                    Reason = "人脸清晰度不足"
                });
            }

            var faceImage = validFaces.First().Value;
            DisposeFacesExcept(validFaces, faceImage);
            var base64 = EncodeImageToBase64(faceImage);
            faceImage.Dispose();

            return Task.FromResult(new FaceCheckResponse
            {
                IsFace = true,
                IsCompliant = true,
                FaceImageBase64 = base64,
                Reason = "人脸合规"
            });
        }

        private static SKImage ExtractBestFace(
            SKImage image,
            FaceDetector detector,
            string label,
            out string base64)
        {
            var detections = detector.DetectFaces(image);
            if (detections == null || detections.Count == 0)
                throw new InvalidOperationException($"{label}未检测到人脸");

            var validFaces = detector.CropAndFilterSharpFaces(image, detections);
            if (validFaces == null || validFaces.Count == 0)
                throw new InvalidOperationException($"{label}未检测到清晰人脸");

            var faceImage = validFaces.First().Value;
            DisposeFacesExcept(validFaces, faceImage);
            base64 = EncodeImageToBase64(faceImage);
            return faceImage;
        }

        private float[] ExtractVector(FaceFeatureService featureExtractor, SKImage faceImage)
        {
            using var stream = new MemoryStream();
            using var encoded = faceImage.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
                throw new InvalidOperationException("人脸编码失败");
            encoded.SaveTo(stream);
            stream.Position = 0;

            var vector = featureExtractor.ExtractFeaturesFromStream(stream);
            if (_faceOptions.VectorSize > 0 && vector.Length != _faceOptions.VectorSize)
            {
                vector = FaceFeatureService.ResizeVector(vector, _faceOptions.VectorSize);
            }
            return vector;
        }

        private static string EncodeImageToBase64(SKImage image)
        {
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
                throw new InvalidOperationException("图像编码失败");
            return Convert.ToBase64String(encoded.ToArray());
        }

        private static void DisposeFacesExcept(Dictionary<int, SKImage> faces, SKImage keep)
        {
            foreach (var kv in faces)
            {
                if (!ReferenceEquals(kv.Value, keep))
                {
                    kv.Value?.Dispose();
                }
            }
        }

        private static string? TryGetFirstFaceBase64(
            SKImage image,
            FaceDetector detector,
            List<ObjectDetection> detections)
        {
            var validFaces = detector.CropAndFilterSharpFaces(image, detections);
            if (validFaces == null || validFaces.Count == 0)
                return null;

            var faceImage = validFaces.First().Value;
            DisposeFacesExcept(validFaces, faceImage);
            var base64 = EncodeImageToBase64(faceImage);
            faceImage.Dispose();
            return base64;
        }
    }
}
