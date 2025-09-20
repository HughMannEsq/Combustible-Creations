// Controllers/DatabaseMigrationController.cs
// Fixed version that works with older EF Core versions

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Services;
using System.Text;

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

        // Add this to your DatabaseMigrationController.cs

        [HttpPost("reset-and-create-users-from-csv")]
        public async Task<IActionResult> ResetAndCreateUsersFromCsv([FromQuery] string key, IFormFile csvFile)
        {
            if (key != "your-secret-migration-key-2024")
                return Unauthorized("Invalid key");

            if (csvFile == null || csvFile.Length == 0)
                return BadRequest("CSV file is required");

            try
            {
                // Step 1: Clear all existing users
                var existingUsers = await _context.Users.ToListAsync();
                _context.Users.RemoveRange(existingUsers);
                await _context.SaveChangesAsync();

                var results = new List<object>();
                var errors = new List<string>();

                // Step 2: Parse CSV file
                using var stream = csvFile.OpenReadStream();
                using var reader = new StreamReader(stream);

                string headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine))
                {
                    return BadRequest("CSV file appears to be empty");
                }

                // Parse header to understand column positions
                var headers = headerLine.Split(',').Select(h => h.Trim('"').ToLower()).ToArray();
                var columnMap = new Dictionary<string, int>();

                for (int i = 0; i < headers.Length; i++)
                {
                    columnMap[headers[i]] = i;
                }

                // Verify required columns exist
                var requiredColumns = new[] { "email", "firstname", "lastname", "role", "password" };
                foreach (var required in requiredColumns)
                {
                    if (!columnMap.ContainsKey(required))
                    {
                        return BadRequest($"Required column '{required}' not found in CSV. Available columns: {string.Join(", ", headers)}");
                    }
                }

                // Step 3: Process each user row
                var securityService = HttpContext.RequestServices.GetRequiredService<ISecurityService>();
                string line;
                int rowNumber = 1;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    rowNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var values = ParseCsvLine(line);

                        if (values.Length < columnMap.Count)
                        {
                            errors.Add($"Row {rowNumber}: Insufficient columns");
                            continue;
                        }

                        // Extract user data from CSV
                        var email = GetColumnValue(values, columnMap, "email")?.Trim();
                        var firstName = GetColumnValue(values, columnMap, "firstname")?.Trim();
                        var lastName = GetColumnValue(values, columnMap, "lastname")?.Trim();
                        var role = GetColumnValue(values, columnMap, "role")?.Trim();
                        var password = GetColumnValue(values, columnMap, "password")?.Trim();

                        // Validate required fields
                        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                        {
                            errors.Add($"Row {rowNumber}: Email and password are required");
                            continue;
                        }

                        if (!IsValidRole(role))
                        {
                            errors.Add($"Row {rowNumber}: Invalid role '{role}'. Must be Admin, Client, or Manager");
                            continue;
                        }

                        // Generate security data
                        var salt = securityService.GenerateSalt();
                        var hash = securityService.HashPassword(password, salt);
                        var userId = await GenerateUniqueUserIdFromCsv();

                        // Create user entity
                        var user = new Models.User
                        {
                            Email = email,
                            FirstName = firstName ?? "Unknown",
                            LastName = lastName ?? "User",
                            PasswordHash = hash,
                            Salt = salt,
                            Role = role,
                            IsConfirmed = true,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            ConfirmedAt = DateTime.UtcNow,

                            // Optional fields from CSV
                            Phone = GetColumnValue(values, columnMap, "phone"),
                            Address = GetColumnValue(values, columnMap, "address"),
                            City = GetColumnValue(values, columnMap, "city"),
                            State = GetColumnValue(values, columnMap, "state"),
                            ZipCode = GetColumnValue(values, columnMap, "zipcode") ?? GetColumnValue(values, columnMap, "zip")
                        };

                        _context.Users.Add(user);
                        results.Add(new
                        {
                            email = user.Email,
                            role = user.Role,
                            userId = user.UserId,
                            name = $"{user.FirstName} {user.LastName}"
                        });

                        _logger.LogInformation("Created user from CSV: {Email} with role: {Role}", email, role);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {rowNumber}: {ex.Message}");
                        _logger.LogError(ex, "Error processing CSV row {RowNumber}", rowNumber);
                    }
                }

                // Save all users
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Successfully created {results.Count} users from CSV",
                    users = results,
                    errors = errors,
                    totalProcessed = rowNumber - 1,
                    successCount = results.Count,
                    errorCount = errors.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file");
                return StatusCode(500, new { error = "Error processing CSV file", details = ex.Message });
            }
        }

        // Helper methods for CSV processing
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    values.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            values.Add(current.ToString().Trim());
            return values.ToArray();
        }

        private string? GetColumnValue(string[] values, Dictionary<string, int> columnMap, string columnName)
        {
            if (columnMap.TryGetValue(columnName, out int index) && index < values.Length)
            {
                var value = values[index].Trim('"').Trim();
                return string.IsNullOrEmpty(value) ? null : value;
            }
            return null;
        }

        private bool IsValidRole(string? role)
        {
            if (string.IsNullOrEmpty(role)) return false;
            var validRoles = new[] { "Admin", "Client", "Manager" };
            return validRoles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        private async Task<string> GenerateUniqueUserIdFromCsv()
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
        // Add this to your DatabaseMigrationController.cs

        [HttpPost("reset-and-create-users-from-excel")]
        public async Task<IActionResult> ResetAndCreateUsersFromExcel([FromQuery] string key, IFormFile excelFile)
        {
            if (key != "your-secret-migration-key-2024")
                return Unauthorized("Invalid key");

            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Excel file is required");

            if (!excelFile.FileName.EndsWith(".xlsx") && !excelFile.FileName.EndsWith(".xls"))
                return BadRequest("File must be an Excel file (.xlsx or .xls)");

            try
            {
                // Step 1: Clear all existing users
                var existingUsers = await _context.Users.ToListAsync();
                _context.Users.RemoveRange(existingUsers);
                await _context.SaveChangesAsync();

                var results = new List<object>();
                var errors = new List<string>();

                // Step 2: Process Excel file using ClosedXML
                using var stream = excelFile.OpenReadStream();
                using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1); // Get first worksheet

                // Get the header row to map columns
                var headerRow = worksheet.Row(1);
                var columnMap = new Dictionary<string, int>();

                // Map column names to indices
                for (int col = 1; col <= headerRow.LastCellUsed().Address.ColumnNumber; col++)
                {
                    var columnName = headerRow.Cell(col).GetString().Trim().ToLower();
                    columnMap[columnName] = col;
                }

                // Ensure required columns exist
                var requiredColumns = new[] { "email", "firstname", "lastname", "role", "password" };
                foreach (var required in requiredColumns)
                {
                    if (!columnMap.ContainsKey(required) && !columnMap.ContainsKey(required.Replace(" ", "")))
                    {
                        return BadRequest($"Required column '{required}' not found in Excel file. Available columns: {string.Join(", ", columnMap.Keys)}");
                    }
                }

                // Step 3: Process each data row
                var securityService = HttpContext.RequestServices.GetRequiredService<ISecurityService>();
                var dataRows = worksheet.RowsUsed().Skip(1); // Skip header row

                foreach (var xlRow in dataRows)
                {
                    var row = xlRow as ClosedXML.Excel.IXLRow;
                    if (row == null) continue;

                    try
                    {
                        // Extract data from row
                        var email = GetExcelCellValue(row, columnMap, new[] { "email", "e-mail", "emailaddress" })?.Trim();
                        var firstName = GetExcelCellValue(row, columnMap, new[] { "firstname", "first name", "fname" })?.Trim();
                        var lastName = GetExcelCellValue(row, columnMap, new[] { "lastname", "last name", "lname" })?.Trim();
                        var role = GetExcelCellValue(row, columnMap, new[] { "role" })?.Trim();
                        var password = GetExcelCellValue(row, columnMap, new[] { "password", "pwd" })?.Trim();

                        // Validate required fields
                        if (string.IsNullOrEmpty(email))
                        {
                            errors.Add($"Row {row.RowNumber()}: Email is required");
                            continue;
                        }

                        if (string.IsNullOrEmpty(password))
                        {
                            errors.Add($"Row {row.RowNumber()}: Password is required");
                            continue;
                        }

                        if (!IsValidRole(role))
                        {
                            errors.Add($"Row {row.RowNumber()}: Invalid role '{role}'. Must be Admin, Client, or Manager");
                            continue;
                        }

                        // Generate security data
                        var salt = securityService.GenerateSalt();
                        var hash = securityService.HashPassword(password, salt);
                        var userId = await GenerateUniqueUserIdFromExcel();

                        // Create user entity
                        var user = new Models.User
                        {
                            Email = email,
                            FirstName = firstName ?? "Unknown",
                            LastName = lastName ?? "User",
                            PasswordHash = hash,
                            Salt = salt,
                            Role = role,
                            IsConfirmed = true,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            ConfirmedAt = DateTime.UtcNow,

                            // Optional fields from Excel
                            Phone = GetExcelCellValue(row, columnMap, new[] { "phone", "phonenumber", "phone number", "telephone" }),
                            Address = GetExcelCellValue(row, columnMap, new[] { "address", "street", "streetaddress", "street address" }),
                            City = GetExcelCellValue(row, columnMap, new[] { "city" }),
                            State = GetExcelCellValue(row, columnMap, new[] { "state", "st" }),
                            ZipCode = GetExcelCellValue(row, columnMap, new[] { "zip", "zipcode", "zip code", "postal code" })
                        };

                        _context.Users.Add(user);
                        results.Add(new
                        {
                            email = user.Email,
                            role = user.Role,
                            userId = user.UserId,
                            name = $"{user.FirstName} {user.LastName}"
                        });

                        _logger.LogInformation("Created user from Excel: {Email} with role: {Role}", email, role);

                        // Save in batches of 50 to avoid memory issues
                        if (results.Count % 50 == 0)
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Saved batch of 50 users. Total so far: {Count}", results.Count);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                        _logger.LogError(ex, "Error processing Excel row {RowNumber}", row.RowNumber());
                    }
                }

                // Save any remaining records
                if (_context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    message = $"Successfully created {results.Count} users from Excel file",
                    users = results,
                    errors = errors,
                    successCount = results.Count,
                    errorCount = errors.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Excel file");
                return StatusCode(500, new { error = "Error processing Excel file", details = ex.Message });
            }
        }

        // Helper methods for Excel processing
        private string? GetExcelCellValue(ClosedXML.Excel.IXLRow row, Dictionary<string, int> columnMap, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var normalizedName = name.ToLower().Replace(" ", "");

                // Try exact match first
                if (columnMap.TryGetValue(name, out int colIndex))
                {
                    return row.Cell(colIndex).GetString();
                }

                // Try normalized match
                if (columnMap.TryGetValue(normalizedName, out colIndex))
                {
                    return row.Cell(colIndex).GetString();
                }

                // Try to find partial match
                var partialMatch = columnMap.FirstOrDefault(kvp =>
                    kvp.Key.Contains(normalizedName) || normalizedName.Contains(kvp.Key));

                if (!partialMatch.Equals(default(KeyValuePair<string, int>)))
                {
                    return row.Cell(partialMatch.Value).GetString();
                }
            }

            return null;
        }

        private async Task<string> GenerateUniqueUserIdFromExcel()
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