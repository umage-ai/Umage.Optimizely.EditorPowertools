using System.Text.Json;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public interface IGraphQueryDialect
{
    /// <summary>False until the dialect's field names have been verified against a real schema.</summary>
    bool IsVerified { get; }

    /// <summary>A minimal query the health check sends to prove the gateway answers this schema.</summary>
    string HealthQuery { get; }

    GraphRequest BuildSearch(GraphSearchRequest request);

    GraphSearchResult ParseSearch(JsonElement data, bool includeFacets);
}
