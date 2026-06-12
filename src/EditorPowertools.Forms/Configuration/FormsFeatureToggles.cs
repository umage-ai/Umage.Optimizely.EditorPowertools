namespace UmageAI.Optimizely.EditorPowerTools.Forms.Configuration;

/// <summary>
/// Feature toggles for the Forms add-on tools. All features default to enabled.
/// Bound from the <c>CodeArt:EditorPowertools:Forms</c> configuration section.
/// </summary>
public class FormsFeatureToggles
{
    public bool FormsOverview { get; set; } = true;
    public bool SubmissionsTimeline { get; set; } = true;
}

/// <summary>
/// Container for Forms add-on options. Bound from <c>CodeArt:EditorPowertools:Forms</c>.
/// </summary>
public class EditorPowertoolsFormsOptions
{
    public FormsFeatureToggles Features { get; set; } = new();
}
