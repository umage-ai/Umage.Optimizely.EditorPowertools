using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class UnusedContentCheck : HealthCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;

    public UnusedContentCheck(IContentRepository contentRepository, IContentLoader contentLoader)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
    }

    public override string Name => "Unused Content";
    public override string Description => "Checks for content not referenced by anything (potential orphans).";
    public override string Group => "Content";
    public override int SortOrder => 30;
    public override string[] Tags => new[] { "Maintenance", "Performance" };

    public override Models.HealthCheckResult PerformCheck()
    {
        var allContent = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var unreferenced = 0;
        var sampled = 0;

        // Sample up to 500 items to keep check fast
        foreach (var contentRef in allContent.Take(500))
        {
            sampled++;
            try
            {
                var refs = _contentRepository.GetReferencesToContent(contentRef, false);
                if (!refs.Any())
                {
                    if (_contentLoader.TryGet<IContent>(contentRef, out var content) &&
                        content is not PageData) // Pages can be top-level
                    {
                        unreferenced++;
                    }
                }
            }
            catch { }
        }

        var details = $"Sampled {sampled} of {allContent.Count} items.";
        if (unreferenced == 0)
            return Ok("No unreferenced blocks/media found in sample.", details);

        var pct = sampled > 0 ? (unreferenced * 100 / sampled) : 0;
        if (pct > 20)
            return Warning($"{unreferenced} unreferenced items ({pct}% of sample). Consider cleanup.", details);

        return Ok($"{unreferenced} unreferenced items ({pct}% of sample).", details);
    }
}
