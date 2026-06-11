using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UmageAI.Optimizely.EditorPowerTools.Components;

/// <summary>
/// API endpoints for reusable UI components (content picker, content type picker, etc.).
/// </summary>
[Authorize(Policy = "codeart:editorpowertools")]
public class ComponentsApiController : Controller
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentSearchProvider? _searchProvider;

    public ComponentsApiController(
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IContentSearchProvider? searchProvider = null)
    {
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _searchProvider = searchProvider;
    }

    // ── Content Tree ───────────────────────────────────────────────

    /// <summary>
    /// Get a content node with basic info (for tree root or navigation).
    /// </summary>
    [HttpGet]
    public IActionResult GetContent(int id)
    {
        var contentRef = id == 0 ? ContentReference.RootPage : new ContentReference(id);
        if (!_contentLoader.TryGet<IContent>(contentRef, out var content))
            return NotFound();

        return Ok(MapContent(content));
    }

    /// <summary>
    /// Get children of a content node (for lazy-loading the tree).
    /// </summary>
    [HttpGet]
    public IActionResult GetChildren(int id)
    {
        var contentRef = id == 0 ? ContentReference.RootPage : new ContentReference(id);
        var children = _contentLoader.GetChildren<IContent>(
                contentRef,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() })
            .Select(MapContent)
            .ToList();

        return Ok(children);
    }

    /// <summary>
    /// Search content by name. Returns up to 50 results — but only when a host project
    /// has registered an IContentSearchProvider (e.g. via the EditorPowertools.Graph
    /// add-on). Without one, returns an empty array; the picker UI then shows its
    /// "no results" empty state and editors fall back to tree navigation. Walking the
    /// content tree on every keystroke (the previous implementation) is not allowed
    /// on a live request path.
    /// </summary>
    [HttpGet]
    public IActionResult SearchContent([FromQuery] string q, [FromQuery] int rootId = 0)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        if (_searchProvider == null)
            return Ok(Array.Empty<object>());

        var root = rootId == 0 ? null : new ContentReference(rootId);
        IEnumerable<ContentSearchHit> hits;
        try
        {
            hits = _searchProvider.Search(q, root, maxResults: 50);
        }
        catch
        {
            return Ok(Array.Empty<object>());
        }

        var results = hits.Select(h => (object)new
        {
            Id = h.ContentId,
            h.Name,
            h.TypeName,
            h.HasChildren
        }).ToList();
        return Ok(results);
    }

    // ── Content Types ──────────────────────────────────────────────

    /// <summary>
    /// Get all content types (for content type picker).
    /// </summary>
    [HttpGet]
    public IActionResult GetContentTypes([FromQuery] string? q)
    {
        var types = _contentTypeRepository.List()
            .Where(t => t.ModelType != null)
            .Select(ct => new
            {
                ct.ID,
                ct.Name,
                DisplayName = ct.DisplayName ?? ct.Name,
                ct.Description,
                ct.GroupName,
                Base = ct.Base.ToString(),
                IsSystemType = ct.ModelType?.Namespace?.StartsWith("EPiServer", StringComparison.OrdinalIgnoreCase) == true
            });

        if (!string.IsNullOrWhiteSpace(q))
        {
            types = types.Where(t =>
                t.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                t.Name.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(types.OrderBy(t => t.DisplayName).ToList());
    }

    private object MapContent(IContent content)
    {
        bool hasChildren;
        try
        {
            hasChildren = _contentLoader.GetChildren<IContent>(
                content.ContentLink,
                new LoaderOptions { LanguageLoaderOption.FallbackWithMaster() })
                .Any();
        }
        catch
        {
            hasChildren = false;
        }

        var contentType = _contentTypeRepository.Load(content.ContentTypeID);

        return new
        {
            Id = content.ContentLink.ID,
            content.Name,
            TypeName = contentType?.DisplayName ?? contentType?.Name ?? "Unknown",
            HasChildren = hasChildren
        };
    }
}
