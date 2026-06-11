using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Menu;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors;
using UmageAI.Optimizely.EditorPowerTools.Tools.VisitorGroupTester;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Registers Editor Powertools runtime middleware. The Visitor Group Tester injects a
    /// floating toolbar into the public site for authenticated editors and therefore buffers
    /// HTML responses on those requests; it bails out for anonymous users, non-HTML
    /// responses, edit-mode pages, static asset paths, and non-GET requests before any
    /// buffering happens, so anonymous traffic is unaffected.
    ///
    /// To skip this middleware entirely (e.g. on a high-traffic site that doesn't use the
    /// Visitor Group Tester), set <c>EditorPowertoolsOptions.Features.VisitorGroupTester</c>
    /// to <c>false</c> via <see cref="ServiceCollectionExtensions.AddEditorPowertools"/> or the
    /// <c>CodeArt:EditorPowertools:Features:VisitorGroupTester</c> config key — when disabled,
    /// the middleware is not added to the pipeline at all.
    /// </summary>
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<IOptions<EditorPowertoolsOptions>>();
        if (options?.Value.Features.VisitorGroupTester != false)
        {
            app.UseMiddleware<VisitorGroupTesterMiddleware>();
        }
        return app;
    }

    public static IEndpointRouteBuilder MapEditorPowertools(this IEndpointRouteBuilder endpoints)
    {
        // Register conventional route at the module's virtual path so all API controllers
        // are accessible at {modulePath}/{controller}/{action}/{id?} without hardcoded prefixes.
        var basePath = Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "")
            .TrimStart('/').TrimEnd('/');
        endpoints.MapControllerRoute(
            name: "EditorPowertoolsDefault",
            pattern: $"{basePath}/{{controller}}/{{action}}/{{id?}}");

        // Hub path is now module-path-aware
        endpoints.MapHub<ActiveEditorsHub>(
            Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "hubs/active-editors"));

        return endpoints;
    }
}
