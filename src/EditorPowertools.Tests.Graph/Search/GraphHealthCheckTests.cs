using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;
using UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphHealthCheckTests
{
    private static GraphHealthCheck Create(GraphAvailabilityState state, Exception? executorFailure = null, List<GraphRequest>? sent = null)
    {
        GraphTestSetup.EnsureInitialized();
        var availability = new Mock<IGraphAvailability>();
        availability.SetupGet(a => a.State).Returns(state);
        var executor = new Mock<IGraphQueryExecutor>();
        if (executorFailure is null)
            executor.Setup(e => e.ExecuteAsync(It.IsAny<GraphRequest>(), It.IsAny<CancellationToken>()))
                .Callback((GraphRequest r, CancellationToken _) => sent?.Add(r))
                .ReturnsAsync(JsonDocument.Parse("{}").RootElement.Clone());
        else
            executor.Setup(e => e.ExecuteAsync(It.IsAny<GraphRequest>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(executorFailure);

        var services = new ServiceCollection();
        services.AddScoped(_ => availability.Object);
        services.AddScoped(_ => executor.Object);
        var dialect = new Mock<IGraphQueryDialect>();
        dialect.SetupGet(d => d.HealthQuery).Returns("query { Probe }");
        services.AddSingleton(dialect.Object);
        var provider = services.BuildServiceProvider();
        return new GraphHealthCheck(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<GraphHealthCheck>.Instance);
    }

    [Theory]
    [InlineData(GraphAvailabilityState.Available, HealthStatus.OK)]
    [InlineData(GraphAvailabilityState.ClientNotRegistered, HealthStatus.Warning)]
    [InlineData(GraphAvailabilityState.NotConfigured, HealthStatus.Warning)]
    [InlineData(GraphAvailabilityState.DialectNotVerified, HealthStatus.Warning)]
    public void Maps_availability_to_status(GraphAvailabilityState state, HealthStatus expected) =>
        Create(state).PerformCheck().Status.Should().Be(expected);

    [Fact]
    public void Failing_smoke_query_is_a_fault_without_exception_text()
    {
        var result = Create(GraphAvailabilityState.Available, new GraphQueryException("secret-marker")).PerformCheck();
        result.Status.Should().Be(HealthStatus.Fault);
        (result.Details ?? "").Should().NotContain("secret-marker");
        result.StatusText.Should().NotContain("secret-marker");
    }

    // The probe must match the schema: CMS 12 has Content, CMS 13 only _Content.
    [Fact]
    public void Probes_with_the_dialect_health_query()
    {
        var sent = new List<GraphRequest>();
        Create(GraphAvailabilityState.Available, sent: sent).PerformCheck();
        sent.Should().ContainSingle().Which.Query.Should().Be("query { Probe }");
    }

    [Fact]
    public void Metadata_is_set()
    {
        var check = Create(GraphAvailabilityState.Available);
        check.Name.Should().Be("Optimizely Graph for editor search");
        check.Group.Should().Be("Graph");
        check.Tags.Should().BeEquivalentTo("Graph", "Search", "Configuration");
    }
}
