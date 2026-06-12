using EPiServer.Shell;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using UmageAI.Optimizely.EditorPowerTools.Forms.Menu;

namespace UmageAI.Optimizely.EditorPowerTools.Forms.Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Currently a no-op — kept for symmetry with the base library so consumer
    /// startup code reads <c>app.UseEditorPowertools().UseEditorPowertoolsForms();</c>.
    /// </summary>
    public static IApplicationBuilder UseEditorPowertoolsForms(this IApplicationBuilder app)
    {
        return app;
    }

    /// <summary>
    /// Registers an MVC route under the Forms module's protected path so its
    /// controllers are reachable at <c>/EPiServer/EditorPowertoolsForms/{controller}/{action}</c>.
    /// Call this from the host's endpoint mapping alongside
    /// <c>endpoints.MapEditorPowertools()</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapEditorPowertoolsForms(this IEndpointRouteBuilder endpoints)
    {
        var basePath = Paths.ToResource(typeof(EditorPowertoolsFormsMenuProvider), "")
            .TrimStart('/').TrimEnd('/');

        endpoints.MapControllerRoute(
            name: "EditorPowertoolsForms",
            pattern: $"{basePath}/{{controller}}/{{action}}/{{id?}}");

        return endpoints;
    }
}
