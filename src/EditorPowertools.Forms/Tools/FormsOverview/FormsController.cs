using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.FormsOverview;

/// <summary>
/// Serves the page views for the Forms add-on tools.
/// Mirrors the central <c>EditorPowertoolsController</c> pattern in the base library —
/// thin actions whose names match the menu URL slugs.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class EditorPowertoolsFormsController : Controller
{
    private readonly FormsFeatureAccessChecker _accessChecker;

    public EditorPowertoolsFormsController(FormsFeatureAccessChecker accessChecker)
    {
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult FormsOverview()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.FormsOverview),
            EditorPowertoolsFormsPermissions.FormsOverview))
            return Forbid();

        return View("/Views/FormsOverview/Index.cshtml");
    }

    [HttpGet]
    public IActionResult SubmissionsTimeline()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(FormsFeatureToggles.SubmissionsTimeline),
            EditorPowertoolsFormsPermissions.SubmissionsTimeline))
            return Forbid();

        return View("/Views/SubmissionsTimeline/Index.cshtml");
    }
}
