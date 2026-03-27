using EditorPowertools.Permissions;
using EditorPowertools.Tools.ManageChildren.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ManageChildren;

[Authorize(Policy = "codeart:editorpowertools")]
[Route("editorpowertools/api/manage-children")]
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

    [HttpGet("{parentId:int}")]
    public IActionResult GetChildren(int parentId, [FromQuery] string? sortBy = null, [FromQuery] bool sortDesc = false)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.GetChildren(parentId, sortBy, sortDesc));
    }

    [HttpGet("parent/{contentId:int}")]
    public IActionResult GetParent(int contentId)
    {
        if (!HasAccess()) return Forbid();
        var info = _service.GetParentInfo(contentId);
        return info == null ? NotFound() : Ok(info);
    }

    [HttpPost("delete")]
    public IActionResult BulkDelete([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkMoveToTrash(request.ContentIds));
    }

    [HttpPost("delete-permanently")]
    public IActionResult BulkDeletePermanently([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkDelete(request.ContentIds));
    }

    [HttpPost("publish")]
    public IActionResult BulkPublish([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkPublish(request.ContentIds));
    }

    [HttpPost("unpublish")]
    public IActionResult BulkUnpublish([FromBody] BulkActionRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkUnpublish(request.ContentIds));
    }

    [HttpPost("move")]
    public IActionResult BulkMove([FromBody] BulkMoveRequest request)
    {
        if (!HasAccess()) return Forbid();
        return Ok(_service.BulkMove(request.ContentIds, request.TargetParentId));
    }
}
