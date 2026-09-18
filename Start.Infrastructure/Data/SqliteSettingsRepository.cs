using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Data
{
    public sealed class SqliteSettingsRepository : ISettingsRepository
    {
        private readonly string _connectionString;

        public SqliteSettingsRepository(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        private async Task EnsureTableCreatedAsync(SqliteConnection connection)
        {
            await connection.ExecuteAsync(
                "CREATE TABLE IF NOT EXISTS AppSettings (Id INTEGER PRIMARY KEY, JsonData TEXT NOT NULL);");
        }

        public async Task<DownloadProcessingSettings> LoadAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await EnsureTableCreatedAsync(connection);
            var json = await connection.QueryFirstOrDefaultAsync<string>(
                "SELECT JsonData FROM AppSettings WHERE Id = 1");
            return string.IsNullOrWhiteSpace(json)
                ? new DownloadProcessingSettings()
                : JsonConvert.DeserializeObject<DownloadProcessingSettings>(json)
                    ?? new DownloadProcessingSettings();
        }

        public async Task SaveAsync(DownloadProcessingSettings settings)
        {
            using var connection = new SqliteConnection(_connectionString);
            await EnsureTableCreatedAsync(connection);
            await connection.ExecuteAsync(
                "INSERT INTO AppSettings (Id, JsonData) VALUES (1, @JsonData) " +
                "ON CONFLICT(Id) DO UPDATE SET JsonData = excluded.JsonData",
                new { JsonData = JsonConvert.SerializeObject(settings) });
        }
    }
}