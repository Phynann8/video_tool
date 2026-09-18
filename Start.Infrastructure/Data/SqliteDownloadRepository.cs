using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Start.Core.Interfaces;
using Start.Core.Models;

namespace Start.Infrastructure.Data
{
    public class SqliteDownloadRepository : IDownloadRepository
    {
        private readonly string _connectionString;

        public SqliteDownloadRepository(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task AddJobAsync(DownloadJob job)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = "INSERT INTO Jobs (Id, Url, Status, CreatedAt, JsonData) VALUES (@Id, @Url, @Status, @CreatedAt, @JsonData)";
            
            await connection.ExecuteAsync(sql, new 
            {
                Id = job.Id.ToString(),
                job.Url,
                job.Status,
                CreatedAt = job.CreatedAt.ToString("O"), // ISO 8601
                JsonData = JsonConvert.SerializeObject(job)
            });
        }

        public async Task UpdateJobAsync(DownloadJob job)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = "UPDATE Jobs SET Status = @Status, JsonData = @JsonData WHERE Id = @Id";
            
            await connection.ExecuteAsync(sql, new 
            {
                Id = job.Id.ToString(),
                job.Status,
                JsonData = JsonConvert.SerializeObject(job)
            });
        }

        public async Task<DownloadJob?> GetJobAsync(Guid id)
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = "SELECT JsonData FROM Jobs WHERE Id = @Id";
            
            var jsonData = await connection.QueryFirstOrDefaultAsync<string>(sql, new { Id = id.ToString() });
            
            return jsonData != null ? JsonConvert.DeserializeObject<DownloadJob>(jsonData) : null;
        }

        public async Task<IEnumerable<DownloadJob>> GetAllJobsAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            var sql = "SELECT JsonData FROM Jobs ORDER BY CreatedAt DESC";
            
            var jsonDatas = await connection.QueryAsync<string>(sql);
            
            var jobs = new List<DownloadJob>();
            foreach(var json in jsonDatas)
            {
                var job = JsonConvert.DeserializeObject<DownloadJob>(json);
                if(job != null) jobs.Add(job);
            }
            return jobs;
        }

        public async Task<IEnumerable<DownloadJob>> GetJobsByStatusAsync(IEnumerable<JobStatus> statuses, int limit = 100)
        {
            var statusInts = statuses.Select(s => (int)s).ToList();
            if (statusInts.Count == 0) return Enumerable.Empty<DownloadJob>();

            using var connection = new SqliteConnection(_connectionString);
            var sql = "SELECT JsonData FROM Jobs WHERE Status IN @Statuses ORDER BY CreatedAt DESC LIMIT @Limit";
            
            var jsonDatas = await connection.QueryAsync<string>(sql, new { Statuses = statusInts, Limit = limit });
            
            var jobs = new List<DownloadJob>();
            foreach (var json in jsonDatas)
            {
                var job = JsonConvert.DeserializeObject<DownloadJob>(json);
                if (job != null) jobs.Add(job);
            }
            return jobs;
        }
    }
}
