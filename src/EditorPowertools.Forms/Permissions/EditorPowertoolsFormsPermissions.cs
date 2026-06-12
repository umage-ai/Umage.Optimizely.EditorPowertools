using EPiServer.DataAnnotations;
using EPiServer.Security;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Permissions;

/// <summary>
/// Permission types for the Forms add-on tools. Registered as "functions"
/// in the CMS admin UI under Set Access Rights.
/// </summary>
[PermissionTypes]
public static class EditorPowertoolsFormsPermissions
{
    public static PermissionType FormsOverview { get; } =
        new("EditorPowertools", "FormsOverview");

    public static PermissionType SubmissionsTimeline { get; } =
        new("EditorPowertools", "SubmissionsTimeline");
}
