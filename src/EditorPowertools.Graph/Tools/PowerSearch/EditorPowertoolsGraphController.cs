using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;

/// <summary>
/// Serves the page views for the Graph add-on tools. Thin actions whose names match the menu URL slugs.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class EditorPowertoolsGraphController : Controller
{
    private readonly GraphFeatureAccessChecker _accessChecker;

    public EditorPowertoolsGraphController(GraphFeatureAccessChecker accessChecker)
    {
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult PowerSearch()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(GraphFeatureToggles.PowerSearch),
            EditorPowertoolsGraphPermissions.PowerSearch))
            return Forbid();

        return View("/Views/PowerSearch/Index.cshtml");
    }
}
