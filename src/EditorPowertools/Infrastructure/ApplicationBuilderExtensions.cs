using Microsoft.AspNetCore.Builder;

namespace EditorPowertools.Infrastructure;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Editor Powertools middleware to the request pipeline.
    /// </summary>
    public static IApplicationBuilder UseEditorPowertools(this IApplicationBuilder app)
    {
        // Future: add middleware for static file serving, etc.
        return app;
    }
}
