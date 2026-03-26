using EditorPowertools.Permissions;
using EditorPowertools.Tools.ContentTypeRecommendations.Models;
using EPiServer.Core;
using EPiServer.Data;
using EPiServer.DataAbstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EditorPowertools.Tools.ContentTypeRecommendations;

/// <summary>
/// API controller for Content Type Recommendations CRUD operations.
/// The page view is served by EditorPowertoolsController.ContentTypeRecommendations().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ContentTypeRecommendationsApiController : Controller
{
    private readonly ContentTypeRecommendationService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly IContentTypeRepository _contentTypeRepository;

    public ContentTypeRecommendationsApiController(
        ContentTypeRecommendationService service,
        FeatureAccessChecker accessChecker,
        IContentTypeRepository contentTypeRepository)
    {
        _service = service;
        _accessChecker = accessChecker;
        _contentTypeRepository = contentTypeRepository;
    }

    [HttpGet]
    [Route("editorpowertools/api/recommendations/rules")]
    public IActionResult GetRules()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        var rules = _service.GetAllRulesWithNames();
        return Ok(rules);
    }

    [HttpPost]
    [Route("editorpowertools/api/recommendations/rules")]
    public IActionResult SaveRule([FromBody] SaveRuleRequest request)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        var rule = new ContentTypeRecommendationRule
        {
            Id = string.IsNullOrEmpty(request.Id) ? Identity.NewIdentity() : Identity.Parse(request.Id),
            ParentContentType = request.ParentContentType,
            ParentContent = request.ParentContentId.HasValue ? new ContentReference(request.ParentContentId.Value) : null,
            IncludeDescendants = request.IncludeDescendants,
            ForThisContentFolder = request.ForThisContentFolder,
            ContentTypesToSuggest = request.ContentTypesToSuggest ?? new List<int>()
        };

        _service.SaveRule(rule);
        return Ok(new { success = true });
    }

    [HttpDelete]
    [Route("editorpowertools/api/recommendations/rules/{id}")]
    public IActionResult DeleteRule(string id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        _service.DeleteRule(id);
        return Ok(new { success = true });
    }

    [HttpGet]
    [Route("editorpowertools/api/recommendations/content-types")]
    public IActionResult GetContentTypes()
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        var types = _contentTypeRepository.List()
            .Where(t => t.ModelType != null)
            .Where(t => t.ModelType?.Namespace?.StartsWith("EPiServer", StringComparison.OrdinalIgnoreCase) != true)
            .Select(ct => new
            {
                ct.ID,
                ct.Name,
                DisplayName = ct.DisplayName ?? ct.Name,
                ct.GroupName,
                Base = ct.Base.ToString()
            })
            .OrderBy(t => t.DisplayName)
            .ToList();

        return Ok(types);
    }
}
