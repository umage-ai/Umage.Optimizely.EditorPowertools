using System.Text.Json;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Services.Analyzers;

/// <summary>
/// Pre-aggregates the live sections of the Content Statistics dashboard
/// (creation-by-month, stale content, top editors, average versions per item) during the
/// unified content analysis job's single pass. Without this, GetDashboard would re-scan
/// every descendant on every page load — three times — plus a per-item version-list call.
/// </summary>
public class ContentDashboardAnalyzer : IContentAnalyzer
{
    private readonly IContentVersionRepository _versionRepository;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly ContentDashboardSnapshotRepository _snapshotRepository;
    private readonly ILogger<ContentDashboardAnalyzer> _logger;

    private DateTime _creationCutoff;
    private Dictionary<string, int> _creationByMonth = new();
    private List<StaleContentSnapshotItem> _staleCandidates = new();
    private Dictionary<string, EditorAccumulator> _editorStats = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, ContentType> _contentTypeMap = new();
    private long _totalVersions;
    private int _versionedItems;

    public string Name => "Content Statistics Dashboard";

    public ContentDashboardAnalyzer(
        IContentVersionRepository versionRepository,
        IContentTypeRepository contentTypeRepository,
        ContentDashboardSnapshotRepository snapshotRepository,
        ILogger<ContentDashboardAnalyzer> logger)
    {
        _versionRepository = versionRepository;
        _contentTypeRepository = contentTypeRepository;
        _snapshotRepository = snapshotRepository;
        _logger = logger;
    }

    public void Initialize()
    {
        _creationCutoff = DateTime.UtcNow.AddMonths(-12);
        _creationByMonth = new Dictionary<string, int>();
        for (var i = 11; i >= 0; i--)
            _creationByMonth[DateTime.UtcNow.AddMonths(-i).ToString("yyyy-MM")] = 0;

        _staleCandidates = new List<StaleContentSnapshotItem>();
        _editorStats = new Dictionary<string, EditorAccumulator>(StringComparer.OrdinalIgnoreCase);
        _contentTypeMap = _contentTypeRepository.List().ToDictionary(ct => ct.ID);
        _totalVersions = 0;
        _versionedItems = 0;
    }

    public void Analyze(IContent content, ContentReference contentRef)
    {
        // Creation by month
        if (content is IChangeTrackable trackable && trackable.Created >= _creationCutoff)
        {
            var key = trackable.Created.ToString("yyyy-MM");
            if (_creationByMonth.ContainsKey(key))
                _creationByMonth[key]++;
        }

        // Stale page candidates (pages only)
        if (content is PageData)
        {
            var lastModified = (content as IChangeTrackable)?.Changed ?? DateTime.MinValue;
            var ct = _contentTypeMap.GetValueOrDefault(content.ContentTypeID);
            _staleCandidates.Add(new StaleContentSnapshotItem
            {
                ContentId = contentRef.ID,
                Name = content.Name ?? "[No name]",
                ContentTypeName = ct?.DisplayName ?? ct?.Name ?? "Unknown",
                LastModified = lastModified
            });
        }

        // Versions: drive both editor activity and average-versions metric
        try
        {
            var versions = _versionRepository.List(contentRef).ToList();
            _totalVersions += versions.Count;
            _versionedItems++;

            foreach (var version in versions)
            {
                var user = version.SavedBy;
                if (string.IsNullOrEmpty(user)) continue;

                if (!_editorStats.TryGetValue(user, out var acc))
                {
                    acc = new EditorAccumulator();
                    _editorStats[user] = acc;
                }
                acc.EditCount++;
                if (version.Status == VersionStatus.Published) acc.PublishCount++;
                if (version.Saved > acc.LastActive) acc.LastActive = version.Saved;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not list versions for {ContentRef}", contentRef);
        }
    }

    public void Complete()
    {
        var stale = _staleCandidates
            .OrderBy(i => i.LastModified)
            .Take(20)
            .ToList();

        var topEditors = _editorStats
            .OrderByDescending(kv => kv.Value.EditCount)
            .Take(10)
            .Select(kv => new EditorActivitySnapshotItem
            {
                Username = kv.Key,
                EditCount = kv.Value.EditCount,
                PublishCount = kv.Value.PublishCount,
                LastActive = kv.Value.LastActive == DateTime.MinValue ? null : kv.Value.LastActive
            })
            .ToList();

        var averageVersions = _versionedItems > 0
            ? Math.Round((double)_totalVersions / _versionedItems, 1)
            : 0;

        try
        {
            _snapshotRepository.Save(new ContentDashboardSnapshotRecord
            {
                LastUpdated = DateTime.UtcNow,
                AverageVersionsPerItem = averageVersions,
                CreationByMonthJson = JsonSerializer.Serialize(_creationByMonth),
                StaleContentJson = JsonSerializer.Serialize(stale),
                TopEditorsJson = JsonSerializer.Serialize(topEditors)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save content dashboard snapshot.");
        }
    }

    private class EditorAccumulator
    {
        public int EditCount;
        public int PublishCount;
        public DateTime LastActive = DateTime.MinValue;
    }
}
