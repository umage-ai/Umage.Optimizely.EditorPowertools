using System.Globalization;
using EPiServer.Framework.Localization;
using EPiServer.Globalization;
using EPiServer.Security;
using EPiServer.Shell.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>
/// Shared behaviour of the Graph-backed CMS UI search providers. A concrete provider only supplies
/// its area, category, icon, content-type scope and draft scope check.
/// </summary>
/// <remarks>
/// Access rights on published content are enforced by Graph (queries run as the signed-in editor) and
/// re-checked here against the CMS. Drafts come from the version store, which Graph never sees.
/// Any failure degrades to "no results" so the edit UI and the other providers keep working.
/// </remarks>
public abstract class GraphSearchProviderBase : ISearchProvider
{
    // Drafts are filtered by scope/access/roots only after they come back, so ask for more than is shown.
    private const int DraftCandidateMultiplier = 8;

    private readonly IGraphSearchService _search;
    private readonly IGraphAvailability _availability;
    private readonly DraftSearch _drafts;
    private readonly SearchResultFactory _results;
    private readonly GraphFeatureAccessChecker _access;
    private readonly IHttpContextAccessor _http;
    private readonly IContentLoader _loader;
    private readonly IContentRepository _repository;
    private readonly IOptions<EditorPowertoolsGraphOptions> _options;
    private readonly LocalizationService _localization;
    private readonly ILogger _logger;

    protected GraphSearchProviderBase(
        IGraphSearchService search,
        IGraphAvailability availability,
        DraftSearch drafts,
        SearchResultFactory results,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http,
        IContentLoader loader,
        IContentRepository repository,
        IOptions<EditorPowertoolsGraphOptions> options,
        LocalizationService localization,
        ILogger logger)
    {
        _search = search;
        _availability = availability;
        _drafts = drafts;
        _results = results;
        _access = access;
        _http = http;
        _loader = loader;
        _repository = repository;
        _options = options;
        _localization = localization;
        _logger = logger;
    }

    /// <summary>The area spelled exactly as the built-in CMS provider spells it (e.g. <c>CMS/pages</c>).</summary>
    protected abstract string BuiltInArea { get; }

    /// <summary>Localization path of the category label.</summary>
    protected abstract string CategoryPath { get; }

    protected abstract string CategoryFallback { get; }

    protected abstract string IconCssClass { get; }

    protected abstract IReadOnlyList<string> ScopeTypes { get; }

    protected abstract bool FilterByLocale { get; }

    /// <summary>True when a draft belongs in this provider's area.</summary>
    protected abstract bool IsInScope(IContent content);

    /// <summary>Roots to use when the caller named none (e.g. shared blocks only). Default: none.</summary>
    protected virtual IReadOnlySet<ContentReference> DefaultRoots() => new HashSet<ContentReference>();

    /// <summary>
    /// Shell areas are matched case-insensitively, so by default we use a lower-case spelling that
    /// joins the built-in provider's area without replacing it. With
    /// <see cref="GraphSearchOptions.DisableBuiltInProviders"/> on and Graph available we use the
    /// built-in spelling: the platform's provider manager (CMS 12 and 13) drops the built-in providers
    /// (<c>ISortable.SortOrder == int.MinValue</c>) when another provider registers the identical area
    /// string. When Graph is unavailable the built-ins come back automatically.
    /// </summary>
    public string Area => ReplacesBuiltIn() ? BuiltInArea : BuiltInArea.ToLowerInvariant();

    public string Category => _localization.GetStringByCulture(
        CategoryPath, CategoryFallback, CultureInfo.CurrentUICulture);

#if OPTIMIZELY_CMS13
    /// <summary>Unique, dot-free provider key (CMS 13 <c>ISearchProvider.Key</c>).</summary>
    public string Key => "editorpowertools-graph-" + KeySuffix;

    protected abstract string KeySuffix { get; }
#endif

