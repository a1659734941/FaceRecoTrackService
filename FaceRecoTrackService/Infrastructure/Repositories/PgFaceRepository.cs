using System;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgFaceRepository
    {
        private readonly string _connectionString;

        public PgFaceRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertFaceAsync(FacePerson person, CancellationToken cancellationToken)
        {
            const string sql = @"
INSERT INTO face_persons(id, user_name, ip, description, is_test, image_base64, created_at)
VALUES (@id, @user_name, @ip, @description, @is_test, @image_base64, @created_at);";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", person.Id);
            cmd.Parameters.AddWithValue("user_name", person.UserName);
            cmd.Parameters.AddWithValue("ip", person.Ip);
            cmd.Parameters.AddWithValue("description", (object?)person.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("is_test", person.IsTest);
            cmd.Parameters.AddWithValue("image_base64", (object?)person.ImageBase64 ?? DBNull.Value);
            cmd.Parameters.AddWithValue("created_at", person.CreatedAtUtc);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<long> GetFaceCountAsync(CancellationToken cancellationToken)
        {
            const string sql = "SELECT COUNT(*) FROM face_persons;";
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        public async Task<FacePerson?> GetFaceByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT id, user_name, ip, description, is_test, image_base64, created_at
FROM face_persons WHERE id = @id;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) return null;

            return new FacePerson
            {
                Id = reader.GetGuid(0),
                UserName = reader.GetString(1),
                Ip = reader.GetString(2),
                Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                IsTest = reader.GetBoolean(4),
                ImageBase64 = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAtUtc = reader.GetDateTime(6)
            };
        }

        public async Task<bool> DeleteFaceByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            const string sql = "DELETE FROM face_persons WHERE id = @id;";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            var rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return rows > 0;
        }
    }
}
