using EPiServer;
using EPiServer.Core;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Checks for content not referenced by anything. Hooks into the scheduled job
/// to count references efficiently during traversal.
/// </summary>
public class UnusedContentCheck : AnalyzerDoctorCheckBase
{
    private readonly IContentRepository _contentRepository;
    private int _totalBlocks;
    private int _unreferencedBlocks;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/unusedcontentcheck/";

    public UnusedContentCheck(IContentRepository contentRepository)
    {
        _contentRepository = contentRepository;
    }

    public override string Name => L(Prefix + "name", "Unused Content");
    public override string Description => L(Prefix + "description", "Checks for blocks and media not referenced by anything (potential orphans).");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 30;
    public override string[] Tags => new[] { "Maintenance", "Performance" };

    protected override void OnInitialize()
    {
        _totalBlocks = 0;
        _unreferencedBlocks = 0;
    }

    protected override void OnAnalyze(IContent content, ContentReference contentRef)
    {
        // Only check blocks and media, not pages (pages can be top-level)
        if (content is PageData) return;

        _totalBlocks++;

        try
        {
            var refs = _contentRepository.GetReferencesToContent(contentRef, false);
            if (!refs.Any())
            {
                _unreferencedBlocks++;
            }
        }
        catch { }
    }

    protected override Models.DoctorCheckResult EvaluateResults()
    {
        if (_totalBlocks == 0)
            return Ok(L(Prefix + "noblocks", "No blocks or media found."));

        var pct = _totalBlocks > 0 ? (_unreferencedBlocks * 100 / _totalBlocks) : 0;
        var details = string.Format(
            L(Prefix + "details", "{0} of {1} blocks/media ({2}%) are unreferenced. Last analyzed: {3}"),
            _unreferencedBlocks, _totalBlocks, pct, LastAnalyzed?.ToString("g") ?? "N/A");

        if (pct > 20)
            return Warning(
                string.Format(L(Prefix + "warning", "{0} unreferenced items ({1}%). Consider cleanup."), _unreferencedBlocks, pct),
                details);
        if (_unreferencedBlocks > 0)
            return Ok(
                string.Format(L(Prefix + "minor", "{0} unreferenced items ({1}%, minor)."), _unreferencedBlocks, pct),
                details);

        return Ok(L(Prefix + "ok", "All blocks and media are referenced."), details);
    }
}
