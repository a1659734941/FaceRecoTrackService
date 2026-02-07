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
    face_vector real[],
    created_at timestamptz NOT NULL DEFAULT now()
);

ALTER TABLE face_persons
    ADD COLUMN IF NOT EXISTS face_vector real[];

-- 人脸/抓拍摄像头表（id 主键）
CREATE TABLE IF NOT EXISTS face_cameras (
    id bigserial PRIMARY KEY,
    camera_ip text NOT NULL UNIQUE,
    description text,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- 录像摄像头+位置名称表（id 主键）
CREATE TABLE IF NOT EXISTS record_cameras (
    id bigserial PRIMARY KEY,
    camera_ip text NOT NULL UNIQUE,
    location_name text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
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

            await using (var cmd = new NpgsqlCommand(sql, conn))
                await cmd.ExecuteNonQueryAsync(cancellationToken);

            await EnsureCameraMappingTablesAsync(conn, cancellationToken);
        }

        /// <summary>
        /// 确保 camera_mapping 为新结构（id 主键），若存在旧表则迁移数据。
        /// </summary>
        private static async Task EnsureCameraMappingTablesAsync(
            NpgsqlConnection conn,
            CancellationToken cancellationToken)
        {
            const string checkOld = @"
SELECT 1 FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'camera_mapping' AND column_name = 'snap_camera_ip'
LIMIT 1;";
            await using (var cmd = new NpgsqlCommand(checkOld, conn))
            {
                var hasOld = await cmd.ExecuteScalarAsync(cancellationToken);
                if (hasOld != null && hasOld != DBNull.Value)
                {
                    await MigrateOldCameraMappingAsync(conn, cancellationToken);
                    return;
                }
            }

            const string createNew = @"
CREATE TABLE IF NOT EXISTS camera_mapping (
    id bigserial PRIMARY KEY,
    face_camera_id bigint NOT NULL REFERENCES face_cameras(id) ON DELETE CASCADE,
    record_camera_id bigint NOT NULL REFERENCES record_cameras(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_camera_mapping_face ON camera_mapping(face_camera_id);
CREATE INDEX IF NOT EXISTS idx_camera_mapping_record ON camera_mapping(record_camera_id);";
            await using (var cmd = new NpgsqlCommand(createNew, conn))
                await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private static async Task MigrateOldCameraMappingAsync(
            NpgsqlConnection conn,
            CancellationToken cancellationToken)
        {
            const string migrate = @"
INSERT INTO face_cameras (camera_ip) SELECT DISTINCT snap_camera_ip FROM camera_mapping ON CONFLICT (camera_ip) DO NOTHING;
INSERT INTO record_cameras (camera_ip, location_name) SELECT DISTINCT record_camera_ip, COALESCE(NULLIF(TRIM(room_name),''), '未命名') FROM camera_mapping ON CONFLICT (camera_ip) DO NOTHING;
CREATE TABLE IF NOT EXISTS camera_mapping_new (
    id bigserial PRIMARY KEY,
    face_camera_id bigint NOT NULL REFERENCES face_cameras(id) ON DELETE CASCADE,
    record_camera_id bigint NOT NULL REFERENCES record_cameras(id) ON DELETE CASCADE
);
INSERT INTO camera_mapping_new (face_camera_id, record_camera_id)
SELECT fc.id, rc.id FROM camera_mapping old
JOIN face_cameras fc ON fc.camera_ip = old.snap_camera_ip
JOIN record_cameras rc ON rc.camera_ip = old.record_camera_ip;
DROP TABLE camera_mapping;
ALTER TABLE camera_mapping_new RENAME TO camera_mapping;
CREATE INDEX IF NOT EXISTS idx_camera_mapping_face ON camera_mapping(face_camera_id);
CREATE INDEX IF NOT EXISTS idx_camera_mapping_record ON camera_mapping(record_camera_id);";
            await using var cmd = new NpgsqlCommand(migrate, conn);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
