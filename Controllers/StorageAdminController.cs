// Controllers/StorageAdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AutumnRidgeUSA.Data;
using AutumnRidgeUSA.Models.Storage;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Controllers
{
    [Route("api/storage-admin")]
    public class StorageAdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStorageImportService _storageImportService;
        private readonly IAdminSecurityService _securityService;
        private readonly ILogger<StorageAdminController> _logger;

        public StorageAdminController(
            AppDbContext context,
            IStorageImportService storageImportService,
            IAdminSecurityService securityService,
            ILogger<StorageAdminController> logger)
        {
            _context = context;
            _storageImportService = storageImportService;
            _securityService = securityService;
            _logger = logger;
        }

        [HttpPost("import-excel")]
        public async Task<IActionResult> ImportStorageExcel([FromQuery] string key, IFormFile excelFile)
        {
            if (!_securityService.ValidateMigrationKey(key))
            {
                return Unauthorized("Invalid key");
            }

            if (excelFile == null || excelFile.Length == 0)
            {
                return BadRequest("Excel file is required");
            }

            if (!excelFile.FileName.EndsWith(".xlsx") && !excelFile.FileName.EndsWith(".xls"))
            {
                return BadRequest("File must be an Excel file (.xlsx or .xls)");
            }

            try
            {
                var result = await _storageImportService.ImportStorageDataFromExcel(excelFile);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Message,
                        successCount = result.SuccessCount,
                        errorCount = result.ErrorCount,
                        totalProcessed = result.TotalProcessed,
                        createdContracts = result.CreatedContracts,
                        errors = result.Errors,
                        warnings = result.Warnings
                    });
                }

                return BadRequest(new
                {
                    success = result.Success,
                    message = result.Message,
                    errors = result.Errors,
                    warnings = result.Warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing storage data from Excel");
                return StatusCode(500, new { error = "Error processing Excel file", details = ex.Message });
            }
        }

        [HttpGet("contracts")]
        public async Task<IActionResult> GetAllStorageContracts([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
            {
                return Unauthorized("Invalid key");
            }

            try
            {
                var contracts = await _context.StorageContracts
                    .Include(sc => sc.StorageUnit)
                    .Include(sc => sc.ContractUsers)
                        .ThenInclude(scu => scu.User)
                    .OrderBy(sc => sc.StorageUnit.UnitId)
                    .Select(sc => new
                    {
                        id = sc.Id,
                        contractNumber = sc.ContractNumber,
                        unit = new
                        {
                            id = sc.StorageUnit.UnitId,
                            size = sc.StorageUnit.UnitSize,
                            baseRent = sc.StorageUnit.BaseRent
                        },
                        moveInDate = sc.MoveInDate,
                        grossRent = sc.GrossRent,
                        paymentCycle = sc.PaymentCycle,
                        securityDeposit = sc.SecurityDeposit,
                        securityDepositBalance = sc.SecurityDepositBalance,
                        isOnline = sc.IsOnline,
                        hasAutopay = sc.HasAutopay,
                        isActive = sc.IsActive,
                        contractStartDate = sc.ContractStartDate,
                        contractEndDate = sc.ContractEndDate,
                        users = sc.ContractUsers.Where(scu => scu.IsActive).Select(scu => new
                        {
                            userId = scu.User.UserId,
                            email = scu.User.Email,
                            name = $"{scu.User.FirstName} {scu.User.LastName}",
                            phone = scu.User.Phone,
                            isPrimary = scu.IsPrimaryContractHolder,
                            accessLevel = scu.AccessLevel
                        }).ToList(),
                        createdAt = sc.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalContracts = contracts.Count,
                    activeContracts = contracts.Count(c => c.isActive),
                    contracts = contracts
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving storage contracts");
                return StatusCode(500, new { error = "Error retrieving storage contracts" });
            }
        }

        [HttpGet("units")]
        public async Task<IActionResult> GetAllStorageUnits([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
            {
                return Unauthorized("Invalid key");
            }

            try
            {
                var units = await _context.StorageUnits
                    .Include(su => su.Contracts)
                        .ThenInclude(sc => sc.ContractUsers)
                            .ThenInclude(scu => scu.User)
                    .OrderBy(su => su.UnitId)
                    .Select(su => new
                    {
                        id = su.Id,
                        unitId = su.UnitId,
                        unitSize = su.UnitSize,
                        baseRent = su.BaseRent,
                        description = su.Description,
                        isActive = su.IsActive,
                        isOccupied = su.Contracts.Any(c => c.IsActive),
                        currentContract = su.Contracts.Where(c => c.IsActive).Select(c => new
                        {
                            contractNumber = c.ContractNumber,
                            grossRent = c.GrossRent,
                            moveInDate = c.MoveInDate,
                            tenantCount = c.ContractUsers.Count(scu => scu.IsActive),
                            primaryTenant = c.ContractUsers
                                .Where(scu => scu.IsActive && scu.IsPrimaryContractHolder)
                                .Select(scu => $"{scu.User.FirstName} {scu.User.LastName}")
                                .FirstOrDefault()
                        }).FirstOrDefault(),
                        createdAt = su.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalUnits = units.Count,
                    occupiedUnits = units.Count(u => u.isOccupied),
                    availableUnits = units.Count(u => !u.isOccupied),
                    units = units
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving storage units");
                return StatusCode(500, new { error = "Error retrieving storage units" });
            }
        }

        [HttpDelete("clear-all-storage")]
        public async Task<IActionResult> ClearAllStorageData([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
            {
                return Unauthorized("Invalid key");
            }

            try
            {
                // Remove all storage-related data
                var contractUsers = await _context.StorageContractUsers.ToListAsync();
                var contracts = await _context.StorageContracts.ToListAsync();
                var units = await _context.StorageUnits.ToListAsync();

                _context.StorageContractUsers.RemoveRange(contractUsers);
                _context.StorageContracts.RemoveRange(contracts);
                _context.StorageUnits.RemoveRange(units);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = $"Cleared {contractUsers.Count} contract users, {contracts.Count} contracts, and {units.Count} units"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing storage data");
                return StatusCode(500, new { error = "Error clearing storage data" });
            }
        }
    }
}