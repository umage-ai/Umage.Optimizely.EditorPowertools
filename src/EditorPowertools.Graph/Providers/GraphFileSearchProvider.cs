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

/// <summary>Graph-backed CMS UI search provider for File.</summary>
public sealed class GraphFileSearchProvider : GraphSearchProviderBase
{
    public GraphFileSearchProvider(
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
        ILogger<GraphFileSearchProvider> logger)
        : base(search, availability, drafts, results, access, http, loader, repository, options, localization, logger)
    {
    }

    protected override string BuiltInArea => "CMS/files";
    protected override string CategoryPath => "/editorpowertools/graph/search/category/media";
    protected override string CategoryFallback => "Media (Graph)";
    protected override string IconCssClass => "epi-iconObjectImage";
    protected override IReadOnlyList<string> ScopeTypes { get; } = new[] { "MediaData" };
    protected override bool FilterByLocale => false;
    protected override bool IsInScope(IContent content) => content is MediaData;
#if OPTIMIZELY_CMS13
    protected override string KeySuffix => "files";
#endif
}
