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

    [HttpGet]
    public IActionResult PersonalizationAudit()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.PersonalizationUsageAudit),
            EditorPowertoolsPermissions.PersonalizationUsageAudit))
            return Forbid();

        return View("/Views/PersonalizationAudit/Index.cshtml");
    }

    [HttpGet]
    public IActionResult AudienceManager()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.AudienceManager),
            EditorPowertoolsPermissions.AudienceManager))
            return Forbid();

        return View("/Views/AudienceManager/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ContentTypeRecommendations()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        return View("/Views/ContentTypeRecommendations/Index.cshtml");
    }

    [HttpGet]
    public IActionResult BulkPropertyEditor()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BulkPropertyEditor),
            EditorPowertoolsPermissions.BulkPropertyEditor))
            return Forbid();

        return View("/Views/BulkPropertyEditor/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ScheduledJobsGantt()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ScheduledJobsGantt),
            EditorPowertoolsPermissions.ScheduledJobsGantt))
            return Forbid();

        return View("/Views/ScheduledJobsGantt/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ActivityTimeline()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ActivityTimeline),
            EditorPowertoolsPermissions.ActivityTimeline))
            return Forbid();

        return View("/Views/ActivityTimeline/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ManageChildren()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ManageChildren),
            EditorPowertoolsPermissions.ManageChildren))
            return Forbid();

        return View("/Views/ManageChildren/Index.cshtml");
    }

    [HttpGet]
    public IActionResult ContentImporter()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentImporter),
            EditorPowertoolsPermissions.ContentImporter))
            return Forbid();

        return View("/Views/ContentImporter/Index.cshtml");
    }

    [HttpGet]
    public IActionResult LinkChecker()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.BrokenLinkChecker),
            EditorPowertoolsPermissions.BrokenLinkChecker))
            return Forbid();

        return View("/Views/LinkChecker/Index.cshtml");
    }
}
