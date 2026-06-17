using System.Collections;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class GetaCategoryHandlerTests
{
    // Minimal PropertyData stub that satisfies the abstract surface of both CMS 12 and CMS 13.
    // Modelled on the UnknownProp stub in BulkPropertyEditorIntegrationTests.cs.
    private sealed class TestProp : PropertyData
    {
        public override PropertyDataType Type => PropertyDataType.LongString;
        public override Type PropertyValueType => typeof(object);
        public override object? Value { get; set; }
        public override void ParseToSelf(string value) { }
        protected override void SetDefaultValue() { }
        public override PropertyData CreateWritableClone() => new TestProp { Value = Value };
    }

    /// <summary>
    /// Stand-in for Geta.Optimizely.Categories.CategoryList — a List&lt;ContentReference&gt; subclass
    /// with only a default constructor (no IEnumerable&lt;ContentReference&gt; ctor), so the handler
    /// falls through to the IList.Add path.
    /// </summary>
    public sealed class FakeGetaList : List<ContentReference> { }

    private static PropertyHandlerContext Ctx(PropertyData prop) =>
        PropertyHandlerContext.ForProperty(prop);

    private static GetaCategoryHandler Handler(
        IContentLoader? loader = null,
        string valueTypeFullName = "No.Such.Type",
        Func<Type, bool>? match = null) =>
        new(
            loader ?? Mock.Of<IContentLoader>(),
            Mock.Of<IContentTypeRepository>(),
            Mock.Of<IContentModelUsage>(),
            valueTypeFullName: valueTypeFullName,
            isGetaCategoryType: match);

    // ── CanHandle ──────────────────────────────────────────────────────────────

    [Fact]
    public void CanHandle_FalseForNonGetaTypes()
    {
        var h = Handler();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
        h.CanHandle(Ctx(new PropertyContentReference())).Should().BeFalse();
    }

    [Fact]
    public void CanHandle_TrueWhenPredicateMatches()
    {
        var h = Handler(match: t => t.Name == nameof(TestProp));
        h.CanHandle(Ctx(new TestProp())).Should().BeTrue();
    }

    // ── TryParse ───────────────────────────────────────────────────────────────

    [Fact]
    public void TryParse_FailsClosed_WhenGetaTypeAbsent()
    {
        var h = Handler(valueTypeFullName: "Not.A.Real.Type");
        h.TryParse("3;4", Ctx(new TestProp()), out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_BuildsListType_WhenResolvable()
    {
        // FakeGetaList has only a default ctor → handler uses IList.Add path → Count == 2
        var h = Handler(valueTypeFullName: typeof(FakeGetaList).FullName!);
        h.TryParse("3;4", Ctx(new TestProp()), out var v).Should().BeTrue();
        ((IList)v!).Count.Should().Be(2);
    }

    // ── GetDisplay ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDisplay_ResolvesCategoryNames()
    {
        var loader = new Mock<IContentLoader>();
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns("Cat A");
        IContent? oc = content.Object;
        loader.Setup(l => l.TryGet(new ContentReference(3), out oc)).Returns(true);

        var h = Handler(loader.Object);
        var prop = new TestProp { Value = new FakeGetaList { new ContentReference(3) } };
        h.GetDisplay(Ctx(prop)).Should().Be("Cat A");
    }

    // ── GetEditor ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetEditor_ReturnsNull_WhenGetaTypeAbsent()
    {
        var h = Handler(valueTypeFullName: "Not.A.Real.Type");
        h.GetEditor(Ctx(new TestProp())).Should().BeNull();
    }

    [Fact]
    public void GetEditor_ReturnsCategoryKind_WhenGetaTypePresent()
    {
        var h = Handler(valueTypeFullName: typeof(FakeGetaList).FullName!);
        var ed = h.GetEditor(Ctx(new TestProp()))!;
        ed.Kind.Should().Be("category");
        ed.Multiple.Should().BeTrue();
    }

    // ── Priority ───────────────────────────────────────────────────────────────

    [Fact]
    public void Priority_Is50()
    {
        Handler().Priority.Should().Be(50);
    }
}
