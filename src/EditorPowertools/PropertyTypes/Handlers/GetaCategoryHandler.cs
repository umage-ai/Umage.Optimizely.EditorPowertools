using System.Collections;
using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using Microsoft.Extensions.Logging;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>
/// GETA categories (Geta.Optimizely.Categories.CategoryList) via reflection — no compile-time
/// dependency on Geta. Full edit (picker + reflective write-back). Inert when Geta isn't loaded.
/// Priority 50.
/// </summary>
/// <remarks>
/// NOTE: the Geta API shape (value type name, constructor, category base type,
/// value-is-IEnumerable&lt;ContentReference&gt;) is assumed and must be verified against a real
/// Geta install — see docs/superpowers/plans/2026-06-16-shared-property-type-handlers.md Task 11 Step 0.
/// </remarks>
public sealed class GetaCategoryHandler : IPropertyTypeHandler
{
    private readonly ILogger<GetaCategoryHandler> _logger;
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _modelUsage;
    private readonly Func<Type, bool> _isGetaCategoryType;
    private readonly Lazy<Type?> _valueType;
    private readonly Lazy<Type?> _categoryModelBaseType;

    public GetaCategoryHandler(
        ILogger<GetaCategoryHandler> logger,
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage modelUsage,
        string valueTypeFullName = "Geta.Optimizely.Categories.CategoryList",
        string categoryModelBaseFullName = "Geta.Optimizely.Categories.Category",
        Func<Type, bool>? isGetaCategoryType = null)
    {
        _logger = logger;
        _contentLoader = contentLoader;
        _contentTypeRepository = contentTypeRepository;
        _modelUsage = modelUsage;
        _isGetaCategoryType = isGetaCategoryType ?? DefaultIsGeta;
        _valueType = new Lazy<Type?>(() => FindType(valueTypeFullName));
        _categoryModelBaseType = new Lazy<Type?>(() => FindType(categoryModelBaseFullName));
    }

    public int Priority => 50;

    public bool CanHandle(PropertyHandlerContext ctx) =>
        _isGetaCategoryType(ctx.PropertyClrType)
        || (ctx.Value != null && _isGetaCategoryType(ctx.Value.GetType()));

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        var names = EnumerateReferences(ctx.Value).Select(r =>
        {
            try { return _contentLoader.TryGet<IContent>(r, out var c) ? c.Name : $"ID: {r.ID}"; }
            catch { return $"ID: {r.ID}"; }
        });
        return string.Join(", ", names);
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx)
    {
        if (_valueType.Value == null) return null; // Geta absent → read-only
        return new PropertyEditorDescriptor { Kind = "category", Multiple = true, Options = LoadCategoryOptions() };
    }

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = null;
        var listType = _valueType.Value;
        if (listType == null) return false; // Geta absent → fail closed

        var refs = ParseReferenceIds(input).Select(id => new ContentReference(id)).ToList();
        try
        {
            var ctor = listType.GetConstructor(new[] { typeof(IEnumerable<ContentReference>) });
            if (ctor != null) { value = ctor.Invoke(new object[] { refs }); return true; }

            var instance = Activator.CreateInstance(listType);
            if (instance is IList list)
            {
                foreach (var r in refs) list.Add(r);
                value = instance;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not construct GETA CategoryList value via reflection for input '{Input}'.", input);
        }
        return false;
    }

    private IReadOnlyList<EditorOption> LoadCategoryOptions()
    {
        var baseType = _categoryModelBaseType.Value;
        if (baseType == null) return Array.Empty<EditorOption>();
        var options = new List<EditorOption>();
        try
        {
            foreach (var ct in _contentTypeRepository.List()
                         .Where(ct => ct.ModelType != null && baseType.IsAssignableFrom(ct.ModelType)))
            {
                foreach (var usage in _modelUsage.ListContentOfContentType(ct))
                {
                    var link = usage.ContentLink.ToReferenceWithoutVersion();
                    if (_contentLoader.TryGet<IContent>(link, out var c))
                        options.Add(new EditorOption(link.ID.ToString(CultureInfo.InvariantCulture), c.Name));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load GETA category options via reflection; returning none.");
            return Array.Empty<EditorOption>();
        }
        return options.GroupBy(o => o.Value).Select(g => g.First()).OrderBy(o => o.Label).ToList();
    }

    private static bool DefaultIsGeta(Type t) =>
        t.Namespace?.Contains("Geta.Optimizely.Categories", StringComparison.OrdinalIgnoreCase) == true
        && t.Name.Contains("Category", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<int> ParseReferenceIds(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) yield break;
        var sep = input.Contains(';') ? ';' : input.Contains('|') ? '|' : ',';
        foreach (var part in input.Split(sep).Select(p => p.Trim()).Where(p => p.Length > 0))
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                yield return id;
    }

    private static IEnumerable<ContentReference> EnumerateReferences(object? value)
    {
        if (value is IEnumerable e and not string)
            foreach (var item in e)
                if (item is ContentReference cr && !ContentReference.IsNullOrEmpty(cr))
                    yield return cr;
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { var t = asm.GetType(fullName); if (t != null) return t; }
            catch { /* unresolvable assembly */ }
        }
        return null;
    }
}
