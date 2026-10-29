using System.Security.Principal;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.Security;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Tools.PowerSearch;

public class PowerSearchServiceTests
{
    private delegate bool TryGetGuid(Guid guid, out IContent? content);

    private sealed class TestService : PowerSearchService
    {
        public TestService(Fixture f) : base(
            f.Search.Object, f.Availability.Object, f.Types.Object, f.Repository.Object, f.Loader.Object,
            f.Drafts.Object, new MemoryLocalizationService(), Microsoft.Extensions.Options.Options.Create(f.Options),
            Mock.Of<ILogger<PowerSearchService>>())
        {
        }

        protected override string ResolveEditUrl(IContent content) => "/edit/" + content.ContentLink.ID;
    }

    private sealed class Fixture
    {
        public Mock<IGraphSearchService> Search = new();
        public Mock<IGraphAvailability> Availability = new();
        public Mock<IContentTypeRepository> Types = new();
        public Mock<IContentRepository> Repository = new();
        public Mock<IContentLoader> Loader = new();
        public Mock<DraftSearch> Drafts = new(Mock.Of<IContentVersionRepository>(), Mock.Of<ILogger<DraftSearch>>());
        public EditorPowertoolsGraphOptions Options = new();
        public List<GraphSearchRequest> Requests = new();
        public Dictionary<Guid, IContent> ByGuid = new();
        public int InFlight;
        public int MaxInFlight;

        public Fixture()
        {
            Availability.SetupGet(a => a.State).Returns(GraphAvailabilityState.Available);
            Availability.SetupGet(a => a.IsAvailable).Returns(true);
            Options.Search.IncludeDrafts = false;
            Types.Setup(t => t.List()).Returns(new[]
            {
                new ContentType { Name = "ArticlePage", Base = ContentTypeBase.Page },
                new ContentType { Name = "TeaserBlock", Base = ContentTypeBase.Block },
                new ContentType { Name = "Photo", Base = ContentTypeBase.Image },
                new ContentType { Name = "Doc", Base = ContentTypeBase.Media },
                new ContentType { Name = "Clip", Base = ContentTypeBase.Video },
                new ContentType { Name = "WeirdThing", Base = ContentTypeBase.Folder }
            });
            Search.Setup(s => s.ValidateContentTypes(It.IsAny<IEnumerable<string>?>()))
                .Returns((IEnumerable<string>? n) => n?.ToList() ?? new List<string>());
            Drafts.Setup(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()))
                .Returns(Array.Empty<ContentReference>());
            Repository.Setup(r => r.TryGet<IContent>(It.IsAny<Guid>(), out It.Ref<IContent?>.IsAny))
                .Returns(new TryGetGuid(TryGet));
            Returns(_ => GraphSearchResult.Empty);
        }

        private bool TryGet(Guid guid, out IContent? content)
        {
            var now = Interlocked.Increment(ref InFlight);
            try
            {
                MaxInFlight = Math.Max(MaxInFlight, now);
                Thread.Sleep(2);
                return ByGuid.TryGetValue(guid, out content);
            }
            finally
            {
                Interlocked.Decrement(ref InFlight);
            }
        }

        public void Returns(Func<GraphSearchRequest, GraphSearchResult> factory) =>
            Search.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
                .Returns((GraphSearchRequest r, CancellationToken _) =>
                {
                    lock (Requests) Requests.Add(r);
                    return Task.FromResult(factory(r));
                });

        public Guid Content(int id, string name, bool readable = true)
        {
            var c = new Mock<IContent>();
            c.SetupGet(x => x.ContentLink).Returns(new ContentReference(id));
            c.SetupGet(x => x.ParentLink).Returns(ContentReference.RootPage);
            c.SetupGet(x => x.Name).Returns(name);
            c.SetupGet(x => x.ContentTypeID).Returns(7);
            var guid = Guid.NewGuid();
            c.SetupGet(x => x.ContentGuid).Returns(guid);
            var descriptor = new Mock<ISecurityDescriptor>();
            descriptor.Setup(d => d.HasAccess(It.IsAny<IPrincipal>(), It.IsAny<AccessLevel>())).Returns(readable);
            c.As<ISecurable>().Setup(s => s.GetSecurityDescriptor()).Returns(descriptor.Object);
            ByGuid[guid] = c.Object;
            return guid;
        }

