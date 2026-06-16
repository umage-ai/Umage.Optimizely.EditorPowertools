using System.Globalization;
using EPiServer.Core;
using EPiServer.SpecializedProperties;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>Url must be checked before String (PropertyUrl reports DataType String). Priority 5.</summary>
public sealed class UrlHandler : IPropertyTypeHandler
{
    public int Priority => 5;

    public bool CanHandle(PropertyHandlerContext ctx) =>
        ctx.PropertyClrType.Name.Contains("Url", StringComparison.OrdinalIgnoreCase);

    public string GetDisplay(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "url" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = string.IsNullOrWhiteSpace(input) ? null : new EPiServer.Url(input);
        return true;
    }
}

public sealed class StringHandler : IPropertyTypeHandler
{
    public int Priority => 0;

    public bool CanHandle(PropertyHandlerContext ctx) =>
        ctx.DataType is PropertyDataType.String or PropertyDataType.LongString;

    public string GetDisplay(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) =>
        new() { Kind = ctx.DataType == PropertyDataType.LongString ? "textarea" : "text" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = input;
        return true;
    }
}

public sealed class NumberHandler : IPropertyTypeHandler
{
    public int Priority => 0;

    public bool CanHandle(PropertyHandlerContext ctx) => ctx.DataType == PropertyDataType.Number;

    public string GetDisplay(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "number" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            value = i;
            return true;
        }
        value = null;
        return false;
    }
}

public sealed class FloatHandler : IPropertyTypeHandler
{
    public int Priority => 0;

    public bool CanHandle(PropertyHandlerContext ctx) => ctx.DataType == PropertyDataType.FloatNumber;

    public string GetDisplay(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "number" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            value = d;
            return true;
        }
        value = null;
        return false;
    }
}

public sealed class BooleanHandler : IPropertyTypeHandler
{
    public int Priority => 0;

    public bool CanHandle(PropertyHandlerContext ctx) => ctx.DataType == PropertyDataType.Boolean;

    public string GetDisplay(PropertyHandlerContext ctx) => ctx.Value?.ToString() ?? string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "bool" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (bool.TryParse(input, out var b))
        {
            value = b;
            return true;
        }
        value = null;
        return false;
    }
}

public sealed class DateHandler : IPropertyTypeHandler
{
    public int Priority => 0;

    public bool CanHandle(PropertyHandlerContext ctx) => ctx.DataType == PropertyDataType.Date;

    public string GetDisplay(PropertyHandlerContext ctx) =>
        ctx.Value is DateTime dt ? dt.ToString("g", CultureInfo.InvariantCulture) : string.Empty;

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "date" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            value = d;
            return true;
        }
        value = null;
        return false;
    }
}
