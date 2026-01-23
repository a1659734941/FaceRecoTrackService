using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.External;
using FaceRecoTrackService.Utils.QdrantUtil;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace FaceRecoTrackService.Services
{
    public class FtpRecognitionWorker : BackgroundService
    {
        private readonly PipelineOptions _pipelineOptions;
        private readonly FtpFolderOptions _ftpOptions;
        private readonly PayloadMappingOptions _payloadMapping;
        private readonly FaceRecognitionOptions _faceOptions;
        private readonly QdrantConfig _qdrantConfig;
        private readonly QdrantVectorManager _qdrantManager;
        private readonly FtpFolderSnapshotClient _snapshotClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FtpRecognitionWorker> _logger;

        public FtpRecognitionWorker(
            IOptions<PipelineOptions> pipelineOptions,
            IOptions<FtpFolderOptions> ftpOptions,
            IOptions<PayloadMappingOptions> payloadMapping,
            IOptions<FaceRecognitionOptions> faceOptions,
            IOptions<QdrantConfig> qdrantConfig,
            QdrantVectorManager qdrantManager,
            IServiceScopeFactory scopeFactory,
            ILogger<FtpRecognitionWorker> logger)
        {
            _pipelineOptions = pipelineOptions.Value;
            _ftpOptions = ftpOptions.Value;
            _payloadMapping = payloadMapping.Value;
            _faceOptions = faceOptions.Value;
            _qdrantConfig = qdrantConfig.Value;
            _qdrantManager = qdrantManager;
            _snapshotClient = new FtpFolderSnapshotClient();
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(_pipelineOptions.SnapshotSaveDir);
            using var faceDetector = new FaceDetector(_faceOptions);
            using var featureExtractor = new FaceFeatureService(_faceOptions.FaceNetModelPath);

            _logger.LogInformation("开始监听FTP目录：{Path}，轮询间隔：{Interval}ms", _ftpOptions.Path, _pipelineOptions.PollIntervalMs);
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var snapshots = await _snapshotClient.FetchNewSnapshotsAsync(_ftpOptions, stoppingToken);
                    foreach (var snapshot in snapshots)
                    {
                        await ProcessSnapshotAsync(snapshot, faceDetector, featureExtractor, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理FTP目录时发生异常");
                }

                await Task.Delay(_pipelineOptions.PollIntervalMs, stoppingToken);
            }
        }

        private async Task ProcessSnapshotAsync(
            FolderSnapshotResult snapshot,
            FaceDetector faceDetector,
            FaceFeatureService featureExtractor,
            CancellationToken cancellationToken)
        {
            SKImage? snapshotImage = null;
            try
            {
                snapshotImage = ImageUtils.LoadImage(snapshot.ImageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Camera}] 快照解析失败", snapshot.CameraName);
                return;
            }

            using (snapshotImage)
            {
                var detections = faceDetector.DetectFaces(snapshotImage);
                if (detections == null || detections.Count < _pipelineOptions.MinFaceCount)
                    return;

                var validFaces = faceDetector.CropAndFilterSharpFaces(snapshotImage, detections);
                using var scope = _scopeFactory.CreateScope();
                var trackRecordService = scope.ServiceProvider.GetRequiredService<TrackRecordService>();
                foreach (var faceImage in validFaces.Values)
                {
                    // 提取向量并进行相似度检索
                    var match = await MatchFaceAsync(featureExtractor, faceImage, cancellationToken);
                    if (match == null) continue;

                    await trackRecordService.HandleTrackAsync(
                        match.PersonId,
                        snapshot.CameraIp,
                        snapshot.CaptureTimeUtc,
                        snapshot.Location,
                        cancellationToken);
                }

                if (_pipelineOptions.DeleteProcessedSnapshots)
                {
                    TryDeleteSnapshotFile(snapshot.FilePath);
                }
            }
        }

        private async Task<MatchResult?> MatchFaceAsync(
            FaceFeatureService featureExtractor,
            SKImage faceImage,
            CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();
            faceImage.Encode(SKEncodedImageFormat.Png, 100).SaveTo(stream);
            stream.Position = 0;
            var vector = featureExtractor.ExtractFeaturesFromStream(stream);
            if (_faceOptions.VectorSize > 0 && vector.Length != _faceOptions.VectorSize)
            {
                _logger.LogWarning(
                    "人脸特征维度不匹配，尝试自动调整：期望{Expected}，实际{Actual}",
                    _faceOptions.VectorSize,
                    vector.Length);
                vector = FaceFeatureService.ResizeVector(vector, _faceOptions.VectorSize);
            }

            var results = await _qdrantManager.SearchAsync(
                _qdrantConfig.CollectionName,
                vector,
                limit: _pipelineOptions.TopK,
                scoreThreshold: _pipelineOptions.SimilarityThreshold,
                cancellationToken: cancellationToken);

            var top = results?.FirstOrDefault();
            if (top == null)
            {
                var fallbackResults = await _qdrantManager.SearchAsync(
                    _qdrantConfig.CollectionName,
                    vector,
                    limit: _pipelineOptions.TopK,
                    scoreThreshold: null,
                    cancellationToken: cancellationToken);

                var fallbackTop = fallbackResults?.FirstOrDefault();
                if (fallbackTop == null || fallbackTop.Score < _pipelineOptions.FallbackSimilarityThreshold)
                    return null;

                top = fallbackTop;
            }

            var personId = GetPayloadValue(top.Payload, _payloadMapping.PersonIdKey);
            if (!Guid.TryParse(personId, out var guid)) return null;

            return new MatchResult
            {
                PersonId = guid,
                Score = top.Score
            };
        }

        private static string GetPayloadValue(
            System.Collections.Generic.IReadOnlyDictionary<string, Qdrant.Client.Grpc.Value> payload,
            string key)
        {
            if (payload == null || string.IsNullOrWhiteSpace(key)) return "";
            if (!payload.TryGetValue(key, out var value)) return "";
            if (value.KindCase == Qdrant.Client.Grpc.Value.KindOneofCase.StringValue)
                return value.StringValue ?? "";
            return value.ToString();
        }

        private void TryDeleteSnapshotFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;

            const int maxRetry = 3;
            for (int i = 0; i < maxRetry; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                    return;
                }
                catch (Exception ex)
                {
                    if (i == maxRetry - 1)
                        _logger.LogWarning(ex, "删除已处理快照失败：{FilePath}", filePath);
                    else
                        Thread.Sleep(200);
                }
            }
        }

        private class MatchResult
        {
            public Guid PersonId { get; set; }
            public float Score { get; set; }
        }
    }
}
