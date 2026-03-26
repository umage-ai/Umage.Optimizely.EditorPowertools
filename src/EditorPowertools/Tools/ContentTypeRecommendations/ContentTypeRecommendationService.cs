using EditorPowertools.Tools.ContentTypeRecommendations.Models;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace EditorPowertools.Tools.ContentTypeRecommendations;

public class ContentTypeRecommendationService
{
    private readonly ContentTypeRecommendationRepository _repository;
    private readonly IContentRepository _contentRepository;
    private readonly IContentTypeRepository _contentTypeRepository;

    public ContentTypeRecommendationService(
        ContentTypeRecommendationRepository repository,
        IContentRepository contentRepository,
        IContentTypeRepository contentTypeRepository)
    {
        _repository = repository;
        _contentRepository = contentRepository;
        _contentTypeRepository = contentTypeRepository;
    }

    public IEnumerable<ContentTypeRecommendationRule> ListRules() => _repository.GetAll();

    /// <summary>
    /// Evaluates all rules against a parent content item and returns suggested content type IDs.
    /// TODO: Register as IContentTypeAdvisor when EPiServer.Cms.Shell.UI.Rest is available.
    /// </summary>
    public IEnumerable<int> EvaluateRules(IContent parent, bool contentFolder, IEnumerable<string> requestedTypes)
    {
        var parentType = parent.ContentTypeID;
        var parentContent = parent.ContentLink.ToReferenceWithoutVersion();
        var ancestors = _contentRepository.GetAncestors(parent.ContentLink).ToList();
        var rules = ListRules().ToList();
        var suggestions = new List<int>();

        foreach (var rule in rules)
        {
            if (rule.ParentContentType == -1 && rule.ParentContent == null)
                continue;

            if (rule.ParentContentType != -1 && rule.ParentContentType != parentType)
                continue;

            if (rule.ParentContent != null && rule.ParentContent.CompareToIgnoreWorkID(parentContent) && !rule.IncludeDescendants)
                continue;

            if (rule.ParentContent != null && !rule.ParentContent.CompareToIgnoreWorkID(parentContent) && rule.IncludeDescendants && !rule.ForThisContentFolder)
            {
                if (!ancestors.Any(x => x.ContentLink.CompareToIgnoreWorkID(rule.ParentContent)))
                    continue;
            }

            suggestions.AddRange(rule.ContentTypesToSuggest);
        }

        return suggestions.Distinct().ToList();
    }

    public List<RecommendationRuleDto> GetAllRulesWithNames()
    {
        var rules = ListRules().ToList();
        var contentTypes = _contentTypeRepository.List().ToList();

        return rules.Select(r =>
        {
            string? parentContentName = null;
            if (r.ParentContent != null && !ContentReference.IsNullOrEmpty(r.ParentContent))
            {
                try
                {
                    if (_contentRepository.TryGet<IContent>(r.ParentContent, out var content))
                        parentContentName = content.Name;
                }
                catch { /* content may be deleted */ }
            }

            var parentTypeName = r.ParentContentType > 0
                ? contentTypes.FirstOrDefault(ct => ct.ID == r.ParentContentType)?.DisplayName
                  ?? contentTypes.FirstOrDefault(ct => ct.ID == r.ParentContentType)?.Name
                : null;

            var suggestedTypeNames = r.ContentTypesToSuggest
                .Select(id => contentTypes.FirstOrDefault(ct => ct.ID == id))
                .Where(ct => ct != null)
                .Select(ct => new SuggestedTypeDto { Id = ct!.ID, Name = ct.DisplayName ?? ct.Name })
                .ToList();

            return new RecommendationRuleDto
            {
                Id = r.Id.ToString(),
                ParentContentType = r.ParentContentType,
                ParentContentTypeName = parentTypeName,
                ParentContentId = r.ParentContent?.ID,
                ParentContentName = parentContentName,
                IncludeDescendants = r.IncludeDescendants,
                ForThisContentFolder = r.ForThisContentFolder,
                SuggestedTypes = suggestedTypeNames
            };
        }).ToList();
    }

    public void SaveRule(ContentTypeRecommendationRule rule) => _repository.Save(rule);

    public void DeleteRule(string id) => _repository.Delete(EPiServer.Data.Identity.Parse(id));
}
