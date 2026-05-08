using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

/// <summary>
/// Counts content that has never been published or that's been a draft for more than
/// three months. Hooks the unified analysis job — the previous implementation walked
/// the full content tree on every dashboard click via <c>GetDescendents</c>, which is
/// not allowed on a live request path.
/// </summary>
public class DraftContentCheck : AnalyzerDoctorCheckBase
{
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/draftcontentcheck/";
    private static readonly TimeSpan StaleDraftThreshold = TimeSpan.FromDays(90);

    private int _neverPublished;
    private int _staleDrafts;
    private DateTime _staleCutoff;

    public override string Name => L(Prefix + "name", "Stale Drafts");
    public override string Description => L(Prefix + "description", "Checks for draft content that has never been published or has old unpublished changes.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 40;
    public override string[] Tags => new[] { "EditorUX", "Maintenance" };

    protected override void OnInitialize()
    {
        _neverPublished = 0;
        _staleDrafts = 0;
        _staleCutoff = DateTime.Now - StaleDraftThreshold;
    }

    protected override void OnAnalyze(IContent content, ContentReference contentRef)
    {
        if (content is not IVersionable versionable) return;

        if (versionable.Status == VersionStatus.NotCreated ||
            (versionable.Status != VersionStatus.Published && versionable.StartPublish == null))
        {
            _neverPublished++;
            return;
        }

        if (versionable.Status == VersionStatus.CheckedOut &&
            content is IChangeTrackable trackable &&
            trackable.Changed < _staleCutoff)
        {
            _staleDrafts++;
        }
    }

    protected override Models.DoctorCheckResult EvaluateResults()
    {
        var total = _neverPublished + _staleDrafts;
        if (total == 0)
            return Ok(L(Prefix + "ok", "No stale drafts found."));

        var details = string.Format(
            L(Prefix + "details", "Never published: {0}, Stale drafts (>3 months): {1}. Last analyzed: {2}"),
            _neverPublished, _staleDrafts, LastAnalyzed?.ToString("g") ?? "N/A");

        if (total > 50)
            return Warning(
                string.Format(L(Prefix + "warning", "{0} stale content items found. Consider reviewing and cleaning up."), total),
                details);

        return Ok(string.Format(L(Prefix + "minor", "{0} stale items found (minor)."), total), details);
    }
}
