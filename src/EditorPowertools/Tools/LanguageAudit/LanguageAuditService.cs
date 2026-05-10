using System.Text.Json;
using UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit.Models;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit;

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
        var enabledLanguages = _languageBranchRepository.ListEnabled()
            .Select(lb => lb.LanguageID)
            .ToList();

        // One pass: parse each record's languages + JSON details exactly once.
        var enriched = LoadEnriched();
        var totalContent = enriched.Count;

        var languageStats = new List<LanguageStatDto>(enabledLanguages.Count);
        foreach (var lang in enabledLanguages)
        {
            var withLangCount = 0;
            var publishedCount = 0;
            foreach (var item in enriched)
            {
                if (!item.LanguageSet.Contains(lang)) continue;
                withLangCount++;
                if (item.PublishedLanguageSet.Contains(lang)) publishedCount++;
            }

            languageStats.Add(new LanguageStatDto
            {
                Language = lang,
                TotalContent = withLangCount,
                PublishedCount = publishedCount,
                CoveragePercent = totalContent > 0 ? Math.Round(100.0 * withLangCount / totalContent, 1) : 0
            });
        }

        var missingCount = 0;
        var staleCount = 0;
        foreach (var item in enriched)
        {
            if (item.Record.IsMissingTranslations) missingCount++;
            if (item.Record.StalestTranslationDays > 30) staleCount++;
        }

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
        var enriched = LoadEnriched();
        var result = new List<MissingTranslationDto>();

        foreach (var item in enriched)
        {
            if (parentId.HasValue && item.Record.ParentContentId != parentId.Value) continue;
            if (item.LanguageSet.Contains(language)) continue;

            var r = item.Record;
            result.Add(new MissingTranslationDto
            {
                ContentId = r.ContentId,
                ContentName = r.ContentName,
                ContentTypeName = r.ContentTypeName,
                Breadcrumb = r.Breadcrumb,
                MasterLanguage = r.MasterLanguage,
                AvailableLanguages = item.LanguageList,
                EditUrl = r.EditUrl
            });
        }

        result.Sort((a, b) => string.Compare(a.Breadcrumb, b.Breadcrumb, StringComparison.Ordinal));
        return result;
    }

    /// <summary>
    /// Gets content tree nodes with coverage stats per language (for hierarchical view).
    /// One bottom-up pass — descendant totals are accumulated as we recurse, so each
    /// subtree is visited exactly once instead of three times.
    /// </summary>
    public List<LanguageCoverageNodeDto> GetCoverageTree(string language)
    {
        var enriched = LoadEnriched();

        var byParent = new Dictionary<int, List<EnrichedRecord>>();
        foreach (var item in enriched)
        {
            if (!byParent.TryGetValue(item.Record.ParentContentId, out var list))
            {
                list = new List<EnrichedRecord>();
                byParent[item.Record.ParentContentId] = list;
            }
            list.Add(item);
        }

        var allIds = new HashSet<int>(enriched.Select(e => e.Record.ContentId));
        var rootParents = byParent.Keys.Where(pid => !allIds.Contains(pid)).OrderBy(p => p).ToList();

        var result = new List<LanguageCoverageNodeDto>();
        foreach (var parentId in rootParents)
        {
            if (!byParent.TryGetValue(parentId, out var children)) continue;
            foreach (var child in children)
            {
                var (node, _, _) = BuildCoverageNode(child, language, byParent);
                if (node != null) result.Add(node);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets content where translations are outdated compared to the master language.
    /// </summary>
    public List<StaleTranslationDto> GetStaleTranslations(int thresholdDays = 30, string? language = null)
    {
        var enriched = LoadEnriched();
        var result = new List<StaleTranslationDto>();

        foreach (var item in enriched)
        {
            var record = item.Record;
            if (record.StalestTranslationDays < thresholdDays) continue;

            var masterDetail = FindDetail(item.Details, record.MasterLanguage);
            if (masterDetail == null || !DateTime.TryParse(masterDetail.LastModified, out var masterDate))
                continue;

            foreach (var detail in item.Details)
            {
                if (string.Equals(detail.Lang, record.MasterLanguage, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (language != null && !string.Equals(detail.Lang, language, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!DateTime.TryParse(detail.LastModified, out var otherDate))
                    continue;

                var daysBehind = (int)(masterDate - otherDate).TotalDays;
                if (daysBehind < thresholdDays) continue;

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

        result.Sort((a, b) => b.DaysBehind.CompareTo(a.DaysBehind));
        return result;
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
        var enriched = LoadEnriched();
        var items = new List<TranslationQueueItemDto>();

        foreach (var item in enriched)
        {
            if (item.LanguageSet.Contains(targetLanguage)) continue;

            var r = item.Record;
            if (!string.IsNullOrEmpty(contentType) &&
                !string.Equals(r.ContentTypeName, contentType, StringComparison.OrdinalIgnoreCase))
                continue;

            var masterDetail = FindDetail(item.Details, r.MasterLanguage);
            DateTime.TryParse(masterDetail?.LastModified, out var masterModified);

            items.Add(new TranslationQueueItemDto
            {
                ContentId = r.ContentId,
                ContentName = r.ContentName,
                ContentTypeName = r.ContentTypeName,
                Breadcrumb = r.Breadcrumb,
                MasterLanguage = r.MasterLanguage,
                MasterLastModified = masterModified,
                AvailableLanguages = item.LanguageList,
                EditUrl = r.EditUrl
            });
        }

        items.Sort((a, b) => b.MasterLastModified.CompareTo(a.MasterLastModified));

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

    /// <summary>
    /// Returns (Node, totalDescendants, descendantsWithLanguage) so each subtree is visited once
    /// instead of being re-walked once per CountDescendants and once per CountDescendantsWithLanguage.
    /// </summary>
    private (LanguageCoverageNodeDto? Node, int Total, int WithLang) BuildCoverageNode(
        EnrichedRecord item,
        string language,
        Dictionary<int, List<EnrichedRecord>> byParent)
    {
        var hasLanguage = item.LanguageSet.Contains(language);
        var children = new List<LanguageCoverageNodeDto>();
        var totalDescendants = 0;
        var descendantsWithLanguage = 0;

        if (byParent.TryGetValue(item.Record.ContentId, out var childItems))
        {
            foreach (var child in childItems)
            {
                var (childNode, childTotal, childWith) = BuildCoverageNode(child, language, byParent);
                totalDescendants += 1 + childTotal;
                descendantsWithLanguage += (child.LanguageSet.Contains(language) ? 1 : 0) + childWith;
                if (childNode != null) children.Add(childNode);
            }
        }

        var node = new LanguageCoverageNodeDto
        {
            ContentId = item.Record.ContentId,
            ContentName = item.Record.ContentName,
            HasLanguage = hasLanguage,
            TotalChildren = totalDescendants,
            ChildrenWithLanguage = descendantsWithLanguage,
            CoveragePercent = totalDescendants > 0
                ? Math.Round(100.0 * descendantsWithLanguage / totalDescendants, 1)
                : (hasLanguage ? 100 : 0),
            Children = children.Count > 0 ? children : null
        };
        return (node, totalDescendants, descendantsWithLanguage);
    }

    /// <summary>
    /// Loads every record once and pre-computes the language set, the language list,
    /// the parsed language details, and the published-language set so callers don't
    /// re-split / re-deserialise the same fields on every iteration.
    /// </summary>
    private List<EnrichedRecord> LoadEnriched()
    {
        var records = _repository.GetAll();
        var result = new List<EnrichedRecord>();
        foreach (var r in records)
        {
            var langList = r.AvailableLanguages
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            var details = ParseLanguageDetails(r.LanguageDetailsJson);

            var publishedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in details)
            {
                if (string.Equals(d.Status, "Published", StringComparison.OrdinalIgnoreCase))
                    publishedSet.Add(d.Lang);
            }

            result.Add(new EnrichedRecord(
                r,
                langList,
                new HashSet<string>(langList, StringComparer.OrdinalIgnoreCase),
                publishedSet,
                details));
        }
        return result;
    }

    private static LanguageDetailParsed? FindDetail(List<LanguageDetailParsed> details, string lang)
    {
        foreach (var d in details)
        {
            if (string.Equals(d.Lang, lang, StringComparison.OrdinalIgnoreCase))
                return d;
        }
        return null;
    }

    private static List<LanguageDetailParsed> ParseLanguageDetails(string json)
    {
        if (string.IsNullOrEmpty(json) || json == "[]")
            return new List<LanguageDetailParsed>();
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

    private sealed record EnrichedRecord(
        LanguageAuditRecord Record,
        List<string> LanguageList,
        HashSet<string> LanguageSet,
        HashSet<string> PublishedLanguageSet,
        List<LanguageDetailParsed> Details);

    private sealed class LanguageDetailParsed
    {
        public string Lang { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LastModified { get; set; } = string.Empty;
    }
}
