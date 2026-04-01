using EditorPowertools.Tools.ActiveEditors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace EditorPowertools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        return app;
    }

    public static IEndpointRouteBuilder MapEditorPowertools(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<ActiveEditorsHub>("/editorpowertools/hubs/active-editors");
        return endpoints;
    }
}
