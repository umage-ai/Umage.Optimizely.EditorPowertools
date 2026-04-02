using System.Text.Json;
using EditorPowertools.Tools.LanguageAudit.Models;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace EditorPowertools.Tools.LanguageAudit;

public class LanguageAuditService
{
    private readonly LanguageAuditRepository _repository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly ILogger<LanguageAuditService> _logger;

    public LanguageAuditService(
        LanguageAuditRepository repository,
        ILanguageBranchRepository languageBranchRepository,
        ILogger<LanguageAuditService> logger)
    {
        _repository = repository;
        _languageBranchRepository = languageBranchRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets overview statistics per language: total content, published, coverage percentage.
    /// </summary>
    public LanguageOverviewDto GetOverview()
    {
        var records = _repository.GetAll().ToList();
        var enabledLanguages = _languageBranchRepository.ListEnabled()
            .Select(lb => lb.LanguageID)
            .ToList();

        var totalContent = records.Count;
        var languageStats = new List<LanguageStatDto>();

        foreach (var lang in enabledLanguages)
        {
            var withLang = records.Where(r =>
                r.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Any(l => string.Equals(l.Trim(), lang, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var publishedCount = 0;
            foreach (var record in withLang)
            {
                var details = ParseLanguageDetails(record.LanguageDetailsJson);
                var langDetail = details.FirstOrDefault(d =>
                    string.Equals(d.Lang, lang, StringComparison.OrdinalIgnoreCase));
                if (langDetail != null && string.Equals(langDetail.Status, "Published", StringComparison.OrdinalIgnoreCase))
                    publishedCount++;
            }

            languageStats.Add(new LanguageStatDto
            {
                Language = lang,
                TotalContent = withLang.Count,
                PublishedCount = publishedCount,
                CoveragePercent = totalContent > 0 ? Math.Round(100.0 * withLang.Count / totalContent, 1) : 0
            });
        }

        var missingCount = records.Count(r => r.IsMissingTranslations);
        var staleCount = records.Count(r => r.StalestTranslationDays > 30);

        return new LanguageOverviewDto
        {
            TotalContent = totalContent,
            EnabledLanguages = enabledLanguages,
            LanguageStats = languageStats,
            MissingTranslationsCount = missingCount,
            StaleTranslationsCount = staleCount
        };
    }

    /// <summary>
    /// Gets content items missing a specific language translation.
    /// Optionally filtered by parent content ID to show subtree.
    /// </summary>
    public List<MissingTranslationDto> GetMissingTranslations(string language, int? parentId = null)
    {
        var records = _repository.GetAll().ToList();

        var missing = records.Where(r =>
        {
            if (parentId.HasValue && r.ParentContentId != parentId.Value)
                return false;

            var langs = r.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim());
            return !langs.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase));
        })
        .Select(r => new MissingTranslationDto
        {
            ContentId = r.ContentId,
            ContentName = r.ContentName,
            ContentTypeName = r.ContentTypeName,
            Breadcrumb = r.Breadcrumb,
            MasterLanguage = r.MasterLanguage,
            AvailableLanguages = r.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim()).ToList(),
            EditUrl = r.EditUrl
        })
        .OrderBy(r => r.Breadcrumb)
        .ToList();

        return missing;
    }

