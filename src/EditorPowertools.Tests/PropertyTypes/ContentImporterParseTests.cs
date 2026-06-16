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

    [Fact]
    public void Registry_UnknownType_DoesNotParse_SoImporterWillWarn()
    {
        // FallbackHandler returns false for a type no handler claims; the importer turns
        // that into a per-row "could not set property" warning rather than silent data loss.
        var registry = new PropertyTypeHandlerRegistry(new IPropertyTypeHandler[] { new FallbackHandler() });
        var ctx = PropertyHandlerContext.ForProperty(new UnknownProp());
        registry.Resolve(ctx).TryParse("anything", ctx, out _).Should().BeFalse();
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
