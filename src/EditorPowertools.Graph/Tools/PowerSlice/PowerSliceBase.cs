using EPiServer.Cms.Shell.UI.Rest.ContentQuery;
using EPiServer.Framework.Localization;
using EPiServer.Security;
using Microsoft.AspNetCore.Http;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;

/// <summary>Base for Power Slice lists. Subclass and register as <c>IContentQuery</c> to add a slice.</summary>
/// <remarks>
/// A slice lists content immediately; the widget's filter box narrows what is already listed. Access is
/// gated three ways: the store the grid calls is behind shell authentication, the slice returns nothing
/// unless <see cref="GraphFeatureAccessChecker.HasUiAccess"/> allows the caller, and every item is
/// re-checked for read access (the slices read the version store directly, which ignores who is asking).
/// </remarks>
public abstract class PowerSliceBase : ContentQueryBase
{
    public const string PlugInArea = "/editorpowertools/graph/powerslice";

    protected PowerSliceBase(
        IContentRepository contentRepository,
        IContentQueryHelper queryHelper,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http,
        LocalizationService localization)
        : base(contentRepository, queryHelper)
    {
        Access = access;
        Http = http;
        Loc = localization;
    }

    protected GraphFeatureAccessChecker Access { get; }

    protected IHttpContextAccessor Http { get; }

    protected LocalizationService Loc { get; }

    public override IEnumerable<string> PlugInAreas => new[] { PlugInArea };

    /// <summary>Localization path for the display name, e.g. <c>/editorpowertools/graph/powerslice/slices/mycontent</c>.</summary>
    protected abstract string DisplayNamePath { get; }

    public override string DisplayName => Loc.GetString(DisplayNamePath);

    /// <summary>How this slice can be ordered, best first. The widget selects the first when the slice opens.</summary>
    public virtual IReadOnlyList<SliceSortOption> SortOptions => new[] { SliceSortOption.ByName, SliceSortOption.Newest };

    /// <summary>Whether the current caller may use Power Slice at all.</summary>
    protected bool HasSliceAccess() =>
        Access.HasUiAccess(Http.HttpContext, nameof(GraphFeatureToggles.PowerSlice), EditorPowertoolsGraphPermissions.PowerSlice);

    /// <summary>The signed-in user name, or null when anonymous.</summary>
    protected string? CurrentUserName
    {
        get
        {
            var name = Http.HttpContext?.User?.Identity?.Name;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
    }

    protected override IEnumerable<IContent> Filter(IEnumerable<IContent> items, ContentQueryParameters parameters)
    {
        if (!HasSliceAccess())
        {
            return Array.Empty<IContent>();
        }

        var phrase = parameters?.AllParameters?["q"];
        if (!string.IsNullOrWhiteSpace(phrase))
        {
            items = items.Where(item => item.Name?.Contains(phrase, StringComparison.OrdinalIgnoreCase) == true);
        }

        items = items.Where(HasReadAccess);

        return base.Filter(items, parameters!);
    }

    protected override IEnumerable<IContent> Sort(IEnumerable<IContent> items, ContentQueryParameters parameters)
    {
        var sortBy = parameters?.AllParameters?["sortBy"];

        // No explicit choice: keep the order the slice built (newest-first window, oldest-first window, ...).
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return items;
        }

        return ApplySort(items, sortBy) ?? items;
    }

    /// <summary>Orders by one of <see cref="SortOptions"/>; null for an unknown key (list keeps its order).</summary>
    protected virtual IEnumerable<IContent>? ApplySort(IEnumerable<IContent> items, string sortBy) =>
        sortBy switch
        {
            SliceSortOption.Name => items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase),
            SliceSortOption.Changed => items.OrderByDescending(SavedDate),
            SliceSortOption.ChangedAsc => items.OrderBy(SavedDate),
            SliceSortOption.Published => items.OrderByDescending(PublishedDate),
            _ => null
        };

    protected static DateTime SavedDate(IContent content) =>
        content is IChangeTrackable trackable ? trackable.Saved : DateTime.MinValue;

    protected static DateTime PublishedDate(IContent content) =>
        content is IVersionable versionable && versionable.StartPublish.HasValue
            ? versionable.StartPublish.Value
            : DateTime.MinValue;

    private bool HasReadAccess(IContent content) =>
        content is not ISecurable securable
        || securable.GetSecurityDescriptor().HasAccess(Http.HttpContext?.User, AccessLevel.Read);
}
