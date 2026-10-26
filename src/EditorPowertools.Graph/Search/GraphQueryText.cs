using System.Text.Json;
using System.Text.RegularExpressions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>Schema-independent pieces shared by the CMS 12 and CMS 13 query dialects.</summary>
internal static partial class GraphQueryText
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex TypeNamePattern();

    /// <summary>
    /// Scope type names are interpolated into the query text. That is only safe for plain
    /// identifiers, so refuse anything else rather than trusting every caller to validate.
    /// </summary>
    public static void EnsureTypeNames(IEnumerable<string> names, string paramName)
    {
        foreach (var name in names)
        {
            if (!TypeNamePattern().IsMatch(name))
            {
                throw new ArgumentException("Invalid content type name in search scope.", paramName);
            }
        }
    }

    // An explicit language choice wins over the scope's locale rule. Otherwise picking "de" in a
    // page search would be intersected with the editor's branch and return nothing.
    public static string[]? ResolveLocales(GraphSearchScope scope, string? language, GraphSearchFilters filters)
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
    public static string LikePattern(string phrase)
    {
        var escaped = phrase
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }

    /// <summary>Follows <paramref name="path"/> from <paramref name="element"/>; false when any step is missing.</summary>
    public static bool TryGet(JsonElement element, out JsonElement result, params string[] path)
    {
        result = element;
        foreach (var step in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(step, out result))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Reads a <c>[{ name, count }]</c> facet array at <paramref name="path"/>, or null when absent.</summary>
    public static IReadOnlyList<GraphFacetValue>? ReadFacet(JsonElement element, params string[] path)
    {
        if (!TryGet(element, out var values, path))
        {
            return null;
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

    // -1 means "Graph did not say" - unknown, not zero.
    public static int ReadTotal(JsonElement data, string root) =>
        TryGet(data, out var total, root, "total") && total.TryGetInt32(out var value) ? value : -1;
}
