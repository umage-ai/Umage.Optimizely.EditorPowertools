using System.Globalization;
using EPiServer.Core;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>
/// Finds unpublished content by name, covering what Graph structurally cannot (Graph only indexes
/// published content). Name match only, via the supported <see cref="IContentVersionRepository"/>.
/// </summary>
public class DraftSearch
{
    private static readonly VersionStatus[] DraftStatuses =
    [
        VersionStatus.CheckedOut,
        VersionStatus.CheckedIn,
        VersionStatus.Rejected,
        VersionStatus.DelayedPublish,
        VersionStatus.AwaitingApproval
    ];

    private readonly IContentVersionRepository _versions;
    private readonly ILogger<DraftSearch> _logger;

    public DraftSearch(IContentVersionRepository versions, ILogger<DraftSearch> logger)
    {
        _versions = versions ?? throw new ArgumentNullException(nameof(versions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Drafts whose name matches; excludes ids in alreadyFound; never throws.</summary>
    public virtual IReadOnlyList<ContentReference> FindDrafts(
        string phrase, string? language, int candidateLimit, ISet<int> alreadyFound)
    {
        if (string.IsNullOrWhiteSpace(phrase) || candidateLimit <= 0)
        {
            return Array.Empty<ContentReference>();
        }

        var filter = new VersionFilter
        {
            Name = phrase,
            Statuses = DraftStatuses,
            // Pinned to the default (CMS) provider: the empty string is its key. Leaving it unset
            // aggregates across providers with an unstable "first" provider taking the row budget.
            ProviderName = string.Empty,
            ExcludeDeleted = true,
            SortColumn = VersionSortColumn.Saved,
            SortDirection = VersionSortDirection.Descending
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(language))
            {
                filter.Languages = [CultureInfo.GetCultureInfo(language)];
            }

            // Over-fetch: de-duplication discards many rows (drafts of already-found content).
            var versions = _versions.List(filter, 0, candidateLimit * 4, out int _);

            var references = new List<ContentReference>();
            var seen = new HashSet<int>();
            foreach (var version in versions)
            {
                var id = version.ContentLink.ID;
                if (alreadyFound.Contains(id) || !seen.Add(id))
                {
                    continue;
                }

                references.Add(version.ContentLink.ToReferenceWithoutVersion());
                if (references.Count >= candidateLimit)
                {
                    break;
                }
            }

            return references;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Draft search failed for '{SearchPhrase}'.", phrase);
            return Array.Empty<ContentReference>();
        }
    }
}
