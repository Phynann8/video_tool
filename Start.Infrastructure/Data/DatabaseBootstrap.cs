using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Start.Infrastructure.Data
{
    public class DatabaseBootstrap
    {
        private readonly string _connectionString;

        public DatabaseBootstrap(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task SetupAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                CREATE TABLE IF NOT EXISTS Jobs (
                    Id TEXT PRIMARY KEY,
                    Url TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    JsonData TEXT NOT NULL
                );
            ";

            await connection.ExecuteAsync(sql);
        }
    }
}
