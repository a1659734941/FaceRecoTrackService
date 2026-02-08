using System;
using System.Collections.Generic;
using System.Linq;
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
SELECT fc.camera_ip, rc.camera_ip, rc.location_name
FROM camera_mapping m
JOIN face_cameras fc ON fc.id = m.face_camera_id
JOIN record_cameras rc ON rc.id = m.record_camera_id
WHERE fc.camera_ip = @snap_camera_ip;";

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

        /// <summary>查询已绑定列表：mappingId, faceCameraId, faceCameraIp, recordCameraId, recordCameraIp, locationName</summary>
        public async Task<List<BoundMappingRow>> ListBindingsAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT m.id, m.face_camera_id, fc.camera_ip, m.record_camera_id, rc.camera_ip, rc.location_name
FROM camera_mapping m
JOIN face_cameras fc ON fc.id = m.face_camera_id
JOIN record_cameras rc ON rc.id = m.record_camera_id
ORDER BY m.id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var list = new List<BoundMappingRow>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                list.Add(new BoundMappingRow
                {
                    Id = reader.GetInt64(0),
                    FaceCameraId = reader.GetInt64(1),
                    FaceCameraIp = reader.GetString(2),
                    RecordCameraId = reader.GetInt64(3),
                    RecordCameraIp = reader.GetString(4),
                    LocationName = reader.GetString(5)
                });
            return list;
        }

        /// <summary>新增绑定，返回映射 id。</summary>
        public async Task<long> BindAsync(long faceCameraId, long recordCameraId, CancellationToken cancellationToken)
        {
            const string sql = @"
INSERT INTO camera_mapping (face_camera_id, record_camera_id)
VALUES (@face_camera_id, @record_camera_id)
RETURNING id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("face_camera_id", faceCameraId);
            cmd.Parameters.AddWithValue("record_camera_id", recordCameraId);
            var id = await cmd.ExecuteScalarAsync(cancellationToken);
            if (id == null || id == DBNull.Value)
                throw new InvalidOperationException("绑定失败");
            return Convert.ToInt64(id);
        }

        public async Task<List<CameraBinding>> GetBindingsByFaceCameraIdAsync(long faceCameraId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, face_camera_id, record_camera_id FROM camera_mapping WHERE face_camera_id = @face_camera_id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("face_camera_id", faceCameraId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var bindings = new List<CameraBinding>();
            while (await reader.ReadAsync(cancellationToken))
            {
                bindings.Add(new CameraBinding
                {
                    Id = reader.GetInt64(0),
                    FaceCameraId = reader.GetInt64(1),
                    RecordCameraId = reader.GetInt64(2)
                });
            }
            return bindings;
        }

        public async Task<CameraBinding?> GetBindingByFaceCameraIdAsync(long faceCameraId, CancellationToken cancellationToken)
        {
            var bindings = await GetBindingsByFaceCameraIdAsync(faceCameraId, cancellationToken);
            return bindings.FirstOrDefault();
        }

        public async Task<List<CameraBinding>> GetBindingsByRecordCameraIdAsync(long recordCameraId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, face_camera_id, record_camera_id FROM camera_mapping WHERE record_camera_id = @record_camera_id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("record_camera_id", recordCameraId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var bindings = new List<CameraBinding>();
            while (await reader.ReadAsync(cancellationToken))
            {
                bindings.Add(new CameraBinding
                {
                    Id = reader.GetInt64(0),
                    FaceCameraId = reader.GetInt64(1),
                    RecordCameraId = reader.GetInt64(2)
                });
            }
            return bindings;
        }

        public async Task<CameraBinding?> GetBindingByRecordCameraIdAsync(long recordCameraId, CancellationToken cancellationToken)
        {
            var bindings = await GetBindingsByRecordCameraIdAsync(recordCameraId, cancellationToken);
            return bindings.FirstOrDefault();
        }

        public async Task<CameraBinding?> GetBindingByMappingIdAsync(long mappingId, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, face_camera_id, record_camera_id FROM camera_mapping WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", mappingId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return new CameraBinding
            {
                Id = reader.GetInt64(0),
                FaceCameraId = reader.GetInt64(1),
                RecordCameraId = reader.GetInt64(2)
            };
        }

        public async Task<bool> UnbindByMappingIdAsync(long mappingId, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM camera_mapping WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", mappingId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UnbindByFaceCameraIdAsync(long faceCameraId, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM camera_mapping WHERE face_camera_id = @face_camera_id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("face_camera_id", faceCameraId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UnbindByRecordCameraIdAsync(long recordCameraId, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM camera_mapping WHERE record_camera_id = @record_camera_id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("record_camera_id", recordCameraId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UnbindByFaceAndRecordCameraIdAsync(long faceCameraId, long recordCameraId, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM camera_mapping WHERE face_camera_id = @face_camera_id AND record_camera_id = @record_camera_id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("face_camera_id", faceCameraId);
            cmd.Parameters.AddWithValue("record_camera_id", recordCameraId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateRecordCameraIdAsync(long mappingId, long newRecordCameraId, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE camera_mapping SET record_camera_id = @record_camera_id WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", mappingId);
            cmd.Parameters.AddWithValue("record_camera_id", newRecordCameraId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public sealed class BoundMappingRow
        {
            public long Id { get; set; }
            public long FaceCameraId { get; set; }
            public string FaceCameraIp { get; set; } = "";
            public long RecordCameraId { get; set; }
            public string RecordCameraIp { get; set; } = "";
            public string LocationName { get; set; } = "";
        }
    }
}
