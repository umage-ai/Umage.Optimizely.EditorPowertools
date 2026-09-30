using Microsoft.Extensions.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

public enum GraphAvailabilityState
{
    Available,
    ClientNotRegistered,
    NotConfigured,
    DialectNotVerified
}

/// <summary>Tells consumers whether Optimizely Graph can be used for editor search right now.</summary>
public interface IGraphAvailability
{
    GraphAvailabilityState State { get; }
    bool IsAvailable => State == GraphAvailabilityState.Available;
}

/// <summary>
/// Only checks that the gateway, app key and secret are present. The values are never read into logs or responses.
/// The secret is required: without it the platform client falls back to public single-key access, which cannot
/// see restricted content, so we fail loudly instead of returning a silently partial index.
/// </summary>
public sealed class GraphAvailability : IGraphAvailability
{
    private const string Section = "Optimizely:ContentGraph:";

    private readonly IGraphQueryExecutor _executor;
    private readonly IGraphQueryDialect _dialect;
    private readonly IConfiguration _configuration;

    public GraphAvailability(IGraphQueryExecutor executor, IGraphQueryDialect dialect, IConfiguration configuration)
    {
        _executor = executor;
        _dialect = dialect;
        _configuration = configuration;
    }

    public GraphAvailabilityState State
    {
        get
        {
            if (!_executor.IsClientAvailable) return GraphAvailabilityState.ClientNotRegistered;
            if (IsEmpty("GatewayAddress") || IsEmpty("AppKey") || IsEmpty("Secret")) return GraphAvailabilityState.NotConfigured;
            if (!_dialect.IsVerified) return GraphAvailabilityState.DialectNotVerified;
            return GraphAvailabilityState.Available;
        }
    }

    private bool IsEmpty(string key) => string.IsNullOrWhiteSpace(_configuration[Section + key]);
}
