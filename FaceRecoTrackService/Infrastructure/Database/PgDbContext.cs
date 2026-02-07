using System.Data;
using Npgsql;

namespace FaceRecoTrackService.Infrastructure.Database
{
    public class PgDbContext
    {
        private readonly string _connectionString;

        public PgDbContext(string connectionString)
        {
            // Optimize connection string for better pooling
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            builder.Pooling = true;
            builder.MinPoolSize = 5;
            builder.MaxPoolSize = 20;
            builder.ConnectionIdleLifetime = 300;
            builder.ConnectionPruningInterval = 60;
            builder.CommandTimeout = 30;
            _connectionString = builder.ToString();
        }

        public IDbConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        public async Task<NpgsqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
