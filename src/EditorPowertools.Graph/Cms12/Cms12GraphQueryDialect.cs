using System.Text.Json;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;

/// <summary>
/// Builds and parses the relevance query used by every Graph search surface (CMS 12 schema).
/// </summary>
/// <remarks>
/// Four clauses matter, each earning its place:
///   - <c>Name contains</c> with a high boost: an editor typing a title expects that page first.
///   - <c>Name contains fuzzy</c> with a low boost: catches typos.
///   - <c>RouteSegment like</c>: editors routinely remember the URL.
///   - <c>_fulltext match</c>: body text, unboosted, so it ranks below a title match.
/// The operator vocabularies differ and the schema rejects the wrong one: Name is a
/// SearchableStringFilterInput (contains/match/fuzzy/boost) while RouteSegment is a plain
/// StringFilterInput with no "contains" - it needs like "%...%".
/// </remarks>
internal sealed class Cms12GraphQueryDialect(IOptions<EditorPowertoolsGraphOptions> options) : IGraphQueryDialect
{
    // Hand-tuned weights, deliberately far apart so a title match cannot be displaced by body text.
    private const int NameBoost = 10;
    private const int RouteSegmentBoost = 5;
    private const int FuzzyNameBoost = 3;


    private GraphSearchOptions Search => options.Value.Search;

    public bool IsVerified => true;

    public string HealthQuery => "query { Content(limit: 1) { total } }";

    public GraphRequest BuildSearch(GraphSearchRequest request)
    {
        GraphQueryText.EnsureTypeNames(request.Scope.ContentTypes, nameof(request));

        var filters = request.Filters ?? GraphSearchFilters.None;

        // No phrase means browsing: no relevance clause, newest first. The phrase variables are then
        // left out entirely - Graph rejects a declared variable the query does not use.
        var browse = string.IsNullOrWhiteSpace(request.Phrase);
        var variables = new Dictionary<string, object?>
        {
            ["limit"] = request.Limit,
            ["locale"] = GraphQueryText.ResolveLocales(request.Scope, request.Language, filters)
        };
        if (!browse)
        {
            variables["phrase"] = request.Phrase;
            variables["pattern"] = GraphQueryText.LikePattern(request.Phrase);
        }

        if (!request.IncludeFacets)
        {
            return new GraphRequest(BuildPlainQuery(request.Scope, browse), variables);
        }

        variables["skip"] = Math.Max(request.Skip, 0);

        // Present only when the query declares them - sending an undeclared variable is an error.
        var selectedTypes = filters.ContentTypes;
        var selectedSections = filters.Sections.Select(g => g.ToString("D")).ToArray();
        if (selectedTypes.Count > 0)
        {
            variables["selectedTypes"] = selectedTypes;
        }

        if (selectedSections.Length > 0)
        {
            variables["selectedSections"] = selectedSections;
        }

        return new GraphRequest(BuildFacetedQuery(request.Scope, browse, selectedTypes, selectedSections), variables);
    }

    public GraphSearchResult ParseSearch(JsonElement data, bool includeFacets) =>
        new(ReadHits(data), GraphQueryText.ReadTotal(data, "Content"), includeFacets ? ReadFacets(data) : GraphFacets.Empty);

    private string BuildPlainQuery(GraphSearchScope scope, bool browse) => $$"""
        query EditorSearch({{PhraseVariables(browse)}}$limit: Int!, $locale: [Locales!]) {
            Content(
                limit: $limit
                locale: $locale
                where: {{BuildWhere(scope, browse, Array.Empty<string>(), Array.Empty<string>())}}
                orderBy: {{OrderBy(browse)}}
            ) {
                items {
                    Name
                    RouteSegment
                    ContentLink { GuidValue }
                    Language { Name }
                }
            }
        }
        """;

