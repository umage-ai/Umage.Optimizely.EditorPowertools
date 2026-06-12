using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;

/// <summary>
/// Flags forms that exist in the content tree but are not referenced by any
/// page or block. Often candidates for cleanup.
/// </summary>
public class UnusedFormsCheck : DoctorCheckBase
{
    private readonly IFormsAggregationService _forms;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/unusedformscheck/";

    public UnusedFormsCheck(IFormsAggregationService forms)
    {
        _forms = forms;
    }

    public override string Name => L(Prefix + "name", "Unused Forms");
    public override string Description => L(Prefix + "description", "Finds Optimizely Forms that are not referenced by any content on the site.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/forms", "Forms");
    public override int SortOrder => 50;
    public override string[] Tags => new[] { "Forms", "Maintenance" };

    public override DoctorCheckResult PerformCheck()
    {
        var forms = _forms.GetForms();
        var unused = forms.Where(f => f.UsageCount == 0).ToList();

        if (unused.Count == 0)
            return Ok(L(Prefix + "ok", "Every form is referenced from at least one page or block."));

        var examples = string.Join(", ", unused.Take(5).Select(f => f.Name));
        var details = string.Format(L(Prefix + "examples", "Examples: {0}"), examples);
        if (unused.Count > 5)
            details += " " + string.Format(L(Prefix + "andmore", "(and {0} more)"), unused.Count - 5);

        return BadPractice(
            string.Format(L(Prefix + "found", "{0} forms are not used anywhere on the site."), unused.Count),
            details);
    }
}
