using EPiServer;
using System.Security.Principal;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Providers;

public class GraphContentPickerSearchProviderTests
{
    private readonly Mock<IGraphSearchService> _service = new();
    private readonly Mock<IContentLoader> _loader = new();
    private readonly Mock<IContentRepository> _repo = new();
    private readonly Mock<IContentTypeRepository> _types = new();

    private GraphContentPickerSearchProvider Create()
    {
        _service.Setup(s => s.ValidateContentTypes(It.IsAny<IEnumerable<string>?>()))
            .Returns((IEnumerable<string>? n) => n!.ToList());
        return new(_service.Object, _loader.Object, _repo.Object, _types.Object,
            NullLogger<GraphContentPickerSearchProvider>.Instance);
    }

    private Mock<IContent> Content(int id, string name, bool readable = true)
    {
        var c = new Mock<IContent>();
        c.SetupGet(x => x.ContentLink).Returns(new ContentReference(id));
        c.SetupGet(x => x.Name).Returns(name);
        var guid = Guid.NewGuid();
        c.SetupGet(x => x.ContentGuid).Returns(guid);
        c.SetupGet(x => x.ContentTypeID).Returns(7);
        var d = new Mock<ISecurityDescriptor>();
        d.Setup(x => x.HasAccess(It.IsAny<IPrincipal>(), It.IsAny<AccessLevel>())).Returns(readable);
        c.As<ISecurable>().Setup(s => s.GetSecurityDescriptor()).Returns(d.Object);
        IContent? value = c.Object;
        _repo.Setup(r => r.TryGet<IContent>(guid, out value)).Returns(true);
        return c;
    }

    private void Hits(params Guid[] guids) =>
        _service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphSearchResult(
                guids.Select(g => new GraphSearchHit(g, "n", "")).ToList(), guids.Length, GraphFacets.Empty));

    [Fact]
    public void Maps_fields_and_filters_unreadable()
    {
        _types.Setup(t => t.Load(7)).Returns(new ContentType { Name = "StartPage" });
        var ok = Content(1, "Start");
        var denied = Content(2, "Secret", readable: false);
        _loader.Setup(l => l.GetChildren<IContent>(ok.Object.ContentLink)).Returns(new[] { Mock.Of<IContent>() });
        Hits(denied.Object.ContentGuid, ok.Object.ContentGuid);

        var result = Create().Search("st", null, 10).ToList();

        result.Should().ContainSingle();
        result[0].ContentId.Should().Be(1);
        result[0].Name.Should().Be("Start");
        result[0].TypeName.Should().Be("StartPage");
        result[0].HasChildren.Should().BeTrue();
    }

    [Fact]
    public void Uses_root_guid_as_ancestor_and_no_locale_filter()
    {
        var root = Content(5, "root");
        IContent? rootValue = root.Object;
        _loader.Setup(l => l.TryGet<IContent>(It.Is<ContentReference>(c => c.ID == 5), out rootValue)).Returns(true);
        GraphSearchRequest? captured = null;
        _service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback((GraphSearchRequest r, CancellationToken _) => captured = r)
            .ReturnsAsync(GraphSearchResult.Empty);

        Create().Search("x", new ContentReference(5), 10).Should().BeEmpty();

        captured!.Scope.AncestorGuids.Should().Equal(root.Object.ContentGuid);
        captured.Scope.FilterByLocale.Should().BeFalse();
        captured.Scope.ContentTypes.Should().BeEquivalentTo("PageData", "BlockData", "MediaData", "ContentFolder");
    }

    [Fact]
    public void Swallows_exceptions()
    {
        _service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GraphQueryException("boom"));
        Create().Search("x", null, 10).Should().BeEmpty();
    }

    [Fact]
    public void Blank_query_is_empty()
    {
        Create().Search(" ", null, 10).Should().BeEmpty();
        _service.Verify(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
