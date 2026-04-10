using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Tools.ManageChildren.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ManageChildren;

[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class ManageChildrenApiController : Controller
{
    private readonly ManageChildrenService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ManageChildrenApiController(
        ManageChildrenService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    private bool HasAccess() => _accessChecker.HasAccess(HttpContext,
        nameof(Configuration.FeatureToggles.ManageChildren),
        EditorPowertoolsPermissions.ManageChildren);

    [HttpGet]
    public IActionResult GetChildren(int id, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetChildren(id, sortBy, sortDesc));
    }

    [HttpGet]
    public IActionResult GetParent(int id)
    {
        if (!HasAccess()) return Forbid();
        var info = _service.GetParentInfo(id);
        return info == null ? NotFound() : Ok(info);
    }

    [HttpPost]
    public IActionResult BulkDelete([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkMoveToTrash(request.ContentIds));
    }

    [HttpPost]
    public IActionResult BulkDeletePermanently([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkDelete(request.ContentIds));
    }

    [HttpPost]
    public IActionResult BulkPublish([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkPublish(request.ContentIds));
    }

    [HttpPost]
    public IActionResult BulkUnpublish([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkUnpublish(request.ContentIds));
    }

    [HttpPost]
    public IActionResult BulkMove([FromBody] BulkMoveRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkMove(request.ContentIds, request.TargetParentId));
    }
}
