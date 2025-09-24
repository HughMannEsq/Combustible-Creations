// Services/IUserImportService.cs
using Microsoft.AspNetCore.Http;
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Services
{
    public interface IUserImportService
    {
        Task<UserImportResult> CreateInitialUsers();
        Task<UserImportResult> ResetAndCreateUsers();
        Task<UserImportResult> ImportUsersFromCsv(IFormFile csvFile);
        Task<UserImportResult> ImportUsersFromExcel(IFormFile excelFile);
        Task<List<UserSummary>> GetAllUsers();
    }

    public class UserImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<object> Users { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public string? Instructions { get; set; }
        public string? Note { get; set; }
    }

    public class UserSummary
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public bool IsConfirmed { get; set; }
        public bool HasValidSalt { get; set; }
    }
}
