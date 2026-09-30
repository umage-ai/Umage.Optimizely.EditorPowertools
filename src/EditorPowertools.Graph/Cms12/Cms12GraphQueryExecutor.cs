using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Optimizely.ContentGraph.Core;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;

internal sealed class Cms12GraphQueryExecutor(IServiceProvider services, IHttpContextAccessor http) : IGraphQueryExecutor
{
    // Resolved lazily: IClient exists only if the host called AddContentGraph().
    private IClient? Client => services.GetService<IClient>();

    public bool IsClientAvailable => Client is not null;

    public async Task<JsonElement> ExecuteAsync(GraphRequest request, CancellationToken cancellationToken)
    {
        var client = Client ?? throw new GraphQueryException("Optimizely Graph client is not registered.");
        string json;
        try
        {
            json = await client.QueryAsync(request.Query, request.Variables,
                GraphAccessHeaders.Build(http.HttpContext?.User), new Dictionary<string, string>(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GraphQueryException("Graph request failed.", ex);
        }
        return GraphResponseReader.ReadData(json);
    }
}
