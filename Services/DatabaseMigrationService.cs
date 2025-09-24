
// Services/DatabaseMigrationService.cs
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Services
{
    public class DatabaseMigrationService : IDatabaseMigrationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseMigrationService> _logger;

        public DatabaseMigrationService(AppDbContext context, ILogger<DatabaseMigrationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MigrationResult> MigrateDatabase()
        {
            var result = new MigrationResult();

            try
            {
                result.Details.Add("Starting database migration...");

                var migrations = new[]
                {
                    "ALTER TABLE Users ADD COLUMN Salt TEXT",
                    "ALTER TABLE Users ADD COLUMN CurrentSessionToken TEXT",
                    "ALTER TABLE Users ADD COLUMN SessionExpiresAt TEXT",
                    "ALTER TABLE Users ADD COLUMN LastLoginAt TEXT",
                    "ALTER TABLE Users ADD COLUMN LastLoginIP TEXT",
                    "ALTER TABLE Users ADD COLUMN Phone TEXT",
                    "ALTER TABLE Users ADD COLUMN PhoneType TEXT",
                    "ALTER TABLE Users ADD COLUMN Phone2 TEXT",
                    "ALTER TABLE Users ADD COLUMN Phone2Type TEXT",
                    "ALTER TABLE Users ADD COLUMN Address TEXT",
                    "ALTER TABLE Users ADD COLUMN City TEXT",
                    "ALTER TABLE Users ADD COLUMN State TEXT",
                    "ALTER TABLE Users ADD COLUMN ZipCode TEXT",
                    "ALTER TABLE Users ADD COLUMN UserId TEXT",
                    "ALTER TABLE Users ADD COLUMN ConfirmationToken TEXT",
                    "ALTER TABLE Users ADD COLUMN ConfirmedAt TEXT"
                };

                foreach (var migration in migrations)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(migration);
                        result.Details.Add($"✓ Executed: {migration}");
                    }
                    catch (Exception ex)
                    {
                        var columnName = migration.Split(' ').Last();
                        result.Details.Add($"- Skipped {columnName} (may already exist)");
                        _logger.LogInformation("Migration skipped: {Message}", ex.Message);
                    }
                }

                // Update users without salt
                try
                {
                    var updateCount = await _context.Database.ExecuteSqlRawAsync(@"
                        UPDATE Users 
                        SET Salt = 'TEMP_SALT_NEEDS_RESET' 
                        WHERE PasswordHash IS NOT NULL 
                        AND (Salt IS NULL OR Salt = '')");

                    result.Details.Add($"✓ Updated {updateCount} users with temporary salt marker");
                }
                catch (Exception ex)
                {
                    result.Details.Add($"- Could not update users: {ex.Message}");
                }

                // Test database accessibility
                try
                {
                    var userCount = await _context.Users.CountAsync();
                    result.Details.Add($"✓ Database accessible - found {userCount} users");
                }
                catch (Exception ex)
                {
                    result.Details.Add($"✗ Error accessing users: {ex.Message}");
                }

                // Verify columns exist
                result.ColumnsExist = await CheckColumnsExist();
                if (result.ColumnsExist)
                {
                    result.Details.Add("✓ Session columns verified successfully!");
                }
                else
                {
                    result.Details.Add("⚠ Session columns may not be accessible yet");
                }

                result.Success = true;
                result.Message = "Migration completed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed");
                result.Success = false;
                result.Message = "Migration encountered an error";
                result.Error = ex.Message;
            }

            return result;
        }

        public async Task<DatabaseStatus> CheckDatabaseStatus()
        {
            try
            {
                var userCount = await _context.Users.CountAsync();
                var hasSessionColumns = await CheckColumnsExist();

                var sampleUser = await _context.Users
                    .Select(u => new
                    {
                        HasEmail = !string.IsNullOrEmpty(u.Email),
                        HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                        Role = u.Role
                    })
                    .FirstOrDefaultAsync();

                return new DatabaseStatus
                {
                    Status = "connected",
                    UserCount = userCount,
                    HasSessionColumns = hasSessionColumns,
                    SampleUser = sampleUser,
                    Recommendation = hasSessionColumns
                        ? "Database is ready for session management"
                        : "Run migration to add session columns"
                };
            }
            catch (Exception ex)
            {
                return new DatabaseStatus
                {
                    Status = "error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> CheckColumnsExist()
        {
            try
            {
                var query = _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Salt,
                        u.CurrentSessionToken,
                        u.SessionExpiresAt
                    })
                    .Take(1);

                await query.FirstOrDefaultAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}