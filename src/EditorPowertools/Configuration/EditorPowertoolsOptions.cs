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
    /// When true, each feature checks the user's EPiServer permissions (Permissions For Functions)
    /// in addition to the authorization policy. When false, only the role-based policy is checked.
    /// Enable this to allow granular per-tool access control via the CMS admin "Permissions For Functions" UI.
    /// </summary>
    public bool CheckPermissionForEachFeature { get; set; }

    /// <summary>
    /// Roles that have full access to all Editor Powertools features.
    /// Users in these roles bypass per-feature permission checks.
    /// Default: WebAdmins, Administrators.
    /// </summary>
    public string[] AuthorizedRoles { get; set; } = ["WebAdmins", "Administrators"];

    /// <summary>
    /// Options specific to the Content Audit tool.
    /// </summary>
    public ContentAuditOptions ContentAudit { get; set; } = new();
}