    /// <summary>
    /// Gets content tree nodes with coverage stats per language (for hierarchical view).
    /// </summary>
    public List<LanguageCoverageNodeDto> GetCoverageTree(string language)
    {
        var records = _repository.GetAll().ToList();
        var enabledLanguages = _languageBranchRepository.ListEnabled()
            .Select(lb => lb.LanguageID).ToList();

        // Group by parent for tree building
        var byParent = records.GroupBy(r => r.ParentContentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Find root-level groups (content whose parent is not in our records)
        var allIds = records.Select(r => r.ContentId).ToHashSet();
        var rootParents = byParent.Keys.Where(pid => !allIds.Contains(pid)).OrderBy(p => p).ToList();

        var result = new List<LanguageCoverageNodeDto>();
        foreach (var parentId in rootParents)
        {
            if (byParent.TryGetValue(parentId, out var children))
            {
                foreach (var child in children)
                {
                    var node = BuildCoverageNode(child, language, byParent);
                    if (node != null)
                        result.Add(node);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Gets content where translations are outdated compared to the master language.
    /// </summary>
    public List<StaleTranslationDto> GetStaleTranslations(int thresholdDays = 30, string? language = null)
    {
        var records = _repository.GetAll()
            .Where(r => r.StalestTranslationDays >= thresholdDays)
            .ToList();

        var result = new List<StaleTranslationDto>();

        foreach (var record in records)
        {
            var details = ParseLanguageDetails(record.LanguageDetailsJson);
            var masterDetail = details.FirstOrDefault(d =>
                string.Equals(d.Lang, record.MasterLanguage, StringComparison.OrdinalIgnoreCase));

            if (masterDetail == null || !DateTime.TryParse(masterDetail.LastModified, out var masterDate))
                continue;

            foreach (var detail in details)
            {
                if (string.Equals(detail.Lang, record.MasterLanguage, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (language != null && !string.Equals(detail.Lang, language, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!DateTime.TryParse(detail.LastModified, out var otherDate))
                    continue;

                var daysBehind = (int)(masterDate - otherDate).TotalDays;
                if (daysBehind >= thresholdDays)
                {
                    result.Add(new StaleTranslationDto
                    {
                        ContentId = record.ContentId,
                        ContentName = record.ContentName,
                        ContentTypeName = record.ContentTypeName,
                        Breadcrumb = record.Breadcrumb,
                        MasterLanguage = record.MasterLanguage,
                        MasterLastModified = masterDate,
                        OtherLanguage = detail.Lang,
                        OtherLastModified = otherDate,
                        DaysBehind = daysBehind,
                        EditUrl = record.EditUrl
                    });
                }
            }
        }

        return result.OrderByDescending(r => r.DaysBehind).ToList();
    }

    /// <summary>
    /// Gets a prioritized translation queue: content needing translation, sorted by most recently updated master.
    /// </summary>
    public TranslationQueueResultDto GetTranslationQueue(
        string targetLanguage,
        string? contentType = null,
        int page = 1,
        int pageSize = 50)
    {
        var records = _repository.GetAll().ToList();

        // Filter to content missing the target language
        var missing = records.Where(r =>
        {
            var langs = r.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim());
            return !langs.Any(l => string.Equals(l, targetLanguage, StringComparison.OrdinalIgnoreCase));
        });

        if (!string.IsNullOrEmpty(contentType))
        {
            missing = missing.Where(r =>
                string.Equals(r.ContentTypeName, contentType, StringComparison.OrdinalIgnoreCase));
        }

        var items = missing.Select(r =>
        {
            var details = ParseLanguageDetails(r.LanguageDetailsJson);
            var masterDetail = details.FirstOrDefault(d =>
                string.Equals(d.Lang, r.MasterLanguage, StringComparison.OrdinalIgnoreCase));
            DateTime.TryParse(masterDetail?.LastModified, out var masterModified);

            return new TranslationQueueItemDto
            {
                ContentId = r.ContentId,
                ContentName = r.ContentName,
                ContentTypeName = r.ContentTypeName,
                Breadcrumb = r.Breadcrumb,
                MasterLanguage = r.MasterLanguage,
                MasterLastModified = masterModified,
                AvailableLanguages = r.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim()).ToList(),
                EditUrl = r.EditUrl
            };
        })
        .OrderByDescending(r => r.MasterLastModified)
        .ToList();

        var totalCount = items.Count;
        var paged = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new TranslationQueueResultDto
        {
            Items = paged,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    /// <summary>
    /// Gets all items for the translation queue (for CSV export).
    /// </summary>
    public List<TranslationQueueItemDto> ExportTranslationQueue(string targetLanguage)
    {
        var result = GetTranslationQueue(targetLanguage, page: 1, pageSize: int.MaxValue);
        return result.Items;
    }

    private LanguageCoverageNodeDto? BuildCoverageNode(
        LanguageAuditRecord record,
        string language,
        Dictionary<int, List<LanguageAuditRecord>> byParent)
    {
        var langs = record.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim()).ToList();
        var hasLanguage = langs.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase));

        var children = new List<LanguageCoverageNodeDto>();
        if (byParent.TryGetValue(record.ContentId, out var childRecords))
        {
            foreach (var child in childRecords)
            {
                var childNode = BuildCoverageNode(child, language, byParent);
                if (childNode != null)
                    children.Add(childNode);
            }
        }

        // Count total descendants and how many have the target language
        var totalDescendants = CountDescendants(record.ContentId, byParent);
        var withLanguageCount = CountDescendantsWithLanguage(record.ContentId, language, byParent);

        return new LanguageCoverageNodeDto
        {
            ContentId = record.ContentId,
            ContentName = record.ContentName,
            HasLanguage = hasLanguage,
            TotalChildren = totalDescendants,
            ChildrenWithLanguage = withLanguageCount,
            CoveragePercent = totalDescendants > 0 ? Math.Round(100.0 * withLanguageCount / totalDescendants, 1) : (hasLanguage ? 100 : 0),
            Children = children.Count > 0 ? children : null
        };
    }

    private int CountDescendants(int contentId, Dictionary<int, List<LanguageAuditRecord>> byParent)
    {
        if (!byParent.TryGetValue(contentId, out var children))
            return 0;

        var count = children.Count;
        foreach (var child in children)
            count += CountDescendants(child.ContentId, byParent);
        return count;
    }

    private int CountDescendantsWithLanguage(int contentId, string language,
        Dictionary<int, List<LanguageAuditRecord>> byParent)
    {
        if (!byParent.TryGetValue(contentId, out var children))
            return 0;

        var count = 0;
        foreach (var child in children)
        {
            var langs = child.AvailableLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim());
            if (langs.Any(l => string.Equals(l, language, StringComparison.OrdinalIgnoreCase)))
                count++;
            count += CountDescendantsWithLanguage(child.ContentId, language, byParent);
        }
        return count;
    }

    private static List<LanguageDetailParsed> ParseLanguageDetails(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<LanguageDetailParsed>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private class LanguageDetailParsed
    {
        public string Lang { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LastModified { get; set; } = string.Empty;
    }
}
