namespace UmageAI.Optimizely.EditorPowerTools.Graph.Search;

/// <summary>
/// Maps CMS culture names to Graph locale enum values.
/// </summary>
/// <remarks>
/// Region-qualified names map by rule <c>xx-YY</c> to <c>xx_YY</c> and are never truncated to
/// <c>xx</c>: that would silently search another market (France instead of Switzerland).
/// </remarks>
public static class GraphLocaleMapper
{
    public static string? ToGraphLocale(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return null;
        }

        var parts = cultureName.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        parts[0] = parts[0].ToLowerInvariant();
        for (var i = 1; i < parts.Length; i++)
        {
            // Two-letter subtags are regions (upper-case); longer ones are scripts (Hans) and keep their case.
            if (parts[i].Length == 2)
            {
                parts[i] = parts[i].ToUpperInvariant();
            }
        }

        return string.Join('_', parts);
    }
}
