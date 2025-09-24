// Services/Helpers/ICsvParsingHelper.cs & CsvParsingHelper.cs
using System.Text;

namespace AutumnRidgeUSA.Services.Helpers
{
    public interface ICsvParsingHelper
    {
        Task<ParsedFileResult> ParseCsvFile(StreamReader reader);
    }

    public class CsvParsingHelper : ICsvParsingHelper
    {
        public async Task<ParsedFileResult> ParseCsvFile(StreamReader reader)
        {
            var result = new ParsedFileResult();

            try
            {
                var headerLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(headerLine))
                {
                    result.ErrorMessage = "CSV file appears to be empty";
                    return result;
                }

                // Parse headers
                var headers = headerLine.Split(',').Select(h => h.Trim('"').ToLower()).ToArray();
                var columnMap = new Dictionary<string, int>();

                for (int i = 0; i < headers.Length; i++)
                {
                    columnMap[headers[i]] = i;
                }

                // Verify required columns
                var requiredColumns = new[] { "email", "firstname", "lastname", "role", "password" };
                foreach (var required in requiredColumns)
                {
                    if (!columnMap.ContainsKey(required))
                    {
                        result.ErrorMessage = $"Required column '{required}' not found in CSV. Available columns: {string.Join(", ", headers)}";
                        return result;
                    }
                }

                // Parse data rows
                string? line;
                int rowNumber = 1;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    rowNumber++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = ParseCsvLine(line);
                    if (values.Length < columnMap.Count)
                    {
                        continue; // Skip incomplete rows
                    }

                    var userData = new UserData
                    {
                        RowNumber = rowNumber,
                        Email = GetColumnValue(values, columnMap, "email")?.Trim(),
                        FirstName = GetColumnValue(values, columnMap, "firstname")?.Trim(),
                        LastName = GetColumnValue(values, columnMap, "lastname")?.Trim(),
                        Role = GetColumnValue(values, columnMap, "role")?.Trim(),
                        Password = GetColumnValue(values, columnMap, "password")?.Trim(),
                        Phone = GetColumnValue(values, columnMap, "phone"),
                        PhoneType = GetColumnValue(values, columnMap, "phonetype"),
                        Phone2 = GetColumnValue(values, columnMap, "phone2"),
                        Phone2Type = GetColumnValue(values, columnMap, "phone2type"),
                        Address = GetColumnValue(values, columnMap, "address"),
                        City = GetColumnValue(values, columnMap, "city"),
                        State = GetColumnValue(values, columnMap, "state"),
                        ZipCode = GetColumnValue(values, columnMap, "zipcode") ?? GetColumnValue(values, columnMap, "zip")
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
    }
}
