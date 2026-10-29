using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>CMS Doctor check reporting whether Optimizely Graph is usable for editor search.</summary>
public class GraphHealthCheck : DoctorCheckBase
{
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/graphhealthcheck/";

    // Doctor checks are transient but captured by a singleton service, while the availability and
    // executor services are scoped, so a scope is created per check run.
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GraphHealthCheck> _logger;

    public GraphHealthCheck(IServiceScopeFactory scopeFactory, ILogger<GraphHealthCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override string Name => L(Prefix + "name", "Optimizely Graph for editor search");
    public override string Description => L(Prefix + "description",
        "Checks that Optimizely Graph is installed, configured and answering, so editor search can use it.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/graph", "Graph");
    public override int SortOrder => 50;
    public override string[] Tags => new[] { "Graph", "Search", "Configuration" };

    public override DoctorCheckResult PerformCheck()
    {
        using var scope = _scopeFactory.CreateScope();
        var availability = scope.ServiceProvider.GetRequiredService<IGraphAvailability>();

        switch (availability.State)
        {
            case GraphAvailabilityState.ClientNotRegistered:
                return Warning(L(Prefix + "notinstalled", "Optimizely Graph is not installed (call AddContentGraph())."));
            case GraphAvailabilityState.NotConfigured:
                return Warning(L(Prefix + "notconfigured", "Graph gateway, app key or secret is missing."));
            case GraphAvailabilityState.DialectNotVerified:
                return Warning(L(Prefix + "notverified", "Graph search is not yet verified for this CMS version."));
        }

        try
        {
            var executor = scope.ServiceProvider.GetRequiredService<IGraphQueryExecutor>();
            var dialect = scope.ServiceProvider.GetRequiredService<IGraphQueryDialect>();
            executor.ExecuteAsync(new GraphRequest(dialect.HealthQuery, new Dictionary<string, object?>()), CancellationToken.None)
                .GetAwaiter().GetResult();
            return Ok(L(Prefix + "ok", "Optimizely Graph is configured and responding."));
        }
        catch (GraphQueryException ex)
        {
            _logger.LogWarning(ex, "Graph health check smoke query failed");
            return Fault(L(Prefix + "noresponse", "Graph did not respond."));
        }
    }
}
