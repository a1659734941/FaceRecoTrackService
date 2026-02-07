using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Utils;
using FaceRecoTrackService.Utils.QdrantUtil;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace FaceRecoTrackService.Services
{
    public class FaceVerificationService
    {
        private readonly FaceRecognitionOptions _faceOptions;
        private readonly PipelineOptions _pipelineOptions;
        private readonly QdrantVectorManager _qdrantManager;
        private readonly QdrantConfig _qdrantConfig;
        private readonly PgFaceRepository _faceRepository;

        public FaceVerificationService(
            FaceRecognitionOptions faceOptions,
            IOptions<PipelineOptions> pipelineOptions,
            QdrantVectorManager qdrantManager,
            QdrantConfig qdrantConfig,
            PgFaceRepository faceRepository)
        {
            _faceOptions = faceOptions ?? throw new ArgumentNullException(nameof(faceOptions));
            _pipelineOptions = pipelineOptions?.Value ?? throw new ArgumentNullException(nameof(pipelineOptions));
            _qdrantManager = qdrantManager ?? throw new ArgumentNullException(nameof(qdrantManager));
            _qdrantConfig = qdrantConfig ?? throw new ArgumentNullException(nameof(qdrantConfig));
            _faceRepository = faceRepository ?? throw new ArgumentNullException(nameof(faceRepository));
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
            var detector = FaceDetector.GetInstance(_faceOptions);
            var featureExtractor = FaceFeatureService.GetInstance(_faceOptions);

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
            var detector = FaceDetector.GetInstance(_faceOptions);

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

        /// <summary>
        /// 电脑人脸识别
        /// </summary>
        /// <param name="request">电脑人脸识别请求</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>电脑人脸识别响应</returns>
        public async Task<ComputerFaceRecognitionResponse> ComputerRecognizeAsync(
            ComputerFaceRecognitionRequest request, 
            CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Base64Image))
                throw new ArgumentException("Base64图像不能为空");

            cancellationToken.ThrowIfCancellationRequested();

            // 解码Base64图像
            var bytes = Base64Helper.DecodeImage(request.Base64Image);
            using var image = ImageUtils.LoadImage(bytes);
            
            // 初始化检测器和特征提取器
            var detector = FaceDetector.GetInstance(_faceOptions);
            var featureExtractor = FaceFeatureService.GetInstance(_faceOptions);

            // 检测人脸
            var detections = detector.DetectFaces(image);
            if (detections == null || detections.Count == 0)
            {
                return new ComputerFaceRecognitionResponse
                {
                    Recognized = false,
                    Message = "未检测到人脸"
                };
            }

            // 提取最佳人脸
            using var face = ExtractBestFace(image, detector, "图像", out var croppedFaceBase64);
            
            // 提取特征向量
            var vector = ExtractVector(featureExtractor, face);
            
            // 使用Qdrant进行向量搜索
            var matchResult = await MatchFaceAsync(vector, request.Threshold, cancellationToken);
            
            if (matchResult != null)
            {
                return new ComputerFaceRecognitionResponse
                {
                    Recognized = true,
                    FaceId = matchResult.PersonId,
                    UserName = matchResult.UserName,
                    Similarity = matchResult.Score,
                    Message = "识别成功",
                    CroppedFaceBase64 = croppedFaceBase64
                };
            }
            else
            {
                return new ComputerFaceRecognitionResponse
                {
                    Recognized = false,
                    Message = "未识别到匹配的人脸",
                    CroppedFaceBase64 = croppedFaceBase64
                };
            }
        }

        /// <summary>
        /// 匹配人脸
        /// </summary>
        /// <param name="vector">人脸特征向量</param>
        /// <param name="threshold">识别阈值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>匹配结果</returns>
        private async Task<MatchResult?> MatchFaceAsync(
            float[] vector, 
            double? threshold, 
            CancellationToken cancellationToken)
        {
            // 使用用户指定的阈值或默认阈值
            var similarityThreshold = threshold ?? _pipelineOptions.SimilarityThreshold;
            var fallbackThreshold = _pipelineOptions.FallbackSimilarityThreshold;

            // 尝试使用Qdrant进行向量搜索
            try
            {
                var results = await _qdrantManager.SearchAsync(
                    _qdrantConfig.CollectionName,
                    vector,
                    5,
                    (float)fallbackThreshold,
                    cancellationToken);

                if (results != null && results.Count > 0)
                {
                    var bestResult = results.OrderByDescending(r => r.Score).First();
                    if (bestResult.Score >= similarityThreshold)
                    {
                        if (Guid.TryParse(bestResult.PointId, out var personId))
                        {
                            // 获取人脸信息
                            var faceInfo = await _faceRepository.GetFaceByIdAsync(personId, cancellationToken);
                            return new MatchResult 
                            {
                                PersonId = personId, 
                                UserName = faceInfo?.UserName,
                                Score = bestResult.Score 
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Qdrant搜索失败，回退到本地搜索
                Console.WriteLine($"Qdrant搜索失败，回退到本地搜索: {ex.Message}");
            }

            // 回退到本地数据库搜索
            var candidates = await _faceRepository.GetAllFaceVectorsAsync(cancellationToken);
            if (candidates == null || candidates.Count == 0)
                return null;

            float bestScore = float.MinValue;
            Guid bestId = Guid.Empty;
            string? bestUserName = null;
            
            // 初始化特征提取器
            var featureExtractor = FaceFeatureService.GetInstance(_faceOptions);
            
            foreach (var candidate in candidates)
            {
                if (candidate.Vector == null || candidate.Vector.Length == 0)
                    continue;
                if (candidate.Vector.Length != vector.Length)
                    continue;

                var score = featureExtractor.CalculateSimilarity(vector, candidate.Vector);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = candidate.Id;
                    bestUserName = candidate.UserName;
                }
            }

            if (bestScore >= similarityThreshold && bestId != Guid.Empty)
                return new MatchResult { PersonId = bestId, UserName = bestUserName, Score = bestScore };

            return null;
        }

        /// <summary>
        /// 匹配结果类
        /// </summary>
        private class MatchResult
        {
            public Guid PersonId { get; set; }
            public string? UserName { get; set; }
            public float Score { get; set; }
        }
    }
}
