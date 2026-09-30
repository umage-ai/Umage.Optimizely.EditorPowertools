namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>Resolves the first matching handler by descending priority.</summary>
public sealed class PropertyTypeHandlerRegistry
{
    private readonly IReadOnlyList<IPropertyTypeHandler> _handlers;

    public PropertyTypeHandlerRegistry(IEnumerable<IPropertyTypeHandler> handlers)
        => _handlers = handlers.OrderByDescending(h => h.Priority).ToList();

    /// <summary>Always returns a handler (the FallbackHandler matches everything).</summary>
    public IPropertyTypeHandler Resolve(PropertyHandlerContext ctx)
        => _handlers.First(h => h.CanHandle(ctx));
}
