using EPiServer.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>
/// Backs the core Editor Powertools content picker's name search with Optimizely Graph.
/// Never throws: any failure yields no hits so the picker falls back to tree navigation.
/// </summary>
public sealed class GraphContentPickerSearchProvider : IContentSearchProvider
{
    private static readonly string[] PickerTypes = { "PageData", "BlockData", "MediaData", "ContentFolder" };

    private readonly IGraphSearchService _search;
    private readonly IContentLoader _loader;
    private readonly IContentRepository _repository;
    private readonly IContentTypeRepository _types;
    private readonly GraphFeatureAccessChecker _access;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<GraphContentPickerSearchProvider> _logger;

    public GraphContentPickerSearchProvider(
        IGraphSearchService search,
        IContentLoader loader,
        IContentRepository repository,
        IContentTypeRepository types,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http,
        ILogger<GraphContentPickerSearchProvider> logger)
    {
        _search = search;
        _loader = loader;
        _repository = repository;
        _types = types;
        _access = access;
        _http = http;
        _logger = logger;
    }

    public IEnumerable<ContentSearchHit> Search(string query, ContentReference? root, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return Array.Empty<ContentSearchHit>();
        }

        try
        {
            if (!_access.HasUiAccess(_http.HttpContext, nameof(GraphFeatureToggles.SearchProviders),
                    EditorPowertoolsGraphPermissions.SearchProviders))
            {
                return Array.Empty<ContentSearchHit>();
            }

            var roots = new HashSet<ContentReference>();
            var ancestors = new List<Guid>();
            if (!ContentReference.IsNullOrEmpty(root)
                && _loader.TryGet<IContent>(root!, out var rootContent)
                && rootContent is not null
                && rootContent.ContentGuid != Guid.Empty)
            {
                roots.Add(rootContent.ContentLink.ToReferenceWithoutVersion());
                ancestors.Add(rootContent.ContentGuid);
            }

            var types = _search.ValidateContentTypes(PickerTypes);
            // Over-fetch when a root is given or permissions may drop rows, then cap.
            var limit = Math.Min(Math.Max(maxResults * 3, maxResults), GraphSearchService.MaxLimit);
            var hits = Query(query, types, ancestors, limit);

            // A root Graph cannot express yields nothing (CMS 13 paths omit the root folders, such as
            // global and site assets); retry without it. Hits are checked against the root below.
            if (hits.Count == 0 && ancestors.Count > 0)
            {
                hits = Query(query, types, Array.Empty<Guid>(), GraphSearchService.MaxLimit);
            }

            var seen = new HashSet<int>();
            var list = new List<ContentSearchHit>();
            foreach (var hit in hits)
            {
                // Graph returns one item per language branch; the picker lists content once.
                if (!_repository.TryGet<IContent>(hit.ContentGuid, out var content) || content is null
                    || !seen.Add(content.ContentLink.ID)
                    || !content.QueryDistinctAccess(AccessLevel.Read)
                    || !SearchRoots.Contains(content, roots, _loader))
                {
                    continue;
                }

                list.Add(new ContentSearchHit
                {
                    ContentId = content.ContentLink.ID,
                    Name = content.Name,
                    TypeName = _types.Load(content.ContentTypeID)?.Name ?? string.Empty,
                    HasChildren = _loader.GetChildren<IContent>(content.ContentLink).Any()
                });

                if (list.Count >= maxResults)
                {
                    break;
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph content picker search failed.");
            return Array.Empty<ContentSearchHit>();
        }
    }

    private IReadOnlyList<GraphSearchHit> Query(string query, IReadOnlyList<string> types, IReadOnlyList<Guid> ancestors, int limit)
    {
        var scope = new GraphSearchScope(types, ancestors, false);
        var request = new GraphSearchRequest(query, scope, null, GraphSearchFilters.None, limit, 0, false);
        return _search.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult().Hits;
    }
}
