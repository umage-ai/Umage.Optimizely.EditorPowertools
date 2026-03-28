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

    public UnusedContentCheck(IContentRepository contentRepository)
    {
        _contentRepository = contentRepository;
    }

    public override string Name => "Unused Content";
    public override string Description => "Checks for blocks and media not referenced by anything (potential orphans).";
    public override string Group => "Content";
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
            return Ok("No blocks or media found.");

        var pct = _totalBlocks > 0 ? (_unreferencedBlocks * 100 / _totalBlocks) : 0;
        var details = $"{_unreferencedBlocks} of {_totalBlocks} blocks/media ({pct}%) are unreferenced. Last analyzed: {LastAnalyzed:g}";

        if (pct > 20)
            return Warning($"{_unreferencedBlocks} unreferenced items ({pct}%). Consider cleanup.", details);
        if (_unreferencedBlocks > 0)
            return Ok($"{_unreferencedBlocks} unreferenced items ({pct}%, minor).", details);

        return Ok("All blocks and media are referenced.", details);
    }
}
