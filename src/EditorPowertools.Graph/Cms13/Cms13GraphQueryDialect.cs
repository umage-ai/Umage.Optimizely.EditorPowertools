using System.Text.Json;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;

/// <summary>
/// Builds and parses the relevance query used by every Graph search surface (CMS 13 schema).
/// </summary>
/// <remarks>
/// CMS 13 syncs to the schema shared with SaaS CMS: the root is <c>_Content</c>, system fields
/// live under <c>_metadata</c> (key, displayName, locale, types, status, path, url) and the
/// instance's own type under <c>_itemMetadata.type</c>. Keys are dash-less GUIDs and the CMS base
/// types appear as <c>_Page</c>, <c>_Component</c>, <c>_Media</c> and <c>_Folder</c>.
/// The relevance rules mirror the CMS 12 dialect: a boosted title match, a fuzzy title match, a
/// URL match (there is no route-segment filter, so the default URL stands in) and body text.
/// </remarks>
internal sealed class Cms13GraphQueryDialect(IOptions<EditorPowertoolsGraphOptions> options) : IGraphQueryDialect
{
    // Hand-tuned weights, deliberately far apart so a title match cannot be displaced by body text.
    private const int NameBoost = 10;
    private const int UrlBoost = 5;
    private const int FuzzyNameBoost = 3;

    private static readonly Dictionary<string, string> BaseTypes = new(StringComparer.Ordinal)
    {
        ["PageData"] = "_Page",
        ["BlockData"] = "_Component",
        ["MediaData"] = "_Media",
        ["ContentFolder"] = "_Folder"
    };

    private GraphSearchOptions Search => options.Value.Search;

    public bool IsVerified => true;

    public string HealthQuery => "query { _Content(limit: 1) { total } }";

    public GraphRequest BuildSearch(GraphSearchRequest request)
    {
        GraphQueryText.EnsureTypeNames(request.Scope.ContentTypes, nameof(request));

        var filters = request.Filters ?? GraphSearchFilters.None;

        var variables = new Dictionary<string, object?>
        {
            ["phrase"] = request.Phrase,
            ["pattern"] = GraphQueryText.LikePattern(request.Phrase),
            ["limit"] = request.Limit,
            ["locale"] = GraphQueryText.ResolveLocales(request.Scope, request.Language, filters)
        };

        if (!request.IncludeFacets)
        {
            return new GraphRequest(BuildPlainQuery(request.Scope), variables);
        }

        variables["skip"] = Math.Max(request.Skip, 0);

        // Present only when the query declares them - sending an undeclared variable is an error.
        var selectedTypes = filters.ContentTypes;
        var selectedSections = filters.Sections.Select(g => g.ToString("N")).ToArray();
        if (selectedTypes.Count > 0)
        {
            variables["selectedTypes"] = selectedTypes;
        }

        if (selectedSections.Length > 0)
        {
            variables["selectedSections"] = selectedSections;
        }

        return new GraphRequest(BuildFacetedQuery(request.Scope, selectedTypes, selectedSections), variables);
    }

    public GraphSearchResult ParseSearch(JsonElement data, bool includeFacets)
    {
        var hits = ReadHits(data);
        return new(hits, GraphQueryText.ReadTotal(data, "_Content"), includeFacets ? ReadFacets(data, hits) : GraphFacets.Empty);
    }

    private string BuildPlainQuery(GraphSearchScope scope) => $$"""
        query EditorSearch($phrase: String!, $pattern: String!, $limit: Int!, $locale: [Locales]) {
            _Content(
                limit: $limit
                locale: $locale
                where: {{BuildWhere(scope, Array.Empty<string>(), Array.Empty<string>())}}
                orderBy: { _ranking: RELEVANCE }
            ) {
                items {
                    _metadata { key displayName locale }
                }
            }
        }
        """;

