using System.Globalization;
using ClosedXML.Excel;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;

public class ExcelFileParser : IFileParser
{
    public bool CanParse(string fileExtension)
        => fileExtension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".xls", StringComparison.OrdinalIgnoreCase);

    public ParseResult Parse(Stream stream, string fileName)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());

        var rowCount = ws.LastRowUsed()?.RowNumber() ?? 0;
        var colCount = ws.LastColumnUsed()?.ColumnNumber() ?? 0;

        if (rowCount == 0 || colCount == 0)
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());

        // Read headers from row 1
        var columns = new List<string>();
        for (var col = 1; col <= colCount; col++)
        {
            var header = ws.Cell(1, col).GetString().Trim();
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
                var value = ReadCellInvariant(ws.Cell(row, col));
                rowData[columns[col - 1]] = value;
                if (!string.IsNullOrWhiteSpace(value)) hasData = true;
            }

            if (hasData) rows.Add(rowData);
        }

        return new ParseResult(columns, rows);
    }

    // Reads a cell as an invariant-culture string so numeric / date values
    // serialize consistently regardless of the host machine's culture.
    private static string ReadCellInvariant(IXLCell cell)
    {
        var v = cell.Value;
        if (v.IsBlank) return "";
        if (v.IsNumber) return v.GetNumber().ToString(CultureInfo.InvariantCulture);
        if (v.IsDateTime) return v.GetDateTime().ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        if (v.IsBoolean) return v.GetBoolean() ? "true" : "false";
        if (v.IsTimeSpan) return v.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture);
        if (v.IsError) return "";
        return v.GetText();
    }
}
