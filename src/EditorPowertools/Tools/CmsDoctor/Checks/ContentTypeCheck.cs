using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class ContentTypeCheck : DoctorCheckBase
{
    private readonly IContentTypeRepository _contentTypeRepository;

    public ContentTypeCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => "Content Type Count";
    public override string Description => "Checks if the number of content types is within reasonable limits.";
    public override string Group => "Content";
    public override int SortOrder => 10;
    public override string[] Tags => new[] { "EditorUX" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var types = _contentTypeRepository.List().Where(ct => ct.ModelType != null).ToList();
        var pageTypes = types.Count(ct => typeof(EPiServer.Core.PageData).IsAssignableFrom(ct.ModelType));
        var blockTypes = types.Count(ct => typeof(EPiServer.Core.BlockData).IsAssignableFrom(ct.ModelType));
        var mediaTypes = types.Count(ct => typeof(EPiServer.Core.MediaData).IsAssignableFrom(ct.ModelType));

        var details = $"Page types: {pageTypes}, Block types: {blockTypes}, Media types: {mediaTypes}, Total: {types.Count}";

        if (pageTypes > 50)
            return BadPractice($"Too many page types ({pageTypes}). Editors may struggle to find the right one.", details);
        if (blockTypes > 80)
            return BadPractice($"Too many block types ({blockTypes}). Consider consolidating.", details);
        if (pageTypes < 2)
            return Warning($"Only {pageTypes} page type(s). Content may lack structure.", details);

        return Ok($"{types.Count} content types ({pageTypes} pages, {blockTypes} blocks, {mediaTypes} media)", details);
    }
}
