namespace EditorPowertools.Configuration;

/// <summary>
/// Configuration options for Editor Powertools.
/// Can be set via code in AddEditorPowertools() or via appsettings section "CodeArt:EditorPowertools".
/// </summary>
public class EditorPowertoolsOptions
{
    /// <summary>
    /// Feature toggles to enable/disable individual tools.
    /// All features are enabled by default.
    /// </summary>
    public FeatureToggles Features { get; set; } = new();

    /// <summary>
    /// When true, each feature checks the user's EPiServer permissions (PermissionTypes)
    /// in addition to the authorization policy. When false, only the policy is checked.
    /// </summary>
    public bool CheckPermissionForEachFeature { get; set; }
}
