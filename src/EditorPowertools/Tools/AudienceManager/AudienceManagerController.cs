using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.AudienceManager;

/// <summary>
/// API-only controller for Audience Manager data endpoints.
/// The page view is served by EditorPowertoolsController.AudienceManager().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class AudienceManagerApiController : Controller
{
    private readonly AudienceManagerService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public AudienceManagerApiController(
        AudienceManagerService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    [Route("editorpowertools/api/audience/visitor-groups")]
    public IActionResult GetVisitorGroups()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        var groups = _service.GetAllVisitorGroups();
        return Ok(groups);
    }

    [HttpGet]
    [Route("editorpowertools/api/audience/visitor-groups/{id}/criteria")]
    public IActionResult GetCriteria(Guid id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        var criteria = _service.GetCriteria(id);
        return Ok(criteria);
    }

    [HttpGet]
    [Route("editorpowertools/api/audience/visitor-groups/{id}/usages")]
    public IActionResult GetUsages(Guid id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        var usages = _service.GetUsages(id);
        return Ok(usages);
    }
}
