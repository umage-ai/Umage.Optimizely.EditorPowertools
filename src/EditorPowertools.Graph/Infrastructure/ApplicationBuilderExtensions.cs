using EPiServer.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using UmageAI.Optimizely.EditorPowerTools.Graph.Menu;

namespace UmageAI.Optimizely.EditorPowerTools.Graph.Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Currently a no-op — kept for symmetry with the base library so consumer
    /// startup code reads <c>app.UseEditorPowertools().UseEditorPowertoolsGraph();</c>.
    /// </summary>
    public static IApplicationBuilder UseEditorPowertoolsGraph(this IApplicationBuilder app)
    {
        return app;
    }

    /// <summary>
    /// Registers an MVC route under the Graph module's protected path so its
    /// controllers are reachable at <c>/EPiServer/EditorPowertoolsGraph/{controller}/{action}</c>.
    /// Call this from the host's endpoint mapping alongside
    /// <c>endpoints.MapEditorPowertools()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapEditorPowertoolsGraph(this IEndpointRouteBuilder endpoints)
    {
        var basePath = Paths.ToResource(typeof(EditorPowertoolsGraphMenuProvider), "")
            .TrimStart('/').TrimEnd('/');

        endpoints.MapControllerRoute(
            name: "EditorPowertoolsGraph",
            pattern: $"{basePath}/{{controller}}/{{action}}/{{id?}}");

        return endpoints;
    }
}
