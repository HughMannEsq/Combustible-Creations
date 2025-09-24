// Controllers/DatabaseMigrationController.cs
// Refactored clean version

using Microsoft.AspNetCore.Mvc;
using AutumnRidgeUSA.Services;

namespace AutumnRidgeUSA.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class DatabaseMigrationController : ControllerBase
    {
        private readonly IDatabaseMigrationService _migrationService;
        private readonly IUserImportService _userImportService;
        private readonly IAdminSecurityService _securityService;
        private readonly ILogger<DatabaseMigrationController> _logger;

        public DatabaseMigrationController(
            IDatabaseMigrationService migrationService,
            IUserImportService userImportService,
            IAdminSecurityService securityService,
            ILogger<DatabaseMigrationController> logger)
        {
            _migrationService = migrationService;
            _userImportService = userImportService;
            _securityService = securityService;
            _logger = logger;
        }

        [HttpGet("migrate-database")]
        public async Task<IActionResult> MigrateDatabase([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
            {
                return Unauthorized("Invalid migration key");
            }

            var result = await _migrationService.MigrateDatabase();

            if (result.Success)
            {
                return Ok(new
                {
                    success = result.Success,
                    message = result.Message,
                    columnsExist = result.ColumnsExist,
                    details = result.Details
                });
            }

            return StatusCode(500, new
            {
                success = result.Success,
                message = result.Message,
                error = result.Error,
                details = result.Details
            });
        }

        [HttpGet("check-database")]
        public async Task<IActionResult> CheckDatabase()
        {
            var status = await _migrationService.CheckDatabaseStatus();

            if (status.Status == "error")
            {
                return StatusCode(500, new
                {
                    status = status.Status,
                    message = status.ErrorMessage
                });
            }

            return Ok(new
            {
                status = status.Status,
                userCount = status.UserCount,
                hasSessionColumns = status.HasSessionColumns,
                sampleUser = status.SampleUser,
                database = status.Database,
                recommendation = status.Recommendation
            });
        }

        [HttpPost("reset-and-create-users")]
        public async Task<IActionResult> ResetAndCreateUsers([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
                return Unauthorized("Invalid key");

            try
            {
                var result = await _userImportService.ResetAndCreateUsers();

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Message,
                        users = result.Users,
                        note = result.Note
                    });
                }

                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting and creating users");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("list-all-users")]
        public async Task<IActionResult> ListAllUsers([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
                return Unauthorized("Invalid key");

            try
            {
                var users = await _userImportService.GetAllUsers();

                return Ok(new
                {
                    totalUsers = users.Count,
                    users = users
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing users");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("create-initial-users")]
        public async Task<IActionResult> CreateInitialUsers([FromQuery] string key)
        {
            if (!_securityService.ValidateMigrationKey(key))
                return Unauthorized("Invalid key");

            try
            {
                var result = await _userImportService.CreateInitialUsers();

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Message,
                        users = result.Users,
                        instructions = result.Instructions
                    });
                }

                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating initial users");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("reset-and-create-users-from-csv")]
        public async Task<IActionResult> ResetAndCreateUsersFromCsv([FromQuery] string key, IFormFile csvFile)
        {
            if (!_securityService.ValidateMigrationKey(key))
                return Unauthorized("Invalid key");

            if (csvFile == null || csvFile.Length == 0)
                return BadRequest("CSV file is required");

            try
            {
                var result = await _userImportService.ImportUsersFromCsv(csvFile);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Message,
                        users = result.Users,
                        errors = result.Errors,
                        totalProcessed = result.TotalProcessed,
                        successCount = result.SuccessCount,
                        errorCount = result.ErrorCount
                    });
                }

                return BadRequest(new { message = result.Message, errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CSV file");
                return StatusCode(500, new { error = "Error processing CSV file", details = ex.Message });
            }
        }

        [HttpPost("reset-and-create-users-from-excel")]
        public async Task<IActionResult> ResetAndCreateUsersFromExcel([FromQuery] string key, IFormFile excelFile)
        {
            if (!_securityService.ValidateMigrationKey(key))
                return Unauthorized("Invalid key");

            if (excelFile == null || excelFile.Length == 0)
                return BadRequest("Excel file is required");

            if (!excelFile.FileName.EndsWith(".xlsx") && !excelFile.FileName.EndsWith(".xls"))
                return BadRequest("File must be an Excel file (.xlsx or .xls)");

            try
            {
                var result = await _userImportService.ImportUsersFromExcel(excelFile);

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = result.Success,
                        message = result.Message,
                        users = result.Users,
                        errors = result.Errors,
                        successCount = result.SuccessCount,
                        errorCount = result.ErrorCount
                    });
                }

                return BadRequest(new { message = result.Message, errors = result.Errors });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Excel file");
                return StatusCode(500, new { error = "Error processing Excel file", details = ex.Message });
            }
        }
    }
}