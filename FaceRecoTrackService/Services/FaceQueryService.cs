using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Dtos;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Utils.QdrantUtil;

namespace FaceRecoTrackService.Services
{
    public class FaceQueryService
    {
        private readonly PgFaceRepository _faceRepository;
        private readonly QdrantConfig _qdrantConfig;
        private readonly QdrantVectorManager _qdrantManager;

        public FaceQueryService(
            PgFaceRepository faceRepository,
            QdrantConfig qdrantConfig,
            QdrantVectorManager qdrantManager)
        {
            _faceRepository = faceRepository;
            _qdrantConfig = qdrantConfig;
            _qdrantManager = qdrantManager;
        }

        public Task<long> GetCountAsync(CancellationToken cancellationToken)
        {
            return _faceRepository.GetFaceCountAsync(cancellationToken);
        }

        public async Task<FaceInfoResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            var entity = await _faceRepository.GetFaceByIdAsync(id, cancellationToken);
            if (entity == null) return null;

            return new FaceInfoResponse
            {
                Id = entity.Id,
                UserName = entity.UserName,
                Ip = entity.Ip,
                Description = entity.Description,
                IsTest = entity.IsTest,
                ImageBase64 = null,
                CreatedAtUtc = entity.CreatedAtUtc
            };
        }

        public Task<long> GetQdrantCountAsync(CancellationToken cancellationToken)
        {
            return _qdrantManager.GetCollectionPointsCountAsync(
                _qdrantConfig.CollectionName,
                cancellationToken);
        }
    }
}
