using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgRecordCameraRepository
    {
        private readonly string _connectionString;

        public PgRecordCameraRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<RecordCamera> AddAsync(string cameraIp, string locationName, CancellationToken cancellationToken)
        {
            const string sql = @"
INSERT INTO record_cameras (camera_ip, location_name)
VALUES (@camera_ip, @location_name)
RETURNING id, camera_ip, location_name, created_at;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            cmd.Parameters.AddWithValue("location_name", locationName);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Insert record_cameras did not return row.");
            return RowToRecordCamera(reader);
        }

        public async Task<RecordCamera?> GetByIdAsync(long id, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, location_name, created_at FROM record_cameras WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return RowToRecordCamera(reader);
        }

        public async Task<RecordCamera?> GetByIpAsync(string cameraIp, CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, location_name, created_at FROM record_cameras WHERE camera_ip = @camera_ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;
            return RowToRecordCamera(reader);
        }

        public async Task<List<RecordCamera>> ListAllAsync(CancellationToken cancellationToken)
        {
            const string sql = "SELECT id, camera_ip, location_name, created_at FROM record_cameras ORDER BY id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var list = new List<RecordCamera>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                list.Add(RowToRecordCamera(reader));
            return list;
        }

        public async Task<bool> UpdateByIdAsync(long id, string? cameraIp, string? locationName, CancellationToken cancellationToken)
        {
            if (cameraIp == null && locationName == null) return true;
            var set = new List<string>();
            if (cameraIp != null) set.Add("camera_ip = @camera_ip");
            if (locationName != null) set.Add("location_name = @location_name");
            var sql = $"UPDATE record_cameras SET {string.Join(", ", set)} WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            if (cameraIp != null) cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            if (locationName != null) cmd.Parameters.AddWithValue("location_name", locationName);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateByIpAsync(string ip, string? cameraIp, string? locationName, CancellationToken cancellationToken)
        {
            if (cameraIp == null && locationName == null) return true;
            var set = new List<string>();
            if (cameraIp != null) set.Add("camera_ip = @camera_ip");
            if (locationName != null) set.Add("location_name = @location_name");
            var sql = $"UPDATE record_cameras SET {string.Join(", ", set)} WHERE camera_ip = @ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("ip", ip);
            if (cameraIp != null) cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            if (locationName != null) cmd.Parameters.AddWithValue("location_name", locationName);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM record_cameras WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        public async Task<bool> DeleteByIpAsync(string cameraIp, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM record_cameras WHERE camera_ip = @camera_ip;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("camera_ip", cameraIp);
            return await cmd.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        private static RecordCamera RowToRecordCamera(NpgsqlDataReader reader)
        {
            return new RecordCamera
            {
                Id = reader.GetInt64(0),
                CameraIp = reader.GetString(1),
                LocationName = reader.GetString(2),
                CreatedAt = reader.GetDateTime(3)
            };
        }
    }
}
