namespace EditorPowertools.Tools.ContentImporter.Parsers;

public interface IFileParser
{
    bool CanParse(string fileExtension);
    ParseResult Parse(Stream stream, string fileName);
}

public record ParseResult(List<string> Columns, List<Dictionary<string, string>> Rows);
