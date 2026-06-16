using System.Globalization;
using EPiServer;
using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>ContentReference / PageReference properties. Priority 10 (beats scalars).</summary>
public sealed class ContentReferenceHandler : IPropertyTypeHandler
{
    private readonly IContentLoader _contentLoader;
    public ContentReferenceHandler(IContentLoader contentLoader) => _contentLoader = contentLoader;

    public int Priority => 10;

    public bool CanHandle(PropertyHandlerContext ctx)
    {
        var name = ctx.PropertyClrType.Name;
        return name.Contains("ContentReference", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PageReference", StringComparison.OrdinalIgnoreCase);
    }

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        if (ctx.Value is not ContentReference cr || ContentReference.IsNullOrEmpty(cr))
            return string.Empty;
        try
        {
            if (_contentLoader.TryGet<IContent>(cr, out var content))
                return $"{content.Name} (ID: {cr.ID})";
        }
        catch { /* fall through */ }
        return $"ID: {cr.ID}";
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "reference" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (string.IsNullOrWhiteSpace(input) || input == "0") { value = ContentReference.EmptyReference; return true; }
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) { value = new ContentReference(id); return true; }
        value = null;
        return false;
    }
}
