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
        
        // 缓存所有人脸向量，避免重复从数据库获取
        private readonly Dictionary<Guid, float[]> _faceVectorsCache = new Dictionary<Guid, float[]>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private const int CacheUpdateIntervalMs = 120000; // 2分钟更新一次缓存
        
        // 统计信息类
        public class FaceStats
        {
            public int TotalFaces { get; set; } = 0;
            public int BlurryFaces { get; set; } = 0;
        }

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

            // 预加载模型并确认初始化完成
            _logger.LogInformation("预加载人脸检测和特征提取模型...");
            var tempDetector = FaceDetector.GetInstance(_faceOptions);
            var tempExtractor = FaceFeatureService.GetInstance(_faceOptions);
            _logger.LogInformation("模型加载完成，开始处理任务...");

            // 添加启动延迟，让系统稳定
            _logger.LogInformation("系统启动中，等待2秒后开始处理任务...");
            await Task.Delay(2000, stoppingToken);

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
                    int totalImages = snapshots.Count;

                    foreach (var snapshot in snapshots)
                    {
                        await channel.Writer.WriteAsync(snapshot, stoppingToken);
                    }

                    if (totalImages > 0)
                    {
                        _logger.LogInformation("此轮处理{TotalImages}张图片", totalImages);
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
            var faceDetector = FaceDetector.GetInstance(_faceOptions);
            var featureExtractor = FaceFeatureService.GetInstance(_faceOptions);

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
                // 检查快照数据是否有效
                if (snapshot == null || snapshot.ImageBytes == null || snapshot.ImageBytes.Length == 0)
                {
                    _logger.LogWarning("快照数据无效，跳过处理: {FilePath}", snapshot?.FilePath);
                    // 即使数据无效，也尝试删除文件
                    if (_pipelineOptions.DeleteProcessedSnapshots && !string.IsNullOrEmpty(snapshot?.FilePath))
                    {
                        TryDeleteSnapshotFile(snapshot.FilePath);
                    }
                    return;
                }

                snapshotImage = ImageUtils.LoadImage(snapshot.ImageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{Camera}] 快照解析失败", snapshot.CameraName);
                // 即使解析失败，也删除文件
                if (_pipelineOptions.DeleteProcessedSnapshots && !string.IsNullOrEmpty(snapshot?.FilePath))
                {
                    TryDeleteSnapshotFile(snapshot.FilePath);
                }
                return;
            }

            using (snapshotImage)
            {
                try
                {
                    var detections = faceDetector.DetectFaces(snapshotImage);
                    if (detections != null && detections.Count >= _pipelineOptions.MinFaceCount)
                    {
                        int totalFaces, blurryFaces;
                    var validFaces = faceDetector.CropAndFilterSharpFaces(snapshotImage, detections, out totalFaces, out blurryFaces);
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
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理快照时发生异常");
                }
                finally
                {
                    // 无论处理结果如何，都删除文件
                    if (_pipelineOptions.DeleteProcessedSnapshots)
                    {
                        TryDeleteSnapshotFile(snapshot.FilePath);
                    }
                }
            }
        }

        private async Task UpdateFaceVectorsCacheAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var faceRepository = scope.ServiceProvider.GetRequiredService<PgFaceRepository>();
                var candidates = await faceRepository.GetAllFaceVectorsAsync(cancellationToken);
                if (candidates != null && candidates.Count > 0)
                {
                    lock (_faceVectorsCache)
                    {
                        _faceVectorsCache.Clear();
                        foreach (var candidate in candidates)
                        {
                            if (candidate.Vector != null && candidate.Vector.Length > 0)
                            {
                                _faceVectorsCache[candidate.Id] = candidate.Vector;
                            }
                        }
                    }
                    _lastCacheUpdate = DateTime.UtcNow;
                    _logger.LogInformation("人脸向量缓存更新完成，缓存数量: {Count}", _faceVectorsCache.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新人脸向量缓存失败");
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
            
            // 检查并更新缓存
            if ((DateTime.UtcNow - _lastCacheUpdate).TotalMilliseconds > CacheUpdateIntervalMs)
            {
                await UpdateFaceVectorsCacheAsync(cancellationToken);
            }

            // Try to use Qdrant for similarity search if available
            using var scope = _scopeFactory.CreateScope();
            var qdrantManager = scope.ServiceProvider.GetService<FaceRecoTrackService.Utils.QdrantUtil.QdrantVectorManager>();
            if (qdrantManager != null)
            {
                try
                {
                    var results = await qdrantManager.SearchAsync(
                        "face_collection",
                        vector,
                        5,
                        _pipelineOptions.FallbackSimilarityThreshold,
                        cancellationToken);

                    if (results != null && results.Count > 0)
                    {
                        var bestResult = results.OrderByDescending(r => r.Score).First();
                        if (bestResult.Score >= _pipelineOptions.SimilarityThreshold)
                        {
                            if (Guid.TryParse(bestResult.PointId, out var personId))
                            {
                                return new MatchResult { PersonId = personId, Score = bestResult.Score };
                            }
                        }
                        else if (bestResult.Score >= _pipelineOptions.FallbackSimilarityThreshold)
                        {
                            if (Guid.TryParse(bestResult.PointId, out var personId))
                            {
                                return new MatchResult { PersonId = personId, Score = bestResult.Score };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Qdrant搜索失败，回退到本地搜索");
                }
            }

            // Fallback to local search if Qdrant is not available
            float bestScore = float.MinValue;
            Guid bestId = Guid.Empty;
            
            // 使用缓存的人脸向量
            Dictionary<Guid, float[]> cacheSnapshot;
            lock (_faceVectorsCache)
            {
                cacheSnapshot = new Dictionary<Guid, float[]>(_faceVectorsCache);
            }
            
            if (cacheSnapshot.Count == 0)
            {
                // 缓存为空时，从数据库获取
                var candidates = await faceRepository.GetAllFaceVectorsAsync(cancellationToken);
                if (candidates == null || candidates.Count == 0)
                    return null;
                
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
            }
            else
            {
                // 使用缓存进行搜索
                foreach (var kvp in cacheSnapshot)
                {
                    var candidateId = kvp.Key;
                    var candidateVector = kvp.Value;
                    
                    if (candidateVector == null || candidateVector.Length == 0)
                        continue;
                    if (candidateVector.Length != vector.Length)
                        continue;

                    var score = featureExtractor.CalculateSimilarity(vector, candidateVector);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestId = candidateId;
                    }
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
                    // 检查文件是否存在
                    if (!File.Exists(filePath))
                    {
                        // 文件已不存在，可能被其他线程删除，直接返回
                        return;
                    }
                    
                    File.Delete(filePath);
                    return;
                }
                catch (FileNotFoundException)
                {
                    // 文件不存在，可能被其他线程删除，直接返回
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
