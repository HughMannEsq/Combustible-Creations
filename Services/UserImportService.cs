// Services/UserImportService.cs
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Services.Helpers;
using System.Text;

namespace AutumnRidgeUSA.Services
{
    public class UserImportService : IUserImportService
    {
        private readonly AppDbContext _context;
        private readonly ISecurityService _securityService;
        private readonly ILogger<UserImportService> _logger;
        private readonly ICsvParsingHelper _csvHelper;
        private readonly IExcelParsingHelper _excelHelper;

        public UserImportService(
            AppDbContext context,
            ISecurityService securityService,
            ILogger<UserImportService> logger,
            ICsvParsingHelper csvHelper,
            IExcelParsingHelper excelHelper)
        {
            _context = context;
            _securityService = securityService;
            _logger = logger;
            _csvHelper = csvHelper;
            _excelHelper = excelHelper;
        }

        public async Task<UserImportResult> CreateInitialUsers()
        {
            var existingCount = await _context.Users.CountAsync();
            if (existingCount > 0)
            {
                return new UserImportResult
                {
                    Success = false,
                    Message = $"Database already has {existingCount} users. Use reset endpoint instead."
                };
            }

            var users = new List<User>
            {
                await CreateUser("admin@autumnridge.com", "Admin123!", "Admin", "User", "Admin", "ADM-001"),
                await CreateUser("client@example.com", "Client123!", "John", "Client", "Client", "CLT-001")
            };

            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();

            return new UserImportResult
            {
                Success = true,
                Message = "Initial users created successfully!",
                Users = users.Select(u => new { u.Email, password = "***", u.Role }).ToList<object>(),
                Instructions = "You can now login with any of these credentials"
            };
        }

        public async Task<UserImportResult> ResetAndCreateUsers()
        {
            await ClearAllUsers();

            var users = new List<User>
            {
                await CreateUser("admin@autumnridge.com", "Admin123!", "Admin", "User", "Admin", "ADM-001")
            };

            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();

            return new UserImportResult
            {
                Success = true,
                Message = "Database reset and users created successfully",
                Users = users.Select(u => new { u.Email, password = "Admin123!", u.Role }).ToList<object>(),
                Note = "All users have properly salted and hashed passwords"
            };
        }

