// Services/StorageImportService.cs
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models;
using AutumnRidgeUSA.Models.Storage;
using AutumnRidgeUSA.Services.Helpers;

namespace AutumnRidgeUSA.Services
{
    public class StorageImportService : IStorageImportService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<StorageImportService> _logger;
        private readonly IExcelParsingHelper _excelHelper;
        private readonly ICsvParsingHelper _csvHelper;

        public StorageImportService(
            AppDbContext context,
            ILogger<StorageImportService> logger,
            IExcelParsingHelper excelHelper,
            ICsvParsingHelper csvHelper)
        {
            _context = context;
            _logger = logger;
            _excelHelper = excelHelper;
            _csvHelper = csvHelper;
        }

        public async Task<StorageImportResult> ImportStorageDataFromExcel(IFormFile excelFile)
        {
            var result = new StorageImportResult();

            try
            {
                using var stream = excelFile.OpenReadStream();
                var parsedData = ParseStorageExcelFile(stream);

                if (!parsedData.IsValid)
                {
                    return new StorageImportResult
                    {
                        Success = false,
                        Message = parsedData.ErrorMessage ?? "Invalid Excel format"
                    };
                }

                foreach (var storageData in parsedData.StorageData)
                {
                    try
                    {
                        await ProcessStorageRecord(storageData, result);
                    }
                    catch (Exception ex)
                    {
                        var innerMsg = ex.InnerException?.Message ?? "No inner exception";
                        result.Errors.Add($"Row {storageData.RowNumber}: {ex.Message} | Inner: {innerMsg}");
                        _logger.LogError(ex, "Error processing storage row {RowNumber}", storageData.RowNumber);
                    }
                }

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    var innerMsg = ex.InnerException?.Message ?? "No inner exception";
                    result.Errors.Add($"SaveChanges failed: {ex.Message} | Inner: {innerMsg}");
                    _logger.LogError(ex, "Error saving changes");
                }

                result.Success = result.Errors.Count == 0;
                result.Message = result.Success ? $"Successfully imported {result.SuccessCount} storage contracts" : "Import completed with errors";
                result.TotalProcessed = parsedData.StorageData.Count;
                result.ErrorCount = result.Errors.Count;
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.Message ?? "No inner exception";
                _logger.LogError(ex, "Error importing storage data from Excel");
                result.Success = false;
                result.Message = "Error processing Excel file";
                result.Errors.Add($"{ex.Message} | Inner: {innerMsg}");
            }

