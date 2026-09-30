using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Framework.Localization;
using EPiServer.Shell.ContentQuery;
using EPiServer.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

/// <summary>Shared dependencies of the slices that read the version store.</summary>
public abstract class VersionBackedSlice : PowerSliceBase
{
    protected VersionBackedSlice(
        IContentRepository contentRepository,
        IContentQueryHelper queryHelper,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http,
        LocalizationService localization,
        IContentVersionRepository versions,
        IContentLoader loader,
        IOptions<EditorPowertoolsGraphOptions> options,
        TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization)
    {
        Versions = versions;
        Loader = loader;
        Options = options.Value.PowerSlice;
        Time = time;
    }

    protected IContentVersionRepository Versions { get; }

    protected IContentLoader Loader { get; }

    protected PowerSliceOptions Options { get; }

    protected TimeProvider Time { get; }

    protected DateTime Now => Time.GetLocalNow().DateTime;
}

/// <summary>Content the current editor last touched.</summary>
public class MyContentSlice : VersionBackedSlice
{
    public MyContentSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentVersionRepository versions,
        IContentLoader loader, IOptions<EditorPowertoolsGraphOptions> options, TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization, versions, loader, options, time)
    {
    }

    public override string Name => "ept-slice-mycontent";
    public override int SortOrder => 10;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/mycontent";

    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Newest, SliceSortOption.ByName, SliceSortOption.Oldest };

    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters)
    {
        var me = CurrentUserName;
        if (me is null)
        {
            return Array.Empty<IContent>();
        }

        // Matched by the version store: filtering a capped recent window in memory would miss this
        // editor's work on a busy site.
        return SliceVersionHelper.RecentlySaved(Versions, Loader, Options.MaxVersions, me).Select(x => x.Content);
    }
}

/// <summary>Content saved within the last <see cref="PowerSliceOptions.RecentlyChangedDays"/> days.</summary>
public class RecentlyChangedSlice : VersionBackedSlice
{
    public RecentlyChangedSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentVersionRepository versions,
        IContentLoader loader, IOptions<EditorPowertoolsGraphOptions> options, TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization, versions, loader, options, time)
    {
    }

    public override string Name => "ept-slice-recentlychanged";
    public override int SortOrder => 20;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/recentlychanged";

    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Newest, SliceSortOption.ByName };

    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters)
    {
        var cutoff = Now.AddDays(-Options.RecentlyChangedDays);
        return SliceVersionHelper.RecentlySaved(Versions, Loader, Options.MaxVersions)
            .Where(x => x.Saved >= cutoff)
            .Select(x => x.Content);
    }
}

/// <summary>Drafts waiting to be finished or published.</summary>
public class DraftsSlice : VersionBackedSlice
{
    private static readonly VersionStatus[] Statuses =
        { VersionStatus.CheckedOut, VersionStatus.CheckedIn, VersionStatus.Rejected };

    public DraftsSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentVersionRepository versions,
        IContentLoader loader, IOptions<EditorPowertoolsGraphOptions> options, TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization, versions, loader, options, time)
    {
    }

    public override string Name => "ept-slice-drafts";
    public override int SortOrder => 30;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/drafts";

    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Newest, SliceSortOption.Oldest, SliceSortOption.ByName };

    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters) =>
        SliceVersionHelper.Load(Versions, Loader, SliceVersionHelper.Window(Statuses, true), Options.MaxVersions)
            .Select(x => x.Content);
}

/// <summary>Content waiting for approval.</summary>
public class AwaitingApprovalSlice : VersionBackedSlice
{
    private static readonly VersionStatus[] Statuses = { VersionStatus.AwaitingApproval };

    public AwaitingApprovalSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentVersionRepository versions,
        IContentLoader loader, IOptions<EditorPowertoolsGraphOptions> options, TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization, versions, loader, options, time)
    {
    }

    public override string Name => "ept-slice-awaitingapproval";
    public override int SortOrder => 40;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/awaitingapproval";

    // Whoever has waited longest is first in line.
    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Oldest, SliceSortOption.Newest, SliceSortOption.ByName };

    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters) =>
        SliceVersionHelper.Load(Versions, Loader, SliceVersionHelper.Window(Statuses, false), Options.MaxVersions)
            .Select(x => x.Content);
}

