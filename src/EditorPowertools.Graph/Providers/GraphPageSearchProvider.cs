using EPiServer.Framework.Localization;
using EPiServer.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Providers;

/// <summary>Graph-backed CMS UI search provider for Page.</summary>
public sealed class GraphPageSearchProvider : GraphSearchProviderBase
{
    public GraphPageSearchProvider(
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
        ILogger<GraphPageSearchProvider> logger)
        : base(search, availability, drafts, results, access, http, loader, repository, options, localization, logger)
    {
    }

    protected override string BuiltInArea => "CMS/pages";
    protected override string CategoryPath => "/editorpowertools/graph/search/category/pages";
    protected override string CategoryFallback => "Pages (Graph)";
    protected override string IconCssClass => "epi-iconObjectPage";
    protected override IReadOnlyList<string> ScopeTypes { get; } = new[] { "PageData" };
    protected override bool FilterByLocale => true;
    protected override bool IsInScope(IContent content) => content is PageData;
#if OPTIMIZELY_CMS13
    protected override string KeySuffix => "pages";
#endif
}
