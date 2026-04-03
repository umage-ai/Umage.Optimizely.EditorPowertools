using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.CmsDoctor.Checks;

public class ContentTypeCheck : DoctorCheckBase
{
    private readonly IContentTypeRepository _contentTypeRepository;
    private const string Prefix = "/editorpowertools/cmsdoctor/checks/contenttypecheck/";

    public ContentTypeCheck(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public override string Name => L(Prefix + "name", "Content Type Count");
    public override string Description => L(Prefix + "description", "Checks if the number of content types is within reasonable limits.");
    public override string Group => L("/editorpowertools/cmsdoctor/groups/content", "Content");
    public override int SortOrder => 10;
    public override string[] Tags => new[] { "EditorUX" };

    public override Models.DoctorCheckResult PerformCheck()
    {
        var types = _contentTypeRepository.List().Where(ct => ct.ModelType != null).ToList();
        var pageTypes = types.Count(ct => typeof(EPiServer.Core.PageData).IsAssignableFrom(ct.ModelType));
        var blockTypes = types.Count(ct => typeof(EPiServer.Core.BlockData).IsAssignableFrom(ct.ModelType));
        var mediaTypes = types.Count(ct => typeof(EPiServer.Core.MediaData).IsAssignableFrom(ct.ModelType));

        var details = string.Format(L(Prefix + "details", "Page types: {0}, Block types: {1}, Media types: {2}, Total: {3}"),
            pageTypes, blockTypes, mediaTypes, types.Count);

        if (pageTypes > 50)
            return BadPractice(string.Format(L(Prefix + "toomanypagetypes", "Too many page types ({0}). Editors may struggle to find the right one."), pageTypes), details);
        if (blockTypes > 80)
            return BadPractice(string.Format(L(Prefix + "toomanyblocktypes", "Too many block types ({0}). Consider consolidating."), blockTypes), details);
        if (pageTypes < 2)
            return Warning(string.Format(L(Prefix + "toofewpagetypes", "Only {0} page type(s). Content may lack structure."), pageTypes), details);

        return Ok(string.Format(L(Prefix + "ok", "{0} content types ({1} pages, {2} blocks, {3} media)"),
            types.Count, pageTypes, blockTypes, mediaTypes), details);
    }
}