            return result;
        }

        private StorageParseResult ParseStorageExcelFile(Stream stream)
        {
            var result = new StorageParseResult();

            try
            {
                using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                // Get header row and create column mapping
                var headerRow = worksheet.Row(1);
                var columnMap = new Dictionary<string, int>();

                for (int col = 1; col <= headerRow.LastCellUsed().Address.ColumnNumber; col++)
                {
                    var columnName = headerRow.Cell(col).GetString().Trim().ToLower();
                    columnMap[columnName] = col;
                }

                // Process data rows
                var dataRows = worksheet.RowsUsed().Skip(1);
                foreach (var xlRow in dataRows)
                {
                    var row = xlRow as ClosedXML.Excel.IXLRow;
                    if (row == null) continue;
                    var phoneData = ParsePhoneData(row, columnMap);

                    var storageData = new StorageData
                    {
                        RowNumber = row.RowNumber(),
                        UnitId = GetExcelValue(row, columnMap, new[] { "unit", "unitid", "unit id", "locker", "lockerid" })?.Trim(),
                        UnitSize = GetExcelValue(row, columnMap, new[] { "unit size", "unitsize", "size" })?.Trim(),
                        MoveInDate = ParseDate(GetExcelValue(row, columnMap, new[] { "move-in date", "moveindate", "move in date", "move in" })),
                        GrossRent = ParseDecimal(GetExcelValue(row, columnMap, new[] { "gross rent", "grossrent", "rent", "monthly rent" })),
                        PaymentCycle = GetExcelValue(row, columnMap, new[] { "payment cycle", "paymentcycle", "cycle" })?.Trim() ?? "Monthly",
                        SecurityDeposit = ParseDecimal(GetExcelValue(row, columnMap, new[] { "security deposit", "securitydeposit", "deposit" })),
                        SecurityDepositBalance = ParseDecimal(GetExcelValue(row, columnMap, new[] { "sd balance", "sdbalance", "deposit balance", "security deposit balance" })),
                        IsOnline = ParseBool(GetExcelValue(row, columnMap, new[] { "online", "online access" })) ?? false,
                        HasAutopay = ParseBool(GetExcelValue(row, columnMap, new[] { "autopay", "auto pay", "automatic payment" })) ?? false,

                        // User identification fields - handle semicolon-separated values
                        Email = GetExcelValue(row, columnMap, new[] { "email", "e-mail", "user email" })?.Trim(),
                        FullName = GetExcelValue(row, columnMap, new[] { "name", "full name", "tenant name", "customer name" })?.Trim(),
                        // Phone handling - support both combined and separate formats
                        Phone = phoneData.Phone,
                        PhoneType = phoneData.PhoneType,
                        Phone2 = phoneData.Phone2,
                        Phone2Type = phoneData.Phone2Type,

                        IsPrimaryHolder = ParseBool(GetExcelValue(row, columnMap, new[] { "primary", "primary holder", "main tenant" })) ?? true
                    };

                    result.StorageData.Add(storageData);
                }

                result.IsValid = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private async Task ProcessStorageRecord(StorageData data, StorageImportResult result)
        {
            try
            {
                // Handle semicolon-separated values
                var userIds = SplitBySemicolon(data.UnitId);
                var fullNames = SplitBySemicolon(data.FullName);

                // Ensure we have matching counts or handle mismatches
                var maxCount = Math.Max(userIds.Count, fullNames.Count);

                for (int i = 0; i < maxCount; i++)
                {
                    var currentUserId = i < userIds.Count ? userIds[i] : userIds.LastOrDefault();
                    var currentFullName = i < fullNames.Count ? fullNames[i] : fullNames.LastOrDefault();

                    if (string.IsNullOrEmpty(currentUserId))
                    {
                        result.Errors.Add($"Row {data.RowNumber}, Entry {i + 1}: User ID is required");
                        continue;
                    }

                    // Parse the full name in "Last, First" format
                    var (firstName, lastName) = ParseFullName(currentFullName);

                    // Create a new data object for this specific entry
                    var entryData = new StorageData
                    {
                        RowNumber = data.RowNumber,
                        UnitId = currentUserId,
                        UnitSize = data.UnitSize,
                        MoveInDate = data.MoveInDate,
                        GrossRent = data.GrossRent,
                        PaymentCycle = data.PaymentCycle,
                        SecurityDeposit = data.SecurityDeposit,
                        SecurityDepositBalance = data.SecurityDepositBalance,
                        IsOnline = data.IsOnline,
                        HasAutopay = data.HasAutopay,
                        Email = data.Email, // This might be shared or empty
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = data.Phone,
                        IsPrimaryHolder = i == 0 // First person is primary
                    };

                    await ProcessSingleStorageEntry(entryData, result);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Row {data.RowNumber}: {ex.Message}");
            }
        }

        private async Task ProcessSingleStorageEntry(StorageData data, StorageImportResult result)
        {
            if (string.IsNullOrEmpty(data.UnitId))
            {
                result.Errors.Add($"Row {data.RowNumber}: Unit ID is required");
                return;
            }

            // Find or create storage unit
            var storageUnit = await _context.StorageUnits
                .FirstOrDefaultAsync(su => su.UnitId == data.UnitId);

            if (storageUnit == null)
            {
                storageUnit = new StorageUnit
                {
                    UnitId = data.UnitId,
                    UnitSize = data.UnitSize ?? "Unknown",
                    BaseRent = data.GrossRent,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StorageUnits.Add(storageUnit);
            }

            // Find or create user
            var user = await FindOrCreateUser(data);
            if (user == null)
            {
                result.Errors.Add($"Row {data.RowNumber}: Could not create or find user");
                return;
            }

            // Create or find contract
            var existingContract = await _context.StorageContracts
                .Include(sc => sc.ContractUsers)
                .FirstOrDefaultAsync(sc => sc.StorageUnit.UnitId == data.UnitId && sc.IsActive);

            StorageContract contract;
            if (existingContract == null)
            {
                contract = new StorageContract
                {
                    ContractNumber = await GenerateContractNumber(),
                    StorageUnit = storageUnit,
                    MoveInDate = data.MoveInDate ?? DateTime.UtcNow,
                    GrossRent = data.GrossRent,
                    PaymentCycle = data.PaymentCycle,
                    SecurityDeposit = data.SecurityDeposit,
                    SecurityDepositBalance = data.SecurityDepositBalance,
                    IsOnline = data.IsOnline,
                    HasAutopay = data.HasAutopay,
                    IsActive = true,
                    ContractStartDate = data.MoveInDate,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StorageContracts.Add(contract);
            }
            else
            {
                contract = existingContract;
            }

            // Check if user is already associated with this contract
            var existingContractUser = await _context.StorageContractUsers
                .FirstOrDefaultAsync(scu => scu.StorageContract == contract && scu.UserId == user.Id);

            if (existingContractUser == null)
            {
                var contractUser = new StorageContractUser
                {
                    StorageContract = contract,
                    UserId = user.Id,
                    IsPrimaryContractHolder = data.IsPrimaryHolder,
                    AccessLevel = "Full",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.StorageContractUsers.Add(contractUser);

                result.CreatedContracts.Add(new
                {
                    contractNumber = contract.ContractNumber,
                    unitId = storageUnit.UnitId,
                    userEmail = user.Email,
                    isPrimary = data.IsPrimaryHolder
                });

                result.SuccessCount++;
            }
            else
            {
                result.Warnings.Add($"Row {data.RowNumber}: User {user.Email} already associated with unit {data.UnitId}");
            }
        }

        private async Task<User?> FindOrCreateUser(StorageData data)
        {
            User? user = null;

            // Try to find by email first
            if (!string.IsNullOrEmpty(data.Email))
            {
                user = await _context.Users.FirstOrDefaultAsync(u => u.Email == data.Email);
            }

            // If not found and we have name info, create new user
            if (user == null && (!string.IsNullOrEmpty(data.FirstName) || !string.IsNullOrEmpty(data.LastName)))
            {
                user = new User
                {
                    Email = data.Email ?? $"storage.{Guid.NewGuid():N}@autumnridge.temp",
                    FirstName = data.FirstName ?? "Storage",
                    LastName = data.LastName ?? "Client",
                    Phone = data.Phone,
                    PhoneType = data.PhoneType,
                    Phone2 = data.Phone2,
                    Phone2Type = data.Phone2Type,
                    Role = "Client",
                    IsConfirmed = true,
                    UserId = await GenerateUniqueUserId(),
                    CreatedAt = DateTime.UtcNow,
                    ConfirmedAt = DateTime.UtcNow,
                    PasswordHash = "TEMP_STORAGE_IMPORT", // Temporary - admin should reset
                    Salt = "TEMP_STORAGE_IMPORT"
                };
                _context.Users.Add(user);
            }

            return user;
        }

        private async Task<string> GenerateContractNumber()
        {
            var year = DateTime.Now.Year;
            var count = await _context.StorageContracts.CountAsync() + 1;
            return $"SC-{year}-{count:D3}";
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
                userId = $"{numbers}-{letters}";  // Format: 1234-ABC (8 characters total)
            } while (await _context.Users.AnyAsync(u => u.UserId == userId));

            return userId;
        }

        private List<string> SplitBySemicolon(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => !string.IsNullOrEmpty(s))
                       .ToList();
        }

        private (string firstName, string lastName) ParseFullName(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return ("Unknown", "User");

            var trimmed = fullName.Trim();

            // Handle "Last, First" format
            if (trimmed.Contains(','))
            {
                var parts = trimmed.Split(',', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var lastName = parts[0].Trim();
                    var firstName = parts[1].Trim();
                    return (firstName, lastName);
                }
            }

            // Handle "First Last" format as fallback
            var spaceParts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (spaceParts.Length >= 2)
            {
                var firstName = spaceParts[0];
                var lastName = string.Join(" ", spaceParts.Skip(1));
                return (firstName, lastName);
            }

            // Single name - treat as first name
            return (trimmed, "");
        }

        private PhoneData ParsePhoneData(ClosedXML.Excel.IXLRow row, Dictionary<string, int> columnMap)
        {
            var result = new PhoneData();

            // Try to get phone data from combined format first
            var combinedPhone = GetExcelValue(row, columnMap, new[] { "phone", "telephone", "phone number", "phone numbers" });

            if (!string.IsNullOrEmpty(combinedPhone) && (combinedPhone.Contains("{") || combinedPhone.Contains(";")))
            {
                // Parse combined format like "(814) 310-1159 {C}; (814) 839-0135 {H}"
                ParseCombinedPhoneFormat(combinedPhone, result);
            }
            else
            {
                // Try separate columns format
                result.Phone = GetExcelValue(row, columnMap, new[] { "phone", "phone1", "phone number" })?.Trim();
                result.PhoneType = ParsePhoneType(GetExcelValue(row, columnMap, new[] { "phone type", "phonetype", "phone1type", "phone_type" }));
                result.Phone2 = GetExcelValue(row, columnMap, new[] { "phone2", "phone 2", "second phone" })?.Trim();
                result.Phone2Type = ParsePhoneType(GetExcelValue(row, columnMap, new[] { "phone2type", "phone 2 type", "phone2_type", "second phone type" }));

                // Clean up phone numbers (remove formatting but keep digits and basic characters)
                if (!string.IsNullOrEmpty(result.Phone))
                    result.Phone = CleanPhoneNumber(result.Phone);
                if (!string.IsNullOrEmpty(result.Phone2))
                    result.Phone2 = CleanPhoneNumber(result.Phone2);
            }

            return result;
        }

        private void ParseCombinedPhoneFormat(string phoneString, PhoneData result)
        {
            var phoneEntries = phoneString.Split(';', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < phoneEntries.Length && i < 2; i++)
            {
                var entry = phoneEntries[i].Trim();
                var phoneNumber = ExtractPhoneNumber(entry);
                var phoneType = ExtractPhoneTypeFromEntry(entry);

                if (i == 0)
                {
                    result.Phone = phoneNumber;
                    result.PhoneType = phoneType;
                }
                else
                {
                    result.Phone2 = phoneNumber;
                    result.Phone2Type = phoneType;
                }
            }
        }

        private string ExtractPhoneNumber(string entry)
        {
            // Remove type indicators and clean up
            var cleaned = entry;
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{[CHW]\}", ""); // Remove {C}, {H}, {W}
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\([CHW]\)", ""); // Remove (C), (H), (W)
            return CleanPhoneNumber(cleaned);
        }

        private string ExtractPhoneTypeFromEntry(string entry)
        {
            if (entry.Contains("{C}") || entry.Contains("(C)")) return "Cell";
            if (entry.Contains("{H}") || entry.Contains("(H)")) return "Home";
            if (entry.Contains("{W}") || entry.Contains("(W)")) return "Work";
            return "Cell"; // Default assumption
        }

        private string ParsePhoneType(string? phoneType)
        {
            if (string.IsNullOrEmpty(phoneType)) return "Cell"; // Default

            var lower = phoneType.ToLower().Trim();
            return lower switch
            {
                "cell" or "mobile" or "c" => "Cell",
                "home" or "h" => "Home",
                "work" or "office" or "w" => "Work",
                _ => "Cell"
            };
        }

        private string CleanPhoneNumber(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return phone;

            // Keep the formatted phone number but remove extra whitespace
            return phone.Trim();
        }

        private string? GetExcelValue(ClosedXML.Excel.IXLRow row, Dictionary<string, int> columnMap, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (columnMap.TryGetValue(name, out int colIndex))
                {
                    return row.Cell(colIndex).GetString();
                }
            }
            return null;
        }

        private DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return DateTime.TryParse(value, out var date) ? date : null;
        }

        private decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            var cleaned = value.Replace("$", "").Replace(",", "");
            return decimal.TryParse(cleaned, out var result) ? result : 0;
        }

        private bool? ParseBool(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            var lower = value.ToLower();
            if (lower == "yes" || lower == "true" || lower == "1" || lower == "y") return true;
            if (lower == "no" || lower == "false" || lower == "0" || lower == "n") return false;
            return null;
        }
    }

    // Supporting interfaces and models
    public interface IStorageImportService
    {
        Task<StorageImportResult> ImportStorageDataFromExcel(IFormFile excelFile);
    }

    public class StorageImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public int TotalProcessed { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<object> CreatedContracts { get; set; } = new();
    }

    public class StorageParseResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
        public List<StorageData> StorageData { get; set; } = new();
    }

    public class StorageData
    {
        public int RowNumber { get; set; }
        public string? UnitId { get; set; }
        public string? UnitSize { get; set; }
        public DateTime? MoveInDate { get; set; }
        public decimal GrossRent { get; set; }
        public string PaymentCycle { get; set; } = "Monthly";
        public decimal SecurityDeposit { get; set; }
        public decimal SecurityDepositBalance { get; set; }
        public bool IsOnline { get; set; }
        public bool HasAutopay { get; set; }

        // User info - supports both individual and combined formats
        public string? Email { get; set; }
        public string? FullName { get; set; }  // For "Last, First" format
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public string? PhoneType { get; set; }
        public string? Phone2 { get; set; }
        public string? Phone2Type { get; set; }
        public bool IsPrimaryHolder { get; set; } = true;
    }

    public class PhoneData
    {
        public string? Phone { get; set; }
        public string? PhoneType { get; set; }
        public string? Phone2 { get; set; }
        public string? Phone2Type { get; set; }
    }
}