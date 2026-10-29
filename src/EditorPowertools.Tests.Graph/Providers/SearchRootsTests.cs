using EPiServer;
using EPiServer.Core;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Providers;

public class SearchRootsTests
{
    [Fact]
    public void Parse_StripsVersionIgnoresGarbage()
    {
        var set = SearchRoots.Parse(["5", "5_12", "", "garbage", "-1073741823__CatalogContent"]);
        set.Should().Contain(new ContentReference(5));
        set.Count(r => r.ID == 5).Should().Be(1);
        set.Should().NotContain(r => r.ID == 0);
    }

    [Fact]
    public void Parse_Null_Empty() => SearchRoots.Parse(null).Should().BeEmpty();

    private static IContent Content(int id)
    {
        var m = new Mock<IContent>();
        m.SetupGet(c => c.ContentLink).Returns(new ContentReference(id));
        return m.Object;
    }

    [Fact]
    public void Contains_EmptyRoots_True() =>
        SearchRoots.Contains(Content(1), new HashSet<ContentReference>(), Mock.Of<IContentLoader>()).Should().BeTrue();

    [Fact]
    public void Contains_SelfIsRoot_True() =>
        SearchRoots.Contains(Content(5), SearchRoots.Parse(["5"]), Mock.Of<IContentLoader>()).Should().BeTrue();

    [Fact]
    public void Contains_AncestorMatches_True()
    {
        var content = Content(9);
        var loader = new Mock<IContentLoader>();
        loader.Setup(l => l.GetAncestors(content.ContentLink)).Returns(new[] { Content(7), Content(5) });
        SearchRoots.Contains(content, SearchRoots.Parse(["5"]), loader.Object).Should().BeTrue();
    }

    [Fact]
    public void Contains_NoMatch_False()
    {
        var content = Content(9);
        var loader = new Mock<IContentLoader>();
        loader.Setup(l => l.GetAncestors(content.ContentLink)).Returns(new[] { Content(7) });
        SearchRoots.Contains(content, SearchRoots.Parse(["5"]), loader.Object).Should().BeFalse();
    }

    [Fact]
    public void ToGuids_ReturnsLoadableRoots()
    {
        var g = Guid.NewGuid();
        var root = new Mock<IContent>();
        root.SetupGet(c => c.ContentGuid).Returns(g);
        IContent? outRoot = root.Object;
        var loader = new Mock<IContentLoader>();
        loader.Setup(l => l.TryGet<IContent>(new ContentReference(5), out outRoot)).Returns(true);
        SearchRoots.ToGuids(SearchRoots.Parse(["5", "6"]), loader.Object).Should().Equal(g);
    }
}
