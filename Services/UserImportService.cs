using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Services
{
    public interface IUserImportService
    {
        Task<ImportResult> ImportUsersFromCsvAsync(Stream csvStream);
        Task<ImportResult> CreateBulkUsersAsync(List<BulkUserData> users);
    }

    public class UserImportService : IUserImportService
    {
        private readonly AppDbContext _context;
        private readonly ISecurityService _securityService;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserImportService> _logger;

        public UserImportService(
            AppDbContext context,
            ISecurityService securityService,
            IEmailService emailService,
            ILogger<UserImportService> logger)
        {
            _context = context;
            _securityService = securityService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<ImportResult> CreateBulkUsersAsync(List<BulkUserData> users)
        {
            var result = new ImportResult();

            foreach (var userData in users)
            {
                try
                {
                    // Check if user already exists
                    var existingUser = await _context.Users
                        .FirstOrDefaultAsync(u => u.Email.ToLower() == userData.Email.ToLower());

                    if (existingUser != null)
                    {
                        result.Errors.Add($"User already exists: {userData.Email}");
                        continue;
                    }

                    // Generate temporary password
                    var tempPassword = GenerateTemporaryPassword();
                    var salt = _securityService.GenerateSalt();
                    var hash = _securityService.HashPassword(tempPassword, salt);

                    // Generate unique UserId
                    var userId = await GenerateUniqueUserId();

                    var user = new User
                    {
                        Email = userData.Email.Trim(),
                        PasswordHash = hash,
                        Salt = salt,
                        FirstName = userData.FirstName?.Trim(),
                        LastName = userData.LastName?.Trim(),
                        Phone = userData.Phone?.Trim(),
                        Address = userData.Address?.Trim(),
                        City = userData.City?.Trim(),
                        State = userData.State?.Trim(),
                        ZipCode = userData.ZipCode?.Trim(),
                        Role = "Client",
                        IsConfirmed = true, // Auto-confirm imported users
                        UserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        ConfirmedAt = DateTime.UtcNow
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    // Send welcome email with temporary password
                    await SendWelcomeEmail(user, tempPassword);

                    result.SuccessCount++;
                    result.CreatedUsers.Add(new CreatedUserInfo
                    {
                        Email = user.Email,
                        UserId = user.UserId,
                        TemporaryPassword = tempPassword
                    });

                    _logger.LogInformation("Bulk user created: {Email} with UserId: {UserId}",
                        user.Email, user.UserId);
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error creating user {userData.Email}: {ex.Message}");
                    _logger.LogError(ex, "Error creating bulk user: {Email}", userData.Email);
                }
            }

            return result;
        }

        public async Task<ImportResult> ImportUsersFromCsvAsync(Stream csvStream)
        {
            // Parse CSV and convert to BulkUserData list
            // Implementation depends on your CSV format
            var users = new List<BulkUserData>();

            // Example CSV parsing (you can enhance this)
            using var reader = new StreamReader(csvStream);
            string line;
            var isFirstLine = true;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (isFirstLine)
                {
                    isFirstLine = false;
                    continue; // Skip header
                }

                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    users.Add(new BulkUserData
                    {
                        Email = parts[0].Trim('"'),
                        FirstName = parts.Length > 1 ? parts[1].Trim('"') : "",
                        LastName = parts.Length > 2 ? parts[2].Trim('"') : "",
                        Phone = parts.Length > 3 ? parts[3].Trim('"') : null,
                        Address = parts.Length > 4 ? parts[4].Trim('"') : null,
                        City = parts.Length > 5 ? parts[5].Trim('"') : null,
                        State = parts.Length > 6 ? parts[6].Trim('"') : null,
                        ZipCode = parts.Length > 7 ? parts[7].Trim('"') : null
                    });
                }
            }

            return await CreateBulkUsersAsync(users);
        }

        private string GenerateTemporaryPassword()
        {
            // Generate a secure temporary password
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray()) + "!";
        }

        private async Task<string> GenerateUniqueUserId()
        {
            string userId;
            do
            {
                var random = new Random();
                var numbers = random.Next(1000, 9999);
                var letters = new string(Enumerable.Range(0, 3)
                    .Select(_ => (char)random.Next('A', 'Z' + 1)).ToArray());
                userId = $"{numbers}-{letters}";
            } while (await _context.Users.AnyAsync(u => u.UserId == userId));

            return userId;
        }

        private async Task SendWelcomeEmail(User user, string tempPassword)
        {
            try
            {
                var subject = "Account Migrated - Welcome to Autumn Ridge LLC";
                var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); 
                   color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border: 1px solid #e0e0e0; 
                    border-radius: 0 0 10px 10px; }}
        .credentials {{ background: #fff3cd; border: 1px solid #ffeaa7; padding: 20px; 
                       margin: 20px 0; border-radius: 8px; text-align: center; }}
        .temp-password {{ font-size: 18px; font-weight: bold; color: #856404; 
                         letter-spacing: 1px; background: #fff; padding: 10px; 
                         border-radius: 5px; margin: 10px 0; }}
        .btn {{ display: inline-block; padding: 14px 30px; background: #667eea; 
                color: white; text-decoration: none; border-radius: 25px; 
                margin: 20px 0; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Your Account Has Been Migrated</h1>
            <p style='margin: 0; opacity: 0.9;'>Welcome to the New Autumn Ridge LLC System</p>
        </div>
        <div class='content'>
            <h2>Hello {user.FirstName} {user.LastName}!</h2>
            <p>Your account has been successfully migrated to our new system.</p>
            
            <div class='credentials'>
                <h3>Your Login Credentials</h3>
                <p><strong>Email:</strong> {user.Email}</p>
                <p><strong>User ID:</strong> {user.UserId}</p>
                <p><strong>Temporary Password:</strong></p>
                <div class='temp-password'>{tempPassword}</div>
                <p style='color: #856404; font-size: 14px;'>
                    <strong>Important:</strong> Please change this password after your first login
                </p>
            </div>

            <p style='text-align: center;'>
                <a href='https://your-railway-app.up.railway.app' class='btn'>Login Now</a>
            </p>

            <p><strong>Next Steps:</strong></p>
            <ol>
                <li>Click the login button above</li>
                <li>Use your email and temporary password</li>
                <li>Change your password immediately</li>
                <li>Update your profile information</li>
            </ol>
            
            <p>Best regards,<br>The Autumn Ridge LLC Team</p>
        </div>
    </div>
</body>
</html>";

                var success = await _emailService.SendEmailAsync(user.Email, subject, htmlBody);

                if (!success)
                {
                    _logger.LogError("Failed to send migration email to {Email}", user.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception sending migration email to {Email}", user.Email);
            }
        }
    }

    public class BulkUserData
    {
        public string Email { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
    }

    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<CreatedUserInfo> CreatedUsers { get; set; } = new();
    }

    public class CreatedUserInfo
    {
        public string Email { get; set; } = string.Empty;
        public string? UserId { get; set; }
        public string TemporaryPassword { get; set; } = string.Empty;
    }
}