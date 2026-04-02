using EditorPowertools.Infrastructure;
using EditorPowertools.Permissions;
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

    public ContentStatisticsApiController(
        ContentStatisticsService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
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
}
