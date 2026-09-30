using EPiServer.Shell.Modules;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Infrastructure;

public class ServiceRegistrationTests
{
    [Fact]
    public void Binds_options_from_configuration_section()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CodeArt:EditorPowertools:Graph:Features:PowerSlice"] = "false",
            ["CodeArt:EditorPowertools:Graph:Search:OverfetchFactor"] = "3"
        }).Build();
        var services = new ServiceCollection().AddSingleton<IConfiguration>(config);
        services.AddEditorPowertoolsGraph();
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<EditorPowertoolsGraphOptions>>().Value;
        options.Features.PowerSlice.Should().BeFalse();
        options.Search.OverfetchFactor.Should().Be(3);
    }

    [Fact]
    public void Registers_protected_module()
    {
        var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddEditorPowertoolsGraph();
        services.BuildServiceProvider().GetRequiredService<IOptions<ProtectedModuleOptions>>().Value
            .Items.Should().Contain(m => m.Name == "EditorPowertoolsGraph");
    }
}
