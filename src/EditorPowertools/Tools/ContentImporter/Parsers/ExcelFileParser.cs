using System.Globalization;
using OfficeOpenXml;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;

public class ExcelFileParser : IFileParser
{
    public bool CanParse(string fileExtension)
        => fileExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase);

    public ParseResult Parse(Stream stream, string fileName)
    {
        using var package = new ExcelPackage(stream);
        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
        if (worksheet == null)
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());

        var rowCount = worksheet.Dimension?.Rows ?? 0;
        var colCount = worksheet.Dimension?.Columns ?? 0;

        if (rowCount == 0 || colCount == 0)
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());

        // Read headers from row 1
        var columns = new List<string>();
        for (var col = 1; col <= colCount; col++)
        {
            var header = worksheet.Cells[1, col].Text?.Trim();
            columns.Add(string.IsNullOrEmpty(header) ? $"Column{col}" : header);
        }

        // Read data rows
        var rows = new List<Dictionary<string, string>>();
        for (var row = 2; row <= rowCount; row++)
        {
            var rowData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var hasData = false;

            for (var col = 1; col <= colCount; col++)
            {
                var cellValue = worksheet.Cells[row, col].Value;
                var value = cellValue is IFormattable f
                    ? f.ToString(null, CultureInfo.InvariantCulture)
                    : cellValue?.ToString() ?? "";
                rowData[columns[col - 1]] = value;
                if (!string.IsNullOrWhiteSpace(value)) hasData = true;
            }

            if (hasData) rows.Add(rowData);
        }

        return new ParseResult(columns, rows);
    }
}
