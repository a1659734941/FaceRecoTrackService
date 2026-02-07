using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Core.Models;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Utils;
using FaceRecoTrackService.Utils.QdrantUtil;
using Microsoft.AspNetCore.Hosting;
using Qdrant.Client.Grpc;
using Serilog;
using SkiaSharp;

namespace FaceRecoTrackService.Services
{
    public class FaceRegistrationService
    {
        private readonly FaceRecognitionOptions _faceOptions;
        private readonly QdrantConfig _qdrantConfig;
        private readonly QdrantVectorManager _qdrantManager;
        private readonly PgFaceRepository _faceRepository;
        private readonly IWebHostEnvironment _environment;

        public FaceRegistrationService(
            FaceRecognitionOptions faceOptions,
            QdrantConfig qdrantConfig,
            QdrantVectorManager qdrantManager,
            PgFaceRepository faceRepository,
            IWebHostEnvironment environment)
        {
            _faceOptions = faceOptions;
            _qdrantConfig = qdrantConfig;
            _qdrantManager = qdrantManager;
            _faceRepository = faceRepository;
            _environment = environment;
        }

        public async Task<FaceRegisterResponse> RegisterAsync(FaceRegisterRequest request, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.Base64Image))
                throw new ArgumentException("base64Image不能为空");
            if (string.IsNullOrWhiteSpace(request.UserName))
                throw new ArgumentException("userName不能为空");

            // Base64解码并加载图像
            var imageBytes = Base64Helper.DecodeImage(request.Base64Image);
            if (imageBytes.Length == 0)
                throw new InvalidOperationException("图像解码失败：空字节");

            using var image = ImageUtils.LoadImage(imageBytes);
            if (image == null)
                throw new InvalidOperationException("图像解码失败：无法生成图像对象");

            // 人脸检测与清晰度筛选
            var detector = FaceDetector.GetInstance(_faceOptions);
            var detections = detector.DetectFaces(image);
            if (detections == null || detections.Count == 0)
                throw new InvalidOperationException("未检测到人脸");

            var validFaces = detector.CropAndFilterSharpFaces(
                image,
                detections,
                out var maxSharpness,
                out var maxThreshold,
                out var evaluatedCount);
            if (validFaces.Count == 0)
                throw new InvalidOperationException(
                    $"未检测到清晰人脸：检测到{detections.Count}张，评估{evaluatedCount}张，" +
                    $"最大清晰度{maxSharpness:F2}，阈值{maxThreshold:F2}");

            // 提取向量并持久化
            var personId = Guid.NewGuid();
            var faceImage = validFaces.First().Value;
            if (faceImage == null)
                throw new InvalidOperationException("人脸裁剪失败：人脸图像为空");
            TrySaveDebugFaceImage(faceImage, personId);
            using var stream = new MemoryStream();
            using var encoded = faceImage.Encode(SKEncodedImageFormat.Png, 100);
            if (encoded == null)
                throw new InvalidOperationException("人脸编码失败：无法生成PNG数据");
            encoded.SaveTo(stream);
            stream.Position = 0;

            var featureExtractor = FaceFeatureService.GetInstance(_faceOptions);
            var vector = featureExtractor.ExtractFeaturesFromStream(stream);
            if (vector == null || vector.Length == 0)
                throw new InvalidOperationException("人脸特征提取失败：特征向量为空");
            if (_faceOptions.VectorSize > 0 && vector.Length != _faceOptions.VectorSize)
            {
                Log.Warning(
                    "人脸特征维度不匹配，尝试自动调整：期望{Expected}，实际{Actual}",
                    _faceOptions.VectorSize,
                    vector.Length);
                vector = FaceFeatureService.ResizeVector(vector, _faceOptions.VectorSize);
            }

            var person = new FacePerson
            {
                Id = personId,
                UserName = request.UserName,
                Ip = request.Ip ?? "",
                Description = request.Description ?? "",
                ImageBase64 = request.Base64Image,
                FaceVector = vector,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _faceRepository.InsertFaceAsync(person, cancellationToken);

            await _qdrantManager.EnsureCollectionAsync(
                _qdrantConfig.CollectionName,
                _faceOptions.VectorSize,
                _qdrantConfig.RecreateOnVectorSizeMismatch,
                cancellationToken: cancellationToken);

            var points = new List<PointStruct>
            {
                new PointStruct
                {
                    Id = new PointId { Uuid = person.Id.ToString() },
                    Vectors = new Vectors { Vector = vector },
                    Payload =
                    {
                        { "person_id", new Value { StringValue = person.Id.ToString() } },
                        { "person_name", new Value { StringValue = person.UserName } }
                    }
                }
            };

            await _qdrantManager.UpsertPointsAsync(_qdrantConfig.CollectionName, points, cancellationToken: cancellationToken);

            return new FaceRegisterResponse { Id = personId };
        }

        private void TrySaveDebugFaceImage(SKImage faceImage, Guid personId)
        {
            if (!_environment.IsDevelopment()) return;
            if (!_faceOptions.EnableDebugSaveFaces) return;
            if (string.IsNullOrWhiteSpace(_faceOptions.DebugSaveDir)) return;

            try
            {
                Directory.CreateDirectory(_faceOptions.DebugSaveDir);
                var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{personId}.png";
                var path = Path.Combine(_faceOptions.DebugSaveDir, fileName);
                using var encoded = faceImage.Encode(SKEncodedImageFormat.Png, 100);
                if (encoded != null)
                {
                    using var fileStream = File.OpenWrite(path);
                    encoded.SaveTo(fileStream);
                }
            }
            catch (Exception)
            {
                // 开发调试辅助，不影响主流程
            }
        }
    }
}
