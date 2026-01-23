using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Utils.QdrantUtil;

namespace FaceRecoTrackService.Services
{
    public class FaceDeletionService
    {
        private readonly PgFaceRepository _faceRepository;
        private readonly PgTrackRepository _trackRepository;
        private readonly QdrantConfig _qdrantConfig;
        private readonly QdrantVectorManager _qdrantManager;

        public FaceDeletionService(
            PgFaceRepository faceRepository,
            PgTrackRepository trackRepository,
            QdrantConfig qdrantConfig,
            QdrantVectorManager qdrantManager)
        {
            _faceRepository = faceRepository;
            _trackRepository = trackRepository;
            _qdrantConfig = qdrantConfig;
            _qdrantManager = qdrantManager;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            await _trackRepository.DeleteTracksByPersonIdAsync(id, cancellationToken);
            var deletedInPg = await _faceRepository.DeleteFaceByIdAsync(id, cancellationToken);

            if (await _qdrantManager.ExistsAsync(_qdrantConfig.CollectionName, cancellationToken))
            {
                await _qdrantManager.DeletePointByIdAsync(
                    _qdrantConfig.CollectionName,
                    id,
                    cancellationToken: cancellationToken);
            }

            return deletedInPg;
        }
    }
}
