using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgFaceCameraRepository
    {
        private readonly string _connectionString;

        public PgFaceCameraRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<FaceCamera> AddAsync(string cameraIp, string? description, CancellationToken cancellationToken)
        {
            const string sql = @"
INSERT INTO face_cameras (camera_ip, description)
VALUES (@camera_ip, @description)
RETURNING id, camera_ip, description, created_at;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Insert face_cameras did not return row.");
            return RowToFaceCamera(reader);
        }

        public async Task<FaceCamera?> GetByIdAsync(long id, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, description, created_at FROM face_cameras WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return RowToFaceCamera(reader);
        }

        public async Task<FaceCamera?> GetByIpAsync(string cameraIp, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, description, created_at FROM face_cameras WHERE camera_ip = @camera_ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return RowToFaceCamera(reader);
        }

        public async Task<List<FaceCamera>> ListAllAsync(CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, description, created_at FROM face_cameras ORDER BY id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var list = new List<FaceCamera>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                list.Add(RowToFaceCamera(reader));
            return list;
        }

        public async Task<bool> UpdateByIdAsync(long id, string? cameraIp, string? description, CancellationToken cancellationToken)
        {
            if (cameraIp == null && description == null) return true;
            var set = new List<string>();
            if (cameraIp != null) set.Add("camera_ip = @camera_ip");
            if (description != null) set.Add("description = @description");
            var sql = $"UPDATE face_cameras SET {string.Join(", ", set)} WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            if (cameraIp != null) cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            if (description != null) cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateByIpAsync(string ip, string? cameraIp, string? description, CancellationToken cancellationToken)
        {
            if (cameraIp == null && description == null) return true;
            var set = new List<string>();
            if (cameraIp != null) set.Add("camera_ip = @camera_ip");
            if (description != null) set.Add("description = @description");
            var sql = $"UPDATE face_cameras SET {string.Join(", ", set)} WHERE camera_ip = @ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("ip", ip);
            if (cameraIp != null) cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            if (description != null) cmd.Parameters.AddWithValue("description", (object?)description ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM face_cameras WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteByIpAsync(string cameraIp, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM face_cameras WHERE camera_ip = @camera_ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private static FaceCamera RowToFaceCamera(NpgsqlDataReader reader)
        {
            return new FaceCamera
            {
                Id = reader.GetInt64(0),
                CameraIp = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                CreatedAt = reader.GetDateTime(3)
            };
        }
    }
}