    private bool ReplacesBuiltIn()
    {
        try
        {
            return _options.Value.Search.DisableBuiltInProviders
                && _access.IsFeatureEnabled(nameof(GraphFeatureToggles.SearchProviders))
                && _availability.IsAvailable;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<SearchResult> Search(Query query)
    {
        if (query is null || string.IsNullOrWhiteSpace(query.SearchQuery))
        {
            return Array.Empty<SearchResult>();
        }

        try
        {
            if (!_access.HasUiAccess(_http.HttpContext, nameof(GraphFeatureToggles.SearchProviders),
                    EditorPowertoolsGraphPermissions.SearchProviders))
            {
                return Array.Empty<SearchResult>();
            }

            if (!_availability.IsAvailable)
            {
                return Array.Empty<SearchResult>();
            }

            var options = _options.Value.Search;
            var maxResults = Math.Max(query.MaxResults, 1);
            var language = ResolveLanguage(query);
            var graphLanguage = FilterByLocale && query.FilterOnCulture ? language : null;

            var explicitRoots = SearchRoots.Parse(query.SearchRoots);
            var roots = explicitRoots.Count > 0 ? explicitRoots : DefaultRoots();
            var types = _search.ValidateContentTypes(ScopeTypes);

            var hits = Query(query.SearchQuery, types, roots, graphLanguage, maxResults, options);

            // A root Graph cannot express yields nothing; retry unconstrained. Every hit is still checked
            // against the roots afterwards, so this widens what is considered, never what is returned.
            if (hits.Count == 0 && explicitRoots.Count > 0
                && SearchRoots.ToGuids(explicitRoots, _loader).Count > 0)
            {
                hits = Query(query.SearchQuery, types, new HashSet<ContentReference>(), graphLanguage, maxResults, options);
            }

            var published = BuildPublished(hits, roots, language);

            var drafts = options.IncludeDrafts
                ? FindDrafts(query.SearchQuery, hits, Math.Max(1, maxResults / 3), roots, graphLanguage, language)
                : new List<SearchResult>();

            return drafts.Concat(published).Take(maxResults).ToList();
        }
        catch (Exception ex)
        {
            // A failing search must not take the edit UI down. Log; never surface the message.
            _logger.LogError(ex, "Graph editor search failed for area {Area}.", Area);
            return Array.Empty<SearchResult>();
        }
    }

    private IReadOnlyList<GraphSearchHit> Query(
        string phrase, IReadOnlyList<string> types, IReadOnlySet<ContentReference> roots,
        string? language, int maxResults, GraphSearchOptions options)
    {
        var guids = SearchRoots.ToGuids(roots, _loader);
        var limit = guids.Count > 0
            ? Math.Min(Math.Min(maxResults * Math.Max(1, options.OverfetchFactor), Math.Max(1, options.MaxOverfetch)), GraphSearchService.MaxLimit)
            : maxResults;

        var scope = new GraphSearchScope(types, guids, FilterByLocale);
        var request = new GraphSearchRequest(phrase, scope, language, GraphSearchFilters.None, limit, 0, false);

        // The provider interface is synchronous; ASP.NET Core has no sync context to deadlock on.
        return _search.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult().Hits;
    }

    private List<SearchResult> BuildPublished(
        IReadOnlyList<GraphSearchHit> hits, IReadOnlySet<ContentReference> roots, string? language)
    {
        var list = new List<SearchResult>();
        foreach (var hit in hits)
        {
            var content = Load(hit);
            if (content is null
                || !content.QueryDistinctAccess(AccessLevel.Read)
                || !SearchRoots.Contains(content, roots, _loader))
            {
                continue;
            }

            list.Add(_results.Create(content, false, language, IconCssClass));
        }

        return list;
    }

    private IContent? Load(GraphSearchHit hit)
    {
        if (!_repository.TryGet<IContent>(hit.ContentGuid, out var content) || content is null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(hit.Language))
        {
            try
            {
                var selector = new LanguageSelector(hit.Language);
                if (_loader.TryGet<IContent>(content.ContentLink.ToReferenceWithoutVersion(), selector, out var branch)
                    && branch is not null)
                {
                    return branch;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or CultureNotFoundException)
            {
                // Unknown language tag from the index: fall back to the language-agnostic load.
            }
        }

        return content;
    }

    private List<SearchResult> FindDrafts(
        string phrase, IReadOnlyList<GraphSearchHit> hits, int budget, IReadOnlySet<ContentReference> roots,
        string? draftLanguage, string? language)
    {
        var found = new HashSet<int>();
        foreach (var hit in hits)
        {
            if (_repository.TryGet<IContent>(hit.ContentGuid, out var indexed) && indexed is not null)
            {
                found.Add(indexed.ContentLink.ID);
            }
        }

        var candidates = _drafts.FindDrafts(phrase, draftLanguage, budget * DraftCandidateMultiplier, found);
        var list = new List<SearchResult>();
        foreach (var reference in candidates)
        {
            if (!_loader.TryGet<IContent>(reference, out var content) || content is null)
            {
                continue;
            }

            // Drafts never went through Graph, so access is checked here.
            if (!content.QueryDistinctAccess(AccessLevel.Read)
                || !IsInScope(content)
                || !SearchRoots.Contains(content, roots, _loader))
            {
                continue;
            }

            list.Add(_results.Create(content, true, language, IconCssClass));
            if (list.Count >= budget)
            {
                break;
            }
        }

        return list;
    }

    /// <summary>The editor's preferred content language (static platform accessor; overridable for tests).</summary>
    protected virtual string? PreferredLanguage() => ContentLanguage.PreferredCulture?.Name;

    private string? ResolveLanguage(Query query)
    {
        if (query.Parameters is not null
            && query.Parameters.TryGetValue("language", out var value)
            && value is string name
            && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return PreferredLanguage();
    }
}
