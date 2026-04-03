using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

/// <summary>
/// Checks for images missing alt text. Hooks into the scheduled job to
/// traverse all content efficiently rather than scanning on-demand.
/// </summary>
public class MissingAltTextCheck : AnalyzerDoctorCheckBase
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private int _imageCount;
    private int _missingAlt;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/missingalttextcheck/";

    public MissingAltTextCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => L(Prefix + "name", "Missing Alt Text");
    public override string Description => L(Prefix + "description", "Checks for images missing alt text (accessibility and SEO issue).");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 50;
    public override string[] Tags => new[] { "SEO", "Accessibility" };

    protected override void OnInitialize()
    {
        _imageCount = 0;
        _missingAlt = 0;
    }

    protected override void OnAnalyze(IContent content, ContentReference contentRef)
    {
        if (content is not MediaData) return;

        var ct = _contentTypeRepository.Load(content.ContentTypeID);
        if (ct?.ModelType == null || !typeof(ImageData).IsAssignableFrom(ct.ModelType)) return;

        _imageCount++;

        var altProp = content.Property["AltText"] ?? content.Property["Description"] ??
                      content.Property["AlternativeText"] ?? content.Property["Alt"];
        if (altProp == null || altProp.Value == null || string.IsNullOrWhiteSpace(altProp.Value.ToString()))
        {
            _missingAlt++;
        }
    }

    protected override Models.DoctorCheckResult EvaluateResults()
    {
        if (_imageCount == 0)
            return Ok(L(Prefix + "noimages", "No images found."));

        var pct = _imageCount > 0 ? (_missingAlt * 100 / _imageCount) : 0;
        var details = string.Format(
            L(Prefix + "details", "{0} of {1} images ({2}%) have no alt text. Last analyzed: {3}"),
            _missingAlt, _imageCount, pct, LastAnalyzed?.ToString("g") ?? "N/A");

        if (pct > 50)
            return BadPractice(
                string.Format(L(Prefix + "badpractice", "{0} images missing alt text ({1}%)."), _missingAlt, pct),
                details);
        if (_missingAlt > 0)
            return Warning(
                string.Format(L(Prefix + "warning", "{0} images missing alt text."), _missingAlt),
                details);

        return Ok(L(Prefix + "ok", "All images have alt text."), details);
    }
}
