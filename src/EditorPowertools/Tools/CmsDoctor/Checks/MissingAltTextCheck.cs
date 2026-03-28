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

    public MissingAltTextCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => "Missing Alt Text";
    public override string Description => "Checks for images missing alt text (accessibility and SEO issue).";
    public override string Group => "Content";
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
            return Ok("No images found.");

        var pct = _imageCount > 0 ? (_missingAlt * 100 / _imageCount) : 0;
        var details = $"{_missingAlt} of {_imageCount} images ({pct}%) have no alt text. Last analyzed: {LastAnalyzed:g}";

        if (pct > 50)
            return BadPractice($"{_missingAlt} images missing alt text ({pct}%).", details);
        if (_missingAlt > 0)
            return Warning($"{_missingAlt} images missing alt text.", details);

        return Ok("All images have alt text.", details);
    }
}
