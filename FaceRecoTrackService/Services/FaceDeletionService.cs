using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Utils.QdrantUtil;

namespace FaceRecoTrackService.Services
{
    public class FaceDeletionService
    {
        private readonly QdrantConfig _qdrantConfig;
        private readonly QdrantVectorManager _qdrantManager;

        public FaceDeletionService(
            QdrantConfig qdrantConfig,
            QdrantVectorManager qdrantManager)
        {
            _qdrantConfig = qdrantConfig;
            _qdrantManager = qdrantManager;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
        {
            if (!await _qdrantManager.ExistsAsync(_qdrantConfig.CollectionName, cancellationToken))
                return false;

            await _qdrantManager.DeletePointByIdAsync(
                _qdrantConfig.CollectionName,
                id,
                cancellationToken: cancellationToken);

            return true;
        }
    }
}
