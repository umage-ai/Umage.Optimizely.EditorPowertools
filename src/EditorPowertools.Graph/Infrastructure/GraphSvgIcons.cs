namespace UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;

/// <summary>
/// Inline SVG path data for Graph add-on tool icons. Each constant is the
/// inner markup of a 24x24 stroked icon (no surrounding <c>&lt;svg&gt;</c> tag).
/// </summary>
internal static class GraphSvgIcons
{
    // Magnifier whose lens holds a funnel - search with facets.
    public const string PowerSearch =
        @"<circle cx=""10"" cy=""10"" r=""6.5""/><line x1=""15"" y1=""15"" x2=""21"" y2=""21""/><polyline points=""7 8 13 8 11 10.5 11 13 9 12 9 10.5 7 8""/>";
}
