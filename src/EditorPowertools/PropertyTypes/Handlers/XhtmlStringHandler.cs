using System.Text.RegularExpressions;
using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>XhtmlString: readable plain-text display + plain-text edit. Priority 15.</summary>
public sealed class XhtmlStringHandler : IPropertyTypeHandler
{
    private static readonly Regex TagPattern =
        new("<.*?>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public int Priority => 15;
    public bool CanHandle(PropertyHandlerContext ctx) =>
        ctx.PropertyClrType.Name.Contains("XhtmlString", StringComparison.OrdinalIgnoreCase);

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        var html = (ctx.Value as XhtmlString)?.ToHtmlString() ?? ctx.Value?.ToString();
        if (string.IsNullOrEmpty(html)) return string.Empty;
        try
        {
            return TagPattern.Replace(html, string.Empty).Trim();
        }
        catch (RegexMatchTimeoutException)
        {
            return html;
        }
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "textarea" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = string.IsNullOrEmpty(input) ? null : new XhtmlString(input);
        return true;
    }
}
