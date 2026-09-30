#if OPTIMIZELY_CMS12
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;

namespace EditorPowertools.Tests.Graph.Cms12;

public class Cms12GraphQueryDialectTests
{
    private static readonly Cms12GraphQueryDialect Dialect = new(Options.Create(new EditorPowertoolsGraphOptions()));
    private static GraphSearchRequest Req(string phrase = "bim", GraphSearchFilters? f = null, bool facets = false) =>
        new(phrase, new GraphSearchScope(new[] { "PageData" }, Array.Empty<Guid>(), true), "en-GB",
            f ?? GraphSearchFilters.None, Limit: 10, Skip: 0, IncludeFacets: facets);

    [Fact]
    public void Is_verified() => Dialect.IsVerified.Should().BeTrue();

    [Fact]
    public void Phrase_travels_only_as_variable()
    {
        var r = Dialect.BuildSearch(Req("\"} ) { __schema { types { name } } } #"));
        r.Query.Should().NotContain("__schema");
        r.Variables["phrase"].Should().Be("\"} ) { __schema { types { name } } } #");
    }

    [Fact]
    public void Uses_field_specific_operators()
    {
        var q = Dialect.BuildSearch(Req()).Query;
        q.Should().Contain("Name: { contains: $phrase, boost: 10 }");
        q.Should().Contain("Name: { contains: $phrase, fuzzy: true, boost: 3 }");
        q.Should().Contain("RouteSegment: { like: $pattern, boost: 5 }");
        q.Should().Contain("_fulltext: { match: $phrase }");
        q.Should().NotContain("RouteSegment: { contains");
        q.Should().Contain("orderBy: { _ranking: RELEVANCE }");
    }

    [Theory]
    [InlineData("50%", @"%50\%%")]
    [InlineData("a_b", @"%a\_b%")]
    [InlineData(@"c:\x", @"%c:\\x%")]
    public void Escapes_like_wildcards(string phrase, string pattern) =>
        Dialect.BuildSearch(Req(phrase)).Variables["pattern"].Should().Be(pattern);

    [Fact]
    public void Maps_locale_variable() =>
        Dialect.BuildSearch(Req()).Variables["locale"].Should().BeEquivalentTo(new[] { "en_GB" });

    [Fact]
    public void Without_facets_query_has_no_facets_or_total()
    {
        var q = Dialect.BuildSearch(Req()).Query;
        q.Should().NotContain("facets").And.NotContain("total").And.NotContain("$skip");
    }

    [Fact]
    public void Selected_types_and_sections_are_variables_and_declared_only_when_used()
    {
        var none = Dialect.BuildSearch(Req(facets: true)).Query;
        none.Should().NotContain("$selectedTypes").And.NotContain("$selectedSections").And.NotContain("TypeFacet");

        var g = Guid.NewGuid();
        var r = Dialect.BuildSearch(Req(facets: true, f: new GraphSearchFilters(new[] { "ArticlePage" }, Array.Empty<string>(), new[] { g })));
        r.Query.Should().Contain("$selectedTypes: [String!]").And.Contain("TypeFacet:").And.Contain("SectionFacet:");
        r.Query.Should().NotContain("ArticlePage");
        r.Variables["selectedTypes"].Should().BeEquivalentTo(new[] { "ArticlePage" });
        r.Variables["selectedSections"].Should().BeEquivalentTo(new[] { g.ToString("D") });
    }

    [Fact]
    public void Explicit_language_selection_overrides_scope_locale() =>
        Dialect.BuildSearch(Req(f: new GraphSearchFilters(Array.Empty<string>(), new[] { "de", "fr-CH" }, Array.Empty<Guid>())))
            .Variables["locale"].Should().BeEquivalentTo(new[] { "de", "fr_CH" });

    [Fact]
    public void Scope_without_locale_filter_sends_null_locale() =>
        Dialect.BuildSearch(Req() with { Scope = new GraphSearchScope(new[] { "MediaData" }, Array.Empty<Guid>(), false) })
            .Variables["locale"].Should().BeNull();

    [Fact]
    public void Invalid_scope_type_name_is_rejected()
    {
        var act = () => Dialect.BuildSearch(Req() with { Scope = new GraphSearchScope(new[] { "Page\") {" }, Array.Empty<Guid>(), true) });
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parses_hits_total_and_facets_dropping_zero_counts_and_preferring_aliases()
    {
        var data = JsonDocument.Parse("""
        {"Content":{"total":42,"items":[
            {"Name":"A","ContentLink":{"GuidValue":"6a4b3f3e-7f0e-4a7c-9d0b-0a2c3e4f5a6b"},"Language":{"Name":"en"}},
            {"Name":"broken","ContentLink":{"GuidValue":"not-a-guid"}}],
         "facets":{"ContentType":[{"name":"PageData","count":42},{"name":"Dead","count":0}],
                   "Language":{"Name":[{"name":"en","count":40}]},
                   "Ancestors":[{"name":"11111111-1111-1111-1111-111111111111","count":5}]}},
         "TypeFacet":{"facets":{"ContentType":[{"name":"ArticlePage","count":7}]}}}
        """).RootElement;

        var result = Dialect.ParseSearch(data, includeFacets: true);

        result.Total.Should().Be(42);
        result.Hits.Should().ContainSingle().Which.Name.Should().Be("A");
        result.Facets.ContentTypes.Should().ContainSingle().Which.Should().Be(new GraphFacetValue("ArticlePage", 7));
        result.Facets.Languages.Should().ContainSingle();
        result.Facets.Sections.Should().ContainSingle();
    }

    [Fact]
    public void Missing_total_is_unknown_not_zero() =>
        Dialect.ParseSearch(JsonDocument.Parse("""{"Content":{"items":[]}}""").RootElement, true).Total.Should().Be(-1);
}
#endif
