using System.Text;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.SecurityAudit;

/// <summary>
/// API controller for Security Audit data endpoints.
/// The page view is served by EditorPowertoolsController.SecurityAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class SecurityAuditApiController : Controller
{
    private readonly SecurityAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly AggregationJobStatusService _aggregationJobService;

    public SecurityAuditApiController(
        SecurityAuditService service,
        FeatureAccessChecker accessChecker,
        AggregationJobStatusService aggregationJobService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _aggregationJobService = aggregationJobService;
    }

    // --- Content Tree View ---

    [HttpGet]
    public IActionResult GetChildren([FromQuery] int parentId = 0)
    {
        if (!HasAccess()) return Forbid();

        var children = _service.GetChildren(parentId);
        return Ok(children);
    }

    [HttpGet]
    public IActionResult GetNodeDetail(int id)
    {
        if (!HasAccess()) return Forbid();

        var node = _service.GetNodeDetail(id);
        if (node == null) return NotFound();

        return Ok(node);
    }

    [HttpGet]
    public IActionResult GetPathToContent(int id)
    {
        if (!HasAccess()) return Forbid();

        var path = _service.GetPathToContent(id);
        return Ok(path);
    }

    // --- Role/User Explorer ---

    [HttpGet]
    public IActionResult GetRoles()
    {
        if (!HasAccess()) return Forbid();

        var roles = _service.GetAllRolesAndUsers();
        return Ok(roles);
    }

    [HttpGet]
    public IActionResult GetContentForRole(
        [FromQuery] string id,
        [FromQuery] string entityType = "Role",
        [FromQuery] string? access = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!HasAccess()) return Forbid();

        var result = _service.GetContentForRoleOrUser(id, entityType, access, page, pageSize);
        return Ok(result);
    }

    // --- Issues Dashboard ---

    [HttpGet]
    public IActionResult GetIssuesSummary()
    {
        if (!HasAccess()) return Forbid();

        var summary = _service.GetIssuesSummary();
        return Ok(summary);
    }

    [HttpGet]
    public IActionResult GetIssues(
        [FromQuery] string? type = null,
        [FromQuery] string? severity = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!HasAccess()) return Forbid();

        var result = _service.GetIssues(type, severity, page, pageSize);
        return Ok(result);
    }

    // --- Utility ---

    [HttpGet]
    public IActionResult GetStatus()
    {
        if (!HasAccess()) return Forbid();

        var lastAnalysis = _service.GetLastAnalysisTime();
        var summary = _service.GetIssuesSummary();

        return Ok(new
        {
            LastAnalysis = lastAnalysis,
            TotalIssues = summary.TotalIssues,
            HasData = lastAnalysis.HasValue
        });
    }

    [HttpPost]
    [RequireAjax]
    public async Task<IActionResult> StartAggregationJob()
    {
        if (!HasAccess()) return Forbid();

        var result = await _aggregationJobService.StartJobAsync();
        if (!result.Started)
        {
            return result.Reason == "already_running"
                ? Conflict(new { success = false, message = "Job is already running." })
                : StatusCode(503, new { success = false, message = "Job not found in scheduler." });
        }
        return Ok(new { success = true, started = true });
    }

    [HttpPost]
    [RequireAjax]
    public IActionResult Export()
    {
        if (!HasAccess()) return Forbid();

        var rows = _service.ExportAll().ToList();
        var csv = new StringBuilder();
        csv.AppendLine("ContentId,ContentName,Breadcrumb,ContentType,IsPage,AclEntries,IsInheriting,Issues");

        foreach (var row in rows)
        {
            csv.AppendLine(
                $"{row.ContentId}," +
                $"\"{EscapeCsv(row.ContentName)}\"," +
                $"\"{EscapeCsv(row.Breadcrumb ?? "")}\"," +
                $"\"{EscapeCsv(row.ContentTypeName ?? "")}\"," +
                $"{row.IsPage}," +
                $"\"{EscapeCsv(row.AclEntries)}\"," +
                $"{row.IsInheriting}," +
                $"\"{EscapeCsv(row.Issues)}\"");
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", "security-audit-export.csv");
    }

    private bool HasAccess()
    {
        return _accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.SecurityAudit),
            EditorPowertoolsPermissions.SecurityAudit);
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }
}
