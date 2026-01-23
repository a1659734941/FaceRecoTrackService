using System.Data;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Database
{
    public class PgDbContext
    {
        private readonly string _connectionString;

        public PgDbContext(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
