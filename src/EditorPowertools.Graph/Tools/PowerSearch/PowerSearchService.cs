using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Editor;
using EPiServer.Framework.Localization;
using EPiServer.Globalization;
using EPiServer.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;

/// <summary>
/// Faceted editor search over Optimizely Graph. Graph answers "which content matches"; everything
/// shown to the editor is re-loaded through the CMS and access-checked, so stale or restricted
/// index entries never reach the page.
/// </summary>
public class PowerSearchService : IPowerSearchService
{
    private const int MaxTake = 50;
    private const int SectionFacetCap = 50;
    private const string BreadcrumbSeparator = " › ";
    private const string StatusPrefix = "/editorpowertools/graph/powersearch/status/";

    private static readonly PowerSearchTab[] AllTabs =
        { PowerSearchTab.Pages, PowerSearchTab.Blocks, PowerSearchTab.Media, PowerSearchTab.Other };

    private readonly IGraphSearchService _search;
    private readonly IGraphAvailability _availability;
    private readonly IContentTypeRepository _types;
    private readonly IContentRepository _repository;
    private readonly IContentLoader _loader;
    private readonly DraftSearch _drafts;
    private readonly LocalizationService _localization;
    private readonly IOptions<EditorPowertoolsGraphOptions> _options;
    private readonly ILogger<PowerSearchService> _logger;

    public PowerSearchService(
        IGraphSearchService search,
        IGraphAvailability availability,
        IContentTypeRepository types,
        IContentRepository repository,
        IContentLoader loader,
        DraftSearch drafts,
        LocalizationService localization,
        IOptions<EditorPowertoolsGraphOptions> options,
        ILogger<PowerSearchService> logger)
    {
        _search = search;
        _availability = availability;
        _types = types;
        _repository = repository;
        _loader = loader;
        _drafts = drafts;
        _localization = localization;
        _options = options;
        _logger = logger;
    }

    public async Task<PowerSearchResponseDto> SearchAsync(PowerSearchQuery query, CancellationToken ct)
    {
        var options = _options.Value.Search;
        var defaultTake = options.PowerSearchPageSize > 0 ? options.PowerSearchPageSize : 20;
        var take = Math.Clamp(query.Take <= 0 ? defaultTake : query.Take, 1, MaxTake);
        var skip = Math.Max(0, query.Skip);

        if (string.IsNullOrWhiteSpace(query.Q))
        {
            return Response(Array.Empty<PowerSearchHitDto>(), 0, null, PowerSearchFacetsDto.Empty, UnknownTabs(), "ok");
        }

        if (!_availability.IsAvailable)
        {
            return Response(Array.Empty<PowerSearchHitDto>(), 0, null, PowerSearchFacetsDto.Empty, UnknownTabs(), "unavailable");
        }

        var phrase = query.Q.Trim();
        var filters = new GraphSearchFilters(query.Types, query.Languages, query.Sections);
        var limit = Math.Min(take * Math.Max(1, options.OverfetchFactor), Math.Max(1, options.MaxOverfetch));

        GraphSearchResult active;
        var counts = new Dictionary<PowerSearchTab, int>();
        try
        {
            var calls = new Dictionary<PowerSearchTab, Task<GraphSearchResult>>();
            foreach (var tab in AllTabs)
            {
                var isActive = tab == query.Tab;
                calls[tab] = RunTab(
                    tab, phrase,
                    isActive ? filters : GraphSearchFilters.None,
                    isActive ? limit : 1,
                    isActive ? skip : 0,
                    ct);
            }

            try
            {
                await Task.WhenAll(calls.Values).ConfigureAwait(false);
            }
            catch
            {
                // Observe every task so none is left faulted and unobserved, then surface the first failure.
                foreach (var failed in calls.Values.Where(t => t.IsFaulted)) _ = failed.Exception;
                throw;
            }

            foreach (var entry in calls) counts[entry.Key] = entry.Value.Result.Total;
            active = calls[query.Tab].Result;
        }
        catch (GraphQueryException ex)
        {
            _logger.LogError(ex, "PowerSearch Graph query failed.");
            return Response(Array.Empty<PowerSearchHitDto>(), 0, null, PowerSearchFacetsDto.Empty, UnknownTabs(), "error");
        }

        // Graph work is done: from here on CMS loading is strictly sequential (shared SQL connection).
        var hits = new List<PowerSearchHitDto>();
        var seen = new HashSet<int>();

        if (options.IncludeDrafts && skip == 0)
        {
            foreach (var draft in LoadDrafts(phrase, query, take))
            {
                if (seen.Add(draft.ContentId)) hits.Add(draft);
            }
        }

        var consumed = 0;
        foreach (var raw in active.Hits)
        {
            if (hits.Count >= take) break;
            consumed++;

            var content = Load(raw);
            if (content is null || !content.QueryDistinctAccess(AccessLevel.Read)) continue;
            if (!seen.Add(content.ContentLink.ID)) continue;
            hits.Add(ToDto(content, isDraft: false));
        }

        var rawRemaining = consumed < active.Hits.Count;
        var more = active.Total >= 0
            ? active.Total > skip + consumed
            : active.Hits.Count >= limit;
        int? nextSkip = (rawRemaining || more) && consumed > 0 ? skip + consumed : null;

        var facets = BuildFacets(active, query);
        var tabs = AllTabs.Select(t => new PowerSearchTabCountDto(t, counts.TryGetValue(t, out var c) ? c : -1)).ToList();
        return Response(hits, active.Total, nextSkip, facets, tabs, "ok");
    }

