using EPiServer.Core;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ContentImporterParseTests
{
    [Fact]
    public void Registry_ParsesNumberForImport()
    {
        var registry = new PropertyTypeHandlerRegistry(new IPropertyTypeHandler[]
        {
            new FallbackHandler(), new NumberHandler(),
        });
        var ctx = PropertyHandlerContext.ForProperty(new PropertyNumber());

        registry.Resolve(ctx).TryParse("99", ctx, out var v).Should().BeTrue();
        v.Should().Be(99);
    }
}
