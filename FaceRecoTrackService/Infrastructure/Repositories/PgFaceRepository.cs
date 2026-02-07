using System;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Models;
using Npgsql;
using NpgsqlTypes;

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
            const string sql = @"INSERT INTO face_persons(id, user_name, ip, description, image_base64, face_vector, created_at)
            VALUES (@id, @user_name, @ip, @description, @image_base64, @face_vector, @created_at);";

            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", person.Id);
            cmd.Parameters.AddWithValue("user_name", person.UserName);
            cmd.Parameters.AddWithValue("ip", person.Ip);
            cmd.Parameters.AddWithValue("description", (object?)person.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("image_base64", (object?)person.ImageBase64 ?? DBNull.Value);
            var vectorParam = cmd.Parameters.Add("face_vector", NpgsqlDbType.Array | NpgsqlDbType.Real);
            vectorParam.Value = (object?)person.FaceVector ?? DBNull.Value;
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
            SELECT id, user_name, ip, description, image_base64, created_at
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
                ImageBase64 = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAtUtc = reader.GetDateTime(5)
            };
        }

        public async Task<IReadOnlyList<FaceVectorRecord>> GetAllFaceVectorsAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT id, user_name, face_vector
FROM face_persons
WHERE face_vector IS NOT NULL;";

            var results = new List<FaceVectorRecord>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var vector = reader.IsDBNull(2) ? Array.Empty<float>() : (float[])reader.GetValue(2);
                results.Add(new FaceVectorRecord
                {
                    Id = reader.GetGuid(0),
                    UserName = reader.GetString(1),
                    Vector = vector
                });
            }

            return results;
        }

        public async Task<IReadOnlyList<Guid>> GetAllFaceIdsAsync(CancellationToken cancellationToken)
        {
            const string sql = "SELECT id FROM face_persons;";

            var results = new List<Guid>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(reader.GetGuid(0));
            }

            return results;
        }

        public async Task<IReadOnlyList<FacePerson>> GetAllFacesAsync(CancellationToken cancellationToken)
        {
            const string sql = @"
SELECT id, user_name, ip, description, image_base64, created_at
FROM face_persons;";

            var results = new List<FacePerson>();
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(sql, conn);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new FacePerson
                {
                    Id = reader.GetGuid(0),
                    UserName = reader.GetString(1),
                    Ip = reader.GetString(2),
                    Description = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ImageBase64 = reader.IsDBNull(4) ? null : reader.GetString(4),
                    CreatedAtUtc = reader.GetDateTime(5)
                });
            }

            return results;
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
