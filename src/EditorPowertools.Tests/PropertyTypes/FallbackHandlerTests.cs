using System.Collections.Generic;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class FallbackHandlerTests
{
    private static PropertyHandlerContext Ctx(object? value)
    {
        var prop = new Mock<EPiServer.Core.PropertyData> { CallBase = false };
        prop.Setup(p => p.Value).Returns(value!);
        return new PropertyHandlerContext { Definition = null!, Property = prop.Object };
    }

    [Fact]
    public void CanHandle_AlwaysTrue() =>
        new FallbackHandler().CanHandle(null!).Should().BeTrue();

    [Fact]
    public void GetDisplay_Null_ReturnsEmpty() =>
        new FallbackHandler().GetDisplay(Ctx(null)).Should().BeEmpty();

    [Fact]
    public void GetDisplay_Collection_ReturnsItemCount() =>
        new FallbackHandler().GetDisplay(Ctx(new List<int> { 1, 2, 3 })).Should().Be("(3 items)");

    [Fact]
    public void GetEditor_IsNull_ReadOnly() =>
        new FallbackHandler().GetEditor(Ctx("x")).Should().BeNull();

    [Fact]
    public void TryParse_AlwaysFalse() =>
        new FallbackHandler().TryParse("x", Ctx("x"), out _).Should().BeFalse();
}
