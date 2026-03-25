using EditorPowertools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ContentTypeAudit;

/// <summary>
/// API-only controller for Content Type Audit data endpoints.
/// The page view is served by EditorPowertoolsController.ContentTypeAudit().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ContentTypeAuditApiController : Controller
{
    private readonly ContentTypeAuditService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ContentTypeAuditApiController(
        ContentTypeAuditService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types")]
    public IActionResult GetTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var types = _service.GetAllContentTypes();
        return Ok(types);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/{id}/properties")]
    public IActionResult GetProperties(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var properties = _service.GetProperties(id);
        return Ok(properties);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/{id}/content")]
    public IActionResult GetContentOfType(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var content = _service.GetContentOfType(id);
        return Ok(content);
    }

    [HttpGet]
    [Route("editorpowertools/api/content/{id}/references")]
    public IActionResult GetContentReferences(int id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var references = _service.GetContentReferences(id);
        return Ok(references);
    }

    [HttpGet]
    [Route("editorpowertools/api/content-types/inheritance-tree")]
    public IActionResult GetInheritanceTree()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeAudit),
            EditorPowertoolsPermissions.ContentTypeAudit))
            return Forbid();

        var tree = _service.GetInheritanceTree();
        return Ok(tree);
    }
}
