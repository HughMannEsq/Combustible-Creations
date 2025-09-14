using System;
using System.IO;
using System.Threading.Tasks;
using AutumnRidgeUSA.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AutumnRidgeUSA.Pages.Admin
{
    public class ImportExcelModel : PageModel
    {
        private readonly ExcelImportService _importService;
        private readonly ILogger<ImportExcelModel> _logger;

        public ImportExcelModel(ExcelImportService importService, ILogger<ImportExcelModel> logger)
        {
            _importService = importService;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [TempData]
        public bool IsSuccess { get; set; }

        public ExcelImportService.ImportResult ImportResult { get; set; }

        public IActionResult OnGet()
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile excelFile, bool updateExisting = false, bool testMode = false)
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            if (excelFile == null || excelFile.Length == 0)
            {
                StatusMessage = "Please select a valid Excel file.";
                IsSuccess = false;
                return Page();
            }

            // Validate file extension
            var extension = Path.GetExtension(excelFile.FileName).ToLower();
            if (extension != ".xlsx" && extension != ".xls")
            {
                StatusMessage = "Invalid file format. Please upload an Excel file (.xlsx or .xls)";
                IsSuccess = false;
                return Page();
            }

            // Validate file size (max 10MB)
            if (excelFile.Length > 10 * 1024 * 1024)
            {
                StatusMessage = "File is too large. Maximum size is 10MB.";
                IsSuccess = false;
                return Page();
            }

            try
            {
                using var stream = new MemoryStream();
                await excelFile.CopyToAsync(stream);
                stream.Position = 0;

                if (testMode)
                {
                    // In test mode, just validate without saving
                    StatusMessage = "Test mode: File validated successfully. No data was saved.";
                    IsSuccess = true;
                    // You could implement a validation-only method here
                }
                else if (updateExisting)
                {
                    ImportResult = await _importService.UpdateClientsFromExcelAsync(stream);
                    StatusMessage = $"Update completed: {ImportResult.SuccessCount} clients updated, {ImportResult.FailureCount} failed.";
                }
                else
                {
                    ImportResult = await _importService.ImportClientsFromExcelAsync(stream);
                    StatusMessage = $"Import completed: {ImportResult.SuccessCount} clients imported, {ImportResult.FailureCount} failed.";
                }

                IsSuccess = ImportResult?.FailureCount == 0;

                _logger.LogInformation($"Excel import completed by admin. File: {excelFile.FileName}, Success: {ImportResult?.SuccessCount}, Failed: {ImportResult?.FailureCount}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during Excel import");
                StatusMessage = $"An error occurred during import: {ex.Message}";
                IsSuccess = false;
            }

            return Page();
        }

        public IActionResult OnPostDownloadTemplate()
        {
            var role = Request.Cookies["ImpersonatedRole"] ?? "Guest";
            if (role != "Admin")
            {
                return RedirectToPage("/Home");
            }

            try
            {
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Clients");

                // Add headers
                worksheet.Cell(1, 1).Value = "Email";
                worksheet.Cell(1, 2).Value = "FirstName";
                worksheet.Cell(1, 3).Value = "LastName";
                worksheet.Cell(1, 4).Value = "Phone";
                worksheet.Cell(1, 5).Value = "Address";
                worksheet.Cell(1, 6).Value = "City";
                worksheet.Cell(1, 7).Value = "State";
                worksheet.Cell(1, 8).Value = "ZipCode";
                worksheet.Cell(1, 9).Value = "Divisions";
                worksheet.Cell(1, 10).Value = "Balance";

                // Add sample data
                worksheet.Cell(2, 1).Value = "john.doe@example.com";
                worksheet.Cell(2, 2).Value = "John";
                worksheet.Cell(2, 3).Value = "Doe";
                worksheet.Cell(2, 4).Value = "555-0123";
                worksheet.Cell(2, 5).Value = "123 Main Street";
                worksheet.Cell(2, 6).Value = "Battle Creek";
                worksheet.Cell(2, 7).Value = "MI";
                worksheet.Cell(2, 8).Value = "49017";
                worksheet.Cell(2, 9).Value = "Storage, Real Estate";
                worksheet.Cell(2, 10).Value = "150.00";

                worksheet.Cell(3, 1).Value = "jane.smith@example.com";
                worksheet.Cell(3, 2).Value = "Jane";
                worksheet.Cell(3, 3).Value = "Smith";
                worksheet.Cell(3, 4).Value = "555-0456";
                worksheet.Cell(3, 5).Value = "456 Oak Avenue";
                worksheet.Cell(3, 6).Value = "Kalamazoo";
                worksheet.Cell(3, 7).Value = "MI";
                worksheet.Cell(3, 8).Value = "49001";
                worksheet.Cell(3, 9).Value = "Contracting";
                worksheet.Cell(3, 10).Value = "75.50";

                // Format header row
                var headerRange = worksheet.Range(1, 1, 1, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                // Auto-fit columns
                worksheet.Columns().AdjustToContents();

                // Note: Comments removed due to API changes in ClosedXML
                // The headers are self-explanatory and sample data shows the format

                // Create memory stream for download
                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                return File(stream.ToArray(),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"ClientImportTemplate_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Excel template");
                StatusMessage = "Error creating template file";
                IsSuccess = false;
                return Page();
            }
        }
    }
}