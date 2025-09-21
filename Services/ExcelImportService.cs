using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;

namespace AutumnRidgeUSA.Services
{
    public class ExcelImportService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ExcelImportService> _logger;

        public ExcelImportService(AppDbContext context, ILogger<ExcelImportService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public class ImportResult
        {
            public int SuccessCount { get; set; }
            public int FailureCount { get; set; }
            public List<string> Errors { get; set; } = new List<string>();
            public List<string> Warnings { get; set; } = new List<string>();
        }

        public async Task<ImportResult> ImportClientsFromExcelAsync(Stream excelStream)
        {
            var result = new ImportResult();

            try
            {
                using var workbook = new XLWorkbook(excelStream);
                var worksheet = workbook.Worksheet(1); // Get first worksheet

                // Get the header row to map columns
                var headerRow = worksheet.Row(1);
                var columnMap = new Dictionary<string, int>();

                // Map column names to indices (adjust these to match your Excel file)
                for (int col = 1; col <= headerRow.LastCellUsed().Address.ColumnNumber; col++)
                {
                    var columnName = headerRow.Cell(col).GetString().Trim().ToLower();
                    columnMap[columnName] = col;
                }

                // Ensure required columns exist
                var requiredColumns = new[] { "email", "firstname", "lastname" };
                foreach (var required in requiredColumns)
                {
                    if (!columnMap.ContainsKey(required) && !columnMap.ContainsKey(required.Replace(" ", "")))
                    {
                        result.Errors.Add($"Required column '{required}' not found in Excel file");
                        return result;
                    }
                }

                // Get or create divisions
                var divisions = await GetOrCreateDivisionsAsync();

                // Process each data row - Fixed: Cast to IXLRow
                var dataRows = worksheet.RowsUsed().Skip(1); // Skip header row

                foreach (var xlRow in dataRows)
                {
                    // Cast IXLRangeRow to IXLRow
                    var row = xlRow as IXLRow;
                    if (row == null) continue;

                    try
                    {
                        // Extract data from row
                        var email = GetCellValue(row, columnMap, new[] { "email", "e-mail", "emailaddress" })?.Trim();

                        if (string.IsNullOrEmpty(email))
                        {
                            result.Warnings.Add($"Row {row.RowNumber()}: Skipped - no email address");
                            continue;
                        }

                        // Check if user already exists
                        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                        if (existingUser != null)
                        {
                            result.Warnings.Add($"Row {row.RowNumber()}: User {email} already exists - skipped");
                            continue;
                        }

                        // Create new user
                        var user = new User
                        {
                            Email = email,
                            FirstName = GetCellValue(row, columnMap, new[] { "firstname", "first name", "fname" }) ?? "Unknown",
                            LastName = GetCellValue(row, columnMap, new[] { "lastname", "last name", "lname" }) ?? "User",
                            Phone = GetCellValue(row, columnMap, new[] { "phone", "phonenumber", "phone number", "telephone" }),
                            Address = GetCellValue(row, columnMap, new[] { "address", "street", "streetaddress", "street address" }),
                            City = GetCellValue(row, columnMap, new[] { "city" }),
                            State = GetCellValue(row, columnMap, new[] { "state", "st" }),
                            ZipCode = GetCellValue(row, columnMap, new[] { "zip", "zipcode", "zip code", "postal code" }),
                            UserId = GenerateUserId(),
                            Role = "Client",
                            IsConfirmed = true,
                            CreatedAt = DateTime.UtcNow
                        };

                        // Handle divisions
                        var divisionString = GetCellValue(row, columnMap, new[] { "division", "divisions", "department", "departments" });
                        if (!string.IsNullOrEmpty(divisionString))
                        {
                            user.UserDivisions = ParseAndAssignDivisions(divisionString, divisions);
                        }

                        // Handle balance if present
                        var balanceString = GetCellValue(row, columnMap, new[] { "balance", "amount", "accountbalance", "account balance" });
                        if (!string.IsNullOrEmpty(balanceString))
                        {
                            if (decimal.TryParse(balanceString.Replace("$", "").Replace(",", ""), out var balance))
                            {
                                // You might want to store this in a separate Account or Transaction table
                                // For now, we'll just log it
                                _logger.LogInformation($"User {email} has balance: {balance:C}");
                            }
                        }

                        _context.Users.Add(user);
                        result.SuccessCount++;

                        // Save in batches of 100
                        if (result.SuccessCount % 100 == 0)
                        {
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Saved batch of 100 users. Total so far: {result.SuccessCount}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.Errors.Add($"Row {row.RowNumber()}: {ex.Message}");
                        _logger.LogError(ex, $"Error processing row {row.RowNumber()}");
                    }
                }

                // Save any remaining records
                if (_context.ChangeTracker.HasChanges())
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation($"Import completed. Success: {result.SuccessCount}, Failed: {result.FailureCount}");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Fatal error: {ex.Message}");
                _logger.LogError(ex, "Fatal error during Excel import");
            }

            return result;
        }

        private string GetCellValue(IXLRow row, Dictionary<string, int> columnMap, string[] possibleNames)
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

        private async Task<Dictionary<string, Division>> GetOrCreateDivisionsAsync()
        {
            var divisions = await _context.Divisions.ToDictionaryAsync(d => d.Name.ToLower(), d => d);

            // Create default divisions if they don't exist
            var defaultDivisions = new[] { "Storage", "Contracting", "Real Estate" };

            foreach (var divName in defaultDivisions)
            {
                if (!divisions.ContainsKey(divName.ToLower()))
                {
                    var newDivision = new Division
                    {
                        Name = divName,
                        IsActive = true
                    };
                    _context.Divisions.Add(newDivision);
                    divisions[divName.ToLower()] = newDivision;
                }
            }

            if (_context.ChangeTracker.HasChanges())
            {
                await _context.SaveChangesAsync();
            }

            return divisions;
        }

        private List<UserDivision> ParseAndAssignDivisions(string divisionString, Dictionary<string, Division> availableDivisions)
        {
            var userDivisions = new List<UserDivision>();

            
            // Split by common delimiters
            var divisionNames = divisionString.Split(new[] { ',', ';', '|', '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var divName in divisionNames)
            {
                var trimmedName = divName.Trim().ToLower();

                // Try exact match
                if (availableDivisions.TryGetValue(trimmedName, out var division))
                {
                    userDivisions.Add(new UserDivision
                    {
                        Division = division,
                        IsActive = true
                    });
                }
                // Try to match common variations
                else if (trimmedName.Contains("storage") && availableDivisions.TryGetValue("storage", out division))
                {
                    userDivisions.Add(new UserDivision { Division = division, IsActive = true });
                }
                else if ((trimmedName.Contains("contract") || trimmedName.Contains("construction")) &&
                         availableDivisions.TryGetValue("contracting", out division))
                {
                    userDivisions.Add(new UserDivision { Division = division, IsActive = true });
                }
                else if ((trimmedName.Contains("real") || trimmedName.Contains("estate") || trimmedName.Contains("property")) &&
                         availableDivisions.TryGetValue("real estate", out division))
                {
                    userDivisions.Add(new UserDivision { Division = division, IsActive = true });
                }
                else
                {
                    _logger.LogWarning($"Unknown division: {divName}");
                }
            }

            return userDivisions;
        }

        private string GenerateUserId()
        {
            // Generate a unique user ID
            var random = new Random();
            var prefix = random.Next(1000, 9999);
            var suffix = GenerateRandomString(3);
            return $"{prefix}-{suffix}";
        }

        private string GenerateRandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public async Task<ImportResult> UpdateClientsFromExcelAsync(Stream excelStream)
        {
            // Similar to ImportClientsFromExcelAsync but updates existing records
            var result = new ImportResult();

            try
            {
                using var workbook = new XLWorkbook(excelStream);
                var worksheet = workbook.Worksheet(1);

                // Implementation for updating existing clients
                // Match by email and update other fields

                // This is a placeholder - implement based on your specific update logic
                result.Warnings.Add("Update functionality not yet fully implemented");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Update error: {ex.Message}");
                _logger.LogError(ex, "Error during Excel update");
            }

            return result;
        }
    }
}