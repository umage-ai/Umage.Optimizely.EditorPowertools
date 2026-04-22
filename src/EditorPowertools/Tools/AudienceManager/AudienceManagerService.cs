using System.Text.RegularExpressions;
using EPiServer.Personalization.VisitorGroups;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Tools.AudienceManager.Models;
using UmageAI.Optimizely.EditorPowerTools.Tools.PersonalizationAudit;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.AudienceManager;

public class AudienceManagerService
{
    private readonly IVisitorGroupRepository _visitorGroupRepository;
    private readonly PersonalizationUsageRepository _usageRepository;
    private readonly ILogger<AudienceManagerService> _logger;

    private static readonly Regex CategoryPattern = new(@"^\[([^\]]+)\]\s*(.+)$", RegexOptions.Compiled);

    public AudienceManagerService(
        IVisitorGroupRepository visitorGroupRepository,
        PersonalizationUsageRepository usageRepository,
        ILogger<AudienceManagerService> logger)
    {
        _visitorGroupRepository = visitorGroupRepository;
        _usageRepository = usageRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all visitor groups with details.
    /// </summary>
    public IEnumerable<VisitorGroupDetailDto> GetAllVisitorGroups()
    {
        var groups = _visitorGroupRepository.List();
        var usageCounts = GetUsageCountsByVisitorGroupId();

        return groups.Select(g => MapToDto(g, usageCounts));
    }

    /// <summary>
    /// Gets criteria details for a specific visitor group.
    /// </summary>
    public IEnumerable<CriterionDto> GetCriteria(Guid id)
    {
        var group = _visitorGroupRepository.List().FirstOrDefault(g => g.Id == id);
        if (group == null)
            return Enumerable.Empty<CriterionDto>();

        return group.Criteria.Select(c => new CriterionDto
        {
            TypeName = CleanCriterionTypeName(c.TypeName),
            Description = null
        });
    }

    /// <summary>
    /// Gets usage records for a specific visitor group from the PersonalizationUsage DDS store.
    /// Returns empty if no records exist (personalization job hasn't been run yet).
    /// </summary>
    public IEnumerable<VisitorGroupUsageDto> GetUsages(Guid visitorGroupId)
    {
        try
        {
            return _usageRepository
                .GetByVisitorGroup(visitorGroupId.ToString())
                .Select(r => new VisitorGroupUsageDto
                {
                    ContentId = r.ContentId,
                    ContentName = string.IsNullOrEmpty(r.ContentName) ? "[Unknown]" : r.ContentName,
                    PropertyName = r.PropertyName,
                    UsageType = r.UsageType,
                    EditUrl = r.ContentId != 0
                        ? $"{Paths.ToResource("CMS", "")}#context=epi.cms.contentdata:///{r.ContentId}"
                        : null
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PersonalizationUsage DDS store not available");
            return Enumerable.Empty<VisitorGroupUsageDto>();
        }
    }

    private Dictionary<Guid, int> GetUsageCountsByVisitorGroupId()
    {
        var counts = new Dictionary<Guid, int>();

        try
        {
            foreach (var record in _usageRepository.GetAll())
            {
                if (Guid.TryParse(record.VisitorGroupId, out var vgId))
                {
                    counts.TryGetValue(vgId, out var count);
                    counts[vgId] = count + 1;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PersonalizationUsage DDS store not available for usage counts");
        }

        return counts;
    }

    private static VisitorGroupDetailDto MapToDto(VisitorGroup group, Dictionary<Guid, int> usageCounts)
    {
        var (category, cleanName) = ExtractCategory(group.Name);

        usageCounts.TryGetValue(group.Id, out var usageCount);
        var hasUsageData = usageCounts.Count > 0;

        return new VisitorGroupDetailDto
        {
            Id = group.Id,
            Name = group.Name,
            CleanName = cleanName,
            Category = category,
            Notes = group.Notes,
            CriteriaCount = group.Criteria?.Count ?? 0,
            StatisticsEnabled = false, // Statistics property not available in CMS 12
            CriteriaOperator = group.CriteriaOperator.ToString(),
            Criteria = group.Criteria?.Select(c => new CriterionDto
            {
                TypeName = CleanCriterionTypeName(c.TypeName),
                Description = null
            }).ToList() ?? new List<CriterionDto>(),
            EditUrl = $"{EPiServer.Shell.Paths.ToResource("EPiServer.Cms.UI.VisitorGroups", "ManageVisitorGroups")}#/group/{group.Id}",
            UsageCount = hasUsageData ? usageCount : null
        };
    }

    /// <summary>
    /// Converts a full .NET type name like "EPiServer.Personalization.VisitorGroups.Criteria.PageVisitedCriterion"
    /// to a clean display name like "Page Visited".
    /// </summary>
    private static string CleanCriterionTypeName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return "Unknown";

        // Get just the class name from the full type name
        var className = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;

        // Remove "Criterion" suffix
        if (className.EndsWith("Criterion", StringComparison.Ordinal))
            className = className[..^"Criterion".Length];

        // Insert spaces before capital letters (PascalCase → "Pascal Case")
        return Regex.Replace(className, "(?<!^)([A-Z])", " $1").Trim();
    }

    private static (string? category, string cleanName) ExtractCategory(string name)
    {
        var match = CategoryPattern.Match(name);
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }

        return (null, name);
    }
}
