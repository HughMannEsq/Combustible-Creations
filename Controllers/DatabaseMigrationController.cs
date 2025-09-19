// Controllers/DatabaseMigrationController.cs
// Fixed version that works with older EF Core versions

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;

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
        // Add to DatabaseMigrationController.cs

        [HttpPost("reset-and-create-users")]
        public async Task<IActionResult> ResetAndCreateUsers([FromQuery] string key)
        {
            if (key != "your-secret-migration-key-2024")
                return Unauthorized("Invalid key");

            try
            {
                // Step 1: Clear all existing users
                var existingUsers = await _context.Users.ToListAsync();
                _context.Users.RemoveRange(existingUsers);
                await _context.SaveChangesAsync();

                var results = new List<object>();

                // Step 2: Create fresh users with proper security
                var securityService = HttpContext.RequestServices.GetRequiredService<ISecurityService>();

                // Create Admin user
                var adminSalt = securityService.GenerateSalt();
                var adminHash = securityService.HashPassword("Admin123!", adminSalt);

                var adminUser = new Models.User
                {
                    Email = "admin@autumnridge.com",
                    FirstName = "Admin",
                    LastName = "User",
                    PasswordHash = adminHash,
                    Salt = adminSalt,
                    Role = "Admin",
                    IsConfirmed = true,
                    UserId = "ADM-001",
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                results.Add(new { email = "admin@autumnridge.com", password = "Admin123!", role = "Admin" });

                // Create Client user
                var clientSalt = securityService.GenerateSalt();
                var clientHash = securityService.HashPassword("Client123!", clientSalt);

                var clientUser = new Models.User
                {
                    Email = "client@example.com",
                    FirstName = "John",
                    LastName = "Client",
                    PasswordHash = clientHash,
                    Salt = clientSalt,
                    Role = "Client",
                    IsConfirmed = true,
                    UserId = "CLT-001",
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow
                };

                _context.Users.Add(clientUser);
                results.Add(new { email = "client@example.com", password = "Client123!", role = "Client" });

                // Create Test user (another admin for testing)
                var testSalt = securityService.GenerateSalt();
                var testHash = securityService.HashPassword("Test123!", testSalt);

                var testUser = new Models.User
                {
                    Email = "test@example.com",
                    FirstName = "Test",
                    LastName = "Admin",
                    PasswordHash = testHash,
                    Salt = testSalt,
                    Role = "Admin",
                    IsConfirmed = true,
                    UserId = "TST-001",
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow
                };

                _context.Users.Add(testUser);
                results.Add(new { email = "test@example.com", password = "Test123!", role = "Admin" });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Database reset and users created successfully",
                    users = results,
                    note = "All users have properly salted and hashed passwords"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("list-all-users")]
        public async Task<IActionResult> ListAllUsers([FromQuery] string key)
        {
            if (key != "your-secret-migration-key-2024")
                return Unauthorized("Invalid key");

            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        u.Email,
                        u.Role,
                        u.FirstName,
                        u.LastName,
                        u.UserId,
                        u.IsConfirmed,
                        HasValidSalt = !string.IsNullOrEmpty(u.Salt) && u.Salt != "TEMP_SALT_NEEDS_RESET"
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalUsers = users.Count,
                    users = users
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // Add to DatabaseMigrationController.cs

        [HttpGet("create-initial-users")]  // Using GET for easy browser access
        public async Task<IActionResult> CreateInitialUsers([FromQuery] string key)
        {
            if (key != "your-secret-migration-key-2024")
                return Unauthorized("Invalid key");

            try
            {
                // Check if users already exist
                var existingCount = await _context.Users.CountAsync();
                if (existingCount > 0)
                {
                    return BadRequest(new { message = $"Database already has {existingCount} users. Use reset endpoint instead." });
                }

                var results = new List<object>();
                var securityService = HttpContext.RequestServices.GetRequiredService<ISecurityService>();

                // Create Admin user
                var adminSalt = securityService.GenerateSalt();
                var adminHash = securityService.HashPassword("Admin123!", adminSalt);

                var adminUser = new Models.User
                {
                    Email = "admin@autumnridge.com",
                    FirstName = "Admin",
                    LastName = "User",
                    PasswordHash = adminHash,
                    Salt = adminSalt,
                    Role = "Admin",
                    IsConfirmed = true,
                    UserId = "ADM-001",
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow
                };

                _context.Users.Add(adminUser);
                results.Add(new { email = "admin@autumnridge.com", password = "Admin123!", role = "Admin" });

                // Create Client user for testing
                var clientSalt = securityService.GenerateSalt();
                var clientHash = securityService.HashPassword("Client123!", clientSalt);

                var clientUser = new Models.User
                {
                    Email = "client@example.com",
                    FirstName = "John",
                    LastName = "Client",
                    PasswordHash = clientHash,
                    Salt = clientSalt,
                    Role = "Client",
                    IsConfirmed = true,
                    UserId = "CLT-001",
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow
                };

                _context.Users.Add(clientUser);
                results.Add(new { email = "client@example.com", password = "Client123!", role = "Client" });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Initial users created successfully!",
                    users = results,
                    instructions = "You can now login with any of these credentials"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating initial users");
                return StatusCode(500, new { error = ex.Message });
            }
        }
        // Test user creation with new fields
        [HttpPost("test-user-creation")]
        

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