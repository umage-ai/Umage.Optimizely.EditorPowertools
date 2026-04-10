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
        endpoints.MapHub<ActiveEditorsHub>("/editorpowertools/hubs/active-editors");
        return endpoints;
    }
}
