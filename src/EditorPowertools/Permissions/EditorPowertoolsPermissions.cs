using EPiServer.DataAnnotations;
using EPiServer.Security;

namespace EditorPowertools.Permissions;

/// <summary>
/// Defines EPiServer permission types for each tool.
/// These are registered as "functions" in the CMS admin UI under Set Access Rights.
/// When EditorPowertoolsOptions.CheckPermissionForEachFeature is true,
/// each tool checks its corresponding permission in addition to the authorization policy.
/// </summary>
[PermissionTypes]
public static class EditorPowertoolsPermissions
{
    public static PermissionType ContentTypeAudit { get; } =
        new("EditorPowertools", "ContentTypeAudit");

    public static PermissionType PersonalizationUsageAudit { get; } =
        new("EditorPowertools", "PersonalizationUsageAudit");

    public static PermissionType ContentTypeRecommendations { get; } =
        new("EditorPowertools", "ContentTypeRecommendations");

    public static PermissionType AudienceManager { get; } =
        new("EditorPowertools", "AudienceManager");

    public static PermissionType ContentDetails { get; } =
        new("EditorPowertools", "ContentDetails");

    public static PermissionType BrokenLinkChecker { get; } =
        new("EditorPowertools", "BrokenLinkChecker");

    public static PermissionType OrphanedContentFinder { get; } =
        new("EditorPowertools", "OrphanedContentFinder");

    public static PermissionType UnusedMediaCleaner { get; } =
        new("EditorPowertools", "UnusedMediaCleaner");

    public static PermissionType ContentExporter { get; } =
        new("EditorPowertools", "ContentExporter");

    public static PermissionType BulkPropertyEditor { get; } =
        new("EditorPowertools", "BulkPropertyEditor");

    public static PermissionType ScheduledJobsGantt { get; } =
        new("EditorPowertools", "ScheduledJobsGantt");

    public static PermissionType ActivityTimeline { get; } =
        new("EditorPowertools", "ActivityTimeline");

    public static PermissionType ContentImporter { get; } =
        new("EditorPowertools", "ContentImporter");
}
