namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

/// <summary>Shared version-store helpers so the slices agree on what "recent" means.</summary>
internal static class SliceVersionHelper
{
    internal static readonly VersionStatus[] AllStatuses =
    [
        VersionStatus.Published,
        VersionStatus.PreviouslyPublished,
        VersionStatus.CheckedIn,
        VersionStatus.CheckedOut,
        VersionStatus.AwaitingApproval,
        VersionStatus.Rejected,
        VersionStatus.DelayedPublish
    ];

    /// <summary>
    /// A version filter over CMS content. <c>ProviderName</c> is pinned to the empty string (the default
    /// provider key): left unset, the repository shares the row budget across providers in an unstable
    /// order, which silently empties slices when another provider (for example Commerce) is installed.
    /// </summary>
    internal static VersionFilter Window(VersionStatus[] statuses, bool newestFirst, string? changedBy = null) => new()
    {
        Statuses = statuses,
        ChangedBy = changedBy,
        ProviderName = string.Empty,
        ExcludeDeleted = true,
        SortColumn = VersionSortColumn.Saved,
        SortDirection = newestFirst ? VersionSortDirection.Descending : VersionSortDirection.Ascending
    };

    internal static IEnumerable<(IContent Content, DateTime Saved)> RecentlySaved(
        IContentVersionRepository versions, IContentLoader loader, int maxVersions, string? changedBy = null) =>
        Load(versions, loader, Window(AllStatuses, true, changedBy), maxVersions);

    internal static IEnumerable<(IContent Content, DateTime Saved)> LeastRecentlySaved(
        IContentVersionRepository versions, IContentLoader loader, int maxVersions) =>
        Load(versions, loader, Window(AllStatuses, false), maxVersions);

    /// <summary>Drops the CMS plumbing: folders, the root and the wastebasket.</summary>
    private static bool IsEditorialContent(IContent content) =>
        content is not ContentFolder
        && !ContentReference.IsNullOrEmpty(content.ContentLink)
        && content.ContentLink.ID != ContentReference.RootPage.ID
        && content.ContentLink.ID != ContentReference.WasteBasket.ID;

    internal static IEnumerable<(IContent Content, DateTime Saved)> Load(
        IContentVersionRepository versions, IContentLoader loader, VersionFilter filter, int maxVersions)
    {
        var rows = versions.List(filter, 0, maxVersions, out int _);
        var seen = new HashSet<int>();

        foreach (var version in rows)
        {
            // One row per language version; a slice lists content, so the first one wins.
            if (!seen.Add(version.ContentLink.ID))
            {
                continue;
            }

            if (loader.TryGet(version.ContentLink.ToReferenceWithoutVersion(), out IContent? content)
                && content is not null
                && IsEditorialContent(content))
            {
                yield return (content, version.Saved);
            }
        }
    }
}
