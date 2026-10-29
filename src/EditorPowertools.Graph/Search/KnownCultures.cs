using System.Globalization;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>Looks up cultures the runtime actually knows.</summary>
/// <remarks>
/// <see cref="CultureInfo.GetCultureInfo(string)"/> is not a test of that: it throws for an
/// unknown name on Windows, but under ICU (Linux) it invents a culture for any well-formed tag,
/// so <c>zz-NOPE</c> would get a made-up native name instead of falling back to its code.
/// </remarks>
internal static class KnownCultures
{
    // Immutable for the life of the process: the set of cultures the runtime ships with.
    private static readonly Lazy<HashSet<string>> Names = new(() =>
        CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Select(c => c.Name)
            .Where(n => n.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase));

    public static bool TryGet(string? name, out CultureInfo culture)
    {
        culture = CultureInfo.InvariantCulture;
        if (string.IsNullOrWhiteSpace(name) || !Names.Value.Contains(name))
        {
            return false;
        }

        culture = CultureInfo.GetCultureInfo(name);
        return true;
    }
}
