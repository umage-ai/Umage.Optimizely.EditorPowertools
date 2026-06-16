using System.Collections;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>Matches everything at lowest priority. Read-only, never dumps a raw object.</summary>
public sealed class FallbackHandler : IPropertyTypeHandler
{
    public int Priority => int.MinValue;

    public bool CanHandle(PropertyHandlerContext ctx) => true;

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        var value = ctx.Value;
        if (value is null) return string.Empty;
        if (value is string s) return s;

        if (value is System.Collections.ICollection col)
            return $"({col.Count} item{(col.Count == 1 ? "" : "s")})";

        if (value is IEnumerable enumerable)
        {
            try
            {
                var count = 0;
                foreach (var _ in enumerable) count++;
                return $"({count} item{(count == 1 ? "" : "s")})";
            }
            catch { return $"({value.GetType().Name})"; }
        }

        try
        {
            var text = value.ToString();
            if (string.IsNullOrEmpty(text) || text == value.GetType().FullName)
                return $"({value.GetType().Name})";
            return text;
        }
        catch { return $"({value.GetType().Name})"; }
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => null;

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = null;
        return false;
    }
}
