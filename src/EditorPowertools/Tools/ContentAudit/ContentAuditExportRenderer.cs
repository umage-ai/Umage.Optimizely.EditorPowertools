using System.Text;
using System.Text.Json;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit.Models;
using ClosedXML.Excel;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentAudit;

/// <summary>
/// Pure rendering — converts ContentAuditRow collections to XLSX, CSV, or JSON bytes.
/// No Optimizely dependencies; easily unit-tested.
/// </summary>
public class ContentAuditExportRenderer
{
    public byte[] RenderXlsx(IEnumerable<ContentAuditRow> rows, List<string> columns)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Content Audit");

        for (int c = 0; c < columns.Count; c++)
        {
            var headerCell = ws.Cell(1, c + 1);
            headerCell.Value = GetColumnLabel(columns[c]);
            headerCell.Style.Font.Bold = true;
        }

        int r = 2;
        foreach (var row in rows)
        {
            for (int c = 0; c < columns.Count; c++)
                ws.Cell(r, c + 1).Value = ToCellValue(GetCellValue(row, columns[c]));
            r++;
        }

        for (int c = 1; c <= columns.Count; c++)
        {
            ws.Column(c).AdjustToContents();
            if (ws.Column(c).Width > 50)
                ws.Column(c).Width = 50;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null            => Blank.Value,
        string s        => s,
        bool b          => b,
        int i           => i,
        long l          => l,
        double d        => d,
        decimal m       => m,
        DateTime dt     => dt,
        TimeSpan ts     => ts,
        _               => value.ToString() ?? string.Empty
    };

    public byte[] RenderCsv(IEnumerable<ContentAuditRow> rows, List<string> columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(c => CsvEscape(GetColumnLabel(c)))));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", columns.Select(c => CsvEscape(GetCellValue(row, c)?.ToString() ?? ""))));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    public byte[] RenderJson(IEnumerable<ContentAuditRow> rows)
    {
        var json = JsonSerializer.Serialize(rows.ToList(), new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return Encoding.UTF8.GetBytes(json);
    }

    public string GetExtension(string format) => format.ToLowerInvariant() switch
    {
        "xlsx" => ".xlsx",
        "csv"  => ".csv",
        "json" => ".json",
        _      => ".bin"
    };

    public string GetContentType(string format) => format.ToLowerInvariant() switch
    {
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "csv"  => "text/csv",
        "json" => "application/json",
        _      => "application/octet-stream"
    };

    public static object? GetCellValue(ContentAuditRow row, string column) =>
        column.ToLowerInvariant() switch
        {
            "contentid"          => row.ContentId,
            "name"               => row.Name,
            "language"           => row.Language,
            "contenttype"        => row.ContentType,
            "maintype"           => row.MainType,
            "url"                => row.Url,
            "editurl"            => row.EditUrl,
            "breadcrumb"         => row.Breadcrumb,
            "status"             => row.Status,
            "createdby"          => row.CreatedBy,
            "created"            => row.Created?.ToString("yyyy-MM-dd HH:mm"),
            "changedby"          => row.ChangedBy,
            "changed"            => row.Changed?.ToString("yyyy-MM-dd HH:mm"),
            "published"          => row.Published?.ToString("yyyy-MM-dd HH:mm"),
            "publisheduntil"     => row.PublishedUntil?.ToString("yyyy-MM-dd HH:mm"),
            "masterlanguage"     => row.MasterLanguage,
            "alllanguages"       => row.AllLanguages,
            "referencecount"     => row.ReferenceCount,
            "versioncount"       => row.VersionCount,
            "haspersonalizations"=> row.HasPersonalizations == true ? "Yes" : "No",
            _                    => null
        };

    public static string GetColumnLabel(string column) =>
        column.ToLowerInvariant() switch
        {
            "contentid"          => "Content ID",
            "name"               => "Name",
            "language"           => "Language",
            "contenttype"        => "Content Type",
            "maintype"           => "Main Type",
            "url"                => "URL",
            "editurl"            => "Edit URL",
            "breadcrumb"         => "Breadcrumb",
            "status"             => "Status",
            "createdby"          => "Created By",
            "created"            => "Created",
            "changedby"          => "Changed By",
            "changed"            => "Changed",
            "published"          => "Published",
            "publisheduntil"     => "Published Until",
            "masterlanguage"     => "Master Language",
            "alllanguages"       => "All Languages",
            "referencecount"     => "Reference Count",
            "versioncount"       => "Version Count",
            "haspersonalizations"=> "Has Personalizations",
            _                    => column
        };

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
