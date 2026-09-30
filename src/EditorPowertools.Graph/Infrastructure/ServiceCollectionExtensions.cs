using EPiServer.Shell;
using EPiServer.Shell.Modules;
using EPiServer.Shell.Search;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;
#if OPTIMIZELY_CMS13
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms13;
#else
using UmageAI.Optimizely.EditorPowerTools.Graph.Cms12;
#endif
using UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Graph.Menu;
using UmageAI.Optimizely.EditorPowerTools.Graph.Permissions;
using UmageAI.Optimizely.EditorPowerTools.Graph.Providers;
using UmageAI.Optimizely.EditorPowerTools.Graph.Search;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSearch;
using UmageAI.Optimizely.EditorPowerTools.Graph.Tools.PowerSlice;
using EPiServer.Shell.ContentQuery;
using UmageAI.Optimizely.EditorPowerTools.Tools.CmsDoctor.Models;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Editor Powertools Graph add-on services with default options.
    /// Call this AFTER <c>AddEditorPowertools()</c>.
    /// </summary>
    public static IServiceCollection AddEditorPowertoolsGraph(this IServiceCollection services)
        => services.AddEditorPowertoolsGraph(_ => { });

    /// <summary>
    /// Registers the Editor Powertools Graph add-on services with custom options.
    /// </summary>
    public static IServiceCollection AddEditorPowertoolsGraph(
        this IServiceCollection services,
        Action<EditorPowertoolsGraphOptions> configureOptions)
    {
        services.AddOptions<EditorPowertoolsGraphOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                configuration.GetSection("CodeArt:EditorPowertools:Graph").Bind(options);
            })
            .Configure(configureOptions);

        services.AddSingleton<GraphFeatureAccessChecker>();

        // ---- Registrations added by later tasks (search, providers, slices, PowerSearch) go below ----
        services.AddHttpContextAccessor();
#if OPTIMIZELY_CMS13
        services.AddScoped<IGraphQueryExecutor, Cms13GraphQueryExecutor>();
#else
        services.AddScoped<IGraphQueryExecutor, Cms12GraphQueryExecutor>();
#endif
#if OPTIMIZELY_CMS13
        services.AddSingleton<IGraphQueryDialect, Cms13GraphQueryDialect>();
#else
        services.AddSingleton<IGraphQueryDialect, Cms12GraphQueryDialect>();
#endif

        services.AddScoped<IGraphAvailability, GraphAvailability>();
        services.AddTransient<IDoctorCheck, GraphHealthCheck>();
        services.AddScoped<IGraphSearchService, GraphSearchService>();

        // CMS UI search providers (Pages/Blocks/Media) and the content picker backing store.
        services.AddScoped<DraftSearch>();
        services.AddScoped<SearchResultFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISearchProvider, GraphPageSearchProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISearchProvider, GraphBlockSearchProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<ISearchProvider, GraphFileSearchProvider>());
        services.AddScoped<IContentSearchProvider, GraphContentPickerSearchProvider>();

        // Power Slice: each slice is an IContentQuery the assets-pane widget discovers by plug-in area.
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient<IContentQuery, MyContentSlice>();
        services.AddTransient<IContentQuery, RecentlyChangedSlice>();
        services.AddTransient<IContentQuery, DraftsSlice>();
        services.AddTransient<IContentQuery, AwaitingApprovalSlice>();
        services.AddTransient<IContentQuery, StaleContentSlice>();
        services.AddTransient<IContentQuery, UnusedBlocksSlice>();

        services.AddScoped<IPowerSearchService, PowerSearchService>();

        // ---- end of later-task registrations ----

        string GraphUrl(string path) => Paths.ToResource(typeof(EditorPowertoolsGraphMenuProvider), path);
        bool GraphAvailable(HttpContext ctx, string feature) =>
            ctx.RequestServices.GetService<GraphFeatureAccessChecker>()?.IsFeatureEnabled(feature) ?? true;

        services.AddSingleton<IPowertoolDescriptor>(_ => new PowertoolDescriptor
        {
            Id = "PowerSearch",
            Title = "/editorpowertools/tools/powersearch/title",
            Description = "/editorpowertools/tools/powersearch/description",
            Url = GraphUrl("EditorPowertoolsGraph/PowerSearch"),
            IconSvgPath = GraphSvgIcons.PowerSearch,
            Group = PowertoolGroups.Search,
            SortIndex = 370,
            AvailabilityCheck = ctx => GraphAvailable(ctx, nameof(GraphFeatureToggles.PowerSearch))
        });

        // Register the Graph add-on as its own protected module so it can ship
        // its own ClientResources and Razor controllers.
        services.Configure<ProtectedModuleOptions>(options =>
        {
            if (!options.Items.Any(m => m.Name == "EditorPowertoolsGraph"))
            {
                options.Items.Add(new ModuleDetails { Name = "EditorPowertoolsGraph" });
            }
        });

        return services;
    }
}
