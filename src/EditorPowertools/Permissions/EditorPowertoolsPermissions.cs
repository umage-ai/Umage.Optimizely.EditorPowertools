using EPiServer.DataAnnotations;
using EPiServer.Security;

namespace UmageAI.Optimizely.EditorPowerTools.Permissions;

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
        new("UmageAI.Optimizely.EditorPowerTools", "ContentTypeAudit");

    public static PermissionType PersonalizationUsageAudit { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "PersonalizationUsageAudit");

    public static PermissionType ContentTypeRecommendations { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentTypeRecommendations");

    public static PermissionType AudienceManager { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "AudienceManager");

    public static PermissionType ContentDetails { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentDetails");

    public static PermissionType BrokenLinkChecker { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "BrokenLinkChecker");

    public static PermissionType OrphanedContentFinder { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "OrphanedContentFinder");

    public static PermissionType UnusedMediaCleaner { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "UnusedMediaCleaner");

    public static PermissionType ContentExporter { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentExporter");

    public static PermissionType BulkPropertyEditor { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "BulkPropertyEditor");

    public static PermissionType ScheduledJobsGantt { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ScheduledJobsGantt");

    public static PermissionType ActivityTimeline { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ActivityTimeline");

    public static PermissionType ContentImporter { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentImporter");

    public static PermissionType ManageChildren { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ManageChildren");

    public static PermissionType ContentAudit { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentAudit");

    public static PermissionType CmsDoctor { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "CmsDoctor");

    public static PermissionType ActiveEditors { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ActiveEditors");

    public static PermissionType SecurityAudit { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "SecurityAudit");

    public static PermissionType VisitorGroupTester { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "VisitorGroupTester");

    public static PermissionType ContentStatistics { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "ContentStatistics");

    public static PermissionType LanguageAudit { get; } =
        new("UmageAI.Optimizely.EditorPowerTools", "LanguageAudit");
}