        public PowerSearchService Create() => new TestService(this);
    }

    private static PowerSearchQuery Q(
        string? q = "abc", PowerSearchTab tab = PowerSearchTab.Pages, int take = 10, int skip = 0,
        string[]? types = null, string[]? langs = null, Guid[]? sections = null) =>
        new(q, tab, take, skip, types ?? Array.Empty<string>(), langs ?? Array.Empty<string>(), sections ?? Array.Empty<Guid>());

    private static GraphSearchResult Result(int total, GraphFacets? facets, params Guid[] guids) =>
        new(guids.Select(g => new GraphSearchHit(g, "n", "")).ToList(), total, facets ?? GraphFacets.Empty);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_query_browses_the_tab_with_facets_and_skips_drafts(string? q)
    {
        var f = new Fixture();
        f.Options.Search.IncludeDrafts = true;
        var hit = f.Content(1, "Newest");
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(1, null, hit) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(q, types: new[] { "ArticlePage" }), CancellationToken.None);

        r.State.Should().Be("ok");
        r.Hits.Should().ContainSingle().Which.Name.Should().Be("Newest");
        f.Requests.Should().OnlyContain(req => req.Phrase == string.Empty);
        f.Requests.Single(req => req.Limit > 1).Filters.ContentTypes.Should().Equal("ArticlePage");
        f.Drafts.Verify(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()), Times.Never);
    }

    [Fact]
    public async Task Unavailable_graph_returns_unavailable_state_without_calling_graph()
    {
        var f = new Fixture();
        f.Availability.SetupGet(a => a.IsAvailable).Returns(false);
        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);
        r.State.Should().Be("unavailable");
        f.Search.Verify(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Graph_failure_returns_error_state_without_message()
    {
        var f = new Fixture();
        f.Search.Setup(s => s.SearchAsync(It.IsAny<GraphSearchRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GraphQueryException("secret-marker"));
        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);
        r.State.Should().Be("error");
        System.Text.Json.JsonSerializer.Serialize(r).Should().NotContain("secret-marker");
    }

    [Theory]
    [InlineData(0, 20)]      // default page size
    [InlineData(-5, 20)]
    [InlineData(500, 50)]    // clamped to 50
    [InlineData(7, 7)]
    public async Task Take_is_clamped_and_skip_never_negative(int take, int expectedTake)
    {
        var f = new Fixture();
        f.Options.Search.OverfetchFactor = 1;
        f.Options.Search.MaxOverfetch = 1000;
        await f.Create().SearchAsync(Q(take: take, skip: -3), CancellationToken.None);

        var active = f.Requests.Single(r => r.Scope.ContentTypes.SequenceEqual(new[] { "PageData" }));
        active.Limit.Should().Be(expectedTake);
        active.Skip.Should().Be(0);
    }

    [Fact]
    public async Task Active_tab_overfetches_capped_and_other_tabs_count_only()
    {
        var f = new Fixture();
        f.Options.Search.OverfetchFactor = 5;
        f.Options.Search.MaxOverfetch = 60;
        await f.Create().SearchAsync(Q(take: 20, skip: 4), CancellationToken.None);

        f.Requests.Should().HaveCount(4);
        var active = f.Requests.Single(r => r.Scope.ContentTypes.SequenceEqual(new[] { "PageData" }));
        active.Limit.Should().Be(60);
        active.Skip.Should().Be(4);
        foreach (var other in f.Requests.Where(r => r != active))
        {
            other.Limit.Should().Be(1);
            other.Skip.Should().Be(0);
            other.IncludeFacets.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Media_tab_does_not_filter_by_locale_and_others_do()
    {
        var f = new Fixture();
        await f.Create().SearchAsync(Q(), CancellationToken.None);
        f.Requests.Single(r => r.Scope.ContentTypes.SequenceEqual(new[] { "MediaData" })).Scope.FilterByLocale.Should().BeFalse();
        f.Requests.Single(r => r.Scope.ContentTypes.SequenceEqual(new[] { "PageData" })).Scope.FilterByLocale.Should().BeTrue();
        f.Requests.Single(r => r.Scope.ContentTypes.SequenceEqual(new[] { "BlockData" })).Scope.FilterByLocale.Should().BeTrue();
    }

    [Fact]
    public async Task Other_tab_excludes_page_block_and_media_types()
    {
        var f = new Fixture();
        await f.Create().SearchAsync(Q(tab: PowerSearchTab.Other), CancellationToken.None);

        var other = f.Requests.Single(r => r.Scope.ContentTypes.Contains("WeirdThing"));
        other.Scope.ContentTypes.Should().BeEquivalentTo(new[] { "WeirdThing" });
    }

    [Fact]
    public async Task Other_tab_with_no_types_does_not_query_graph_for_that_tab()
    {
        var f = new Fixture();
        f.Types.Setup(t => t.List()).Returns(new[] { new ContentType { Name = "ArticlePage", Base = ContentTypeBase.Page } });
        var r = await f.Create().SearchAsync(Q(tab: PowerSearchTab.Other), CancellationToken.None);

        r.State.Should().Be("ok");
        r.Hits.Should().BeEmpty();
        f.Requests.Should().HaveCount(3);   // pages, blocks, media only; an empty scope must never mean "everything"
    }

    [Fact]
    public async Task Hits_are_deduplicated_and_unreadable_or_missing_are_skipped()
    {
        var f = new Fixture();
        var a = f.Content(1, "A");
        var hidden = f.Content(2, "Hidden", readable: false);
        var c = f.Content(3, "C");
        var missing = Guid.NewGuid();
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(4, null, a, hidden, missing, a, c) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Hits.Select(h => h.Name).Should().Equal("A", "C");
        r.Hits[0].EditUrl.Should().Be("/edit/1");
        r.Hits[0].IsDraft.Should().BeFalse();
    }

    private delegate bool TryGetBranch(ContentReference reference, LoaderOptions options, out IContent? content);

    [Fact]
    public async Task Other_language_branches_of_the_same_content_are_kept()
    {
        var f = new Fixture();
        var guid = f.Content(1, "Master");
        IContent Branch(string lang)
        {
            var c = new Mock<IContent>();
            c.SetupGet(x => x.ContentLink).Returns(new ContentReference(1));
            c.SetupGet(x => x.ContentGuid).Returns(guid);
            c.SetupGet(x => x.Name).Returns("Name " + lang);
            var descriptor = new Mock<ISecurityDescriptor>();
            descriptor.Setup(d => d.HasAccess(It.IsAny<IPrincipal>(), It.IsAny<AccessLevel>())).Returns(true);
            c.As<ISecurable>().Setup(s => s.GetSecurityDescriptor()).Returns(descriptor.Object);
            c.As<ILocalizable>().SetupGet(l => l.Language).Returns(System.Globalization.CultureInfo.GetCultureInfo(lang));
            return c.Object;
        }

        var en = Branch("en");
        var sv = Branch("sv");
        f.Loader.Setup(l => l.TryGet<IContent>(It.IsAny<ContentReference>(), It.IsAny<LoaderOptions>(), out It.Ref<IContent?>.IsAny))
            .Returns(new TryGetBranch((ContentReference _, LoaderOptions o, out IContent? c) =>
            {
                c = o.Get<LanguageLoaderOption>()?.Language?.Name == "sv" ? sv : en;
                return true;
            }));
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData")
            ? new GraphSearchResult(new[] { new GraphSearchHit(guid, "n", "en"), new GraphSearchHit(guid, "n", "sv") }, 2, GraphFacets.Empty)
            : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Hits.Select(h => h.Language).Should().Equal("en", "sv");
    }

    [Fact]
    public async Task Cms_loading_is_strictly_sequential()
    {
        var f = new Fixture();
        var guids = Enumerable.Range(1, 8).Select(i => f.Content(i, "n" + i)).ToArray();
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(8, null, guids) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Hits.Should().HaveCount(8);
        f.MaxInFlight.Should().Be(1);
    }

    [Fact]
    public async Task Stops_at_take_and_reports_next_skip()
    {
        var f = new Fixture();
        var guids = Enumerable.Range(1, 5).Select(i => f.Content(i, "n" + i)).ToArray();
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(5, null, guids) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(take: 2, skip: 10), CancellationToken.None);

        r.Hits.Should().HaveCount(2);
        r.NextSkip.Should().Be(12);
    }

    [Fact]
    public async Task Next_skip_is_null_at_end_of_results()
    {
        var f = new Fixture();
        var guids = Enumerable.Range(1, 3).Select(i => f.Content(i, "n" + i)).ToArray();
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(3, null, guids) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(take: 10), CancellationToken.None);

        r.Hits.Should().HaveCount(3);
        r.NextSkip.Should().BeNull();
    }

    [Fact]
    public async Task Next_skip_is_offered_when_total_exceeds_what_was_fetched()
    {
        var f = new Fixture();
        var a = f.Content(1, "A");
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(40, null, a) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(take: 10, skip: 0), CancellationToken.None);

        r.NextSkip.Should().Be(1);
    }

    [Fact]
    public async Task Section_facet_with_count_equal_to_total_is_dropped_others_resolved_by_name()
    {
        var f = new Fixture();
        var shared = f.Content(10, "Start");
        var news = f.Content(11, "News");
        var facets = new GraphFacets(
            Array.Empty<GraphFacetValue>(),
            Array.Empty<GraphFacetValue>(),
            new[]
            {
                new GraphFacetValue(f.ByGuid[shared].ContentGuid.ToString("D"), 12),
                new GraphFacetValue(f.ByGuid[news].ContentGuid.ToString("D"), 5),
                new GraphFacetValue(Guid.NewGuid().ToString("D"), 3)   // unresolvable: skipped
            });
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(12, facets, Array.Empty<Guid>()) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Facets.Sections.Should().ContainSingle();
        r.Facets.Sections[0].Label.Should().Be("News");
        r.Facets.Sections[0].Count.Should().Be(5);
    }

    [Fact]
    public async Task Selected_facet_values_are_retained_with_zero_count_when_graph_omits_them()
    {
        var f = new Fixture();
        var sectionGuid = f.Content(20, "Docs");
        f.Types.Setup(t => t.Load("ArticlePage")).Returns(new ContentType { Name = "ArticlePage", DisplayName = "Article" });
        var section = f.ByGuid[sectionGuid].ContentGuid;

        var r = await f.Create().SearchAsync(
            Q(types: new[] { "ArticlePage" }, langs: new[] { "fr-CH" }, sections: new[] { section }),
            CancellationToken.None);

        var type = r.Facets.Types.Should().ContainSingle().Subject;
        (type.Value, type.Label, type.Count, type.Selected).Should().Be(("ArticlePage", "Article", 0, true));
        var lang = r.Facets.Languages.Should().ContainSingle().Subject;
        (lang.Value, lang.Count, lang.Selected).Should().Be(("fr-CH", 0, true));
        var sec = r.Facets.Sections.Should().ContainSingle().Subject;
        (sec.Label, sec.Count, sec.Selected).Should().Be(("Docs", 0, true));
    }

    [Fact]
    public async Task Language_facet_maps_graph_locale_to_culture_with_native_name_and_unknown_to_code()
    {
        var f = new Fixture();
        var facets = new GraphFacets(
            Array.Empty<GraphFacetValue>(),
            new[] { new GraphFacetValue("fr_CH", 4), new GraphFacetValue("zz_NOPE_1234567890", 1) },
            Array.Empty<GraphFacetValue>());
        f.Returns(r => r.Scope.ContentTypes.Contains("PageData") ? Result(5, facets) : GraphSearchResult.Empty);

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Facets.Languages[0].Value.Should().Be("fr-CH");
        r.Facets.Languages[0].Label.Should().Be(System.Globalization.CultureInfo.GetCultureInfo("fr-CH").NativeName);
        r.Facets.Languages[1].Label.Should().Be("zz-NOPE-1234567890");
    }

    [Fact]
    public async Task Tab_totals_come_from_each_graph_call_and_unknown_total_stays_minus_one()
    {
        var f = new Fixture();
        f.Returns(r =>
        {
            var type = r.Scope.ContentTypes[0];
            return type switch
            {
                "PageData" => Result(9, null),
                "BlockData" => Result(-1, null),
                "MediaData" => Result(3, null),
                _ => Result(0, null)
            };
        });

        var r = await f.Create().SearchAsync(Q(), CancellationToken.None);

        r.Tabs.Single(t => t.Tab == PowerSearchTab.Pages).Total.Should().Be(9);
        r.Tabs.Single(t => t.Tab == PowerSearchTab.Blocks).Total.Should().Be(-1);
        r.Tabs.Single(t => t.Tab == PowerSearchTab.Media).Total.Should().Be(3);
    }

    [Fact]
    public async Task Drafts_are_not_searched_when_skipping()
    {
        var f = new Fixture();
        f.Options.Search.IncludeDrafts = true;
        await f.Create().SearchAsync(Q(skip: 10), CancellationToken.None);
        f.Drafts.Verify(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()), Times.Never);
    }

    [Fact]
    public async Task Drafts_are_not_searched_when_disabled()
    {
        var f = new Fixture();
        f.Options.Search.IncludeDrafts = false;
        await f.Create().SearchAsync(Q(), CancellationToken.None);
        f.Drafts.Verify(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()), Times.Never);
    }

    [Fact]
    public async Task Drafts_of_the_wrong_tab_scope_are_left_out()
    {
        var f = new Fixture();
        f.Options.Search.IncludeDrafts = true;
        // A plain IContent mock is neither PageData nor BlockData nor MediaData: only the Other tab admits it.
        var guid = f.Content(30, "Draft thing");
        var link = f.ByGuid[guid].ContentLink;
        f.Drafts.Setup(d => d.FindDrafts(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<ISet<int>>()))
            .Returns(new[] { link });
        var content = f.ByGuid[guid];
        f.Loader.Setup(l => l.TryGet<IContent>(link, out content)).Returns(true);

        var pages = await f.Create().SearchAsync(Q(tab: PowerSearchTab.Pages), CancellationToken.None);
        pages.Hits.Should().BeEmpty();

        var other = await f.Create().SearchAsync(Q(tab: PowerSearchTab.Other), CancellationToken.None);
        other.Hits.Should().ContainSingle().Which.IsDraft.Should().BeTrue();
    }

    [Fact]
    public void Content_type_base_renders_to_the_names_the_other_tab_relies_on()
    {
        ContentTypeBase.Page.ToString().Should().Be("Page");
        ContentTypeBase.Block.ToString().Should().Be("Block");
        ContentTypeBase.Media.ToString().Should().Be("Media");
        ContentTypeBase.Image.ToString().Should().Be("Image");
        ContentTypeBase.Video.ToString().Should().Be("Video");
    }
}