    // ---- Graph ----

    private Task<GraphSearchResult> RunTab(
        PowerSearchTab tab, string phrase, GraphSearchFilters filters, int limit, int skip, CancellationToken ct)
    {
        var (scopeTypes, byLocale) = TabScope(tab);
        if (scopeTypes.Count == 0)
        {
            // An empty scope would mean "everything" downstream; an empty tab must mean "nothing".
            return Task.FromResult(GraphSearchResult.Empty);
        }

        var scope = new GraphSearchScope(scopeTypes, Array.Empty<Guid>(), byLocale);
        var request = new GraphSearchRequest(phrase, scope, null, filters, limit, skip, IncludeFacets: true);
        return _search.SearchAsync(request, ct);
    }

    private (IReadOnlyList<string> Types, bool FilterByLocale) TabScope(PowerSearchTab tab)
    {
        switch (tab)
        {
            case PowerSearchTab.Pages: return (_search.ValidateContentTypes(new[] { "PageData" }), true);
            case PowerSearchTab.Blocks: return (_search.ValidateContentTypes(new[] { "BlockData" }), true);
            case PowerSearchTab.Media: return (_search.ValidateContentTypes(new[] { "MediaData" }), false);
            default:
                var other = _types.List()
                    .Where(t => !IsPageBlockOrMedia(t.Base))
                    .Select(t => t.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();
                return (_search.ValidateContentTypes(other), true);
        }
    }

    private static bool IsPageBlockOrMedia(ContentTypeBase @base) =>
        @base.ToString() is "Page" or "Block" or "Media" or "Image" or "Video";

    // ---- CMS loading ----

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
                // Unknown language tag from the index: use the language-agnostic load.
            }
        }

        return content;
    }

    private IEnumerable<PowerSearchHitDto> LoadDrafts(string phrase, PowerSearchQuery query, int take)
    {
        var budget = Math.Max(1, take / 3);
        var language = query.Languages.Count == 1 ? query.Languages[0] : null;
        var references = _drafts.FindDrafts(phrase, language, budget * 8, new HashSet<int>());
        var list = new List<PowerSearchHitDto>();
        foreach (var reference in references)
        {
            if (!_loader.TryGet<IContent>(reference, out var content) || content is null) continue;
            if (!content.QueryDistinctAccess(AccessLevel.Read)) continue;
            if (!DraftMatches(content, query)) continue;

            list.Add(ToDto(content, isDraft: true));
            if (list.Count >= budget) break;
        }

        return list;
    }

    private bool DraftMatches(IContent content, PowerSearchQuery query)
    {
        if (!InTabScope(content, query.Tab)) return false;

        if (query.Languages.Count > 0)
        {
            var branch = (content as ILocalizable)?.Language?.Name;
            if (branch is null || !query.Languages.Contains(branch, StringComparer.OrdinalIgnoreCase)) return false;
        }

        if (query.Types.Count > 0)
        {
            var typeName = _types.Load(content.ContentTypeID)?.Name;
            if (typeName is null || !query.Types.Contains(typeName, StringComparer.Ordinal)) return false;
        }

        if (query.Sections.Count > 0)
        {
            var ancestors = _loader.GetAncestors(content.ContentLink).Select(a => a.ContentGuid).ToHashSet();
            if (!query.Sections.Any(ancestors.Contains)) return false;
        }

        return true;
    }

    private static bool InTabScope(IContent content, PowerSearchTab tab) => tab switch
    {
        PowerSearchTab.Pages => content is PageData,
        PowerSearchTab.Blocks => content is BlockData,
        PowerSearchTab.Media => content is MediaData,
        _ => content is not PageData and not BlockData and not MediaData
    };

    private PowerSearchHitDto ToDto(IContent content, bool isDraft)
    {
        var branch = (content as ILocalizable)?.Language?.Name ?? string.Empty;
        var tracked = content as IChangeTrackable;
        var status = isDraft
            ? Status("draft")
            : Status((content as IVersionable)?.Status.ToString() ?? nameof(VersionStatus.Published));

        return new PowerSearchHitDto(
            content.ContentLink.ID,
            content.Name ?? string.Empty,
            ResolveTypeName(content),
            branch,
            Breadcrumb(content),
            tracked?.Changed,
            tracked?.ChangedBy,
            status,
            isDraft,
            SearchResultFactory.AppendLanguage(ResolveEditUrl(content), branch));
    }

    /// <summary>Overridable because <see cref="PageEditing"/> is a static platform accessor.</summary>
    protected virtual string ResolveEditUrl(IContent content) => PageEditing.GetEditUrl(content.ContentLink);

    private string Status(string key) =>
        _localization.GetStringByCulture(StatusPrefix + key.ToLowerInvariant(), key, CultureInfo.CurrentUICulture);

    private string ResolveTypeName(IContent content)
    {
        try
        {
            return TypeLabel(_types.Load(content.ContentTypeID)) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? TypeLabel(ContentType? type) =>
        type is null
            ? null
            : new[] { type.LocalizedName, type.DisplayName, type.Name }.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private string Breadcrumb(IContent content)
    {
        try
        {
            // GetAncestors is nearest-first; the breadcrumb reads root-first.
            var names = _loader.GetAncestors(content.ContentLink)
                .Where(a => !ContentReference.IsNullOrEmpty(a.ContentLink)
                    && !a.ContentLink.CompareToIgnoreWorkID(ContentReference.RootPage))
                .Select(a => a.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Reverse();
            return string.Join(BreadcrumbSeparator, names);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Breadcrumb lookup failed for content {ContentId}.", content.ContentLink.ID);
            return string.Empty;
        }
    }

    // ---- Facets ----

    private PowerSearchFacetsDto BuildFacets(GraphSearchResult result, PowerSearchQuery query)
    {
        var graph = result.Facets;

        var typeValues = new List<PowerSearchFacetValueDto>();
        foreach (var value in graph.ContentTypes)
        {
            var selected = query.Types.Contains(value.Name, StringComparer.Ordinal);
            var type = _types.Load(value.Name);
            // Inheritance chain entries (Content, PageData, ...) are not registered types; useless as filters.
            if (type is null && !selected) continue;
            typeValues.Add(new PowerSearchFacetValueDto(value.Name, TypeLabel(type) ?? value.Name, value.Count, selected));
        }

        var languageValues = new List<PowerSearchFacetValueDto>();
        foreach (var value in graph.Languages)
        {
            var cms = ToCmsCulture(value.Name);
            var selected = query.Languages.Contains(cms, StringComparer.OrdinalIgnoreCase);
            languageValues.Add(new PowerSearchFacetValueDto(cms, CultureLabel(cms), value.Count, selected));
        }

        var sectionValues = new List<PowerSearchFacetValueDto>();
        foreach (var value in graph.Sections)
        {
            if (!Guid.TryParse(value.Name, out var guid)) continue;
            var selected = query.Sections.Contains(guid);
            // Shared ancestors (the root, the start page) match everything and filter nothing.
            if (result.Total >= 0 && value.Count == result.Total && !selected) continue;
            if (sectionValues.Count >= SectionFacetCap && !selected) continue;

            var name = SectionName(guid);
            if (name is null) continue;
            sectionValues.Add(new PowerSearchFacetValueDto(guid.ToString("D"), name, value.Count, selected));
        }

        // A selected value must stay visible (and uncheckable) even when Graph no longer reports it.
        foreach (var name in query.Types)
        {
            if (typeValues.Any(v => v.Value == name)) continue;
            typeValues.Add(new PowerSearchFacetValueDto(name, TypeLabel(_types.Load(name)) ?? name, 0, true));
        }

        foreach (var lang in query.Languages)
        {
            if (languageValues.Any(v => string.Equals(v.Value, lang, StringComparison.OrdinalIgnoreCase))) continue;
            languageValues.Add(new PowerSearchFacetValueDto(lang, CultureLabel(lang), 0, true));
        }

        foreach (var guid in query.Sections)
        {
            var id = guid.ToString("D");
            if (sectionValues.Any(v => v.Value == id)) continue;
            sectionValues.Add(new PowerSearchFacetValueDto(id, SectionName(guid) ?? id, 0, true));
        }

        return new PowerSearchFacetsDto(typeValues, languageValues, sectionValues);
    }

    private string? SectionName(Guid guid)
    {
        try
        {
            if (_repository.TryGet<IContent>(guid, out var content) && content is not null
                && content.QueryDistinctAccess(AccessLevel.Read)
                && !string.IsNullOrWhiteSpace(content.Name))
            {
                return content.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Section facet {Section} could not be resolved.", guid);
        }

        return null;
    }

    /// <summary>Graph locale <c>fr_CH</c> to CMS culture <c>fr-CH</c>.</summary>
    internal static string ToCmsCulture(string graphLocale) => graphLocale.Replace('_', '-');

    private static string CultureLabel(string code)
    {
        try
        {
            var native = CultureInfo.GetCultureInfo(code).NativeName;
            return string.IsNullOrWhiteSpace(native) ? code : native;
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    // ---- Helpers ----

    private static IReadOnlyList<PowerSearchTabCountDto> UnknownTabs() =>
        AllTabs.Select(t => new PowerSearchTabCountDto(t, -1)).ToList();

    private static PowerSearchResponseDto Response(
        IReadOnlyList<PowerSearchHitDto> hits, int total, int? nextSkip, PowerSearchFacetsDto facets,
        IReadOnlyList<PowerSearchTabCountDto> tabs, string state) =>
        new(hits, total, nextSkip, facets, tabs, state);
}
