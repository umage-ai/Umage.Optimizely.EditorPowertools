using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;
#if OPTIMIZELY_CMS13
using Optimizely.Graph.Core.Client;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;
#else
using Optimizely.ContentGraph.Core;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;
#endif

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Infrastructure;

/// <summary>
/// Runs on both TFMs against the REAL dialect/executor pair the DI extension registers, so a wrong
/// per-TFM registration or a premature IsVerified flip cannot slip through the mocked availability tests.
/// </summary>
public class DialectWiringTests
{
    private static ServiceProvider Build()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Optimizely:ContentGraph:GatewayAddress"] = "https://example.invalid",
            ["Optimizely:ContentGraph:AppKey"] = "k",
            ["Optimizely:ContentGraph:Secret"] = "s"
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(config);
        services.AddEditorPowertoolsGraph();
#if OPTIMIZELY_CMS13
        services.AddSingleton(new Mock<IGraphClient>().Object);
#else
        services.AddSingleton(new Mock<IClient>().Object);
#endif
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Registers_the_dialect_and_executor_for_this_target()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();
#if OPTIMIZELY_CMS13
        sp.GetRequiredService<IGraphQueryDialect>().Should().BeOfType<Cms13GraphQueryDialect>();
        scope.ServiceProvider.GetRequiredService<IGraphQueryExecutor>().Should().BeOfType<Cms13GraphQueryExecutor>();
#else
        sp.GetRequiredService<IGraphQueryDialect>().Should().BeOfType<Cms12GraphQueryDialect>();
        scope.ServiceProvider.GetRequiredService<IGraphQueryExecutor>().Should().BeOfType<Cms12GraphQueryExecutor>();
#endif
    }

    [Fact]
    public void Real_pair_reports_the_expected_availability_end_to_end()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<IGraphAvailability>().State;
        state.Should().Be(GraphAvailabilityState.Available);
    }

}
