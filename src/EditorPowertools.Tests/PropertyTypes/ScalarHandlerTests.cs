using System.Globalization;
using EPiServer.Core;
using EPiServer.SpecializedProperties;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ScalarHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) =>
        new() { Definition = null!, Property = prop };

    [Fact]
    public void StringHandler_ParsesAndDisplays()
    {
        var h = new StringHandler();
        h.TryParse("hello", Ctx(new PropertyString()), out var v).Should().BeTrue();
        v.Should().Be("hello");
        h.GetDisplay(Ctx(new PropertyString { Value = "hi" })).Should().Be("hi");
        h.GetEditor(Ctx(new PropertyString()))!.Kind.Should().Be("text");
    }

    [Fact]
    public void NumberHandler_ParsesInt_RejectsGarbage()
    {
        var h = new NumberHandler();
        h.TryParse("42", Ctx(new PropertyNumber()), out var v).Should().BeTrue();
        v.Should().Be(42);
        h.TryParse("notanumber", Ctx(new PropertyNumber()), out _).Should().BeFalse();
    }

    [Fact]
    public void BooleanHandler_Parses()
    {
        var h = new BooleanHandler();
        h.TryParse("true", Ctx(new PropertyBoolean()), out var v).Should().BeTrue();
        v.Should().Be(true);
    }

    [Fact]
    public void DateHandler_ParsesInvariant()
    {
        var h = new DateHandler();
        h.TryParse("2026-06-16", Ctx(new PropertyDate()), out var v).Should().BeTrue();
        ((DateTime)v!).Year.Should().Be(2026);
    }

    [Fact]
    public void UrlHandler_BeatsStringByCheckingClrType()
    {
        var h = new UrlHandler();
        h.CanHandle(Ctx(new PropertyUrl())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
        h.TryParse("https://x.com", Ctx(new PropertyUrl()), out var v).Should().BeTrue();
        v.Should().BeOfType<EPiServer.Url>();
    }

    [Fact]
    public void FloatHandler_ParsesInvariant_AndDisplaysInvariant()
    {
        var h = new FloatHandler();
        h.CanHandle(Ctx(new PropertyFloatNumber())).Should().BeTrue();
        h.TryParse("3.14", Ctx(new PropertyFloatNumber()), out var v).Should().BeTrue();
        v.Should().Be(3.14d);
        h.TryParse("notanumber", Ctx(new PropertyFloatNumber()), out _).Should().BeFalse();
        h.GetDisplay(Ctx(new PropertyFloatNumber { Value = 3.14d })).Should().Be("3.14");
    }
}
