using UmageAI.Optimizely.EditorPowerTools.Localization;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Localization;

/// <summary>Publishes the Forms Overview JS strings (lang: <c>ui/formsoverview</c>).</summary>
internal sealed class FormsUiStringsContributor : IUiStringsContributor
{
    public string Section => "formsoverview";

    public IEnumerable<string> Keys { get; } = new[]
    {
        "forms_label",
        "filter_placeholder",
        "retention_all",
        "retention_default",
        "retention_default_short",
        "retention_custom",
        "retention_custom_short",
        "handler_all",
        "handler_with",
        "handler_without",
        "handler_email_tip",
        "handler_webhook_tip",
        "handler_none",
        "usage_only",
        "lang_all",
        "lang_filter_tip",
        "empty",
        "error_load",
        "col_name",
        "col_fields",
        "col_submissions",
        "col_last",
        "col_usage",
        "col_handlers",
        "col_retention",
        "no_submissions",
        "unused",
        "more",
        "retention_partial",
        "retention_final",
        "btn_edit",
        "btn_usage",
        "tip_open",
        "tip_edit",
        "tip_usage",
        "risk_only",
        "flag_privacy",
        "flag_privacy_live",
        "flag_privacy_tip",
        "flag_privacy_live_tip",
        "flag_pii_fields",
        "flag_duplicate",
        "flag_duplicate_tip",
    };
}
