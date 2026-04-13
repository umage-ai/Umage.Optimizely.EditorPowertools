using EPiServer.Shell;
using UmageAI.Optimizely.EditorPowerTools.Menu;
using UmageAI.Optimizely.EditorPowerTools.Tools.ActiveEditors;
using UmageAI.Optimizely.EditorPowerTools.Tools.VisitorGroupTester;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace UmageAI.Optimizely.EditorPowerTools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        app.UseMiddleware<VisitorGroupTesterMiddleware>();
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
