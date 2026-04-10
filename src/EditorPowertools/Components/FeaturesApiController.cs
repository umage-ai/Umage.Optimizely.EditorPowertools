using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace UmageAI.Optimizely.EditorPowerTools.Components;

/// <summary>
/// Returns which UmageAI.Optimizely.EditorPowerTools features are enabled for the current user.
/// Used by the client-side module initializer to conditionally register commands.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api")]
public class FeaturesApiController : Controller
{
    private readonly FeatureAccessChecker _accessChecker;

    public FeaturesApiController(FeatureAccessChecker accessChecker)
    {
        _accessChecker = accessChecker;
    }

    [HttpGet("features")]
    public IActionResult GetFeatures()
    {
        return Ok(new
        {
            ActivityTimeline = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ActivityTimeline),
                EditorPowertoolsPermissions.ActivityTimeline),
            ManageChildren = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ManageChildren),
                EditorPowertoolsPermissions.ManageChildren),
            ActiveEditors = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ActiveEditors),
                EditorPowertoolsPermissions.ActiveEditors),
            ActiveEditorsChat = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.ActiveEditorsChat),
                EditorPowertoolsPermissions.ActiveEditors),
            SecurityAudit = _accessChecker.HasAccess(HttpContext,
                nameof(FeatureToggles.SecurityAudit),
                EditorPowertoolsPermissions.SecurityAudit)
        });
    }
}
