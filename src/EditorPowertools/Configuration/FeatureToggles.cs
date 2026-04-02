namespace EditorPowertools.Configuration;

/// <summary>
/// Feature toggles for enabling/disabling individual tools.
/// All features default to true (enabled).
/// </summary>
public class FeatureToggles
{
    public bool ContentTypeAudit { get; set; } = true;
    public bool PersonalizationUsageAudit { get; set; } = true;
    public bool ContentTypeRecommendations { get; set; } = true;
    public bool AudienceManager { get; set; } = true;
    public bool ContentDetails { get; set; } = true;
    public bool BrokenLinkChecker { get; set; } = true;
    public bool OrphanedContentFinder { get; set; } = true;
    public bool UnusedMediaCleaner { get; set; } = true;
    public bool ContentExporter { get; set; } = true;
    public bool BulkPropertyEditor { get; set; } = true;
    public bool ScheduledJobsGantt { get; set; } = true;
    public bool ActivityTimeline { get; set; } = true;
    public bool ContentImporter { get; set; } = true;
    public bool ManageChildren { get; set; } = true;
    public bool ContentAudit { get; set; } = true;
    public bool CmsDoctor { get; set; } = true;
    public bool ActiveEditors { get; set; } = true;
    public bool ActiveEditorsChat { get; set; } = true;
    public bool SecurityAudit { get; set; } = true;
    public bool VisitorGroupTester { get; set; } = true;
    public bool ContentStatistics { get; set; } = true;
}
