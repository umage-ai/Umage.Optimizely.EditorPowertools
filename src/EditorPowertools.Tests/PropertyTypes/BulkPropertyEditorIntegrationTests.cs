using EPiServer;
using EPiServer.Core;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class BulkPropertyEditorIntegrationTests
{
    public BulkPropertyEditorIntegrationTests() => EpiServerTestSetup.EnsureInitialized();

    private static PropertyTypeHandlerRegistry BuildRegistry(IContentLoader loader) =>
        new(new IPropertyTypeHandler[]
        {
            new FallbackHandler(),
            new StringHandler(), new NumberHandler(), new FloatHandler(),
            new BooleanHandler(), new DateHandler(), new UrlHandler(),
            new ContentReferenceHandler(loader),
        });

    [Fact]
    public void Registry_ResolvesStringPropertyToTextEditor()
    {
        var registry = BuildRegistry(Mock.Of<IContentLoader>());
        var ctx = new PropertyHandlerContext { Definition = null!, Property = new PropertyString { Value = "x" } };

        var handler = registry.Resolve(ctx);
        handler.GetEditor(ctx)!.Kind.Should().Be("text");
        handler.GetDisplay(ctx).Should().Be("x");
    }
}
