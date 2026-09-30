using EPiServer.Security;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

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
    private readonly ILogger<GraphContentPickerSearchProvider> _logger;

    public GraphContentPickerSearchProvider(
        IGraphSearchService search,
        IContentLoader loader,
        IContentRepository repository,
        IContentTypeRepository types,
        ILogger<GraphContentPickerSearchProvider> logger)
    {
        _search = search;
        _loader = loader;
        _repository = repository;
        _types = types;
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
            var ancestors = new List<Guid>();
            if (!ContentReference.IsNullOrEmpty(root)
                && _loader.TryGet<IContent>(root!, out var rootContent)
                && rootContent is not null
                && rootContent.ContentGuid != Guid.Empty)
            {
                ancestors.Add(rootContent.ContentGuid);
            }

            var scope = new GraphSearchScope(_search.ValidateContentTypes(PickerTypes), ancestors, false);
            // Over-fetch when a root is given or permissions may drop rows, then cap.
            var limit = Math.Min(Math.Max(maxResults * 3, maxResults), 250);
            var request = new GraphSearchRequest(query, scope, null, GraphSearchFilters.None, limit, 0, false);
            var result = _search.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult();

            var hits = new List<ContentSearchHit>();
            foreach (var hit in result.Hits)
            {
                if (!_repository.TryGet<IContent>(hit.ContentGuid, out var content) || content is null
                    || !content.QueryDistinctAccess(AccessLevel.Read))
                {
                    continue;
                }

                hits.Add(new ContentSearchHit
                {
                    ContentId = content.ContentLink.ID,
                    Name = content.Name,
                    TypeName = _types.Load(content.ContentTypeID)?.Name ?? string.Empty,
                    HasChildren = _loader.GetChildren<IContent>(content.ContentLink).Any()
                });

                if (hits.Count >= maxResults)
                {
                    break;
                }
            }

            return hits;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graph content picker search failed.");
            return Array.Empty<ContentSearchHit>();
        }
    }
}
