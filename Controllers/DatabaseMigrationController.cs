// Controllers/DatabaseMigrationController.cs
// Fixed version that works with older EF Core versions

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class DatabaseMigrationController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseMigrationController> _logger;

        public DatabaseMigrationController(AppDbContext context, ILogger<DatabaseMigrationController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // TEMPORARY ENDPOINT - REMOVE AFTER MIGRATION
        [HttpGet("migrate-database")]
        public async Task<IActionResult> MigrateDatabase([FromQuery] string key)
        {
            // Simple security check - change this key!
            if (key != "your-secret-migration-key-2024")
            {
                return Unauthorized("Invalid migration key");
            }

            var results = new List<string>();

            try
            {
                results.Add("Starting database migration...");

                // Try to add each column (will fail silently if already exists)
                var migrations = new[]
                {
                    "ALTER TABLE Users ADD COLUMN Salt TEXT",
                    "ALTER TABLE Users ADD COLUMN CurrentSessionToken TEXT",
                    "ALTER TABLE Users ADD COLUMN SessionExpiresAt TEXT",
                    "ALTER TABLE Users ADD COLUMN LastLoginAt TEXT",
                    "ALTER TABLE Users ADD COLUMN LastLoginIP TEXT"
                };

                foreach (var migration in migrations)
                {
                    try
                    {
                        await _context.Database.ExecuteSqlRawAsync(migration);
                        results.Add($"✓ Executed: {migration}");
                    }
                    catch (Exception ex)
                    {
                        // Column probably already exists, which is fine
                        var columnName = migration.Split(' ').Last();
                        results.Add($"- Skipped {columnName} (may already exist)");
                        _logger.LogInformation($"Migration skipped: {ex.Message}");
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

                    results.Add($"✓ Updated {updateCount} users with temporary salt marker");
                }
                catch (Exception ex)
                {
                    results.Add($"- Could not update users: {ex.Message}");
                }

                // Test if we can query users
                try
                {
                    var userCount = await _context.Users.CountAsync();
                    results.Add($"✓ Database accessible - found {userCount} users");
                }
                catch (Exception ex)
                {
                    results.Add($"✗ Error accessing users: {ex.Message}");
                }

                // Check if columns exist by trying to query them
                bool columnsExist = await CheckColumnsExist();
                if (columnsExist)
                {
                    results.Add("✓ Session columns verified successfully!");
                }
                else
                {
                    results.Add("⚠ Session columns may not be accessible yet");
                }

                return Ok(new
                {
                    success = true,
                    message = "Migration completed",
                    columnsExist = columnsExist,
                    details = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration failed");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Migration encountered an error",
                    error = ex.Message,
                    details = results
                });
            }
        }

        // Check database status
        [HttpGet("check-database")]
        public async Task<IActionResult> CheckDatabase()
        {
            try
            {
                var userCount = await _context.Users.CountAsync();

                // Try to access new columns
                bool hasSessionColumns = await CheckColumnsExist();

                // Get sample user info (without sensitive data)
                var sampleUser = await _context.Users
                    .Select(u => new
                    {
                        HasEmail = !string.IsNullOrEmpty(u.Email),
                        HasPassword = !string.IsNullOrEmpty(u.PasswordHash),
                        Role = u.Role
                    })
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    status = "connected",
                    userCount = userCount,
                    hasSessionColumns = hasSessionColumns,
                    sampleUser = sampleUser,
                    database = "SQLite",
                    recommendation = hasSessionColumns ?
                        "Database is ready for session management" :
                        "Run migration to add session columns"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "error",
                    message = ex.Message
                });
            }
        }

        // Test user creation with new fields
        [HttpPost("test-user-creation")]
        public async Task<IActionResult> TestUserCreation([FromQuery] string key)
        {
            if (key != "your-secret-migration-key-2024")
            {
                return Unauthorized("Invalid key");
            }

            try
            {
                // Check if test user already exists
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == "test@example.com");

                if (existingUser != null)
                {
                    return Ok(new
                    {
                        message = "Test user already exists",
                        userId = existingUser.UserId,
                        hasSessionToken = !string.IsNullOrEmpty(existingUser.CurrentSessionToken)
                    });
                }

                // Create a test user to verify all fields work
                var testUser = new Models.User
                {
                    Email = "test@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    PasswordHash = "test_hash",
                    Salt = "test_salt",
                    Role = "Client",
                    IsConfirmed = true,
                    UserId = "TEST-001",
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(testUser);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Test user created successfully",
                    userId = testUser.UserId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Could not create test user",
                    error = ex.Message
                });
            }
        }

        private async Task<bool> CheckColumnsExist()
        {
            try
            {
                // Try to execute a query that uses the new columns
                var query = _context.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.Salt,
                        u.CurrentSessionToken,
                        u.SessionExpiresAt
                    })
                    .Take(1);

                // Try to build and execute the query
                var result = await query.FirstOrDefaultAsync();
                return true;
            }
            catch
            {
                // If the query fails, columns probably don't exist
                return false;
            }
        }
    }
}