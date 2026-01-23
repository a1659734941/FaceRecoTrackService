using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgCameraMappingRepository
    {
        private readonly string _connectionString;

        public PgCameraMappingRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<CameraMapping?> GetMappingAsync(string snapCameraIp, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT snap_camera_ip, record_camera_ip, room_name
FROM camera_mapping WHERE snap_camera_ip = @snap_camera_ip;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("snap_camera_ip", snapCameraIp);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new CameraMapping
            {
                SnapCameraIp = reader.GetString(0),
                RecordCameraIp = reader.GetString(1),
                RoomName = reader.IsDBNull(2) ? "" : reader.GetString(2)
            };
        }
    }
}
