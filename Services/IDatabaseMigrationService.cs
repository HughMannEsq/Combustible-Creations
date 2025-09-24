// Services/IDatabaseMigrationService.cs
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Services
{
    public interface IDatabaseMigrationService
    {
        Task<MigrationResult> MigrateDatabase();
        Task<DatabaseStatus> CheckDatabaseStatus();
        Task<bool> CheckColumnsExist();
    }

    public class MigrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Details { get; set; } = new();
        public bool ColumnsExist { get; set; }
        public string? Error { get; set; }
    }

    public class DatabaseStatus
    {
        public string Status { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public bool HasSessionColumns { get; set; }
        public object? SampleUser { get; set; }
        public string Database { get; set; } = "SQLite";
        public string Recommendation { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }
}
