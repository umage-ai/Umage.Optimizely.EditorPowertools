using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.AudienceManager;

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
    public IActionResult GetCriteria([FromQuery] Guid id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        var criteria = _service.GetCriteria(id);
        return Ok(criteria);
    }

    [HttpGet]
    public IActionResult GetUsages([FromQuery] Guid id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        var usages = _service.GetUsages(id);
        return Ok(usages);
    }
}
