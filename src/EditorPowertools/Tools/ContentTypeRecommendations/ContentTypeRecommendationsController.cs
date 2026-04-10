using UmageAI.Optimizely.EditorPowerTools.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeRecommendations.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.Data;
using EPiServer.DataAbstraction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.ContentTypeRecommendations;

/// <summary>
/// API controller for Content Type Recommendations CRUD operations.
/// The page view is served by EditorPowertoolsController.ContentTypeRecommendations().
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
[RequireAjax]
public class ContentTypeRecommendationsApiController : Controller
{
    private readonly ContentTypeRecommendationService _service;
    private readonly FeatureAccessChecker _accessChecker;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentRepository _contentRepository;

    public ContentTypeRecommendationsApiController(
        ContentTypeRecommendationService service,
        FeatureAccessChecker accessChecker,
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository)
    {
        _service = service;
        _accessChecker = accessChecker;
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
    }

    [HttpGet]
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
    public IActionResult DeleteRule([FromQuery] string id)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        _service.DeleteRule(id);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Evaluates recommendation rules for a given parent content item and returns suggested content type IDs.
    /// TODO: Register an IContentTypeAdvisor implementation when EPiServer.Cms.Shell.UI.Rest is available
    /// to integrate directly with the CMS "create content" dialog.
    /// </summary>
    [HttpGet]
    public IActionResult EvaluateRules([FromQuery] int parentId)
    {
        if (!_accessChecker.HasAccess(HttpContext,
            nameof(Configuration.FeatureToggles.ContentTypeRecommendations),
            EditorPowertoolsPermissions.ContentTypeRecommendations))
            return Forbid();

        var parentRef = new ContentReference(parentId);
        if (!_contentRepository.TryGet<IContent>(parentRef, out var parent))
            return NotFound(new { error = $"Content with ID {parentId} not found." });

        var suggestedTypeIds = _service.EvaluateRules(parent, contentFolder: false, requestedTypes: Enumerable.Empty<string>());
        return Ok(suggestedTypeIds);
    }

    [HttpGet]
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
