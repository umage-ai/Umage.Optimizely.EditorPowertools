using EPiServer.Core;
using EPiServer.Shell.ObjectEditing;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class SelectionHandlerTests
{
    public sealed class ColorFactory : ISelectionFactory
    {
        public IEnumerable<ISelectItem> GetSelections(ExtendedMetadata metadata) => new ISelectItem[]
        {
            new SelectItem { Text = "Red", Value = "r" },
            new SelectItem { Text = "Green", Value = "g" },
        };
    }

    private sealed class Model
    {
        [SelectOne(SelectionFactoryType = typeof(ColorFactory))]
        public string? Single { get; set; }

        [SelectMany(SelectionFactoryType = typeof(ColorFactory))]
        public string? Multi { get; set; }
    }

    private static PropertyHandlerContext Ctx(string modelProp, PropertyData prop) => new()
    {
        Definition = null,
        ModelType = typeof(Model),
        ModelProperty = typeof(Model).GetProperty(modelProp),
        Property = prop
    };

    [Fact]
    public void SelectOne_ProducesSelectEditorWithOptions()
    {
        var h = new SelectionHandler();
        var ctx = Ctx(nameof(Model.Single), new PropertyString());
        h.CanHandle(ctx).Should().BeTrue();
        var ed = h.GetEditor(ctx)!;
        ed.Kind.Should().Be("select");
        ed.Multiple.Should().BeFalse();
        ed.Options.Should().ContainSingle(o => o.Value == "r" && o.Label == "Red");
    }

    [Fact]
    public void SelectMany_IsMultiselect_AndDisplaysLabels()
    {
        var h = new SelectionHandler();
        var ctx = Ctx(nameof(Model.Multi), new PropertyString { Value = "r,g" });
        h.GetEditor(ctx)!.Kind.Should().Be("multiselect");
        h.GetDisplay(ctx).Should().Be("Red, Green");
    }
}
