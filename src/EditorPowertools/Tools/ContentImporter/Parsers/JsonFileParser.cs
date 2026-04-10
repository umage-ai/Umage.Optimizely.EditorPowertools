using System.Text.Json;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentImporter.Parsers;

public class JsonFileParser : IFileParser
{
    public bool CanParse(string fileExtension)
        => fileExtension.Equals(".json", StringComparison.OrdinalIgnoreCase);

    public ParseResult Parse(Stream stream, string fileName)
    {
        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        // Support both array of objects and { "data": [...] } wrapper
        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            // Find first array property
            array = root.EnumerateObject()
                .Where(p => p.Value.ValueKind == JsonValueKind.Array)
                .Select(p => p.Value)
                .FirstOrDefault();
            if (array.ValueKind != JsonValueKind.Array)
                return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());
        }
        else
        {
            return new ParseResult(new List<string>(), new List<Dictionary<string, string>>());
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<Dictionary<string, string>>();

        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            FlattenObject(element, "", row);
            foreach (var key in row.Keys)
                columns.Add(key);
            rows.Add(row);
        }

        return new ParseResult(columns.ToList(), rows);
    }

    private static void FlattenObject(JsonElement element, string prefix, Dictionary<string, string> row)
    {
        foreach (var prop in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.Object:
                    FlattenObject(prop.Value, key, row);
                    break;
                case JsonValueKind.Array:
                    row[key] = prop.Value.GetRawText();
                    break;
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    row[key] = "";
                    break;
                default:
                    row[key] = prop.Value.ToString();
                    break;
            }
        }
    }
}
