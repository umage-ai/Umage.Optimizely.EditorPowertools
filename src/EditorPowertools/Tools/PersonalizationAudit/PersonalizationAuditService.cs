using UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit.Models;
using EPiServer.Personalization.VisitorGroups;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;

public class PersonalizationAuditService
{
    private readonly PersonalizationUsageRepository _usageRepository;
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly ILogger<PersonalizationAuditService> _logger;

    public PersonalizationAuditService(
        PersonalizationUsageRepository usageRepository,
        IVisitorGroupRepository visitorGroupRepository,
        ILogger<PersonalizationAuditService> logger)
    {
        _usageRepository = usageRepository;
        _visitorGroupRepository = visitorGroupRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all personalization usage records from DDS.
    /// </summary>
    public IEnumerable<PersonalizationUsageDto> GetAllUsages()
    {
        return _usageRepository.GetAll().Select(r => new PersonalizationUsageDto
        {
            ContentId = r.ContentId,
            ContentName = r.ContentName,
            ContentTypeName = r.ContentTypeName,
            Language = r.Language,
            PropertyName = r.PropertyName,
            VisitorGroupId = r.VisitorGroupId,
            VisitorGroupName = r.VisitorGroupName,
            UsageType = r.UsageType,
            Breadcrumb = r.Breadcrumb,
            EditUrl = r.EditUrl,
            ParentContentId = r.ParentContentId,
            ParentContentName = r.ParentContentName
        });
    }

    /// <summary>
    /// Gets all visitor groups with their criteria count.
    /// </summary>
    public IEnumerable<VisitorGroupDto> GetVisitorGroups()
    {
        var groups = _visitorGroupRepository.List();
        return groups.Select(g => new VisitorGroupDto
        {
            Id = g.Id.ToString(),
            Name = g.Name,
            CriteriaCount = g.Criteria?.Count ?? 0
        });
    }
}
