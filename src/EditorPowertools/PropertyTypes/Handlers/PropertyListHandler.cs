using System.Collections;
using System.Globalization;
using EPiServer.Core;
using EPiServer.SpecializedProperties;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>PropertyList&lt;T&gt; (e.g. PropertyStringList/PropertyNumberList). Priority 20.</summary>
public sealed class PropertyListHandler : IPropertyTypeHandler
{
    public int Priority => 20;

    public bool CanHandle(PropertyHandlerContext ctx) => GetItemType(ctx.PropertyClrType) != null;

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        if (ctx.Value is IEnumerable e and not string)
            return string.Join(", ", e.Cast<object?>().Select(o => o?.ToString()));
        return ctx.Value?.ToString() ?? string.Empty;
    }

    /// <summary>Editable only when ParseListValue can actually round-trip the item type
    /// (string or IConvertible). PropertyList&lt;ContentReference&gt; etc. stay read-only —
    /// offering an editor whose every commit fails Convert.ChangeType would be a trap.</summary>
    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx)
    {
        var itemType = GetItemType(ctx.PropertyClrType);
        return itemType != null && IsParseableItemType(itemType) ? new() { Kind = "text" } : null;
    }

    private static bool IsParseableItemType(Type t) =>
        t == typeof(string) || typeof(IConvertible).IsAssignableFrom(t);

    /// <summary>Returns semicolon-joined items so the text editor pre-populates, and TryParse can round-trip via its ';' separator path.</summary>
    public string GetEditValue(PropertyHandlerContext ctx) =>
        ctx.Value is IEnumerable e and not string
            ? string.Join(";", e.Cast<object?>().Select(o => o?.ToString()))
            : string.Empty;

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        var itemType = GetItemType(ctx.PropertyClrType);
        if (itemType == null || input == null) { value = null; return false; }
        try
        {
            value = ParseListValue(input, itemType);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    internal static Type? GetItemType(Type propertyClrType)
    {
        var t = propertyClrType;
        while (t != null && t != typeof(object))
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(PropertyList<>))
                return t.GetGenericArguments()[0];
            t = t.BaseType;
        }
        return null;
    }

    internal static IList ParseListValue(string value, Type itemType)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        if (value.TrimStart().StartsWith("[", StringComparison.Ordinal))
        {
            try
            {
                var strings = System.Text.Json.JsonSerializer.Deserialize<string[]>(value);
                if (strings != null)
                {
                    foreach (var s in strings)
                        list.Add(itemType == typeof(string) ? s : Convert.ChangeType(s, itemType, CultureInfo.InvariantCulture));
                    return list;
                }
            }
            catch { /* fall through */ }
        }
        var sep = value.Contains(';') ? ';' : value.Contains('|') ? '|' : ',';
        foreach (var part in value.Split(sep).Select(v => v.Trim()).Where(v => v.Length > 0))
            list.Add(itemType == typeof(string) ? part : Convert.ChangeType(part, itemType, CultureInfo.InvariantCulture));
        return list;
    }
}
