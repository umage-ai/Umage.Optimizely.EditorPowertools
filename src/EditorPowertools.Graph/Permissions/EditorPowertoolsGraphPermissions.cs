using EPiServer.DataAnnotations;
using EPiServer.Security;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

/// <summary>
/// Permission types for the Graph add-on tools. Registered as "functions"
/// in the CMS admin UI under Set Access Rights.
/// </summary>
[PermissionTypes]
public static class EditorPowertoolsGraphPermissions
{
    public static PermissionType PowerSearch { get; } =
        new("EditorPowertools", "PowerSearch");

    public static PermissionType PowerSlice { get; } =
        new("EditorPowertools", "PowerSlice");

    public static PermissionType SearchProviders { get; } =
        new("EditorPowertools", "SearchProviders");
}
