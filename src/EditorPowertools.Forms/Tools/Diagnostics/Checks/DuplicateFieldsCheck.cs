using UmageAI.Optimizely.EditorPowerTools.Forms.Services;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Tools.Diagnostics.Checks;

/// <summary>
/// Flags forms that have two or more input fields sharing the same label. Such
/// fields produce ambiguous, colliding columns in the submission data, which
/// makes exports and notifications confusing and easy to misread.
/// </summary>
public class DuplicateFieldsCheck : DoctorCheckBase
{
    private readonly IFormsAggregationService _forms;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/duplicatefieldscheck/";

    public DuplicateFieldsCheck(IFormsAggregationService forms)
    {
        _forms = forms;
    }

    public override string Name => L(Prefix + "name", "Forms With Duplicate Fields");
    public override string Description => L(Prefix + "description", "Finds forms with two or more input fields that share the same label, producing ambiguous submission columns.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/forms", "Forms");
    public override int SortOrder => 40;
    public override string[] Tags => new[] { "Forms", "Data Quality" };

    public override DoctorCheckResult PerformCheck()
    {
        var affected = _forms.GetForms().Where(f => f.HasDuplicateFields).ToList();

        if (affected.Count == 0)
            return Ok(L(Prefix + "ok", "No form has duplicate field labels."));

        var examples = string.Join(" | ",
            affected.Take(5).Select(f => $"\"{f.Name}\" → {string.Join(", ", f.DuplicateFieldLabels.Take(4))}"));
        var details = string.Format(L(Prefix + "examples", "Examples: {0}"), examples);
        if (affected.Count > 5)
            details += " " + string.Format(L(Prefix + "andmore", "(and {0} more)"), affected.Count - 5);

        return Warning(
            string.Format(L(Prefix + "found", "{0} forms have duplicate field labels."), affected.Count),
            details);
    }
}
