using EditorPowertools.Permissions;
using EditorPowertools.Tools.ContentTypeAudit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.Overview;

/// <summary>
/// Main controller for all Editor Powertools pages.
/// Actions map to menu items via Paths.ToResource("EditorPowertools/{ActionName}").
/// Each tool's business logic stays in its own service; this controller is thin.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class EditorPowertoolsController : Controller
{
    private readonly ContentTypeAuditService _contentTypeAuditService;
    private readonly FeatureAccessChecker _accessChecker;

    public EditorPowertoolsController(
        ContentTypeAuditService contentTypeAuditService,
        FeatureAccessChecker accessChecker)
    {
        _contentTypeAuditService = contentTypeAuditService;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult Overview()
    {
        return View("/Views/Overview/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ContentTypeAudit()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        return View("/Views/ContentTypeAudit/Index.cshtml");
    }

    // Placeholder actions for future tools - return 404 until implemented
    [HttpGet]
    public IActionResult PersonalizationAudit() => View("/Views/Overview/Index.cshtml");

    [HttpGet]
    public IActionResult AudienceManager() => View("/Views/Overview/Index.cshtml");

    [HttpGet]
    public IActionResult ContentTypeRecommendations() => View("/Views/Overview/Index.cshtml");
}
