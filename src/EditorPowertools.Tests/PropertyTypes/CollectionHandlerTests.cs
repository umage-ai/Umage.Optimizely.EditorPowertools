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

    [Fact]
    public void PropertyList_GetEditor_ReadOnly_ForNonConvertibleItemTypes()
    {
        // ParseListValue relies on Convert.ChangeType, which cannot produce ContentReference.
        // Offering an editor for PropertyList<ContentReference> would render an editable cell
        // whose every commit is rejected — such lists must stay read-only (as before the registry).
        var h = new PropertyListHandler();
        var refListProp = new PropertyContentReferenceList();
        h.CanHandle(Ctx(refListProp)).Should().BeTrue("display should still work for reference lists");
        h.GetEditor(Ctx(refListProp)).Should().BeNull();
        h.GetEditor(Ctx(new PropertyStringList())).Should().NotBeNull("convertible item types stay editable");
    }

    [Fact]
    public void Xhtml_TryParseEmpty_ReturnsNullTrue()
    {
        var h = new XhtmlStringHandler();
        h.TryParse("", Ctx(new PropertyXhtmlString()), out var v).Should().BeTrue();
        v.Should().BeNull();
    }

    [Fact]
    public void PropertyList_TryParseNull_ReturnsFalse()
    {
        var h = new PropertyListHandler();
        h.TryParse(null, Ctx(new PropertyStringList()), out _).Should().BeFalse();
    }

    [Fact]
    public void PropertyList_DisplaysJoined()
    {
        var h = new PropertyListHandler();
        var prop = new PropertyStringList();
        h.TryParse("x;y", Ctx(prop), out var parsed).Should().BeTrue();
        prop.Value = (IList<string>?)parsed;
        h.GetDisplay(Ctx(prop)).Should().Be("x, y");
    }

    [Fact]
    public void PropertyList_ParsesJsonArray()
    {
        var h = new PropertyListHandler();
        h.TryParse("[\"a\",\"b\"]", Ctx(new PropertyStringList()), out var v).Should().BeTrue();
        ((System.Collections.IEnumerable)v!).Cast<string>().Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public void PropertyList_TryParseNonConvertible_ReturnsFalse()
    {
        var h = new PropertyListHandler();
        // "abc" cannot be converted to int, so TryParse must return false rather than throw.
        h.TryParse("abc", Ctx(new PropertyIntList()), out var v).Should().BeFalse();
        v.Should().BeNull();
    }

    [Fact]
    public void Xhtml_GetEditValue_ReturnsHtmlNotStripped()
    {
        var h = new XhtmlStringHandler();
        var prop = new PropertyXhtmlString { Value = new XhtmlString("<p>Hi</p>") };
        h.GetEditValue(Ctx(prop)).Should().Contain("<p>");
    }

    /// <summary>Minimal concrete PropertyList&lt;int&gt; for testing — EPiServer ships no int-list type.</summary>
    private sealed class PropertyIntList : PropertyList<int>
    {
        public override EPiServer.Core.PropertyData CreateWritableClone() => new PropertyIntList();
    }
}
