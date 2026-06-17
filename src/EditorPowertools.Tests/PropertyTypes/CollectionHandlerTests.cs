using System.Collections;
using EPiServer.Core;
using EPiServer.SpecializedProperties;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class CollectionHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) => PropertyHandlerContext.ForProperty(prop);

    [Fact]
    public void Xhtml_DisplaysPlainText()
    {
        var h = new XhtmlStringHandler();
        var prop = new PropertyXhtmlString { Value = new XhtmlString("<p>Hello <b>world</b></p>") };
        h.CanHandle(Ctx(prop)).Should().BeTrue();
        h.GetDisplay(Ctx(prop)).Should().Contain("Hello").And.NotContain("<");
        h.GetEditor(Ctx(prop))!.Kind.Should().Be("textarea");
        h.TryParse("plain", Ctx(prop), out var v).Should().BeTrue();
        v.Should().BeOfType<XhtmlString>();
    }

    [Fact]
    public void PropertyList_DisplaysJoined_AndParsesDelimited()
    {
        var h = new PropertyListHandler();
        var prop = new PropertyStringList();
        h.CanHandle(Ctx(prop)).Should().BeTrue();
        h.TryParse("a;b;c", Ctx(prop), out var v).Should().BeTrue();
        ((IEnumerable)v!).Cast<string>().Should().BeEquivalentTo("a", "b", "c");
    }
}
