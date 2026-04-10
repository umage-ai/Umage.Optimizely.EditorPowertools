using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Checks;

public class DraftContentCheck : DoctorCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentVersionRepository _versionRepository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/draftcontentcheck/";

    public DraftContentCheck(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentVersionRepository versionRepository)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _versionRepository = versionRepository;
    }

    public override string Name => L(Prefix + "name", "Stale Drafts");
    public override string Description => L(Prefix + "description", "Checks for draft content that has never been published or has old unpublished changes.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 40;
    public override string[] Tags => new[] { "EditorUX", "Maintenance" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var allContent = _contentRepository.GetDescendents(ContentReference.RootPage).Take(1000).ToList();
        var neverPublished = 0;
        var staleDrafts = 0;
        var cutoff = DateTime.Now.AddMonths(-3);

        foreach (var contentRef in allContent)
        {
            try
            {
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content)) continue;
                if (content is not IVersionable versionable) continue;

                if (versionable.Status == VersionStatus.NotCreated ||
                    (versionable.Status != VersionStatus.Published && versionable.StartPublish == null))
                {
                    neverPublished++;
                }
                else if (versionable.Status == VersionStatus.CheckedOut &&
                         content is IChangeTrackable trackable &&
                         trackable.Changed < cutoff)
                {
                    staleDrafts++;
                }
            }
            catch { }
        }

        var total = neverPublished + staleDrafts;
        if (total == 0)
            return Ok(L(Prefix + "ok", "No stale drafts found."));

        var details = string.Format(L(Prefix + "details", "Never published: {0}, Stale drafts (>3 months): {1}"),
            neverPublished, staleDrafts);
        if (total > 50)
            return Warning(
                string.Format(L(Prefix + "warning", "{0} stale content items found. Consider reviewing and cleaning up."), total),
                details);

        return Ok(string.Format(L(Prefix + "minor", "{0} stale items found (minor)."), total), details);
    }
}
