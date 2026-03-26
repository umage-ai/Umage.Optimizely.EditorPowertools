using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.PersonalizationAudit;

/// <summary>
/// API-only controller for Personalization Audit data endpoints.
/// The page view is served by EditorPowertoolsController.PersonalizationAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
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
    [Route("editorpowertools/api/personalization/usages")]
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
    [Route("editorpowertools/api/personalization/visitor-groups")]
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
    [Route("editorpowertools/api/personalization/job-status")]
    public IActionResult GetJobStatus()
    {
        var status = _jobStatusService.GetStatus();
        return Ok(status);
    }

    [HttpPost]
    [Route("editorpowertools/api/personalization/job-start")]
    public async Task<IActionResult> StartJob()
    {
        var started = await _jobStatusService.StartJobAsync();
        return Ok(new { started });
    }
}
