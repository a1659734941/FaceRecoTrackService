using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgTrackRepository
    {
        private readonly string _connectionString;

        public PgTrackRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertTrackAsync(TrackRecord record, CancellationToken cancellationToken)
        {
            const string sql = @"
INSERT INTO track_records(person_id, snap_time, snap_location, snap_camera_ip, record_camera_ip, record_start_time, record_end_time, created_at)
VALUES (@person_id, @snap_time, @snap_location, @snap_camera_ip, @record_camera_ip, @record_start_time, @record_end_time, @created_at);";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("person_id", record.PersonId);
            cmd.Parameters.AddWithValue("snap_time", record.SnapTimeUtc);
            cmd.Parameters.AddWithValue("snap_location", (object?)record.SnapLocation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("snap_camera_ip", record.SnapCameraIp);
            cmd.Parameters.AddWithValue("record_camera_ip", (object?)record.RecordCameraIp ?? DBNull.Value);
            cmd.Parameters.AddWithValue("record_start_time", record.RecordStartTimeUtc);
            cmd.Parameters.AddWithValue("record_end_time", (object?)record.RecordEndTimeUtc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_at", record.CreatedAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<TrackRecord?> GetLatestTrackAsync(Guid personId, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT id, person_id, snap_time, snap_location, snap_camera_ip, record_camera_ip, record_start_time, record_end_time, created_at
FROM track_records
WHERE person_id = @person_id
ORDER BY snap_time DESC
LIMIT 1;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("person_id", personId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new TrackRecord
            {
                Id = reader.GetInt64(0),
                PersonId = reader.GetGuid(1),
                SnapTimeUtc = reader.GetDateTime(2),
                SnapLocation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                SnapCameraIp = reader.GetString(4),
                RecordCameraIp = reader.IsDBNull(5) ? "" : reader.GetString(5),
                RecordStartTimeUtc = reader.GetDateTime(6),
                RecordEndTimeUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                CreatedAtUtc = reader.GetDateTime(8)
            };
        }

        public async Task UpdateTrackEndTimeAsync(long id, DateTime endTimeUtc, CancellationToken cancellationToken)
        {
            const string sql = "UPDATE track_records SET record_end_time = @end_time WHERE id = @id;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("end_time", endTimeUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<(List<TrackRecord> Items, long Total)> GetTracksByPersonAsync(
            Guid personId,
            int pageNum,
            int pageSize,
            CancellationToken cancellationToken)
        {
            const string countSql = "SELECT COUNT(*) FROM track_records WHERE person_id = @person_id;";
            const string listSql = @"
SELECT id, person_id, snap_time, snap_location, snap_camera_ip, record_camera_ip, record_start_time, record_end_time, created_at
FROM track_records
WHERE person_id = @person_id
ORDER BY snap_time DESC
OFFSET @offset LIMIT @limit;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var countCmd = new NpgsqlCommand(countSql, conn);
            countCmd.Parameters.AddWithValue("person_id", personId);
            var total = Convert.ToInt64(await countCmd.ExecuteScalarAsync(cancellationToken));

            var items = new List<TrackRecord>();
            await using var listCmd = new NpgsqlCommand(listSql, conn);
            listCmd.Parameters.AddWithValue("person_id", personId);
            listCmd.Parameters.AddWithValue("offset", (pageNum - 1) * pageSize);
            listCmd.Parameters.AddWithValue("limit", pageSize);
            await using var reader = await listCmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(new TrackRecord
                {
                    Id = reader.GetInt64(0),
                    PersonId = reader.GetGuid(1),
                    SnapTimeUtc = reader.GetDateTime(2),
                    SnapLocation = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    SnapCameraIp = reader.GetString(4),
                    RecordCameraIp = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    RecordStartTimeUtc = reader.GetDateTime(6),
                    RecordEndTimeUtc = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
                    CreatedAtUtc = reader.GetDateTime(8)
                });
            }

            return (items, total);
        }

        public async Task<int> DeleteTracksByPersonIdAsync(Guid personId, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM track_records WHERE person_id = @person_id;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("person_id", personId);

            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
