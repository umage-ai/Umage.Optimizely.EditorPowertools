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
        var ctx = PropertyHandlerContext.ForProperty(new PropertyString { Value = "x" });

        var handler = registry.Resolve(ctx);
        handler.GetEditor(ctx)!.Kind.Should().Be("text");
        handler.GetDisplay(ctx).Should().Be("x");
    }

    [Fact]
    public void Registry_ContentReferenceDisplaysNameAndId()
    {
        var loader = new Mock<IContentLoader>();
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns("Page X");
        IContent? oc = content.Object;
        loader.Setup(l => l.TryGet(new ContentReference(5), out oc)).Returns(true);
        var registry = BuildRegistry(loader.Object);

        var ctx = PropertyHandlerContext.ForProperty(new PropertyContentReference { Value = new ContentReference(5) });
        var handler = registry.Resolve(ctx);
        handler.GetDisplay(ctx).Should().Be("Page X (ID: 5)");
        handler.GetEditor(ctx)!.Kind.Should().Be("reference");
    }

    [Fact]
    public void Registry_UnknownType_FallsBackReadOnly()
    {
        var registry = BuildRegistry(Mock.Of<IContentLoader>());
        // UnknownProp has DataType.Block — no handler claims it → FallbackHandler.
        var ctx = PropertyHandlerContext.ForProperty(new UnknownProp());
        var handler = registry.Resolve(ctx);
        handler.GetEditor(ctx).Should().BeNull();          // read-only
        handler.TryParse("x", ctx, out _).Should().BeFalse();
    }

    private sealed class UnknownProp : PropertyData
    {
        public override PropertyDataType Type => PropertyDataType.Block;
        public override Type PropertyValueType => typeof(object);
        public override object? Value { get; set; }
        public override void ParseToSelf(string value) { }
        protected override void SetDefaultValue() { }
        public override PropertyData CreateWritableClone() => new UnknownProp { Value = Value };
    }
}
