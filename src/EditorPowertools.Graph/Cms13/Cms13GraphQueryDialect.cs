using System.Text.Json;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;

/// <summary>
/// Placeholder until the CMS 13 Graph schema has been verified against a real instance.
/// Nothing calls it while availability reports <c>DialectNotVerified</c>.
/// </summary>
internal sealed class Cms13GraphQueryDialect : IGraphQueryDialect
{
    public bool IsVerified => false;

    public GraphRequest BuildSearch(GraphSearchRequest request) =>
        throw new NotSupportedException("CMS 13 Graph dialect not verified yet");

    public GraphSearchResult ParseSearch(JsonElement data, bool includeFacets) =>
        throw new NotSupportedException("CMS 13 Graph dialect not verified yet");
}
