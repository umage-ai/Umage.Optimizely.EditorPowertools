using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class DraftContentCheck : DoctorCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentVersionRepository _versionRepository;

    public DraftContentCheck(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentVersionRepository versionRepository)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _versionRepository = versionRepository;
    }

    public override string Name => "Stale Drafts";
    public override string Description => "Checks for draft content that has never been published or has old unpublished changes.";
    public override string Group => "Content";
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
            return Ok("No stale drafts found.");

        var details = $"Never published: {neverPublished}, Stale drafts (>3 months): {staleDrafts}";
        if (total > 50)
            return Warning($"{total} stale content items found. Consider reviewing and cleaning up.", details);

        return Ok($"{total} stale items found (minor).", details);
    }
}
