using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace EditorPowertools.Tools.ContentImporter.Parsers;

public class CsvFileParser : IFileParser
{
    public bool CanParse(string fileExtension)
        => fileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".tsv", StringComparison.OrdinalIgnoreCase);

    public ParseResult Parse(Stream stream, string fileName)
    {
        var isTsv = Path.GetExtension(fileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase);
        var delimiter = isTsv ? "\t" : DetectDelimiter(stream);
        stream.Position = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = delimiter,
            HasHeaderRecord = true,
            BadDataFound = null,
            MissingFieldFound = null,
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        if (!csv.Read())
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());
        csv.ReadHeader();
        var columns = csv.HeaderRecord?.ToList() ?? new List<string>();

        var rows = new List<Dictionary<string, string>>();
        while (csv.Read())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < columns.Count; i++)
            {
                row[columns[i]] = csv.GetField(i) ?? "";
            }
            rows.Add(row);
        }

        return new ParseResult(columns, rows);
    }

    private static string DetectDelimiter(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var firstLine = reader.ReadLine() ?? "";

        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');
        var tabs = firstLine.Count(c => c == '\t');

        if (tabs > commas && tabs > semicolons) return "\t";
        if (semicolons > commas) return ";";
        return ",";
    }
}
