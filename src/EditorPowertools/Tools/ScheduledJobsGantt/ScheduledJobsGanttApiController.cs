using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ScheduledJobsGantt;

/// <summary>
/// API controller for Scheduled Jobs Gantt chart data.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ScheduledJobsGanttApiController : Controller
{
    private readonly ScheduledJobsGanttService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ScheduledJobsGanttApiController(
        ScheduledJobsGanttService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    [Route("editorpowertools/api/jobs-gantt/jobs")]
    public IActionResult GetJobs()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ScheduledJobsGantt),
            EditorPowertoolsPermissions.ScheduledJobsGantt))
            return Forbid();

        var jobs = _service.GetAllJobs();
        return Ok(jobs);
    }

    [HttpGet]
    [Route("editorpowertools/api/jobs-gantt/executions")]
    public async Task<IActionResult> GetExecutions([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ScheduledJobsGantt),
            EditorPowertoolsPermissions.ScheduledJobsGantt))
            return Forbid();

        var fromUtc = from.Kind == DateTimeKind.Utc ? from : from.ToUniversalTime();
        var toUtc = to.Kind == DateTimeKind.Utc ? to : to.ToUniversalTime();

        var executions = await _service.GetAllExecutionHistoryAsync(fromUtc, toUtc);
        return Ok(executions);
    }

    [HttpGet]
    [Route("editorpowertools/api/jobs-gantt/gantt-data")]
    public async Task<IActionResult> GetGanttData([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ScheduledJobsGantt),
            EditorPowertoolsPermissions.ScheduledJobsGantt))
            return Forbid();

        var fromUtc = from.Kind == DateTimeKind.Utc ? from : from.ToUniversalTime();
        var toUtc = to.Kind == DateTimeKind.Utc ? to : to.ToUniversalTime();

        var data = await _service.GetGanttDataAsync(fromUtc, toUtc);
        return Ok(data);
    }
}
