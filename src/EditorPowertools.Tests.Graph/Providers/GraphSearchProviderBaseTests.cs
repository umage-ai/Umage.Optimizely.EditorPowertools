using EPiServer;
using System.Security.Principal;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.Security;
using EPiServer.Shell.Search;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Providers;

public class GraphSearchProviderBaseTests
{
    private sealed class TestProvider : GraphSearchProviderBase
    {
        public TestProvider(Fixture f) : base(
            f.Service.Object, f.Availability.Object, f.Drafts.Object, f.Results.Object, f.Access.Object,
            f.Http.Object, f.Loader.Object, f.Repository.Object, Microsoft.Extensions.Options.Options.Create(f.Options),
            new MemoryLocalizationService(), f.Logger.Object)
        {
        }

        protected override string? PreferredLanguage() => "en";
        protected override string BuiltInArea => "CMS/pages";
        protected override string CategoryPath => "/editorpowertools/graph/search/category/pages";
        protected override string CategoryFallback => "Pages (Graph)";
        protected override string IconCssClass => "icon";
        protected override IReadOnlyList<string> ScopeTypes { get; } = new[] { "PageData" };
        protected override bool FilterByLocale => true;
        protected override bool IsInScope(IContent content) => true;
#if OPTIMIZELY_CMS13
        protected override string KeySuffix => "pages";
#endif
    }

    private sealed class Fixture
    {
        public Mock<IGraphSearchService> Service = new();
        public Mock<IGraphAvailability> Availability = new() { CallBase = true };
        public Mock<DraftSearch> Drafts = new(Mock.Of<IContentVersionRepository>(), Mock.Of<ILogger<DraftSearch>>());
        public Mock<SearchResultFactory> Results = new(Mock.Of<IContentTypeRepository>(), null!, new MemoryLocalizationService());
        public Mock<GraphFeatureAccessChecker> Access = new(Microsoft.Extensions.Options.Options.Create(new EditorPowertoolsGraphOptions()), null!);
        public Mock<IHttpContextAccessor> Http = new();
        public Mock<IContentLoader> Loader = new();
        public Mock<IContentRepository> Repository = new();
        public Mock<ILogger> Logger = new();
        public EditorPowertoolsGraphOptions Options = new();
        public List<GraphSearchRequest> Requests = new();

        public Fixture()
        {
            Http.SetupGet(h => h.HttpContext).Returns(new DefaultHttpContext());
            Access.Setup(a => a.HasUiAccess(It.IsAny<HttpContext?>(), It.IsAny<string>(), It.IsAny<PermissionType>())).Returns(true);
            Access.Setup(a => a.IsFeatureEnabled(It.IsAny<string>())).Returns(true);
            Availability.SetupGet(a => a.State).Returns(GraphAvailabilityState.Available);
            Service.Setup(s => s.ValidateContentTypes(It.IsAny<IEnumerable<string>?>()))
                .Returns((IEnumerable<string>? n) => n!.ToList());
            Results.Setup(r => r.Create(It.IsAny<IContent>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string>()))
                .Returns((IContent c, bool draft, string? l, string i) =>
                    new SearchResult("/x/" + c.ContentLink.ID, (draft ? "draft:" : "pub:") + c.Name, ""));
            Drafts.Setup(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()))
                .Returns(Array.Empty<ContentReference>());
        }

        public TestProvider Create() => new(this);

        public void Hits(params GraphSearchHit[] hits) =>
            Service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
                .Callback((GraphSearchRequest r, CancellationToken _) => Requests.Add(r))
                .ReturnsAsync(new GraphSearchResult(hits, hits.Length, GraphFacets.Empty));

        public Mock<IContent> Content(int id, string name, bool readable = true)
        {
            var c = new Mock<IContent>();
            c.SetupGet(x => x.ContentLink).Returns(new ContentReference(id));
            c.SetupGet(x => x.ParentLink).Returns(ContentReference.RootPage);
            c.SetupGet(x => x.Name).Returns(name);
            var guid = Guid.NewGuid();
            c.SetupGet(x => x.ContentGuid).Returns(guid);
            var descriptor = new Mock<ISecurityDescriptor>();
            descriptor.Setup(d => d.HasAccess(It.IsAny<IPrincipal>(), It.IsAny<AccessLevel>())).Returns(readable);
            c.As<ISecurable>().Setup(s => s.GetSecurityDescriptor()).Returns(descriptor.Object);
            IContent? value = c.Object;
            Repository.Setup(r => r.TryGet<IContent>(guid, out value)).Returns(true);
            return c;
        }

        public void LoaderKnows(Mock<IContent> content)
        {
            IContent? value = content.Object;
            Loader.Setup(l => l.TryGet<IContent>(content.Object.ContentLink, out value)).Returns(true);
        }

