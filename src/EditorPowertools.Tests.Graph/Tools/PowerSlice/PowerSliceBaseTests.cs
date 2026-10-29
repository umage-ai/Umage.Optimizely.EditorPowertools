using EPiServer;
using EPiServer.DataAbstraction;
using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Core;
using EPiServer.Security;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSlice;

public class PowerSliceBaseTests
{
    private sealed class TestSlice : PowerSliceBase
    {
        public TestSlice(SliceFixture f) : base(f.Repository.Object, f.Helper.Object, f.Access.Object, f.Http.Object, f.Localization)
        {
        }

        public override string Name => "test";
        protected override string DisplayNamePath => "/x/test";
        protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters) => Array.Empty<IContent>();
    }

    private static IContent[] Items(params string[] names) =>
        names.Select((n, i) => (IContent)new TestContent(n, i + 1)).ToArray();

    [Fact]
    public void Filter_returns_nothing_when_access_is_denied()
    {
        var slice = new TestSlice(new SliceFixture(allowed: false));

        SlicePipeline.Filter(slice, Items("A", "B"), SliceFixture.Parameters()).Should().BeEmpty();
    }

    [Fact]
    public void Filter_name_match_is_case_insensitive()
    {
        var slice = new TestSlice(new SliceFixture());

        var result = SlicePipeline.Filter(slice, Items("Annual Report", "Contact", "REPORT card"), SliceFixture.Parameters(q: "report"));

        result.Select(c => c.Name).Should().BeEquivalentTo("Annual Report", "REPORT card");
    }

    [Fact]
    public void Filter_removes_items_the_caller_cannot_read()
    {
        var slice = new TestSlice(new SliceFixture());
        var descriptor = new Mock<IContentSecurityDescriptor>();
        descriptor.Setup(d => d.HasAccess(It.IsAny<System.Security.Principal.IPrincipal>(), AccessLevel.Read)).Returns(false);
        var secret = new Mock<IContent>();
        secret.Setup(c => c.Name).Returns("Secret");
        secret.As<ISecurable>().Setup(s => s.GetSecurityDescriptor()).Returns(descriptor.Object);

        var items = new[] { secret.Object, Items("Public")[0] };

        SlicePipeline.Filter(slice, items, SliceFixture.Parameters()).Select(c => c.Name).Should().Equal("Public");
    }

    private static TestContent C(string name, int id, int savedDay, int? publishedDay = null) =>
        new(name, id, new DateTime(2026, 1, savedDay), publishedDay is null ? null : new DateTime(2026, 1, publishedDay.Value));

    [Theory]
    [InlineData("name", "alpha,beta,gamma")]
    [InlineData("changed", "gamma,alpha,beta")]
    [InlineData("changed-asc", "beta,alpha,gamma")]
    [InlineData("published", "beta,gamma,alpha")]
    public void Sort_maps_keys(string key, string expected)
    {
        var slice = new TestSlice(new SliceFixture());
        IContent[] items =
        {
            C("gamma", 1, savedDay: 20, publishedDay: 5),
            C("alpha", 2, savedDay: 10, publishedDay: null),
            C("beta", 3, savedDay: 5, publishedDay: 9)
        };

        SlicePipeline.Sort(slice, items, SliceFixture.Parameters(sortBy: key)).Select(c => c.Name)
            .Should().Equal(expected.Split(','));
    }

    [Fact]
    public void Sort_unknown_or_missing_key_keeps_order()
    {
        var slice = new TestSlice(new SliceFixture());
        var items = Items("z", "a", "m");

        SlicePipeline.Sort(slice, items, SliceFixture.Parameters(sortBy: "bogus")).Select(c => c.Name).Should().Equal("z", "a", "m");
        SlicePipeline.Sort(slice, items, SliceFixture.Parameters()).Select(c => c.Name).Should().Equal("z", "a", "m");
    }
}
