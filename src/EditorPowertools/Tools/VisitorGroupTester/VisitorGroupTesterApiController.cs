using UmageAI.Optimizely.EditorPowerTools.Permissions;
using EPiServer.Personalization.VisitorGroups;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.VisitorGroupTester;

/// <summary>
/// API controller for the Visitor Group Tester toolbar.
/// Provides visitor group data for the floating frontend toolbar.
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class VisitorGroupTesterApiController : Controller
{
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly FeatureAccessChecker _accessChecker;

    public VisitorGroupTesterApiController(
        IVisitorGroupRepository visitorGroupRepository,
        FeatureAccessChecker accessChecker)
    {
        _visitorGroupRepository = visitorGroupRepository;
        _accessChecker = accessChecker;
    }

    [HttpGet]
    public IActionResult GetGroups()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.VisitorGroupTester),
            EditorPowertoolsPermissions.VisitorGroupTester))
            return Forbid();

        var groups = _visitorGroupRepository.List()
            .Select(g => new
            {
                id = g.Id,
                name = g.Name
            })
            .OrderBy(g => g.name)
            .ToList();

        return Ok(groups);
    }
}