        public static GraphSearchHit Hit(Guid guid) => new(guid, "n", "");
    }

    private static Query Q(string text = "abc", int max = 10, params string[] roots) =>
        new(text, max) { SearchRoots = roots };

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_query_returns_empty_without_calling_service(string text)
    {
        var f = new Fixture();
        f.Create().Search(Q(text)).Should().BeEmpty();
        f.Service.Verify(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void No_ui_access_returns_empty_without_calling_service()
    {
        var f = new Fixture();
        f.Access.Setup(a => a.HasUiAccess(It.IsAny<HttpContext?>(), nameof(GraphFeatureToggles.SearchProviders),
            It.IsAny<PermissionType>())).Returns(false);
        f.Create().Search(Q()).Should().BeEmpty();
        f.Service.Verify(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Unavailable_returns_empty_without_calling_service()
    {
        var f = new Fixture();
        f.Availability.SetupGet(a => a.State).Returns(GraphAvailabilityState.NotConfigured);
        f.Create().Search(Q()).Should().BeEmpty();
        f.Service.Verify(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Graph_failure_returns_empty_and_logs_error()
    {
        var f = new Fixture();
        f.Service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GraphQueryException("boom"));
        f.Create().Search(Q()).Should().BeEmpty();
        f.Logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void Skips_unloadable_and_unreadable_hits()
    {
        var f = new Fixture();
        var ok = f.Content(1, "ok");
        var denied = f.Content(2, "denied", readable: false);
        f.Hits(Fixture.Hit(Guid.NewGuid()), Fixture.Hit(denied.Object.ContentGuid), Fixture.Hit(ok.Object.ContentGuid));

        f.Create().Search(Q()).Select(r => r.Title).Should().Equal("pub:ok");
    }

    [Fact]
    public void Roots_overfetch_and_carry_ancestors()
    {
        var f = new Fixture();
        var root = f.Content(5, "root");
        f.LoaderKnows(root);
        var child = f.Content(6, "child");
        f.Loader.Setup(l => l.GetAncestors(child.Object.ContentLink)).Returns(new[] { root.Object });
        f.Hits(Fixture.Hit(child.Object.ContentGuid));
        f.Options.Search.OverfetchFactor = 5;
        f.Options.Search.MaxOverfetch = 30;

        var results = f.Create().Search(Q("abc", 10, "5")).ToList();

        results.Should().HaveCount(1);
        f.Requests.Should().HaveCount(1);
        f.Requests[0].Scope.AncestorGuids.Should().Equal(root.Object.ContentGuid);
        f.Requests[0].Limit.Should().Be(30);
    }

    [Fact]
    public void Roots_zero_hits_retries_without_ancestors_and_removes_out_of_root_hits()
    {
        var f = new Fixture();
        var root = f.Content(5, "root");
        f.LoaderKnows(root);
        var outside = f.Content(9, "outside");

        var calls = 0;
        f.Service.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .Callback((GraphSearchRequest r, CancellationToken _) => f.Requests.Add(r))
            .ReturnsAsync(() => ++calls == 1
                ? GraphSearchResult.Empty
                : new GraphSearchResult(new[] { Fixture.Hit(outside.Object.ContentGuid) }, 1, GraphFacets.Empty));

        f.Create().Search(Q("abc", 10, "5")).Should().BeEmpty();

        f.Requests.Should().HaveCount(2);
        f.Requests[0].Scope.AncestorGuids.Should().NotBeEmpty();
        f.Requests[1].Scope.AncestorGuids.Should().BeEmpty();
    }

    [Fact]
    public void Drafts_listed_first_capped_and_total_limited()
    {
        var f = new Fixture();
        var pubs = Enumerable.Range(1, 6).Select(i => f.Content(i, "p" + i)).ToList();
        f.Hits(pubs.Select(p => Fixture.Hit(p.Object.ContentGuid)).ToArray());
        var drafts = Enumerable.Range(100, 5).Select(i => f.Content(i, "d" + i)).ToList();
        foreach (var d in drafts)
        {
            f.LoaderKnows(d);
        }

        f.Drafts.Setup(d => d.FindDrafts("abc", It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()))
            .Returns(drafts.Select(d => d.Object.ContentLink).ToList());

        // MaxResults 6 -> draft cap max(1, 6/3) = 2; total never above 6.
        var titles = f.Create().Search(Q("abc", 6)).Select(r => r.Title).ToList();

        titles.Should().HaveCount(6);
        titles.Take(2).Should().OnlyContain(t => t.StartsWith("draft:"));
        titles.Skip(2).Should().OnlyContain(t => t.StartsWith("pub:"));
    }

    [Fact]
    public void Drafts_are_skipped_when_disabled()
    {
        var f = new Fixture();
        f.Options.Search.IncludeDrafts = false;
        f.Hits();
        f.Create().Search(Q()).Should().BeEmpty();
        f.Drafts.Verify(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()), Times.Never);
    }

    [Fact]
    public void Area_joins_builtin_area_case_insensitively_by_default()
    {
        new Fixture().Create().Area.Should().Be("cms/pages");
    }

    [Fact]
    public void DisableBuiltInProviders_uses_builtin_area_spelling_only_when_graph_is_available()
    {
        var f = new Fixture();
        f.Options.Search.DisableBuiltInProviders = true;
        var sut = f.Create();
        sut.Area.Should().Be("CMS/pages");

        f.Availability.SetupGet(a => a.State).Returns(GraphAvailabilityState.NotConfigured);
        sut.Area.Should().Be("cms/pages");
    }

    [Fact]
    public void Category_falls_back_to_english()
    {
        new Fixture().Create().Category.Should().Be("Pages (Graph)");
    }

#if OPTIMIZELY_CMS13
    [Fact]
    public void Key_is_unique_and_dot_free() =>
        new Fixture().Create().Key.Should().Be("editorpowertools-graph-pages").And.NotContain(".");
#endif
}
