using System.Reflection;
using EPiServer.Shell.ObjectEditing;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>Properties decorated with [SelectOne]/[SelectMany]. Priority 30 (beats string scalar).</summary>
public sealed class SelectionHandler : IPropertyTypeHandler
{
    public int Priority => 30;

    public bool CanHandle(PropertyHandlerContext ctx) => GetFactoryAttr(ctx, out _) != null;

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        var raw = ctx.Value?.ToString();
        if (string.IsNullOrEmpty(raw)) return string.Empty;
        var options = LoadOptions(ctx);
        var labels = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => options.FirstOrDefault(o => o.Value == v)?.Label ?? v);
        return string.Join(", ", labels);
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx)
    {
        if (GetFactoryAttr(ctx, out var many) == null) return null;
        return new PropertyEditorDescriptor
        {
            Kind = many ? "multiselect" : "select",
            Multiple = many,
            Options = LoadOptions(ctx)
        };
    }

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = input ?? string.Empty;
        return true;
    }

    private static Attribute? GetFactoryAttr(PropertyHandlerContext ctx, out bool many)
    {
        many = false;
        var prop = ctx.ModelProperty;
        if (prop == null) return null;
        var one = prop.GetCustomAttribute<SelectOneAttribute>();
        if (one != null) return one;
        var multi = prop.GetCustomAttribute<SelectManyAttribute>();
        if (multi != null) { many = true; return multi; }
        return null;
    }

    private static IReadOnlyList<EditorOption> LoadOptions(PropertyHandlerContext ctx)
    {
        var attr = GetFactoryAttr(ctx, out _);
        var factoryType = (attr as SelectOneAttribute)?.SelectionFactoryType
                          ?? (attr as SelectManyAttribute)?.SelectionFactoryType;
        if (factoryType == null) return Array.Empty<EditorOption>();
        try
        {
            if (Activator.CreateInstance(factoryType) is ISelectionFactory factory)
                return factory.GetSelections(null!)
                    .Select(s => new EditorOption(s.Value?.ToString() ?? "", s.Text ?? ""))
                    .ToList();
        }
        catch { /* factory needs metadata we can't supply — degrade to no options */ }
        return Array.Empty<EditorOption>();
    }
}
