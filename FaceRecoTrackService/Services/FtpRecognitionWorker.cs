using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.External;
using FaceRecoTrackService.Infrastructure.Repositories;
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
        private readonly FaceRecognitionOptions _faceOptions;
        private readonly FtpFolderSnapshotClient _snapshotClient;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FtpRecognitionWorker> _logger;

        public FtpRecognitionWorker(
            IOptions<PipelineOptions> pipelineOptions,
            IOptions<FtpFolderOptions> ftpOptions,
            IOptions<FaceRecognitionOptions> faceOptions,
            IServiceScopeFactory scopeFactory,
            ILogger<FtpRecognitionWorker> logger)
        {
            _pipelineOptions = pipelineOptions.Value;
            _ftpOptions = ftpOptions.Value;
            _faceOptions = faceOptions.Value;
            _snapshotClient = new FtpFolderSnapshotClient();
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(_pipelineOptions.SnapshotSaveDir);
            var workerCount = Math.Max(1, _pipelineOptions.SnapshotWorkerCount);
            var queueSize = Math.Max(10, _pipelineOptions.SnapshotQueueSize);
            var channel = Channel.CreateBounded<FolderSnapshotResult>(
                new BoundedChannelOptions(queueSize)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = false
                });

            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                workers[i] = Task.Run(() => WorkerLoopAsync(channel.Reader, stoppingToken), stoppingToken);
            }

            _logger.LogInformation(
                "开始监听FTP目录：{Path}，轮询间隔：{Interval}ms，工作线程：{Workers}，队列容量：{QueueSize}",
                _ftpOptions.Path,
                _pipelineOptions.PollIntervalMs,
                workerCount,
                queueSize);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var snapshots = await _snapshotClient.FetchNewSnapshotsAsync(_ftpOptions, stoppingToken);
                    foreach (var snapshot in snapshots)
                    {
                        await channel.Writer.WriteAsync(snapshot, stoppingToken);
                    }

                    await Task.Delay(_pipelineOptions.PollIntervalMs, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常停止
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理FTP目录时发生异常");
            }
            finally
            {
                channel.Writer.TryComplete();
                await Task.WhenAll(workers);
            }
        }

        private async Task WorkerLoopAsync(
            ChannelReader<FolderSnapshotResult> reader,
            CancellationToken cancellationToken)
        {
            using var faceDetector = new FaceDetector(_faceOptions);
            using var featureExtractor = new FaceFeatureService(_faceOptions);

            while (await reader.WaitToReadAsync(cancellationToken))
            {
                while (reader.TryRead(out var snapshot))
                {
                    try
                    {
                        await ProcessSnapshotAsync(snapshot, faceDetector, featureExtractor, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理快照失败：{FilePath}", snapshot?.FilePath);
                    }
                }
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
                var faceRepository = scope.ServiceProvider.GetRequiredService<PgFaceRepository>();
                foreach (var faceImage in validFaces.Values)
                {
                    // 提取向量并进行相似度检索
                    var match = await MatchFaceAsync(featureExtractor, faceRepository, faceImage, cancellationToken);
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
            PgFaceRepository faceRepository,
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

            var candidates = await faceRepository.GetAllFaceVectorsAsync(cancellationToken);
            if (candidates == null || candidates.Count == 0)
                return null;

            float bestScore = float.MinValue;
            Guid bestId = Guid.Empty;
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
                }
            }

            if (bestScore >= _pipelineOptions.SimilarityThreshold && bestId != Guid.Empty)
                return new MatchResult { PersonId = bestId, Score = bestScore };

            if (bestScore >= _pipelineOptions.FallbackSimilarityThreshold && bestId != Guid.Empty)
                return new MatchResult { PersonId = bestId, Score = bestScore };

            return null;
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
