using System.Text;
using System.Text.Json;
using EditorPowertools.Permissions;
using EditorPowertools.Tools.ContentAudit.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;

namespace EditorPowertools.Tools.ContentAudit;

/// <summary>
/// API controller for Content Audit operations.
/// The page view is served by EditorPowertoolsController.ContentAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api/content-audit")]
public class ContentAuditApiController : Controller
{
    private readonly ContentAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly ILogger<ContentAuditApiController> _logger;

    public ContentAuditApiController(
        ContentAuditService service,
        FeatureAccessChecker accessChecker,
        ILogger<ContentAuditApiController> logger)
    {
        _service = service;
        _accessChecker = accessChecker;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetContent(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortDirection = "asc",
        [FromQuery] string? search = null,
        [FromQuery] string? filters = null,
        [FromQuery] string? mainTypeFilter = null,
        [FromQuery] string? quickFilter = null,
        [FromQuery] string? columns = null,
        CancellationToken ct = default)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentAudit),
            EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            List<ContentAuditFilter>? parsedFilters = null;
            if (!string.IsNullOrEmpty(filters))
            {
                parsedFilters = JsonSerializer.Deserialize<List<ContentAuditFilter>>(filters,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            List<string>? parsedColumns = null;
            if (!string.IsNullOrEmpty(columns))
            {
                parsedColumns = columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }

            var request = new ContentAuditRequest
            {
                Page = page,
                PageSize = pageSize,
                SortBy = sortBy,
                SortDirection = sortDirection,
                Search = search,
                Filters = parsedFilters,
                MainTypeFilter = mainTypeFilter,
                QuickFilter = quickFilter,
                Columns = parsedColumns
            };

            ContentAuditResponse response = _service.GetContent(request, ct);
            return Ok(new { success = true, data = response });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { success = false, message = "Request cancelled." });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid filters JSON: {Filters}", filters);
            return BadRequest(new { success = false, message = "Invalid filters format." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get content audit data");
            return StatusCode(500, new { success = false, message = "Failed to get content audit data." });
        }
    }

    [HttpGet("export")]
    public IActionResult Export(
        [FromQuery] string format = "xlsx",
        [FromQuery] string? search = null,
        [FromQuery] string? filters = null,
        [FromQuery] string? mainTypeFilter = null,
        [FromQuery] string? quickFilter = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string sortDirection = "asc",
        [FromQuery] string? columns = null,
        CancellationToken ct = default)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentAudit),
            EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            List<ContentAuditFilter>? parsedFilters = null;
            if (!string.IsNullOrEmpty(filters))
            {
                parsedFilters = JsonSerializer.Deserialize<List<ContentAuditFilter>>(filters,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }

            List<string> parsedColumns = !string.IsNullOrEmpty(columns)
                ? columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : GetAllColumnKeys();

            var request = new ContentAuditExportRequest
            {
                Format = format,
                Search = search,
                Filters = parsedFilters,
                MainTypeFilter = mainTypeFilter,
                QuickFilter = quickFilter,
                SortBy = sortBy,
                SortDirection = sortDirection,
                Columns = parsedColumns
            };

            var allRows = _service.GetAllMatchingRows(request, ct).ToList();

            return format.ToLowerInvariant() switch
            {
                "xlsx" => ExportXlsx(allRows, parsedColumns),
                "csv" => ExportCsv(allRows, parsedColumns),
                "json" => ExportJson(allRows),
                _ => BadRequest(new { success = false, message = $"Unsupported format: {format}" })
            };
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { success = false, message = "Export cancelled." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export content audit data");
            return StatusCode(500, new { success = false, message = "Failed to export." });
        }
    }

    private FileContentResult ExportXlsx(List<ContentAuditRow> rows, List<string> columns)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Content Audit");

        // Headers
        for (int c = 0; c < columns.Count; c++)
        {
            worksheet.Cells[1, c + 1].Value = GetColumnLabel(columns[c]);
            worksheet.Cells[1, c + 1].Style.Font.Bold = true;
        }

        // Data
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                worksheet.Cells[r + 2, c + 1].Value = GetCellValue(rows[r], columns[c]);
            }
        }

        // Auto-fit columns (cap at 50 chars width)
        for (int c = 1; c <= columns.Count; c++)
        {
            worksheet.Column(c).AutoFit();
            if (worksheet.Column(c).Width > 50)
                worksheet.Column(c).Width = 50;
        }

        var bytes = package.GetAsByteArray();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    private FileContentResult ExportCsv(List<ContentAuditRow> rows, List<string> columns)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", columns.Select(c => CsvEscape(GetColumnLabel(c)))));

        // Data
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                columns.Select(c => CsvEscape(GetCellValue(row, c)?.ToString() ?? ""))));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    private FileContentResult ExportJson(List<ContentAuditRow> rows)
    {
        var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static object? GetCellValue(ContentAuditRow row, string column)
    {
        return column.ToLowerInvariant() switch
        {
            "contentid" => row.ContentId,
            "name" => row.Name,
            "language" => row.Language,
            "contenttype" => row.ContentType,
            "maintype" => row.MainType,
            "url" => row.Url,
            "editurl" => row.EditUrl,
            "breadcrumb" => row.Breadcrumb,
            "status" => row.Status,
            "createdby" => row.CreatedBy,
            "created" => row.Created?.ToString("yyyy-MM-dd HH:mm"),
            "changedby" => row.ChangedBy,
            "changed" => row.Changed?.ToString("yyyy-MM-dd HH:mm"),
            "published" => row.Published?.ToString("yyyy-MM-dd HH:mm"),
            "publisheduntil" => row.PublishedUntil?.ToString("yyyy-MM-dd HH:mm"),
            "masterlanguage" => row.MasterLanguage,
            "alllanguages" => row.AllLanguages,
            "referencecount" => row.ReferenceCount,
            "versioncount" => row.VersionCount,
            "haspersonalizations" => row.HasPersonalizations == true ? "Yes" : "No",
            _ => null
        };
    }

    private static string GetColumnLabel(string column)
    {
        return column.ToLowerInvariant() switch
        {
            "contentid" => "Content ID",
            "name" => "Name",
            "language" => "Language",
            "contenttype" => "Content Type",
            "maintype" => "Main Type",
            "url" => "URL",
            "editurl" => "Edit URL",
            "breadcrumb" => "Breadcrumb",
            "status" => "Status",
            "createdby" => "Created By",
            "created" => "Created",
            "changedby" => "Changed By",
            "changed" => "Changed",
            "published" => "Published",
            "publisheduntil" => "Published Until",
            "masterlanguage" => "Master Language",
            "alllanguages" => "All Languages",
            "referencecount" => "Reference Count",
            "versioncount" => "Version Count",
            "haspersonalizations" => "Has Personalizations",
            _ => column
        };
    }

    private static List<string> GetAllColumnKeys()
    {
        return
        [
            "contentId", "name", "language", "contentType", "mainType",
            "url", "editUrl", "breadcrumb", "status",
            "createdBy", "created", "changedBy", "changed",
            "published", "publishedUntil",
            "masterLanguage", "allLanguages",
            "referenceCount", "versionCount", "hasPersonalizations"
        ];
    }
}
