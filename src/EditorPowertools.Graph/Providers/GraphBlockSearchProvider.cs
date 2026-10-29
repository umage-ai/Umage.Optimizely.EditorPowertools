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

/// <summary>Graph-backed CMS UI search provider for Block.</summary>
public sealed class GraphBlockSearchProvider : GraphSearchProviderBase
{
    public GraphBlockSearchProvider(
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
        ILogger<GraphBlockSearchProvider> logger)
        : base(search, availability, drafts, results, access, http, loader, repository, options, localization, logger)
    {
    }

    protected override string BuiltInArea => "CMS/blocks";
    protected override string CategoryPath => "/editorpowertools/graph/search/category/blocks";
    protected override string CategoryFallback => "Blocks (Graph)";
    protected override string IconCssClass => "epi-iconObjectSharedBlock";
    protected override IReadOnlyList<string> ScopeTypes { get; } = new[] { "BlockData" };
    protected override bool FilterByLocale => true;
    protected override bool IsInScope(IContent content) => content is BlockData;
#if OPTIMIZELY_CMS13
    protected override string KeySuffix => "blocks";
#endif

    /// <summary>Shared blocks only by default, so inline "for this page" blocks do not flood results.</summary>
    protected override IReadOnlySet<ContentReference> DefaultRoots()
    {
        var roots = new HashSet<ContentReference>();
        try
        {
            var site = SiteDefinition.Current;
            AddRoot(roots, site?.GlobalAssetsRoot);
            AddRoot(roots, site?.SiteAssetsRoot);
        }
        catch
        {
            // No resolvable site (e.g. background thread): search all blocks.
        }

        return roots;
    }

    private static void AddRoot(HashSet<ContentReference> roots, ContentReference? root)
    {
        if (!ContentReference.IsNullOrEmpty(root))
        {
            roots.Add(root.ToReferenceWithoutVersion());
        }
    }
}
