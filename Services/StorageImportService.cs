// Services/StorageImportService.cs
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models.Storage;
using AutumnRidgeUSA.Services.Helpers;
using AutumnRidgeUSA.Models;

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
                var parsedData = await ParseStorageExcelFile(stream);

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
                        result.Errors.Add($"Row {storageData.RowNumber}: {ex.Message}");
                        _logger.LogError(ex, "Error processing storage row {RowNumber}", storageData.RowNumber);
                    }
                }

                await _context.SaveChangesAsync();

                result.Success = true;
                result.Message = $"Successfully imported {result.SuccessCount} storage contracts";
                result.TotalProcessed = parsedData.StorageData.Count;
                result.ErrorCount = result.Errors.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing storage data from Excel");
                result.Success = false;
                result.Message = "Error processing Excel file";
                result.Errors.Add(ex.Message);
            }

            return result;
        }

        private async Task<StorageParseResult> ParseStorageExcelFile(Stream stream)
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

                        // User identification fields
                        Email = GetExcelValue(row, columnMap, new[] { "email", "e-mail", "user email" })?.Trim(),
                        FirstName = GetExcelValue(row, columnMap, new[] { "first name", "firstname", "fname" })?.Trim(),
                        LastName = GetExcelValue(row, columnMap, new[] { "last name", "lastname", "lname" })?.Trim(),
                        Phone = GetExcelValue(row, columnMap, new[] { "phone", "telephone", "phone number" })?.Trim(),

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

        private async Task<User> FindOrCreateUser(StorageData data)
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
                userId = $"STG-{numbers}-{letters}";
            } while (await _context.Users.AnyAsync(u => u.UserId == userId));

            return userId;
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

        // User info
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Phone { get; set; }
        public bool IsPrimaryHolder { get; set; } = true;
    }
}