using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
using EditorPowertools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ContentStatistics;

/// <summary>
/// API controller for Content Statistics dashboard data.
/// The page view is served by EditorPowertoolsController.ContentStatistics().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class ContentStatisticsApiController : Controller
{
    private readonly ContentStatisticsService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly AggregationJobStatusService _aggregationJobService;

    public ContentStatisticsApiController(
        ContentStatisticsService service,
        FeatureAccessChecker accessChecker,
        AggregationJobStatusService aggregationJobService)
    {
        _service = service;
        _accessChecker = accessChecker;
        _aggregationJobService = aggregationJobService;
    }

    [HttpGet]
    [Route("editorpowertools/api/content-statistics/dashboard")]
    public IActionResult GetDashboard()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentStatistics),
            EditorPowertoolsPermissions.ContentStatistics))
            return Forbid();

        var dashboard = _service.GetDashboard();
        return Ok(dashboard);
    }

    [HttpPost]
    [Route("editorpowertools/api/content-statistics/aggregation-start")]
    public async Task<IActionResult> StartAggregationJob()
    {
        if (!_accessChecker.HasAccess(HttpContext,
                nameof(Configuration.FeatureToggles.ContentStatistics),
                EditorPowertoolsPermissions.ContentStatistics))
            return Forbid();

        var result = await _aggregationJobService.StartJobAsync();
        if (!result.Started)
        {
            return result.Reason == "already_running"
                ? Conflict(new { success = false, message = "Job is already running." })
                : StatusCode(503, new { success = false, message = "Job not found in scheduler." });
        }
        return Ok(new { success = true, started = true });
    }
}