/// <summary>Published pages nobody has touched in <see cref="PowerSliceOptions.StaleContentMonths"/> months, oldest first.</summary>
public class StaleContentSlice : VersionBackedSlice
{
    public StaleContentSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentVersionRepository versions,
        IContentLoader loader, IOptions<EditorPowertoolsGraphOptions> options, TimeProvider time)
        : base(contentRepository, queryHelper, access, http, localization, versions, loader, options, time)
    {
    }

    public override string Name => "ept-slice-stalecontent";
    public override int SortOrder => 50;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/stalecontent";

    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Oldest, SliceSortOption.ByName };

    // Asks the store for the oldest end of the history: filtering a newest-first window for old
    // content returns nothing once the window is capped.
    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters)
    {
        var cutoff = Now.AddMonths(-Options.StaleContentMonths);
        return SliceVersionHelper.LeastRecentlySaved(Versions, Loader, Options.MaxVersions)
            .Where(x => x.Saved <= cutoff)
            .Select(x => x.Content)
            // The oldest version row says nothing about the current content: a page with a recent
            // edit must not be listed just because its first version is old.
            .Where(c => c is PageData && c is IVersionable { Status: VersionStatus.Published }
                        && c is IChangeTrackable t && t.Saved <= cutoff);
    }
}

/// <summary>Shared blocks that nothing references.</summary>
/// <remarks>
/// There is no bulk "which of these are referenced" call, so this asks the reference store once per
/// block. The scan is capped by <see cref="PowerSliceOptions.MaxUnusedBlocksScanned"/> to bound that cost.
/// </remarks>
public class UnusedBlocksSlice : PowerSliceBase
{
    private readonly IContentLoader _loader;
    private readonly IContentRepository _repository;
    private readonly PowerSliceOptions _options;

    public UnusedBlocksSlice(
        IContentRepository contentRepository, IContentQueryHelper queryHelper, GraphFeatureAccessChecker access,
        IHttpContextAccessor http, LocalizationService localization, IContentLoader loader,
        IOptions<EditorPowertoolsGraphOptions> options)
        : base(contentRepository, queryHelper, access, http, localization)
    {
        _loader = loader;
        _repository = contentRepository;
        _options = options.Value.PowerSlice;
    }

    public override string Name => "ept-slice-unusedblocks";
    public override int SortOrder => 60;
    protected override string DisplayNamePath => "/editorpowertools/graph/powerslice/slices/unusedblocks";

    public override IReadOnlyList<SliceSortOption> SortOptions =>
        new[] { SliceSortOption.Oldest, SliceSortOption.ByName, SliceSortOption.Newest };

    /// <summary>Where the scan starts; overridable so tests do not need a site definition.</summary>
    protected virtual ContentReference ScanRoot => SiteDefinition.Current.GlobalAssetsRoot;

    protected override IEnumerable<IContent> GetContent(ContentQueryParameters parameters)
    {
        if (!HasSliceAccess())
        {
            return Array.Empty<IContent>();
        }

        var root = ScanRoot;
        if (ContentReference.IsNullOrEmpty(root))
        {
            return Array.Empty<IContent>();
        }

        var unused = new List<IContent>();
        var queue = new Queue<ContentReference>();
        queue.Enqueue(root);
        var scanned = 0;

        // Breadth-first so the cap samples the whole tree evenly rather than one deep branch.
        while (queue.Count > 0 && scanned < _options.MaxUnusedBlocksScanned)
        {
            foreach (var child in _loader.GetChildren<IContent>(queue.Dequeue()))
            {
                if (scanned >= _options.MaxUnusedBlocksScanned)
                {
                    break;
                }

                scanned++;
                queue.Enqueue(child.ContentLink);

                if (child is BlockData && !_repository.GetReferencesToContent(child.ContentLink, false).Any())
                {
                    unused.Add(child);
                }
            }
        }

        return unused;
    }
}
