using System.Globalization;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>
/// Handles PropertyCategory / CategoryList properties. Priority 40 (beats SelectionHandler at 30).
/// </summary>
public sealed class CategoryListHandler : IPropertyTypeHandler
{
    private readonly CategoryRepository _categoryRepository;

    public CategoryListHandler(CategoryRepository categoryRepository)
        => _categoryRepository = categoryRepository;

    public int Priority => 40;

    public bool CanHandle(PropertyHandlerContext ctx)
        => ctx.PropertyClrType == typeof(PropertyCategory)
           || ctx.PropertyClrType.IsSubclassOf(typeof(PropertyCategory))
           || ctx.Value is CategoryList;

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        if (ctx.Value is not CategoryList list || list.IsEmpty)
            return string.Empty;

        var names = new List<string>();
        foreach (var id in list)
        {
            try
            {
                var cat = _categoryRepository.Get(id);
                names.Add(cat?.Name ?? id.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                names.Add(id.ToString(CultureInfo.InvariantCulture));
            }
        }
        return string.Join(", ", names);
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) =>
        new()
        {
            Kind = "category",
            Multiple = true,
            Options = BuildOptions()
        };

    /// <summary>Returns comma-joined category IDs so the multiselect can pre-select current categories. Round-trips through TryParse.</summary>
    public string GetEditValue(PropertyHandlerContext ctx) =>
        ctx.Value is CategoryList list
            ? string.Join(",", list.Select(id => id.ToString(CultureInfo.InvariantCulture)))
            : string.Empty;

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            value = new CategoryList();
            return true;
        }

        var sep = input.Contains(';') ? ';' : input.Contains('|') ? '|' : ',';
        var ids = new List<int>();
        foreach (var part in input.Split(sep).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                value = null;
                return false;
            }
            ids.Add(id);
        }

        value = new CategoryList(ids);
        return true;
    }

    private IReadOnlyList<EditorOption> BuildOptions()
    {
        try
        {
            var root = _categoryRepository.GetRoot();
            if (root == null) return Array.Empty<EditorOption>();
            var options = new List<EditorOption>();
            WalkCategory(root, options);
            return options;
        }
        catch
        {
            return Array.Empty<EditorOption>();
        }
    }

    private static void WalkCategory(Category cat, List<EditorOption> options)
    {
        // Include available + selectable non-root categories as options
        if (cat.ID > 0 && cat.Available && cat.Selectable)
            options.Add(new EditorOption(cat.ID.ToString(CultureInfo.InvariantCulture), cat.Name));

        if (cat.Categories == null) return;
        foreach (Category child in cat.Categories)
            WalkCategory(child, options);
    }
}
