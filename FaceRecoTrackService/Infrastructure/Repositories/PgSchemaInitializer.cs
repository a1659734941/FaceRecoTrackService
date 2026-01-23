using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Repositories
{
    public class PgSchemaInitializer
    {
        private readonly string _connectionString;

        public PgSchemaInitializer(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            const string sql = @"
CREATE TABLE IF NOT EXISTS face_persons (
    id uuid PRIMARY KEY,
    user_name text NOT NULL,
    ip text NOT NULL,
    description text,
    is_test boolean NOT NULL,
    image_base64 text,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS camera_mapping (
    snap_camera_ip text PRIMARY KEY,
    record_camera_ip text NOT NULL,
    room_name text
);

CREATE TABLE IF NOT EXISTS track_records (
    id bigserial PRIMARY KEY,
    person_id uuid NOT NULL,
    snap_time timestamptz NOT NULL,
    snap_location text,
    snap_camera_ip text NOT NULL,
    record_camera_ip text,
    record_start_time timestamptz NOT NULL,
    record_end_time timestamptz,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_track_person_time ON track_records(person_id, snap_time DESC);
";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
