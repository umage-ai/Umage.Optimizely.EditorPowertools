using System.Text;
using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.SecurityAudit;

/// <summary>
/// API controller for Security Audit data endpoints.
/// The page view is served by EditorPowertoolsController.SecurityAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api/security-audit")]
public class SecurityAuditApiController : Controller
{
    private readonly SecurityAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public SecurityAuditApiController(
        SecurityAuditService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    // --- Content Tree View ---

    [HttpGet("tree/children")]
    public IActionResult GetChildren([FromQuery] int parentId = 0)
    {
        if (!HasAccess()) return Forbid();

        var children = _service.GetChildren(parentId);
        return Ok(children);
    }

    [HttpGet("tree/node/{contentId:int}")]
    public IActionResult GetNodeDetail(int contentId)
    {
        if (!HasAccess()) return Forbid();

        var node = _service.GetNodeDetail(contentId);
        if (node == null) return NotFound();

        return Ok(node);
    }

    [HttpGet("tree/path/{contentId:int}")]
    public IActionResult GetPathToContent(int contentId)
    {
        if (!HasAccess()) return Forbid();

        var path = _service.GetPathToContent(contentId);
        return Ok(path);
    }

    // --- Role/User Explorer ---

    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        if (!HasAccess()) return Forbid();

        var roles = _service.GetAllRolesAndUsers();
        return Ok(roles);
    }

    [HttpGet("roles/{name}/content")]
    public IActionResult GetContentForRole(
        string name,
        [FromQuery] string entityType = "Role",
        [FromQuery] string? access = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!HasAccess()) return Forbid();

        var result = _service.GetContentForRoleOrUser(name, entityType, access, page, pageSize);
        return Ok(result);
    }

    // --- Issues Dashboard ---

    [HttpGet("issues/summary")]
    public IActionResult GetIssuesSummary()
    {
        if (!HasAccess()) return Forbid();

        var summary = _service.GetIssuesSummary();
        return Ok(summary);
    }

    [HttpGet("issues")]
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

    [HttpGet("status")]
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

    [HttpPost("export")]
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
