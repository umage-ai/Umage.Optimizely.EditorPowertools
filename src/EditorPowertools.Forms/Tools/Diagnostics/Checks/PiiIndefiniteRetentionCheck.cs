using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;

/// <summary>
/// Flags forms that capture PII-shaped fields (email, name, phone, address,
/// file uploads, etc.) AND store submission data on the default retention
/// policy — i.e. effectively forever. Common GDPR / privacy-program concern
/// that's easy to miss when standing up a form.
/// </summary>
public class PiiIndefiniteRetentionCheck : DoctorCheckBase
{
    private readonly FormsAggregationService _forms;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/piiindefiniteretentioncheck/";

    public PiiIndefiniteRetentionCheck(FormsAggregationService forms)
    {
        _forms = forms;
    }

    public override string Name => L(Prefix + "name", "PII Stored Indefinitely");
    public override string Description => L(Prefix + "description", "Finds forms that capture personal data on the default (effectively indefinite) retention policy.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/forms", "Forms");
    public override int SortOrder => 30;
    public override string[] Tags => new[] { "Forms", "Privacy", "GDPR" };

    public override DoctorCheckResult PerformCheck()
    {
        var analyses = _forms.AnalyzePii();
        var risky = analyses
            .Where(a => a.PiiFieldLabels.Count > 0 && a.UsesDefaultRetention && a.StoresSubmissionData)
            .ToList();

        if (risky.Count == 0)
            return Ok(L(Prefix + "ok", "No forms with PII-shaped fields are stored on the default retention policy."));

        var bullets = risky.Take(5).Select(a =>
            $"\"{a.FormName}\" → {string.Join(", ", a.PiiFieldLabels.Take(4))}{(a.PiiFieldLabels.Count > 4 ? "…" : string.Empty)}");
        var details = string.Format(L(Prefix + "details", "{0}"), string.Join(" | ", bullets));
        if (risky.Count > 5)
            details += " " + string.Format(L(Prefix + "andmore", "(and {0} more)"), risky.Count - 5);

        return Warning(
            string.Format(L(Prefix + "found", "{0} forms capture PII-shaped fields on the default (indefinite) retention policy. Review for GDPR / privacy compliance."), risky.Count),
            details);
    }
}
