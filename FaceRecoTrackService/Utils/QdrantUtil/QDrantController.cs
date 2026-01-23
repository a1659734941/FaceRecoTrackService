using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace FaceRecoTrackService.Utils.QdrantUtil
{
    /// <summary>
    /// Qdrant向量数据库操作管理器
    /// </summary>
    public class QdrantVectorManager : IDisposable
    {
        private readonly QdrantClient _qdrantClient;
        private bool _disposed;

        public QdrantVectorManager(QdrantConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "Qdrant配置不能为空");

            _qdrantClient = new QdrantClient(
                host: config.Host,
                port: config.Port,
                https: config.UseHttps,
                apiKey: config.ApiKey
            );
        }

        public async Task<bool> CreateCollectionAsync(
            string collectionName,
            int vectorSize,
            Distance distance = Distance.Cosine,
            ulong shardNumber = 1,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (vectorSize <= 0)
                throw new ArgumentException("向量维度必须为正整数", nameof(vectorSize));

            if (await ExistsAsync(collectionName, cancellationToken))
            {
                return false;
            }

            await _qdrantClient.CreateCollectionAsync(
                collectionName: collectionName,
                vectorsConfig: new VectorParams { Size = (ulong)vectorSize, Distance = distance },
                shardNumber: (uint)shardNumber,
                cancellationToken: cancellationToken
            );

            return true;
        }

        public async Task<bool> EnsureCollectionAsync(
            string collectionName,
            int vectorSize,
            bool recreateOnMismatch,
            Distance distance = Distance.Cosine,
            ulong shardNumber = 1,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (vectorSize <= 0)
                throw new ArgumentException("向量维度必须为正整数", nameof(vectorSize));

            if (!await ExistsAsync(collectionName, cancellationToken))
            {
                await _qdrantClient.CreateCollectionAsync(
                    collectionName: collectionName,
                    vectorsConfig: new VectorParams { Size = (ulong)vectorSize, Distance = distance },
                    shardNumber: (uint)shardNumber,
                    cancellationToken: cancellationToken);
                return true;
            }

            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, cancellationToken);
            var existingSize = TryGetVectorSize(info);
            if (existingSize.HasValue && existingSize.Value != vectorSize)
            {
                if (recreateOnMismatch)
                {
                    await _qdrantClient.RecreateCollectionAsync(
                        collectionName: collectionName,
                        vectorsConfig: new VectorParams { Size = (ulong)vectorSize, Distance = distance },
                        shardNumber: (uint)shardNumber,
                        cancellationToken: cancellationToken);
                    return true;
                }

                throw new InvalidOperationException(
                    $"Qdrant集合向量维度不匹配：期望{vectorSize}，实际{existingSize.Value}");
            }

            return false;
        }

        public async Task<bool> ExistsAsync(
            string collectionName,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            return await _qdrantClient.CollectionExistsAsync(collectionName, cancellationToken);
        }

        private static int? TryGetVectorSize(CollectionInfo info)
        {
            if (info?.Config?.Params?.VectorsConfig == null) return null;

            var vectorsConfig = info.Config.Params.VectorsConfig;
            var paramsProp = vectorsConfig.GetType().GetProperty("Params");
            var singleParams = paramsProp?.GetValue(vectorsConfig);
            var size = TryGetSizeFromVectorParams(singleParams);
            if (size.HasValue) return size;

            var paramsMapProp = vectorsConfig.GetType().GetProperty("ParamsMap");
            var paramsMap = paramsMapProp?.GetValue(vectorsConfig);
            if (paramsMap == null) return null;

            var mapProp = paramsMap.GetType().GetProperty("Map");
            var map = mapProp?.GetValue(paramsMap) as IDictionary;
            if (map == null || map.Count == 0) return null;

            foreach (var value in map.Values)
            {
                size = TryGetSizeFromVectorParams(value);
                if (size.HasValue) return size;
            }

            return null;
        }

        private static int? TryGetSizeFromVectorParams(object? vectorParams)
        {
            if (vectorParams == null) return null;
            var sizeProp = vectorParams.GetType().GetProperty("Size");
            var sizeValue = sizeProp?.GetValue(vectorParams);
            if (sizeValue == null) return null;
            try
            {
                return Convert.ToInt32(sizeValue);
            }
            catch
            {
                return null;
            }
        }

        public async Task<UpdateResult> UpsertPointsAsync(
            string collectionName,
            IReadOnlyList<PointStruct> points,
            bool waitForResult = true,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (points == null)
                throw new ArgumentNullException(nameof(points), "点列表不能为空");

            var result = await _qdrantClient.UpsertAsync(
                collectionName: collectionName,
                points: points,
                wait: waitForResult,
                cancellationToken: cancellationToken
            );

            return result;
        }

        public async Task<UpdateResult> DeletePointsAsync(
            string collectionName,
            IReadOnlyList<PointId> pointIds,
            bool waitForResult = true,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (pointIds == null || pointIds.Count == 0)
                throw new ArgumentNullException(nameof(pointIds), "点列表不能为空");

            return await _qdrantClient.DeleteAsync(
                collectionName: collectionName,
                ids: pointIds,
                wait: waitForResult,
                cancellationToken: cancellationToken);
        }

        public Task<UpdateResult> DeletePointByIdAsync(
            string collectionName,
            Guid id,
            bool waitForResult = true,
            CancellationToken cancellationToken = default)
        {
            var points = new List<PointId>
            {
                new PointId { Uuid = id.ToString() }
            };

            return DeletePointsAsync(collectionName, points, waitForResult, cancellationToken);
        }

        public class QdrantSearchResult
        {
            public string PointId { get; set; } = string.Empty;
            public float Score { get; set; }
            public IReadOnlyDictionary<string, Value> Payload { get; set; } =
                new Dictionary<string, Value>();
        }

        public async Task<IReadOnlyList<QdrantSearchResult>> SearchAsync(
            string collectionName,
            float[] vector,
            int limit = 5,
            float? scoreThreshold = null,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (vector == null || vector.Length == 0)
                throw new ArgumentException("向量不能为空", nameof(vector));
            if (limit <= 0) throw new ArgumentException("limit必须为正整数", nameof(limit));

            var results = await _qdrantClient.SearchAsync(
                collectionName: collectionName,
                vector: vector,
                limit: (ulong)limit,
                scoreThreshold: scoreThreshold,
                cancellationToken: cancellationToken
            );

            return results.Select(r => new QdrantSearchResult
            {
                PointId = r.Id.ToString(),
                Score = r.Score,
                Payload = r.Payload
            }).ToList();
        }

        public async Task<long> GetCollectionPointsCountAsync(
            string collectionName,
            CancellationToken cancellationToken = default)
        {
            ValidateCollectionName(collectionName);
            if (!await ExistsAsync(collectionName, cancellationToken))
                return 0;

            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, cancellationToken);
            if (info == null) return 0;
            try
            {
                return Convert.ToInt64(info.PointsCount);
            }
            catch
            {
                return 0;
            }
        }

        private void ValidateCollectionName(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
                throw new ArgumentException("集合名称不能为空", nameof(collectionName));

            if (!System.Text.RegularExpressions.Regex.IsMatch(collectionName, @"^[a-zA-Z0-9_]+$"))
                throw new ArgumentException("集合名称只能包含字母、数字和下划线", nameof(collectionName));
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
                _qdrantClient?.Dispose();
            }

            _disposed = true;
        }

        ~QdrantVectorManager()
        {
            Dispose(false);
        }
    }
}
