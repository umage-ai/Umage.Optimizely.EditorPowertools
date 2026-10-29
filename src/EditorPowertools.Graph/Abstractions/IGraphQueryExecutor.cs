using System.Text.Json;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public sealed record GraphRequest(string Query, IReadOnlyDictionary<string, object?> Variables);

public interface IGraphQueryExecutor
{
    /// <summary>True when the platform Graph client is registered in DI.</summary>
    bool IsClientAvailable { get; }

    /// <summary>Runs the request as the current editor and returns a clone of the <c>data</c> element.</summary>
    /// <exception cref="GraphQueryException">Transport failure or GraphQL <c>errors</c>.</exception>
    Task<JsonElement> ExecuteAsync(GraphRequest request, CancellationToken cancellationToken);
}
