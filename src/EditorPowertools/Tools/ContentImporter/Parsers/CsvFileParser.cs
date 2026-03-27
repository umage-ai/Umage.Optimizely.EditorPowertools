using System.Text;

namespace EditorPowertools.Tools.ContentImporter.Parsers;

public class CsvFileParser : IFileParser
{
    public bool CanParse(string fileExtension)
        => fileExtension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
        || fileExtension.Equals(".tsv", StringComparison.OrdinalIgnoreCase);

    public ParseResult Parse(Stream stream, string fileName)
    {
        var isTsv = Path.GetExtension(fileName).Equals(".tsv", StringComparison.OrdinalIgnoreCase);
        var delimiter = isTsv ? '\t' : DetectDelimiter(stream);
        stream.Position = 0;

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var lines = new List<string[]>();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            lines.Add(ParseLine(line, delimiter));
        }

        if (lines.Count == 0)
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());

        var columns = lines[0].Select(c => c.Trim()).ToList();
        var rows = new List<Dictionary<string, string>>();

        for (var i = 1; i < lines.Count; i++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < columns.Count; j++)
            {
                row[columns[j]] = j < lines[i].Length ? lines[i][j] : "";
            }
            rows.Add(row);
        }

        return new ParseResult(columns, rows);
    }

    private static char DetectDelimiter(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        var firstLine = reader.ReadLine() ?? "";

        // Check common delimiters by count
        var semicolons = firstLine.Count(c => c == ';');
        var commas = firstLine.Count(c => c == ',');
        var tabs = firstLine.Count(c => c == '\t');

        if (tabs > commas && tabs > semicolons) return '\t';
        if (semicolons > commas) return ';';
        return ',';
    }

    private static string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip escaped quote
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == delimiter)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }
}
