// Services/Helpers/IExcelParsingHelper.cs & ExcelParsingHelper.cs
using ClosedXML.Excel;

public interface IExcelParsingHelper
{
    Task<ParsedFileResult> ParseExcelFile(Stream stream);
}

public class ExcelParsingHelper : IExcelParsingHelper
{
    public async Task<ParsedFileResult> ParseExcelFile(Stream stream)
    {
        var result = new ParsedFileResult();

        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);

            // Get header row and create column mapping
            var headerRow = worksheet.Row(1);
            var columnMap = new Dictionary<string, int>();

            for (int col = 1; col <= headerRow.LastCellUsed().Address.ColumnNumber; col++)
            {
                var columnName = headerRow.Cell(col).GetString().Trim().ToLower();
                columnMap[columnName] = col;
            }

            // Verify required columns
            var requiredColumns = new[] { "email", "firstname", "lastname", "role", "password" };
            foreach (var required in requiredColumns)
            {
                if (!columnMap.ContainsKey(required) && !columnMap.ContainsKey(required.Replace(" ", "")))
                {
                    result.ErrorMessage = $"Required column '{required}' not found in Excel file. Available columns: {string.Join(", ", columnMap.Keys)}";
                    return result;
                }
            }

            // Process data rows
            var dataRows = worksheet.RowsUsed().Skip(1);
            foreach (var xlRow in dataRows)
            {
                var row = xlRow as IXLRow;
                if (row == null) continue;

                var userData = new UserData
                {
                    RowNumber = row.RowNumber(),
                    Email = GetExcelCellValue(row, columnMap, new[] { "email", "e-mail", "emailaddress" })?.Trim(),
                    FirstName = GetExcelCellValue(row, columnMap, new[] { "firstname", "first name", "fname" })?.Trim(),
                    LastName = GetExcelCellValue(row, columnMap, new[] { "lastname", "last name", "lname" })?.Trim(),
                    Role = GetExcelCellValue(row, columnMap, new[] { "role" })?.Trim(),
                    Password = GetExcelCellValue(row, columnMap, new[] { "password", "pwd" })?.Trim(),
                    Phone = GetExcelCellValue(row, columnMap, new[] { "phone", "phonenumber", "phone number", "telephone" }),
                    PhoneType = GetExcelCellValue(row, columnMap, new[] { "phonetype", "phone type", "phone_type" }),
                    Phone2 = GetExcelCellValue(row, columnMap, new[] { "phone2", "phone 2", "second phone", "secondphone" }),
                    Phone2Type = GetExcelCellValue(row, columnMap, new[] { "phone2type", "phone 2 type", "phone2_type", "second phone type" }),
                    Address = GetExcelCellValue(row, columnMap, new[] { "address", "street", "streetaddress", "street address" }),
                    City = GetExcelCellValue(row, columnMap, new[] { "city" }),
                    State = GetExcelCellValue(row, columnMap, new[] { "state", "st" }),
                    ZipCode = GetExcelCellValue(row, columnMap, new[] { "zip", "zipcode", "zip code", "postal code" })
                };

                result.UserData.Add(userData);
            }

            result.IsValid = true;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private string? GetExcelCellValue(IXLRow row, Dictionary<string, int> columnMap, string[] possibleNames)
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

            // Try partial match
            var partialMatch = columnMap.FirstOrDefault(kvp =>
                kvp.Key.Contains(normalizedName) || normalizedName.Contains(kvp.Key));

            if (!partialMatch.Equals(default(KeyValuePair<string, int>)))
            {
                return row.Cell(partialMatch.Value).GetString();
            }
        }

        return null;
    }
}

// Shared models for parsing
public class ParsedFileResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public List<UserData> UserData { get; set; } = new();
}

public class UserData
{
    public int RowNumber { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
    public string? Phone { get; set; }
    public string? PhoneType { get; set; }
    public string? Phone2 { get; set; }
    public string? Phone2Type { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
}
