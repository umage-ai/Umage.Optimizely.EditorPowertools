using UmageAI.Optimizely.EditorPowerTools.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentDetails;

/// <summary>
/// API controller for Content Details widget data.
/// Serves the assets panel widget (not a standalone page).
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ContentDetailsApiController : Controller
{
    private readonly ContentDetailsService _service;
    private readonly FeatureAccessChecker _accessChecker;

    public ContentDetailsApiController(
        ContentDetailsService service,
        FeatureAccessChecker accessChecker)
    {
        _service = service;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    [Route("editorpowertools/api/content-details/{contentId:int}")]
    public IActionResult GetDetails(int contentId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentDetails),
            EditorPowertoolsPermissions.ContentDetails))
            return Forbid();

        var details = _service.GetDetails(contentId);
        if (details == null)
            return NotFound();

        return Ok(details);
    }
}
