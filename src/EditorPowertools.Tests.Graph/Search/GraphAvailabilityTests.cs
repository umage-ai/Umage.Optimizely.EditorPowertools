using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace EditorPowertools.Tests.Graph.Search;

public class GraphAvailabilityTests
{
    private static GraphAvailabilityState Evaluate(bool client, bool verified, string? gateway, string? appKey, string? secret)
    {
        var executor = new Mock<IGraphQueryExecutor>();
        executor.SetupGet(e => e.IsClientAvailable).Returns(client);
        var dialect = new Mock<IGraphQueryDialect>();
        dialect.SetupGet(d => d.IsVerified).Returns(verified);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Optimizely:ContentGraph:GatewayAddress"] = gateway,
            ["Optimizely:ContentGraph:AppKey"] = appKey,
            ["Optimizely:ContentGraph:Secret"] = secret
        }).Build();
        return new GraphAvailability(executor.Object, dialect.Object, config).State;
    }

    [Theory]
    [InlineData(true, true, "g", "k", "s", GraphAvailabilityState.Available)]
    [InlineData(false, true, "g", "k", "s", GraphAvailabilityState.ClientNotRegistered)]
    [InlineData(true, true, "", "k", "s", GraphAvailabilityState.NotConfigured)]
    [InlineData(true, true, "g", null, "s", GraphAvailabilityState.NotConfigured)]
    [InlineData(true, true, "g", "k", "", GraphAvailabilityState.NotConfigured)]
    [InlineData(true, false, "g", "k", "s", GraphAvailabilityState.DialectNotVerified)]
    [InlineData(false, false, "", "", "", GraphAvailabilityState.ClientNotRegistered)]
    public void Maps_state(bool client, bool verified, string? gateway, string? appKey, string? secret, GraphAvailabilityState expected) =>
        Evaluate(client, verified, gateway, appKey, secret).Should().Be(expected);

    [Fact]
    public void IsAvailable_is_true_only_for_available()
    {
        var a = new Mock<IGraphAvailability> { CallBase = true };
        a.SetupGet(x => x.State).Returns(GraphAvailabilityState.NotConfigured);
        a.Object.IsAvailable.Should().BeFalse();
        a.SetupGet(x => x.State).Returns(GraphAvailabilityState.Available);
        a.Object.IsAvailable.Should().BeTrue();
    }
}
