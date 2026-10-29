namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public sealed record GraphSearchScope(
    IReadOnlyList<string> ContentTypes,   // validated type names, e.g. "PageData", "BlockData", "MediaData"
    IReadOnlyList<Guid> AncestorGuids,    // search roots
    bool FilterByLocale);

public sealed record GraphSearchFilters(
    IReadOnlyList<string> ContentTypes,   // facet selections (validated)
    IReadOnlyList<string> Languages,      // CMS culture names, e.g. "fr-CH"
    IReadOnlyList<Guid> Sections)         // ancestor GUIDs
{
    public static GraphSearchFilters None { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Guid>());
}

public sealed record GraphSearchRequest(
    string Phrase, GraphSearchScope Scope, string? Language, GraphSearchFilters Filters,
    int Limit, int Skip, bool IncludeFacets);

public sealed record GraphSearchHit(Guid ContentGuid, string Name, string Language);

public sealed record GraphFacetValue(string Name, int Count);

public sealed record GraphFacets(
    IReadOnlyList<GraphFacetValue> ContentTypes, IReadOnlyList<GraphFacetValue> Languages, IReadOnlyList<GraphFacetValue> Sections)
{
    public static GraphFacets Empty { get; } = new(Array.Empty<GraphFacetValue>(), Array.Empty<GraphFacetValue>(), Array.Empty<GraphFacetValue>());
}

/// <param name="Hits">Raw hits, before permission filtering.</param>
/// <param name="Total">-1 when Graph did not report a total (unknown, not zero).</param>
/// <param name="Facets">Facet counts; empty when not requested.</param>
public sealed record GraphSearchResult(IReadOnlyList<GraphSearchHit> Hits, int Total, GraphFacets Facets)
{
    public static GraphSearchResult Empty { get; } = new(Array.Empty<GraphSearchHit>(), 0, GraphFacets.Empty);
}
