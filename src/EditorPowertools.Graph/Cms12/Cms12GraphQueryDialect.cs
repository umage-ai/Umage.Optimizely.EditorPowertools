using System.Text.Json;
using System.Text.RegularExpressions;
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
internal sealed partial class Cms12GraphQueryDialect(IOptions<EditorPowertoolsGraphOptions> options) : IGraphQueryDialect
{
    // Hand-tuned weights, deliberately far apart so a title match cannot be displaced by body text.
    private const int NameBoost = 10;
    private const int RouteSegmentBoost = 5;
    private const int FuzzyNameBoost = 3;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex TypeNamePattern();

    private GraphSearchOptions Search => options.Value.Search;

    public bool IsVerified => true;

    public GraphRequest BuildSearch(GraphSearchRequest request)
    {
        // Scope type names are interpolated into the query text. That is only safe for plain
        // identifiers, so refuse anything else rather than trusting every caller to validate.
        foreach (var type in request.Scope.ContentTypes)
        {
            if (!TypeNamePattern().IsMatch(type))
            {
                throw new ArgumentException("Invalid content type name in search scope.", nameof(request));
            }
        }

        var filters = request.Filters ?? GraphSearchFilters.None;

        var variables = new Dictionary<string, object?>
        {
            ["phrase"] = request.Phrase,
            ["pattern"] = BuildRouteSegmentPattern(request.Phrase),
            ["limit"] = request.Limit,
            ["locale"] = ResolveLocales(request.Scope, request.Language, filters)
        };

        if (!request.IncludeFacets)
        {
            return new GraphRequest(BuildPlainQuery(request.Scope), variables);
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

        return new GraphRequest(BuildFacetedQuery(request.Scope, selectedTypes, selectedSections), variables);
    }

    public GraphSearchResult ParseSearch(JsonElement data, bool includeFacets) =>
        new(ReadHits(data), ReadTotal(data), includeFacets ? ReadFacets(data) : GraphFacets.Empty);

    // An explicit language choice wins over the scope's locale rule. Otherwise picking "de" in a
    // page search would be intersected with the editor's branch and return nothing.
    private static string[]? ResolveLocales(GraphSearchScope scope, string? language, GraphSearchFilters filters)
    {
        if (filters.Languages.Count > 0)
        {
            var mapped = filters.Languages
                .Select(GraphLocaleMapper.ToGraphLocale)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (mapped.Length > 0)
            {
                return mapped;
            }
        }

        if (scope.FilterByLocale && GraphLocaleMapper.ToGraphLocale(language) is { } locale)
        {
            return new[] { locale };
        }

        return null;
    }

    // The phrase comes straight from a search box and "like" treats % and _ as wildcards, so
    // neutralise them with a backslash (the convention Graph's like accepts). This is about
    // relevance, not injection: the phrase itself only ever travels as a variable.
    private static string BuildRouteSegmentPattern(string phrase)
    {
        var escaped = phrase
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    private string BuildPlainQuery(GraphSearchScope scope) => $$"""
        query EditorSearch($phrase: String!, $pattern: String!, $limit: Int!, $locale: [Locales!]) {
            Content(
                limit: $limit
                locale: $locale
                where: {{BuildWhere(scope, Array.Empty<string>(), Array.Empty<string>())}}
                orderBy: { _ranking: RELEVANCE }
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
        GraphSearchScope scope, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
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
                    where: {{BuildWhere(scope, Array.Empty<string>(), selectedSections)}}
                ) {
                    facets {
                        ContentType(limit: {{Search.ContentTypeFacetLimit}}) { name count }
                    }
                }
            """.TrimEnd();

        var sectionFacetAlias = selectedSections.Count == 0
            ? string.Empty
            : $$"""

                SectionFacet: Content(
                    limit: 1
                    locale: $locale
                    where: {{BuildWhere(scope, selectedTypes, Array.Empty<string>())}}
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
                    where: {{BuildWhere(scope, selectedTypes, selectedSections)}}
                ) {
                    facets {
                        Language { Name { name count } }
                    }
                }
            """.TrimEnd();

        return $$"""
            query EditorSearch($phrase: String!, $pattern: String!, $limit: Int!, $locale: [Locales!], $skip: Int!{{selectedTypesVariable}}{{selectedSectionsVariable}}) {
                Content(
                    limit: $limit
                    skip: $skip
                    locale: $locale
                    where: {{BuildWhere(scope, selectedTypes, selectedSections)}}
                    orderBy: { _ranking: RELEVANCE }
                ) {
                    total
                    items {
                        Name
                        RouteSegment
                        ContentLink { GuidValue }
                        Language { Name }
                    }
                    facets {
                        ContentType(limit: {{Search.ContentTypeFacetLimit}}) { name count }
                        Language { Name { name count } }
                        Ancestors(limit: {{Search.SectionFacetLimit}}) { name count }
                    }
                }{{typeFacetAlias}}{{sectionFacetAlias}}{{languageFacetAlias}}
            }
            """;
    }

    // Shared by the main query and the facet aliases, which differ only in which facet filters
    // they apply - built once so relevance rules cannot drift between results and counts.
    private static string BuildWhere(
        GraphSearchScope scope, IReadOnlyList<string> selectedTypes, IReadOnlyList<string> selectedSections)
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

        // Facet selections narrow inside the scope's own types rather than replacing them, and
        // travel as variables: no caller-supplied value belongs in query text.
        var selectedTypeClause = selectedTypes.Count == 0
            ? string.Empty
            : "\n{ ContentType: { in: $selectedTypes } }";

        var selectedSectionClause = selectedSections.Count == 0
            ? string.Empty
            : "\n{ Ancestors: { in: $selectedSections } }";

        return $$"""
            {
                _and: [
                    { ContentType: { in: [{{types}}] } }{{ancestorClause}}{{selectedTypeClause}}{{selectedSectionClause}}
                    { _or: [
                        { Name: { contains: $phrase, boost: {{NameBoost}} } }
                        { Name: { contains: $phrase, fuzzy: true, boost: {{FuzzyNameBoost}} } }
                        { RouteSegment: { like: $pattern, boost: {{RouteSegmentBoost}} } }
                        { _fulltext: { match: $phrase } }
                    ] }
                ]
            }
            """;
    }

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

