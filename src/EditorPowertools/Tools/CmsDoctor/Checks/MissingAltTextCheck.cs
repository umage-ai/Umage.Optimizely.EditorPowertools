using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class MissingAltTextCheck : HealthCheckBase
{
    private readonly IContentRepository _contentRepository;
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;

    public MissingAltTextCheck(
        IContentRepository contentRepository,
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository)
    {
        _contentRepository = contentRepository;
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => "Missing Alt Text";
    public override string Description => "Checks for images missing alt text (accessibility and SEO issue).";
    public override string Group => "Content";
    public override int SortOrder => 50;
    public override string[] Tags => new[] { "SEO", "Accessibility" };

    public override Models.HealthCheckResult PerformCheck()
    {
        var allContent = _contentRepository.GetDescendents(ContentReference.RootPage).ToList();
        var imageCount = 0;
        var missingAlt = 0;

        foreach (var contentRef in allContent)
        {
            try
            {
                if (!_contentLoader.TryGet<IContent>(contentRef, out var content)) continue;
                if (content is not MediaData media) continue;

                var ct = _contentTypeRepository.Load(content.ContentTypeID);
                if (ct?.ModelType == null || !typeof(ImageData).IsAssignableFrom(ct.ModelType)) continue;

                imageCount++;

                // Check for common alt text property names
                var altProp = content.Property["AltText"] ?? content.Property["Description"] ??
                              content.Property["AlternativeText"] ?? content.Property["Alt"];
                if (altProp == null || altProp.Value == null || string.IsNullOrWhiteSpace(altProp.Value.ToString()))
                {
                    missingAlt++;
                }
            }
            catch { }
        }

        if (imageCount == 0)
            return Ok("No images found.");

        var pct = imageCount > 0 ? (missingAlt * 100 / imageCount) : 0;
        var details = $"{missingAlt} of {imageCount} images ({pct}%) have no alt text.";

        if (pct > 50)
            return BadPractice($"{missingAlt} images missing alt text ({pct}%).", details);
        if (missingAlt > 0)
            return Warning($"{missingAlt} images missing alt text.", details);

        return Ok("All images have alt text.", details);
    }
}
