using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>Single entry point for all Graph search surfaces. Clamps paging and allow-lists content type names.</summary>
public sealed class GraphSearchService : IGraphSearchService
{
    internal const int MaxLimit = 250;

    private static readonly string[] BaseTypes = { "PageData", "BlockData", "MediaData", "ContentFolder" };

    private readonly IGraphQueryDialect _dialect;
    private readonly IGraphQueryExecutor _executor;
    private readonly IGraphAvailability _availability;
    private readonly IContentTypeRepository _contentTypes;

    public GraphSearchService(
        IGraphQueryDialect dialect,
        IGraphQueryExecutor executor,
        IGraphAvailability availability,
        IContentTypeRepository contentTypes)
    {
        _dialect = dialect;
        _executor = executor;
        _availability = availability;
        _contentTypes = contentTypes;
    }

    public async Task<GraphSearchResult> SearchAsync(GraphSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Phrase) || !_availability.IsAvailable)
        {
            return GraphSearchResult.Empty;
        }

        var known = KnownNames();
        var safe = request with
        {
            Limit = Math.Clamp(request.Limit, 1, MaxLimit),
            Skip = Math.Max(0, request.Skip),
            Scope = request.Scope with { ContentTypes = Filter(request.Scope.ContentTypes, known) },
            Filters = request.Filters with { ContentTypes = Filter(request.Filters.ContentTypes, known) }
        };

        var graphRequest = _dialect.BuildSearch(safe);
        var data = await _executor.ExecuteAsync(graphRequest, ct).ConfigureAwait(false);
        return _dialect.ParseSearch(data, safe.IncludeFacets);
    }

    public IReadOnlyList<string> ValidateContentTypes(IEnumerable<string>? names) => Filter(names, KnownNames());

    private HashSet<string> KnownNames()
    {
        var set = new HashSet<string>(BaseTypes, StringComparer.Ordinal);
        foreach (var type in _contentTypes.List())
        {
            if (!string.IsNullOrEmpty(type.Name)) set.Add(type.Name);
        }
        return set;
    }

    private static IReadOnlyList<string> Filter(IEnumerable<string>? names, HashSet<string> known) =>
        names == null
            ? Array.Empty<string>()
            : names.Where(n => n != null && known.Contains(n)).Distinct(StringComparer.Ordinal).ToList();
}
