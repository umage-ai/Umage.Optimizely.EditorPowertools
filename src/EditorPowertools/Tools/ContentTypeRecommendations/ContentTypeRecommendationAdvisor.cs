using EPiServer;
using EPiServer.Cms.Shell.UI.Rest;
using EPiServer.Core;

namespace EditorPowertools.Tools.ContentTypeRecommendations;

/// <summary>
/// Implements IContentTypeAdvisor to hook into the CMS "create new content" dialog.
/// When an editor creates content under a parent that has recommendation rules,
/// the suggested content types are surfaced in the dialog.
/// </summary>
public class ContentTypeRecommendationAdvisor : IContentTypeAdvisor
{
    private readonly ContentTypeRecommendationService _service;
    private readonly IContentRepository _contentRepository;

    public ContentTypeRecommendationAdvisor(
        ContentTypeRecommendationService service,
        IContentRepository contentRepository)
    {
        _service = service;
        _contentRepository = contentRepository;
    }

    public IEnumerable<int> GetSuggestions(IContent parent, bool contentFolder, IEnumerable<string> requestedTypes)
    {
        return _service.EvaluateRules(parent, contentFolder, requestedTypes);
    }
}
