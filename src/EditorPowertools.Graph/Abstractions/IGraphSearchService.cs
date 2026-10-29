using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public interface IGraphSearchService
{
    /// <summary>Returns <see cref="GraphSearchResult.Empty"/> when unavailable or the phrase is blank.</summary>
    /// <exception cref="GraphQueryException">Graph failed; callers decide how to degrade.</exception>
    Task<GraphSearchResult> SearchAsync(GraphSearchRequest request, CancellationToken ct);

    /// <summary>Keeps only names of registered content types (plus the base types PageData/BlockData/MediaData/ContentFolder), preserving order, distinct.</summary>
    IReadOnlyList<string> ValidateContentTypes(IEnumerable<string>? names);
}
