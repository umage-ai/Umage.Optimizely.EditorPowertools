using System.Text.Json;
using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using EditorPowertools.Tools.ContentAudit.Models;
using EPiServer.Data.Dynamic;
using EPiServer.DataAbstraction;
using EPiServer.Scheduler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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
    private readonly ContentAuditExportRenderer _renderer;
    private readonly IScheduledJobRepository _jobRepository;
    private readonly IScheduledJobExecutor _jobExecutor;
    private readonly DynamicDataStoreFactory _storeFactory;
    private readonly ILogger<ContentAuditApiController> _logger;

    public ContentAuditApiController(
        ContentAuditService service,
        FeatureAccessChecker accessChecker,
        ContentAuditExportRenderer renderer,
        IScheduledJobRepository jobRepository,
        IScheduledJobExecutor jobExecutor,
        DynamicDataStoreFactory storeFactory,
        ILogger<ContentAuditApiController> logger)
    {
        _service       = service;
        _accessChecker = accessChecker;
        _renderer      = renderer;
        _jobRepository = jobRepository;
        _jobExecutor   = jobExecutor;
        _storeFactory  = storeFactory;
        _logger        = logger;
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
                "xlsx" => File(
                    _renderer.RenderXlsx(allRows, parsedColumns),
                    _renderer.GetContentType("xlsx"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx"),
                "csv" => File(
                    _renderer.RenderCsv(allRows, parsedColumns),
                    _renderer.GetContentType("csv"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"),
                "json" => File(
                    _renderer.RenderJson(allRows),
                    _renderer.GetContentType("json"),
                    $"content-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json"),
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

    /// <summary>
    /// Saves an export request to DDS and triggers the ContentAuditExportJob.
    /// Returns a requestId the client polls with /export-status.
    /// </summary>
    [HttpPost("export-request")]
    [RequireAjax]
    public async Task<IActionResult> RequestExport(
        [FromQuery] string format = "xlsx",
        [FromQuery] string? columns = null,
        [FromQuery] string? mainTypeFilter = null,
        [FromQuery] string? quickFilter = null,
        [FromQuery] string? search = null,
        [FromQuery] string? filters = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
                nameof(Configuration.FeatureToggles.ContentAudit),
                EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            var requestId = Guid.NewGuid();
            var record = new ContentAuditExportJobRequest
            {
                RequestId      = requestId,
                RequestedBy    = User.Identity?.Name ?? "unknown",
                RequestedAt    = DateTime.UtcNow,
                Format         = format,
                Columns        = columns,
                MainTypeFilter = mainTypeFilter,
                QuickFilter    = quickFilter,
                Search         = search,
                FiltersJson    = filters,
                Status         = "Pending"
            };

            var store = GetStore();
            store.Save(record);

            // Trigger the job (best-effort — job may already be running)
            var job = _jobRepository.List()
                .FirstOrDefault(j => j.TypeName?.Contains("ContentAuditExportJob", StringComparison.OrdinalIgnoreCase) == true);

            if (job != null && !job.IsRunning)
                await _jobExecutor.StartAsync(job);

            return Ok(new { success = true, requestId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue content audit export");
            return StatusCode(500, new { success = false, message = "Failed to queue export." });
        }
    }

    /// <summary>
    /// Returns the status of an export request. When Status == "Completed",
    /// includes the ContentLink ID so the client can construct a download URL.
    /// </summary>
    [HttpGet("export-status")]
    public IActionResult GetExportStatus([FromQuery] Guid requestId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
                nameof(Configuration.FeatureToggles.ContentAudit),
                EditorPowertoolsPermissions.ContentAudit))
            return Forbid();

        try
        {
            var store  = GetStore();
            var record = store.Items<ContentAuditExportJobRequest>()
                .FirstOrDefault(r => r.RequestId == requestId);

            if (record == null)
                return NotFound(new { success = false, message = "Export request not found." });

            return Ok(new
            {
                success         = true,
                status          = record.Status,
                resultContentId = record.ResultContentId,
                errorMessage    = record.ErrorMessage,
                completedAt     = record.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get export status for {RequestId}", requestId);
            return StatusCode(500, new { success = false, message = "Failed to get export status." });
        }
    }

    private DynamicDataStore GetStore() =>
        _storeFactory.GetStore(typeof(ContentAuditExportJobRequest))
        ?? _storeFactory.CreateStore(typeof(ContentAuditExportJobRequest));

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
