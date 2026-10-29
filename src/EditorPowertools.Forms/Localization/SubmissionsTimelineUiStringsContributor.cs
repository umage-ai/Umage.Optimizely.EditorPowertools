using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Localization;

/// <summary>Publishes the Submissions Timeline JS strings (lang: <c>ui/submissionstimeline</c>).</summary>
internal sealed class SubmissionsTimelineUiStringsContributor : IUiStringsContributor
{
    public string Section => "submissionstimeline";

    public IEnumerable<string> Keys { get; } = new[]
    {
        "events",
        "range_7",
        "range_30",
        "range_90",
        "range_365",
        "filter_placeholder",
        "form_filter_tip",
        "all_forms",
        "lang_all",
        "lang_filter_tip",
        "finalized_only",
        "empty",
        "error_load",
        "finalized",
        "partial",
        "btn_details",
        "btn_view",
        "tip_details",
        "tip_view",
        "loading_details",
        "no_data",
        "live",
        "live_tip",
        "submission_id",
        "meta_when",
        "meta_user",
        "meta_language",
        "meta_page",
        "anonymous",
    };
}
