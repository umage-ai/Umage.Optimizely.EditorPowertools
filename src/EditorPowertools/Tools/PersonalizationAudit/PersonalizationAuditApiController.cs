using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;

/// <summary>
/// API-only controller for Personalization Audit data endpoints.
/// The page view is served by EditorPowertoolsController.PersonalizationAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class PersonalizationAuditApiController : Controller
{
    private readonly PersonalizationAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly PersonalizationJobStatusService _jobStatusService;

    public PersonalizationAuditApiController(
        PersonalizationAuditService service,
        FeatureAccessChecker accessChecker,
        PersonalizationJobStatusService jobStatusService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _jobStatusService = jobStatusService;
    }

    [HttpGet]
    public IActionResult GetUsages()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.PersonalizationUsageAudit),
            EditorPowertoolsPermissions.PersonalizationUsageAudit))
            return Forbid();

        var usages = _service.GetAllUsages();
        return Ok(usages);
    }

    [HttpGet]
    public IActionResult GetVisitorGroups()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.PersonalizationUsageAudit),
            EditorPowertoolsPermissions.PersonalizationUsageAudit))
            return Forbid();

        var groups = _service.GetVisitorGroups();
        return Ok(groups);
    }

    [HttpGet]
    public IActionResult GetJobStatus()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.PersonalizationUsageAudit),
            EditorPowertoolsPermissions.PersonalizationUsageAudit))
            return Forbid();

        var status = _jobStatusService.GetStatus();
        return Ok(status);
    }

    [HttpPost]
    public async Task<IActionResult> StartJob()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.PersonalizationUsageAudit),
            EditorPowertoolsPermissions.PersonalizationUsageAudit))
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
