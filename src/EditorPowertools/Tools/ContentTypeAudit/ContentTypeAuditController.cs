using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ContentTypeAudit;

/// <summary>
/// API-only controller for Content Type Audit data endpoints.
/// The page view is served by EditorPowertoolsController.ContentTypeAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class ContentTypeAuditApiController : Controller
{
    private readonly ContentTypeAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly AggregationJobStatusService _jobStatusService;

    public ContentTypeAuditApiController(
        ContentTypeAuditService service,
        FeatureAccessChecker accessChecker,
        AggregationJobStatusService jobStatusService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _jobStatusService = jobStatusService;
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types")]
    public IActionResult GetTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var types = _service.GetAllContentTypes();
        return Ok(types);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/{id}/properties")]
    public IActionResult GetProperties(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var properties = _service.GetProperties(id);
        return Ok(properties);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/{id}/content")]
    public IActionResult GetContentOfType(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var content = _service.GetContentOfType(id);
        return Ok(content);
    }

    [HttpGet]
    [Route("editorpowertools/api/content/{id}/references")]
    public IActionResult GetContentReferences(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var references = _service.GetContentReferences(id);
        return Ok(references);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/inheritance-tree")]
    public IActionResult GetInheritanceTree()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var tree = _service.GetInheritanceTree();
        return Ok(tree);
    }

    [HttpGet]
    [Route("editorpowertools/api/aggregation-status")]
    public IActionResult GetAggregationStatus()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var status = _jobStatusService.GetStatus();
        return Ok(status);
    }

    [HttpPost]
    [Route("editorpowertools/api/aggregation-start")]
    public async Task<IActionResult> StartAggregationJob()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var result = await _jobStatusService.StartJobAsync();
        if (!result.Started)
        {
            return result.Reason == "already_running"
                ? Conflict(new { success = false, message = "Job is already running." })
                : StatusCode(503, new { success = false, message = "Job not found in scheduler. Ensure the site has been restarted after installing the plugin." });
        }
        return Ok(new { success = true, started = true });
    }
}
