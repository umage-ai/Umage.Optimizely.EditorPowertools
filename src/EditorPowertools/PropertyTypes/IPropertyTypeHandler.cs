namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>
/// Handles display, inline-edit metadata, and string parsing for one family of property types.
/// Consumed by the Bulk Property Editor (all members) and the Content Importer (display + parse).
/// </summary>
public interface IPropertyTypeHandler
{
    /// <summary>Higher is matched first. The fallback handler uses int.MinValue.</summary>
    int Priority { get; }

    bool CanHandle(PropertyHandlerContext ctx);

    /// <summary>Readable cell/preview value. Never returns a raw object dump.</summary>
    string GetDisplay(PropertyHandlerContext ctx);

    /// <summary>Inline editor metadata for the Bulk Editor, or null for read-only.</summary>
    PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx);

    /// <summary>Parses a string into a property value. Returns false if not parseable.</summary>
    bool TryParse(string? input, PropertyHandlerContext ctx, out object? value);

    /// <summary>
    /// The string used to SEED the inline editor for this property — must round-trip through
    /// <see cref="TryParse"/>. Defaults to the raw value's string form (correct for scalars).
    /// Complex handlers override (e.g. XhtmlString returns its HTML; references/categories return ids).
    /// </summary>
    string GetEditValue(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;
}