    // -1 means "Graph did not say" - unknown, not zero.
    private static int ReadTotal(JsonElement data) =>
        data.ValueKind == JsonValueKind.Object
        && data.TryGetProperty("Content", out var content)
        && content.TryGetProperty("total", out var total)
        && total.TryGetInt32(out var value)
            ? value
            : -1;

    private static GraphFacets ReadFacets(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("Content", out var content)
            || !content.TryGetProperty("facets", out var facets))
        {
            return GraphFacets.Empty;
        }

        // Prefer the alias where one was asked for: it carries that group's counts with its own
        // filter lifted, so a selected value does not erase the alternatives beside it.
        var contentTypes = ReadAliasFacet(data, "TypeFacet", "ContentType") ?? ReadFacet(facets, "ContentType");
        var languages = ReadAliasFacet(data, "LanguageFacet", "Language", "Name") ?? ReadFacet(facets, "Language", "Name");
        var sections = ReadAliasFacet(data, "SectionFacet", "Ancestors") ?? ReadFacet(facets, "Ancestors");

        return new GraphFacets(contentTypes, languages, sections);
    }

    private static IReadOnlyList<GraphFacetValue>? ReadAliasFacet(
        JsonElement data, string alias, string name, string? subField = null)
    {
        if (!data.TryGetProperty(alias, out var aliased)
            || aliased.ValueKind != JsonValueKind.Object
            || !aliased.TryGetProperty("facets", out var facets))
        {
            return null;
        }

        return ReadFacet(facets, name, subField);
    }

    private static IReadOnlyList<GraphFacetValue> ReadFacet(JsonElement facets, string name, string? subField = null)
    {
        if (facets.ValueKind != JsonValueKind.Object || !facets.TryGetProperty(name, out var values))
        {
            return Array.Empty<GraphFacetValue>();
        }

        if (subField is not null && (values.ValueKind != JsonValueKind.Object || !values.TryGetProperty(subField, out values)))
        {
            return Array.Empty<GraphFacetValue>();
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<GraphFacetValue>();
        }

        var results = new List<GraphFacetValue>();
        foreach (var value in values.EnumerateArray())
        {
            if (!value.TryGetProperty("name", out var facetName)
                || facetName.ValueKind != JsonValueKind.String
                || facetName.GetString() is not { Length: > 0 } text)
            {
                continue;
            }

            var count = value.TryGetProperty("count", out var c) && c.TryGetInt32(out var parsed) ? parsed : 0;

            // A zero-count value is a dead end: offering it means offering a click that empties the page.
            if (count > 0)
            {
                results.Add(new GraphFacetValue(text, count));
            }
        }

        return results;
    }
}
