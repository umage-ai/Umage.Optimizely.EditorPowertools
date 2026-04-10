using System.Text.Json;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Localization;
using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.LanguageAudit;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Services.Analyzers;

/// <summary>
/// Analyzer that collects language version data for each content item.
/// Records which languages exist, their publish status, and last modified dates.
/// </summary>
public class LanguageAuditAnalyzer : IContentAnalyzer
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentVersionRepository _contentVersionRepository;
    private readonly ILanguageBranchRepository _languageBranchRepository;
    private readonly LanguageAuditRepository _repository;
    private readonly LocalizationService _localizationService;
    private readonly ILogger<LanguageAuditAnalyzer> _logger;

    private HashSet<string> _enabledLanguages = new(StringComparer.OrdinalIgnoreCase);

    public string Name => _localizationService.GetString("/editorpowertools/analyzers/languageaudit");

    public LanguageAuditAnalyzer(
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IContentVersionRepository contentVersionRepository,
        ILanguageBranchRepository languageBranchRepository,
        LanguageAuditRepository repository,
        LocalizationService localizationService,
        ILogger<LanguageAuditAnalyzer> logger)
    {
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _contentVersionRepository = contentVersionRepository;
        _languageBranchRepository = languageBranchRepository;
        _repository = repository;
        _localizationService = localizationService;
        _logger = logger;
    }

    public void Initialize()
    {
        // Cache enabled languages
        _enabledLanguages = _languageBranchRepository.ListEnabled()
            .Select(lb => lb.LanguageID)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Clear old data
        _repository.Clear();
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        // Only analyze localizable content
        if (content is not ILocalizable localizable)
            return;

        try
        {
            var contentType = _contentTypeRepository.Load(content.ContentTypeID);
            var contentTypeName = contentType?.DisplayName ?? contentType?.Name;
            var masterLanguage = localizable.MasterLanguage?.Name ?? string.Empty;
            var breadcrumb = content.GetBreadcrumb();
            var language = localizable.Language?.Name ?? masterLanguage;
            var editUrl = $"{Paths.ToResource("CMS", "")}#/content/{contentRef.ID}/language/{language}";

            // Get all versions for this content item
            var versions = _contentVersionRepository.List(contentRef).ToList();

            // Group by language — find the most recent version per language
            var languageDetails = new List<LanguageDetailEntry>();
            var languageGroups = versions.GroupBy(v => v.LanguageBranch ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

            foreach (var group in languageGroups)
            {
                var lang = group.Key;
                if (string.IsNullOrEmpty(lang))
                    continue;

                var latestVersion = group.OrderByDescending(v => v.Saved).First();
                var isPublished = group.Any(v => v.Status == VersionStatus.Published);

                languageDetails.Add(new LanguageDetailEntry
                {
                    Lang = lang,
                    Status = isPublished ? "Published" : latestVersion.Status.ToString(),
                    LastModified = latestVersion.Saved.ToString("o")
                });
            }

            var availableLanguages = languageDetails.Select(d => d.Lang).ToList();

            // Determine missing translations — compare against enabled site languages
            var missingLanguages = _enabledLanguages.Except(availableLanguages, StringComparer.OrdinalIgnoreCase).ToList();
            var isMissingTranslations = missingLanguages.Count > 0;

            // Calculate stalest translation days
            var stalestDays = 0;
            var masterDetail = languageDetails.FirstOrDefault(d =>
                string.Equals(d.Lang, masterLanguage, StringComparison.OrdinalIgnoreCase));
            if (masterDetail != null && DateTime.TryParse(masterDetail.LastModified, out var masterDate))
            {
                foreach (var detail in languageDetails)
                {
                    if (string.Equals(detail.Lang, masterLanguage, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (DateTime.TryParse(detail.LastModified, out var otherDate))
                    {
                        var daysBehind = (int)(masterDate - otherDate).TotalDays;
                        if (daysBehind > stalestDays)
                            stalestDays = daysBehind;
                    }
                }
            }

            var record = new LanguageAuditRecord
            {
                ContentId = contentRef.ID,
                ContentName = content.Name,
                ContentTypeName = contentTypeName,
                Breadcrumb = breadcrumb,
                ParentContentId = content.ParentLink?.ID ?? 0,
                MasterLanguage = masterLanguage,
                AvailableLanguages = string.Join(",", availableLanguages),
                LanguageDetailsJson = JsonSerializer.Serialize(languageDetails),
                IsMissingTranslations = isMissingTranslations,
                StalestTranslationDays = stalestDays,
                EditUrl = editUrl
            };

            _repository.Save(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing language versions for {ContentLink}", contentRef);
        }
    }

    public void Complete()
    {
        // Nothing needed - saves happen during Analyze
    }

    private class LanguageDetailEntry
    {
        public string Lang { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LastModified { get; set; } = string.Empty;
    }
}
