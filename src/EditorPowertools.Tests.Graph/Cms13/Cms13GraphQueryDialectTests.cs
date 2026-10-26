#if OPTIMIZELY_CMS13
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;

namespace EditorPowertools.Tests.Graph.Cms13;

/// <summary>
/// The CMS 13 schema (shared with SaaS) nests everything under <c>_metadata</c> and
/// <c>_itemMetadata</c>. These shapes were verified against a live CMS 13 Graph instance.
/// </summary>
public class Cms13GraphQueryDialectTests
{
    private static readonly Cms13GraphQueryDialect Dialect = new(Options.Create(new EditorPowertoolsGraphOptions()));
    private static GraphSearchRequest Req(string phrase = "bim", GraphSearchFilters? f = null, bool facets = false, params string[] types) =>
        new(phrase, new GraphSearchScope(types.Length == 0 ? new[] { "PageData" } : types, Array.Empty<Guid>(), true), "en-GB",
            f ?? GraphSearchFilters.None, Limit: 10, Skip: 0, IncludeFacets: facets);

    [Fact]
    public void Is_verified() => Dialect.IsVerified.Should().BeTrue();

    [Fact]
    public void Health_query_uses_the_cms13_root() => Dialect.HealthQuery.Should().Contain("_Content(limit: 1)");

    [Fact]
    public void Phrase_travels_only_as_variable()
    {
        var r = Dialect.BuildSearch(Req("\"} ) { __schema { types { name } } } #"));
        r.Query.Should().NotContain("__schema");
        r.Variables["phrase"].Should().Be("\"} ) { __schema { types { name } } } #");
    }

    [Fact]
    public void Queries_the_content_root_with_metadata_operators()
    {
        var q = Dialect.BuildSearch(Req()).Query;
        q.Should().Contain("_Content(");
        q.Should().Contain("_metadata: { displayName: { contains: $phrase, boost: 10 } }");
        q.Should().Contain("_metadata: { displayName: { contains: $phrase, fuzzy: true, boost: 3 } }");
        q.Should().Contain("_metadata: { url: { default: { like: $pattern, boost: 5 } } }");
        q.Should().Contain("_fulltext: { match: $phrase }");
        q.Should().Contain("_metadata: { status: { eq: \"Published\" } }");
        q.Should().Contain("orderBy: { _ranking: RELEVANCE }");
        q.Should().Contain("$locale: [Locales]");
    }

    [Theory]
    [InlineData("PageData", "_Page")]
    [InlineData("BlockData", "_Component")]
    [InlineData("MediaData", "_Media")]
    [InlineData("ContentFolder", "_Folder")]
    [InlineData("ArticlePage", "ArticlePage")]
    public void Maps_cms_base_types_to_graph_base_types(string cmsType, string graphType) =>
        Dialect.BuildSearch(Req(types: cmsType)).Query
            .Should().Contain($"_metadata: {{ types: {{ in: [\"{graphType}\"] }} }}");

    [Fact]
    public void Ancestor_scope_and_selected_sections_use_dashless_keys()
    {
        var root = Guid.NewGuid();
        var section = Guid.NewGuid();
        var r = Dialect.BuildSearch(Req(facets: true, f: new GraphSearchFilters(Array.Empty<string>(), Array.Empty<string>(), new[] { section }))
            with { Scope = new GraphSearchScope(new[] { "PageData" }, new[] { root }, true) });
        r.Query.Should().Contain($"_metadata: {{ path: {{ eq: \"{root:N}\" }} }}");
        r.Query.Should().Contain("_metadata: { path: { in: $selectedSections } }");
        r.Variables["selectedSections"].Should().BeEquivalentTo(new[] { section.ToString("N") });
    }

    [Fact]
    public void Type_facet_and_filter_use_the_concrete_type()
    {
        var r = Dialect.BuildSearch(Req(facets: true, f: new GraphSearchFilters(new[] { "ArticlePage" }, Array.Empty<string>(), Array.Empty<Guid>())));
        r.Query.Should().Contain("_itemMetadata: { type: { in: $selectedTypes } }");
        r.Query.Should().Contain("_itemMetadata { type(limit:");
        r.Query.Should().Contain("TypeFacet: _Content(");
        r.Query.Should().NotContain("ArticlePage");
        r.Variables["selectedTypes"].Should().BeEquivalentTo(new[] { "ArticlePage" });
    }

    [Fact]
    public void Undeclared_variables_are_never_sent()
    {
        // Graph rejects a declared-but-unused variable, so selection variables exist only with a selection.
        var r = Dialect.BuildSearch(Req(facets: true));
        r.Query.Should().NotContain("$selectedTypes").And.NotContain("$selectedSections");
        r.Variables.Should().NotContainKey("selectedTypes").And.NotContainKey("selectedSections");
        Dialect.BuildSearch(Req()).Query.Should().NotContain("$skip").And.NotContain("facets");
    }

    [Fact]
    public void Invalid_scope_type_name_is_rejected()
    {
        var act = () => Dialect.BuildSearch(Req(types: "Page\") {"));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parses_hits_total_and_facets_preferring_aliases_and_dropping_self_paths()
    {
        var hit = Guid.Parse("f9106a54-92e7-49cd-851b-6c330a933fa5");
        var data = JsonDocument.Parse("""
        {"_Content":{"total":42,"items":[
            {"_metadata":{"key":"HITKEY","displayName":"A","locale":"sv"}},
            {"_metadata":{"key":"not-a-guid","displayName":"broken"}}],
         "facets":{"_itemMetadata":{"type":[{"name":"Main","count":1}]},
                   "_metadata":{"locale":[{"name":"en","count":40},{"name":"","count":2}],
                                "path":[{"name":"7b08c7d5758547e5add75822da68c3ce","count":5},{"name":"HITKEY","count":1}]}}},
         "TypeFacet":{"facets":{"_itemMetadata":{"type":[{"name":"ArticlePage","count":7},{"name":"Dead","count":0}]}}}}
        """.Replace("HITKEY", hit.ToString("N"))).RootElement;

        var result = Dialect.ParseSearch(data, includeFacets: true);

        result.Total.Should().Be(42);
        result.Hits.Should().ContainSingle().Which.Should().Be(new GraphSearchHit(hit, "A", "sv"));
        result.Facets.ContentTypes.Should().Equal(new GraphFacetValue("ArticlePage", 7));
        result.Facets.Languages.Should().Equal(new GraphFacetValue("en", 40));
        // A path lists the item itself last; the hit's own key is not a section.
        result.Facets.Sections.Should().Equal(new GraphFacetValue("7b08c7d5758547e5add75822da68c3ce", 5));
    }

    [Fact]
    public void Parse_without_facets_leaves_facets_empty()
    {
        var data = JsonDocument.Parse("""{"_Content":{"items":[]}}""").RootElement;
        var result = Dialect.ParseSearch(data, includeFacets: false);
        result.Total.Should().Be(-1);
        result.Facets.Should().Be(GraphFacets.Empty);
    }
}
#endif
