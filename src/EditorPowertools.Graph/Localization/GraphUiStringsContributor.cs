using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Localization;

/// <summary>Publishes the PowerSearch JS strings (lang: <c>ui/powersearch</c>).</summary>
internal sealed class GraphUiStringsContributor : IUiStringsContributor
{
    public string Section => "powersearch";

    public IEnumerable<string> Keys { get; } = new[]
    {
        "placeholder",
        "search",
        "tab_pages",
        "tab_blocks",
        "tab_media",
        "tab_other",
        "facet_type",
        "facet_language",
        "facet_section",
        "show_more",
        "show_less",
        "clear_filters",
        "load_more",
        "no_results",
        "browsing",
        "unavailable",
        "unavailable_link",
        "error",
        "index_lag",
        "draft",
        "by",
        "results_count",
    };
}