        public async Task<UserImportResult> ImportUsersFromCsv(IFormFile csvFile)
        {
            await ClearAllUsers();

            var result = new UserImportResult();
            var users = new List<User>();

            try
            {
                using var stream = csvFile.OpenReadStream();
                using var reader = new StreamReader(stream);

                var parsedData = await _csvHelper.ParseCsvFile(reader);
                if (!parsedData.IsValid)
                {
                    return new UserImportResult
                    {
                        Success = false,
                        Message = parsedData.ErrorMessage ?? "Invalid CSV format"
                    };
                }

                foreach (var userData in parsedData.UserData)
                {
                    try
                    {
                        if (!IsValidUserData(userData, out var validationError))
                        {
                            result.Errors.Add($"Row {userData.RowNumber}: {validationError}");
                            continue;
                        }

                        var user = await CreateUserFromData(userData);
                        users.Add(user);

                        result.Users.Add(new
                        {
                            email = user.Email,
                            role = user.Role,
                            userId = user.UserId,
                            name = $"{user.FirstName} {user.LastName}"
                        });

                        _logger.LogInformation("Created user from CSV: {Email} with role: {Role}", user.Email, user.Role);
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {userData.RowNumber}: {ex.Message}");
                        _logger.LogError(ex, "Error processing CSV row {RowNumber}", userData.RowNumber);
                    }
                }

                _context.Users.AddRange(users);
                await _context.SaveChangesAsync();

                result.Success = true;
                result.Message = $"Successfully created {users.Count} users from CSV";
                result.SuccessCount = users.Count;
                result.ErrorCount = result.Errors.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file");
                result.Success = false;
                result.Message = "Error processing CSV file";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        public async Task<UserImportResult> ImportUsersFromExcel(IFormFile excelFile)
        {
            await ClearAllUsers();

            var result = new UserImportResult();
            var users = new List<User>();

            try
            {
                using var stream = excelFile.OpenReadStream();
                var parsedData = await _excelHelper.ParseExcelFile(stream);

                if (!parsedData.IsValid)
                {
                    return new UserImportResult
                    {
                        Success = false,
                        Message = parsedData.ErrorMessage ?? "Invalid Excel format"
                    };
                }

                foreach (var userData in parsedData.UserData)
                {
                    try
                    {
                        if (!IsValidUserData(userData, out var validationError))
                        {
                            result.Errors.Add($"Row {userData.RowNumber}: {validationError}");
                            continue;
                        }

                        var user = await CreateUserFromData(userData);
                        users.Add(user);

                        result.Users.Add(new
                        {
                            email = user.Email,
                            role = user.Role,
                            userId = user.UserId,
                            name = $"{user.FirstName} {user.LastName}"
                        });

                        _logger.LogInformation("Created user from Excel: {Email} with role: {Role}", user.Email, user.Role);

                        // Save in batches to avoid memory issues
                        if (users.Count % 50 == 0)
                        {
                            _context.Users.AddRange(users);
                            await _context.SaveChangesAsync();
                            users.Clear();
                            _logger.LogInformation("Saved batch of 50 users. Total so far: {Count}", result.Users.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Errors.Add($"Row {userData.RowNumber}: {ex.Message}");
                        _logger.LogError(ex, "Error processing Excel row {RowNumber}", userData.RowNumber);
                    }
                }

                // Save remaining users
                if (users.Any())
                {
                    _context.Users.AddRange(users);
                    await _context.SaveChangesAsync();
                }

                result.Success = true;
                result.Message = $"Successfully created {result.Users.Count} users from Excel file";
                result.SuccessCount = result.Users.Count;
                result.ErrorCount = result.Errors.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Excel file");
                result.Success = false;
                result.Message = "Error processing Excel file";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        public async Task<List<UserSummary>> GetAllUsers()
        {
            return await _context.Users
                .Select(u => new UserSummary
                {
                    Email = u.Email,
                    Role = u.Role,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    UserId = u.UserId,
                    IsConfirmed = u.IsConfirmed,
                    HasValidSalt = !string.IsNullOrEmpty(u.Salt) && u.Salt != "TEMP_SALT_NEEDS_RESET"
                })
                .ToListAsync();
        }

        private async Task ClearAllUsers()
        {
            var existingUsers = await _context.Users.ToListAsync();
            _context.Users.RemoveRange(existingUsers);
            await _context.SaveChangesAsync();
        }

        private async Task<User> CreateUser(string email, string password, string firstName, string lastName, string role, string userId)
        {
            var salt = _securityService.GenerateSalt();
            var hash = _securityService.HashPassword(password, salt);

            return new User
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                PasswordHash = hash,
                Salt = salt,
                Role = role,
                IsConfirmed = true,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow
            };
        }

        private async Task<User> CreateUserFromData(UserData userData)
        {
            var salt = _securityService.GenerateSalt();
            var hash = _securityService.HashPassword(userData.Password!, salt);
            var userId = await GenerateUniqueUserId();

            return new User
            {
                Email = userData.Email!,
                FirstName = userData.FirstName ?? "Unknown",
                LastName = userData.LastName ?? "User",
                PasswordHash = hash,
                Salt = salt,
                Role = userData.Role!,
                IsConfirmed = true,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow,
                Phone = userData.Phone,
                PhoneType = ValidatePhoneType(userData.PhoneType),
                Phone2 = userData.Phone2,
                Phone2Type = ValidatePhoneType(userData.Phone2Type),
                Address = userData.Address,
                City = userData.City,
                State = userData.State,
                ZipCode = userData.ZipCode
            };
        }

        private string? ValidatePhoneType(string? phoneType)
        {
            if (string.IsNullOrEmpty(phoneType)) return null;

            return phoneType.ToLower() switch
            {
                "cell" or "mobile" or "c" => "Cell",
                "home" or "h" => "Home",
                "work" or "office" or "w" => "Work",
                _ => null
            };
        }

        private bool IsValidUserData(UserData userData, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrEmpty(userData.Email))
            {
                error = "Email is required";
                return false;
            }

            if (string.IsNullOrEmpty(userData.Password))
            {
                error = "Password is required";
                return false;
            }

            if (!IsValidRole(userData.Role))
            {
                error = $"Invalid role '{userData.Role}'. Must be Admin, Client, or Manager";
                return false;
            }

            return true;
        }

        private bool IsValidRole(string? role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            var validRoles = new[] { "Admin", "Client", "Manager" };
            return validRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
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
    }
}