    private string BuildFacetedQuery(
        GraphSearchScope scope, bool browse, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
    {
        // A facet group must not narrow itself: once "Article" is selected the content-type facet
        // would contain only "Article" and the editor could not switch type or widen back out.
        // So each selected group gets an aliased query with its own filter lifted and every other
        // filter kept. The aliases share the document (one round trip) and are emitted only when
        // that group has a selection.
        var selectedTypesVariable = selectedTypes.Count == 0 ? string.Empty : ", $selectedTypes: [String!]";
        var selectedSectionsVariable = selectedSections.Count == 0 ? string.Empty : ", $selectedSections: [String!]";

        var typeFacetAlias = selectedTypes.Count == 0
            ? string.Empty
            : $$"""

                TypeFacet: Content(
                    limit: 1
                    locale: $locale
                    where: {{BuildWhere(scope, browse, Array.Empty<string>(), selectedSections)}}
                ) {
                    facets {
                        _concreteType(limit: {{Search.ContentTypeFacetLimit}}) { name count }
                    }
                }
            """.TrimEnd();

        var sectionFacetAlias = selectedSections.Count == 0
            ? string.Empty
            : $$"""

                SectionFacet: Content(
                    limit: 1
                    locale: $locale
                    where: {{BuildWhere(scope, browse, selectedTypes, Array.Empty<string>())}}
                ) {
                    facets {
                        Ancestors(limit: {{Search.SectionFacetLimit}}) { name count }
                    }
                }
            """.TrimEnd();

        // No locale argument: counts every branch, so the language facet keeps offering the
        // branches the editor has not chosen. Language is a ContentLanguageModelFacet whose
        // counts hang off the Name sub-field; asking for them flat fails the whole query.
        var languageFacetAlias = $$"""

                LanguageFacet: Content(
                    limit: 1
                    where: {{BuildWhere(scope, browse, selectedTypes, selectedSections)}}
                ) {
                    facets {
                        Language { Name { name count } }
                    }
                }
            """.TrimEnd();

        return $$"""
            query EditorSearch({{PhraseVariables(browse)}}$limit: Int!, $locale: [Locales!], $skip: Int!{{selectedTypesVariable}}{{selectedSectionsVariable}}) {
                Content(
                    limit: $limit
                    skip: $skip
                    locale: $locale
                    where: {{BuildWhere(scope, browse, selectedTypes, selectedSections)}}
                    orderBy: {{OrderBy(browse)}}
                ) {
                    total
                    items {
                        Name
                        RouteSegment
                        ContentLink { GuidValue }
                        Language { Name }
                    }
                    facets {
                        _concreteType(limit: {{Search.ContentTypeFacetLimit}}) { name count }
                        Language { Name { name count } }
                        Ancestors(limit: {{Search.SectionFacetLimit}}) { name count }
                    }
                }{{typeFacetAlias}}{{sectionFacetAlias}}{{languageFacetAlias}}
            }
            """;
    }

    // Shared by the main query and the facet aliases, which differ only in which facet filters
    // they apply - built once so relevance rules cannot drift between results and counts.
    private static string PhraseVariables(bool browse) => browse ? string.Empty : "$phrase: String!, $pattern: String!, ";

    private static string BuildWhere(
        GraphSearchScope scope, bool browse, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
    {
        // Safe to interpolate: names were validated as identifiers in BuildSearch.
        var types = string.Join(", ", scope.ContentTypes.Select(t => $"\"{t}\""));

        // Ancestors is a list of GUIDs, so the constraint is an OR across the permitted roots.
        // Guid "D" formatting cannot inject.
        var ancestorClause = scope.AncestorGuids.Count == 0
            ? string.Empty
            : "\n{ _or: ["
              + string.Join(", ", scope.AncestorGuids.Select(g => $"{{ Ancestors: {{ eq: \"{g:D}\" }} }}"))
              + "] }";

        // Selections match the concrete type: ContentType also lists every ancestor type, so
        // filtering on it would let "Standard Page" match its subtypes too.
        // Facet selections narrow inside the scope's own types rather than replacing them, and
        // travel as variables: no caller-supplied value belongs in query text.
        var selectedTypeClause = selectedTypes.Count == 0
            ? string.Empty
            : "\n{ _concreteType: { in: $selectedTypes } }";

        var selectedSectionClause = selectedSections.Count == 0
            ? string.Empty
            : "\n{ Ancestors: { in: $selectedSections } }";

        return $$"""
            {
                _and: [
                    { ContentType: { in: [{{types}}] } }
                    { Status: { eq: "Published" } }{{ancestorClause}}{{selectedTypeClause}}{{selectedSectionClause}}{{(browse ? string.Empty : RelevanceClause)}}
                ]
            }
            """;
    }

    private static readonly string RelevanceClause = $$"""

                    { _or: [
                        { Name: { contains: $phrase, boost: {{NameBoost}} } }
                        { Name: { contains: $phrase, fuzzy: true, boost: {{FuzzyNameBoost}} } }
                        { RouteSegment: { like: $pattern, boost: {{RouteSegmentBoost}} } }
                        { _fulltext: { match: $phrase } }
                    ] }
        """.TrimEnd();

    // Browsing has no relevance to rank by: last saved first, the order the CMS lists recent work in.
    private static string OrderBy(bool browse) => browse ? "{ Saved: DESC }" : "{ _ranking: RELEVANCE }";

    private static List<GraphSearchHit> ReadHits(JsonElement data)
    {
        var hits = new List<GraphSearchHit>();

        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("Content", out var content)
            || !content.TryGetProperty("items", out var items)
            || items.ValueKind != JsonValueKind.Array)
        {
            return hits;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("ContentLink", out var link)
                || !link.TryGetProperty("GuidValue", out var guidValue)
                || !Guid.TryParse(guidValue.GetString(), out var contentGuid))
            {
                continue;
            }

            var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var language = item.TryGetProperty("Language", out var lang) && lang.TryGetProperty("Name", out var ln)
                ? ln.GetString() ?? string.Empty
                : string.Empty;

            hits.Add(new GraphSearchHit(contentGuid, name, language));
        }

        return hits;
    }

    private static GraphFacets ReadFacets(JsonElement data)
    {
        if (!GraphQueryText.TryGet(data, out var facets, "Content", "facets"))
        {
            return GraphFacets.Empty;
        }

        // Prefer the alias where one was asked for: it carries that group's counts with its own
        // filter lifted, so a selected value does not erase the alternatives beside it.
        var contentTypes = GraphQueryText.ReadFacet(data, "TypeFacet", "facets", "_concreteType")
            ?? GraphQueryText.ReadFacet(facets, "_concreteType") ?? Array.Empty<GraphFacetValue>();
        var languages = GraphQueryText.ReadFacet(data, "LanguageFacet", "facets", "Language", "Name")
            ?? GraphQueryText.ReadFacet(facets, "Language", "Name") ?? Array.Empty<GraphFacetValue>();
        var sections = GraphQueryText.ReadFacet(data, "SectionFacet", "facets", "Ancestors")
            ?? GraphQueryText.ReadFacet(facets, "Ancestors") ?? Array.Empty<GraphFacetValue>();

        return new GraphFacets(contentTypes, languages, sections);
    }
}
