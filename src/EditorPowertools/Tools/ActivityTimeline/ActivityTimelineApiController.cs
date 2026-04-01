using EditorPowertools.Permissions;
using EditorPowertools.Tools.ActivityTimeline.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ActivityTimeline;

/// <summary>
/// API controller for Activity Timeline data endpoints.
/// The page view is served by EditorPowertoolsController.ActivityTimeline().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ActivityTimelineApiController : Controller
{
    private readonly ActivityTimelineService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ActivityTimelineApiController(
        ActivityTimelineService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    [Route("editorpowertools/api/activity/timeline")]
    public IActionResult GetTimeline(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? user = null,
        [FromQuery] string? action = null,
        [FromQuery] string? contentType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? contentId = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var request = new ActivityFilterRequest
        {
            Skip = skip,
            Take = take,
            User = user,
            Action = action,
            ContentTypeName = contentType,
            FromUtc = from,
            ToUtc = to,
            ContentId = contentId
        };

        var result = _service.GetActivities(request);
        return Ok(result);
    }

    [HttpGet]
    [Route("editorpowertools/api/activity/stats")]
    public IActionResult GetStats()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var stats = _service.GetStats();
        return Ok(stats);
    }

    [HttpGet]
    [Route("editorpowertools/api/activity/compare/{contentId}/{versionId}")]
    public IActionResult CompareVersions(int contentId, int versionId, [FromQuery] string? language = null)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var result = _service.CompareVersions(contentId, versionId, language);
        return Ok(result);
    }

    [HttpGet]
    [Route("editorpowertools/api/activity/users")]
    public IActionResult GetUsers()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var users = _service.GetDistinctUsers();
        return Ok(users);
    }

    [HttpGet]
    [Route("editorpowertools/api/activity/content-types")]
    public IActionResult GetContentTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        var types = _service.GetDistinctContentTypes();
        return Ok(types);
    }
}
