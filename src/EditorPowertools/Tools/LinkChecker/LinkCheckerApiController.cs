using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.LinkChecker;

/// <summary>
/// API-only controller for Link Checker data endpoints.
/// The page view is served by EditorPowertoolsController.LinkChecker().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class LinkCheckerApiController : Controller
{
    private readonly LinkCheckerService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly LinkCheckerJobStatusService _jobStatusService;

    public LinkCheckerApiController(
        LinkCheckerService service,
        FeatureAccessChecker accessChecker,
        LinkCheckerJobStatusService jobStatusService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _jobStatusService = jobStatusService;
    }

    [HttpGet]
    [Route("editorpowertools/api/link-checker/links")]
    public IActionResult GetLinks([FromQuery] bool brokenOnly = false)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BrokenLinkChecker),
            EditorPowertoolsPermissions.BrokenLinkChecker))
            return Forbid();

        var links = brokenOnly ? _service.GetBrokenLinks() : _service.GetAllLinks();
        return Ok(links);
    }

    [HttpGet]
    [Route("editorpowertools/api/link-checker/stats")]
    public IActionResult GetStats()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BrokenLinkChecker),
            EditorPowertoolsPermissions.BrokenLinkChecker))
            return Forbid();

        var stats = _service.GetStats();
        return Ok(stats);
    }

    [HttpGet]
    [Route("editorpowertools/api/link-checker/job-status")]
    public IActionResult GetJobStatus()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BrokenLinkChecker),
            EditorPowertoolsPermissions.BrokenLinkChecker))
            return Forbid();

        var status = _jobStatusService.GetStatus();
        return Ok(status);
    }

    [HttpPost]
    [Route("editorpowertools/api/link-checker/job-start")]
    public async Task<IActionResult> StartJob()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BrokenLinkChecker),
            EditorPowertoolsPermissions.BrokenLinkChecker))
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