    private string BuildFacetedQuery(
        GraphSearchScope scope, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
    {
        // A facet group must not narrow itself: each selected group gets an aliased query with its
        // own filter lifted and every other filter kept (see the CMS 12 dialect for the rationale).
        var selectedTypesVariable = selectedTypes.Count == 0 ? string.Empty : ", $selectedTypes: [String!]";
        var selectedSectionsVariable = selectedSections.Count == 0 ? string.Empty : ", $selectedSections: [String!]";

        var typeFacetAlias = selectedTypes.Count == 0
            ? string.Empty
            : $$"""

                TypeFacet: _Content(
                    limit: 1
                    locale: $locale
                    where: {{BuildWhere(scope, Array.Empty<string>(), selectedSections)}}
                ) {
                    facets {
                        _itemMetadata { type(limit: {{Search.ContentTypeFacetLimit}}) { name count } }
                    }
                }
            """.TrimEnd();

        var sectionFacetAlias = selectedSections.Count == 0
            ? string.Empty
            : $$"""

                SectionFacet: _Content(
                    limit: 1
                    locale: $locale
                    where: {{BuildWhere(scope, selectedTypes, Array.Empty<string>())}}
                ) {
                    facets {
                        _metadata { path(limit: {{Search.SectionFacetLimit}}) { name count } }
                    }
                }
            """.TrimEnd();

        // No locale argument: counts every branch, so the language facet keeps offering the
        // branches the editor has not chosen.
        var languageFacetAlias = $$"""

                LanguageFacet: _Content(
                    limit: 1
                    where: {{BuildWhere(scope, selectedTypes, selectedSections)}}
                ) {
                    facets {
                        _metadata { locale { name count } }
                    }
                }
            """.TrimEnd();

        return $$"""
            query EditorSearch($phrase: String!, $pattern: String!, $limit: Int!, $locale: [Locales], $skip: Int!{{selectedTypesVariable}}{{selectedSectionsVariable}}) {
                _Content(
                    limit: $limit
                    skip: $skip
                    locale: $locale
                    where: {{BuildWhere(scope, selectedTypes, selectedSections)}}
                    orderBy: { _ranking: RELEVANCE }
                ) {
                    total
                    items {
                        _metadata { key displayName locale }
                    }
                    facets {
                        _itemMetadata { type(limit: {{Search.ContentTypeFacetLimit}}) { name count } }
                        _metadata {
                            locale { name count }
                            path(limit: {{Search.SectionFacetLimit}}) { name count }
                        }
                    }
                }{{typeFacetAlias}}{{sectionFacetAlias}}{{languageFacetAlias}}
            }
            """;
    }

    // Shared by the main query and the facet aliases so relevance rules cannot drift between
    // results and counts.
    private static string BuildWhere(
        GraphSearchScope scope, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
    {
        // Safe to interpolate: names were validated as identifiers in BuildSearch.
        var types = string.Join(", ", scope.ContentTypes
            .Select(t => BaseTypes.TryGetValue(t, out var mapped) ? mapped : t)
            .Distinct(StringComparer.Ordinal)
            .Select(t => $"\"{t}\""));

        // path lists every ancestor key (and the item itself). Guid "N" formatting cannot inject.
        var ancestorClause = scope.AncestorGuids.Count == 0
            ? string.Empty
            : "\n{ _or: ["
              + string.Join(", ", scope.AncestorGuids.Select(g => $"{{ _metadata: {{ path: {{ eq: \"{g:N}\" }} }} }}"))
              + "] }";

        // _metadata.types lists every ancestor type, so selections match the instance's own type.
        var selectedTypeClause = selectedTypes.Count == 0
            ? string.Empty
            : "\n{ _itemMetadata: { type: { in: $selectedTypes } } }";

        var selectedSectionClause = selectedSections.Count == 0
            ? string.Empty
            : "\n{ _metadata: { path: { in: $selectedSections } } }";

        // Published only: drafts come from the version repository, which applies CMS access rules.
        return $$"""
            {
                _and: [
                    { _metadata: { types: { in: [{{types}}] } } }
                    { _metadata: { status: { eq: "Published" } } }{{ancestorClause}}{{selectedTypeClause}}{{selectedSectionClause}}
                    { _or: [
                        { _metadata: { displayName: { contains: $phrase, boost: {{NameBoost}} } } }
                        { _metadata: { displayName: { contains: $phrase, fuzzy: true, boost: {{FuzzyNameBoost}} } } }
                        { _metadata: { url: { default: { like: $pattern, boost: {{UrlBoost}} } } } }
                        { _fulltext: { match: $phrase } }
                    ] }
                ]
            }
            """;
    }

    private static List<GraphSearchHit> ReadHits(JsonElement data)
    {
        var hits = new List<GraphSearchHit>();
        if (!GraphQueryText.TryGet(data, out var items, "_Content", "items") || items.ValueKind != JsonValueKind.Array)
        {
            return hits;
        }

        foreach (var item in items.EnumerateArray())
        {
            if (!GraphQueryText.TryGet(item, out var metadata, "_metadata")
                || !metadata.TryGetProperty("key", out var key)
                || !Guid.TryParse(key.GetString(), out var contentGuid))
            {
                continue;
            }

            var name = metadata.TryGetProperty("displayName", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var language = metadata.TryGetProperty("locale", out var l) ? l.GetString() ?? string.Empty : string.Empty;
            hits.Add(new GraphSearchHit(contentGuid, name, language));
        }

        return hits;
    }

    private static GraphFacets ReadFacets(JsonElement data, IReadOnlyList<GraphSearchHit> hits)
    {
        if (!GraphQueryText.TryGet(data, out var facets, "_Content", "facets"))
        {
            return GraphFacets.Empty;
        }

        // Prefer the alias where one was asked for: it carries that group's counts with its own
        // filter lifted, so a selected value does not erase the alternatives beside it.
        var contentTypes = GraphQueryText.ReadFacet(data, "TypeFacet", "facets", "_itemMetadata", "type")
            ?? GraphQueryText.ReadFacet(facets, "_itemMetadata", "type") ?? Array.Empty<GraphFacetValue>();
        var languages = GraphQueryText.ReadFacet(data, "LanguageFacet", "facets", "_metadata", "locale")
            ?? GraphQueryText.ReadFacet(facets, "_metadata", "locale") ?? Array.Empty<GraphFacetValue>();
        var sections = GraphQueryText.ReadFacet(data, "SectionFacet", "facets", "_metadata", "path")
            ?? GraphQueryText.ReadFacet(facets, "_metadata", "path") ?? Array.Empty<GraphFacetValue>();

        // Unlike CMS 12's Ancestors, path ends with the item's own key; a hit is not a section of itself.
        var own = hits.Select(h => h.ContentGuid).ToHashSet();
        sections = sections.Where(s => !Guid.TryParse(s.Name, out var g) || !own.Contains(g)).ToList();

        return new GraphFacets(contentTypes, languages, sections);
    }
}
