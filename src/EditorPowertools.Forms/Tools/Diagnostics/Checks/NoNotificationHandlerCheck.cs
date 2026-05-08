using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;

/// <summary>
/// Flags forms that have submissions but no email or webhook handler — usually
/// indicates the form is collecting data that nobody is being notified about.
/// </summary>
public class NoNotificationHandlerCheck : DoctorCheckBase
{
    private readonly FormsAggregationService _forms;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/nonotificationhandlercheck/";

    public NoNotificationHandlerCheck(FormsAggregationService forms)
    {
        _forms = forms;
    }

    public override string Name => L(Prefix + "name", "Forms Without Notification Handlers");
    public override string Description => L(Prefix + "description", "Finds forms that receive submissions but have no email or webhook handler configured.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/forms", "Forms");
    public override int SortOrder => 60;
    public override string[] Tags => new[] { "Forms", "Editorial" };

    public override DoctorCheckResult PerformCheck()
    {
        var forms = _forms.GetForms();
        var orphans = forms
            .Where(f => f.SubmissionCount > 0 && !f.HasEmailHandler && !f.HasWebhookHandler)
            .ToList();

        if (orphans.Count == 0)
            return Ok(L(Prefix + "ok", "Every form that receives submissions has at least one notification handler."));

        var examples = string.Join(", ",
            orphans.Take(5).Select(f => $"{f.Name} ({f.SubmissionCount})"));
        var details = string.Format(L(Prefix + "examples", "Examples: {0}"), examples);
        if (orphans.Count > 5)
            details += " " + string.Format(L(Prefix + "andmore", "(and {0} more)"), orphans.Count - 5);

        return Warning(
            string.Format(L(Prefix + "found", "{0} forms have submissions but no email/webhook handler."), orphans.Count),
            details);
    }
}
