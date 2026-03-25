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
}
