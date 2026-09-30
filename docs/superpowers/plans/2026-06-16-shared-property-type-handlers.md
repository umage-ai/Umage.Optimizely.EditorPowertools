# Shared Property-Type Handlers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce one pluggable `IPropertyTypeHandler` abstraction + registry, consumed by both the Bulk Property Editor and the Content Importer, so every property type has readable display, optional inline editing, and string parsing in one place.

**Architecture:** A core `PropertyTypes` namespace defines a handler interface, a context object, an editor descriptor, and an ordered registry (highest `Priority` wins; a `FallbackHandler` always matches). Built-in handlers cover scalars, content references, property-lists, selection/enum, native categories, and GETA categories (reflection-only). Both tool services resolve a handler per property and delegate display/parse/editor decisions to it, replacing their private type-name `switch` blocks.

**Tech Stack:** .NET 8 + .NET 10 (multi-target), Optimizely CMS 12/13, xUnit + FluentAssertions + Moq for tests.

**Base branch:** `feature/property-type-handlers` (already created off `security/review-fixes-2026-06`). Spec: `docs/superpowers/specs/2026-06-16-shared-property-type-handlers-design.md`.

---

## File Structure

**New (core — `src/EditorPowertools/PropertyTypes/`):**
- `IPropertyTypeHandler.cs` — the contract.
- `PropertyHandlerContext.cs` — context + factory helpers.
- `PropertyEditorDescriptor.cs` — editor descriptor + `EditorOption`.
- `PropertyTypeHandlerRegistry.cs` — ordered resolver.
- `Handlers/FallbackHandler.cs`
- `Handlers/ScalarHandlers.cs` — String, Number, Float, Boolean, Date, Url (one file, six small classes).
- `Handlers/ContentReferenceHandler.cs`
- `Handlers/XhtmlStringHandler.cs`
- `Handlers/PropertyListHandler.cs`
- `Handlers/SelectionHandler.cs`
- `Handlers/CategoryListHandler.cs`
- `Handlers/GetaCategoryHandler.cs`

**Modified:**
- `Infrastructure/ServiceCollectionExtensions.cs` — register handlers + registry.
- `Tools/BulkPropertyEditor/BulkPropertyEditorService.cs` — resolve via registry.
- `Tools/BulkPropertyEditor/Models/BulkPropertyEditorDtos.cs` — add editor descriptor to `PropertyColumnInfo`.
- `Tools/ContentImporter/ContentImporterService.cs` — `SetPropertyValue` delegates to registry.
- `modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js` — render editors from descriptor.

**Tests (`src/EditorPowertools.Tests/PropertyTypes/`):** one file per handler/task as noted.

**Convention reminders:** test classes call `EpiServerTestSetup.EnsureInitialized();` in their constructor; use `[Fact]`/`[Theory]`, FluentAssertions `.Should()`, Moq. Run tests with `dotnet test` (no `--no-build`).

---

## Task 1: Foundation types (interface, context, descriptor, registry)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/IPropertyTypeHandler.cs`
- Create: `src/EditorPowertools/PropertyTypes/PropertyHandlerContext.cs`
- Create: `src/EditorPowertools/PropertyTypes/PropertyEditorDescriptor.cs`
- Create: `src/EditorPowertools/PropertyTypes/PropertyTypeHandlerRegistry.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/PropertyTypeHandlerRegistryTests.cs`

- [ ] **Step 1: Write the descriptor + context + interface (no behavior to test yet)**

`PropertyEditorDescriptor.cs`:
```csharp
namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>Describes the inline editor the Bulk Property Editor should render for a property.</summary>
public sealed class PropertyEditorDescriptor
{
    /// <summary>text | number | bool | date | url | reference | select | multiselect | category</summary>
    public required string Kind { get; init; }

    /// <summary>Options for select/multiselect/category editors; null otherwise.</summary>
    public IReadOnlyList<EditorOption>? Options { get; init; }

    /// <summary>True when the editor accepts multiple values.</summary>
    public bool Multiple { get; init; }
}

public sealed record EditorOption(string Value, string Label);
```

`PropertyHandlerContext.cs`:
```csharp
using System.Reflection;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes;

/// <summary>Everything a property-type handler needs to inspect a property.</summary>
public sealed class PropertyHandlerContext
{
    public required PropertyDefinition Definition { get; init; }
    public Type? ModelType { get; init; }
    public PropertyInfo? ModelProperty { get; init; }
    public PropertyData? Property { get; init; }

    /// <summary>The PropertyData CLR type (from the instance if present, else the definition).</summary>
    public Type PropertyClrType => Property?.GetType() ?? Definition.Type.DefinitionType;

    public PropertyDataType DataType => Property?.Type ?? Definition.Type.DataType;

    public object? Value => Property?.Value;

    public static PropertyHandlerContext ForDefinition(PropertyDefinition definition, Type? modelType) => new()
    {
        Definition = definition,
        ModelType = modelType,
        ModelProperty = modelType?.GetProperty(definition.Name)
    };

    public static PropertyHandlerContext ForProperty(PropertyData property, PropertyDefinition definition, Type? modelType) => new()
    {
        Definition = definition,
        ModelType = modelType,
        ModelProperty = modelType?.GetProperty(definition.Name),
        Property = property
    };
}
```

`IPropertyTypeHandler.cs`:
```csharp
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
}
```

`PropertyTypeHandlerRegistry.cs`:
```csharp
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
```

- [ ] **Step 2: Write the failing registry test**

`PropertyTypeHandlerRegistryTests.cs`:
```csharp
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class PropertyTypeHandlerRegistryTests
{
    private sealed class StubHandler : IPropertyTypeHandler
    {
        private readonly bool _matches;
        public StubHandler(int priority, bool matches) { Priority = priority; _matches = matches; }
        public int Priority { get; }
        public bool CanHandle(PropertyHandlerContext ctx) => _matches;
        public string GetDisplay(PropertyHandlerContext ctx) => Priority.ToString();
        public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => null;
        public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value) { value = null; return false; }
    }

    [Fact]
    public void Resolve_ReturnsHighestPriorityMatch()
    {
        var registry = new PropertyTypeHandlerRegistry(new IPropertyTypeHandler[]
        {
            new StubHandler(0, matches: true),
            new StubHandler(50, matches: true),
            new StubHandler(100, matches: false),
        });

        var handler = registry.Resolve(null!);

        handler.Priority.Should().Be(50); // 100 doesn't match; 50 beats 0
    }
}
```

- [ ] **Step 3: Run test to verify it passes (types compile, ordering works)**

Run: `dotnet test src/EditorPowertools.Tests --filter PropertyTypeHandlerRegistryTests`
Expected: PASS, both TFMs.

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/PropertyTypes src/EditorPowertools.Tests/PropertyTypes/PropertyTypeHandlerRegistryTests.cs
git commit -m "feat(propertytypes): handler interface, context, descriptor, registry"
```

---

## Task 2: FallbackHandler + DI wiring

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/FallbackHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs` (add after the existing `// Bulk Property Editor` block, around line 94)
- Test: `src/EditorPowertools.Tests/PropertyTypes/FallbackHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using System.Collections.Generic;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class FallbackHandlerTests
{
    private static PropertyHandlerContext Ctx(object? value) =>
        new() { Definition = null!, Property = new TestProp { Value = value } };

    private sealed class TestProp : EPiServer.Core.PropertyData
    {
        public override EPiServer.Core.PropertyDataType Type => EPiServer.Core.PropertyDataType.String;
        public override object? Value { get; set; }
        protected override void SetDefaultValue() { }
        public override EPiServer.PageReference OwnerPage { get => default!; set { } }
    }

    [Fact]
    public void CanHandle_AlwaysTrue() =>
        new FallbackHandler().CanHandle(null!).Should().BeTrue();

    [Fact]
    public void GetDisplay_Null_ReturnsEmpty() =>
        new FallbackHandler().GetDisplay(Ctx(null)).Should().BeEmpty();

    [Fact]
    public void GetDisplay_Collection_ReturnsItemCount() =>
        new FallbackHandler().GetDisplay(Ctx(new List<int> { 1, 2, 3 })).Should().Be("(3 items)");

    [Fact]
    public void GetEditor_IsNull_ReadOnly() =>
        new FallbackHandler().GetEditor(Ctx("x")).Should().BeNull();

    [Fact]
    public void TryParse_AlwaysFalse() =>
        new FallbackHandler().TryParse("x", Ctx("x"), out _).Should().BeFalse();
}
```

> Note: if the inline `TestProp`/`Ctx` helper is reused by later handler tests, extract it to `src/EditorPowertools.Tests/PropertyTypes/PropertyTestHelpers.cs` when you hit the second use. For Task 2 keep it inline.

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter FallbackHandlerTests`
Expected: FAIL — `FallbackHandler` does not exist.

- [ ] **Step 3: Implement FallbackHandler**

```csharp
using System.Collections;
using System.Globalization;

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

        if (value is IEnumerable enumerable)
        {
            var count = 0;
            foreach (var _ in enumerable) count++;
            return $"({count} item{(count == 1 ? "" : "s")})";
        }

        var text = value.ToString();
        // Never surface a type name dump (e.g. "System.Collections...").
        if (string.IsNullOrEmpty(text) || text == value.GetType().FullName)
            return $"({value.GetType().Name})";
        return text;
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => null;

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = null;
        return false;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter FallbackHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register the registry + fallback in DI**

In `ServiceCollectionExtensions.cs`, add a `using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;` and `using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;` at the top, then add this block right after the `// Bulk Property Editor` registration (`services.AddTransient<BulkPropertyEditorService>();`):

```csharp
        // Property-type handlers (shared by Bulk Property Editor + Content Importer).
        // Registered highest-priority-last is irrelevant; the registry orders by Priority.
        services.AddSingleton<PropertyTypeHandlerRegistry>();
        services.AddSingleton<IPropertyTypeHandler, FallbackHandler>();
```

- [ ] **Step 6: Build to verify DI compiles**

Run: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: Build succeeded (both TFMs).

- [ ] **Step 7: Commit**

```bash
git add src/EditorPowertools/PropertyTypes/Handlers/FallbackHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/FallbackHandlerTests.cs
git commit -m "feat(propertytypes): fallback handler + DI registration"
```

---

## Task 3: Scalar handlers (string, number, float, bool, date, url)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/ScalarHandlers.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs` (extend the handler block from Task 2)
- Test: `src/EditorPowertools.Tests/PropertyTypes/ScalarHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Globalization;
using EPiServer.Core;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ScalarHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) =>
        new() { Definition = null!, Property = prop };

    [Fact]
    public void StringHandler_ParsesAndDisplays()
    {
        var h = new StringHandler();
        h.TryParse("hello", Ctx(new PropertyString()), out var v).Should().BeTrue();
        v.Should().Be("hello");
        h.GetDisplay(Ctx(new PropertyString("hi"))).Should().Be("hi");
        h.GetEditor(Ctx(new PropertyString()))!.Kind.Should().Be("text");
    }

    [Fact]
    public void NumberHandler_ParsesInt_RejectsGarbage()
    {
        var h = new NumberHandler();
        h.TryParse("42", Ctx(new PropertyNumber()), out var v).Should().BeTrue();
        v.Should().Be(42);
        h.TryParse("notanumber", Ctx(new PropertyNumber()), out _).Should().BeFalse();
    }

    [Fact]
    public void BooleanHandler_Parses()
    {
        var h = new BooleanHandler();
        h.TryParse("true", Ctx(new PropertyBoolean()), out var v).Should().BeTrue();
        v.Should().Be(true);
    }

    [Fact]
    public void DateHandler_ParsesInvariant()
    {
        var h = new DateHandler();
        h.TryParse("2026-06-16", Ctx(new PropertyDate()), out var v).Should().BeTrue();
        ((DateTime)v!).Year.Should().Be(2026);
    }

    [Fact]
    public void UrlHandler_BeatsStringByCheckingClrType()
    {
        var h = new UrlHandler();
        h.CanHandle(Ctx(new PropertyUrl())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
        h.TryParse("https://x.com", Ctx(new PropertyUrl()), out var v).Should().BeTrue();
        v.Should().BeOfType<EPiServer.Url>();
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter ScalarHandlerTests`
Expected: FAIL — handler classes do not exist.

- [ ] **Step 3: Implement the scalar handlers**

`ScalarHandlers.cs`:
```csharp
using System.Globalization;
using EPiServer.Core;

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
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) { value = i; return true; }
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
        if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
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
        if (bool.TryParse(input, out var b)) { value = b; return true; }
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
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) { value = d; return true; }
        value = null;
        return false;
    }
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter ScalarHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register the scalar handlers in DI**

In `ServiceCollectionExtensions.cs`, extend the handler block:
```csharp
        services.AddSingleton<IPropertyTypeHandler, UrlHandler>();
        services.AddSingleton<IPropertyTypeHandler, StringHandler>();
        services.AddSingleton<IPropertyTypeHandler, NumberHandler>();
        services.AddSingleton<IPropertyTypeHandler, FloatHandler>();
        services.AddSingleton<IPropertyTypeHandler, BooleanHandler>();
        services.AddSingleton<IPropertyTypeHandler, DateHandler>();
```

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/PropertyTypes/Handlers/ScalarHandlers.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/ScalarHandlerTests.cs
git commit -m "feat(propertytypes): scalar handlers (string/number/float/bool/date/url)"
```

---

## Task 4: ContentReferenceHandler

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/ContentReferenceHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/ContentReferenceHandlerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using EPiServer;
using EPiServer.Core;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ContentReferenceHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) => new() { Definition = null!, Property = prop };

    [Fact]
    public void CanHandle_MatchesContentAndPageReference()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.CanHandle(Ctx(new PropertyContentReference())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyPageReference())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
    }

    [Fact]
    public void GetDisplay_ResolvesNameAndId()
    {
        var loader = new Mock<IContentLoader>();
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns("Hello");
        IContent? outContent = content.Object;
        loader.Setup(l => l.TryGet(new ContentReference(7), out outContent)).Returns(true);

        var h = new ContentReferenceHandler(loader.Object);
        var prop = new PropertyContentReference { Value = new ContentReference(7) };

        h.GetDisplay(Ctx(prop)).Should().Be("Hello (ID: 7)");
    }

    [Fact]
    public void TryParse_IntToContentReference()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.TryParse("12", Ctx(new PropertyContentReference()), out var v).Should().BeTrue();
        ((ContentReference)v!).ID.Should().Be(12);
    }

    [Fact]
    public void GetEditor_IsReferencePicker()
    {
        var h = new ContentReferenceHandler(Mock.Of<IContentLoader>());
        h.GetEditor(Ctx(new PropertyContentReference()))!.Kind.Should().Be("reference");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter ContentReferenceHandlerTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Implement ContentReferenceHandler**

```csharp
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
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter ContentReferenceHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register in DI**

```csharp
        services.AddSingleton<IPropertyTypeHandler, ContentReferenceHandler>();
```

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/PropertyTypes/Handlers/ContentReferenceHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/ContentReferenceHandlerTests.cs
git commit -m "feat(propertytypes): content/page reference handler"
```

---

## Task 5: Wire Bulk Property Editor to the registry

**Files:**
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/Models/BulkPropertyEditorDtos.cs` (add editor descriptor to `PropertyColumnInfo`)
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorService.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/BulkPropertyEditorIntegrationTests.cs`

> Read `BulkPropertyEditorDtos.cs` first to get the exact record shape of `PropertyColumnInfo` and `PropertyValue`. The change: add a nullable `PropertyEditorDescriptor? Editor` member to `PropertyColumnInfo` (append as the last positional/optional member so existing call sites still compile — give it a default of `null`).

- [ ] **Step 1: Add the editor descriptor to the column DTO**

In `BulkPropertyEditorDtos.cs`, add `using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;` and extend `PropertyColumnInfo` with a trailing optional member, e.g.:
```csharp
public record PropertyColumnInfo(
    string Name,
    string DisplayName,
    string TypeName,
    bool Editable,
    PropertyEditorDescriptor? Editor = null);
```

- [ ] **Step 2: Inject the registry into BulkPropertyEditorService**

Add `using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;`. Add a `private readonly PropertyTypeHandlerRegistry _handlers;` field and a constructor parameter `PropertyTypeHandlerRegistry handlers`, assigning it. (Mirror the existing constructor-assignment style.)

- [ ] **Step 3: Replace property-listing logic in `GetProperties`**

Replace the `.Select(pd => new PropertyColumnInfo(...))` projection with handler-driven resolution:
```csharp
        var modelType = contentType.ModelType;
        return contentType.PropertyDefinitions
            .Where(pd => !IsSystemProperty(pd.Name))
            .Select(pd =>
            {
                var ctx = PropertyHandlerContext.ForDefinition(pd, modelType);
                var handler = _handlers.Resolve(ctx);
                var editor = handler.GetEditor(ctx);
                return new PropertyColumnInfo(
                    pd.Name,
                    pd.EditCaption ?? pd.Name,
                    editor?.Kind ?? "readonly",
                    editor != null,
                    editor);
            })
            .OrderBy(p => p.DisplayName)
            .ToList();
```

- [ ] **Step 4: Replace per-cell display in `BuildContentItemRow`**

Replace the body of the `foreach (string column in columns)` loop (the block currently doing `prop.Value?.ToString()` + the inline ContentReference special-case) with:
```csharp
            PropertyData? prop = content.Property[column];
            if (prop != null)
            {
                var pd = contentType.PropertyDefinitions.FirstOrDefault(d => d.Name == column);
                var ctx = pd != null
                    ? PropertyHandlerContext.ForProperty(prop, pd, contentType.ModelType)
                    : new PropertyHandlerContext { Definition = null!, Property = prop };
                var handler = _handlers.Resolve(ctx);
                var editor = handler.GetEditor(ctx);
                properties[column] = new PropertyValue(
                    handler.GetDisplay(ctx),
                    prop.Value,
                    editor != null,
                    editor?.Kind ?? "readonly");
            }
            else
            {
                properties[column] = new PropertyValue(null, null, false, "readonly");
            }
```
This requires `contentType` in scope — load it once at the top of `BuildContentItemRow`: `var contentType = _contentTypeRepository.Load(content.ContentTypeID);` (the method already loads it for the row's type name; reuse a single local).

- [ ] **Step 5: Replace write conversion in `SaveAsync` and `BulkSaveAsync`**

Add a private helper and use it in both methods in place of `ConvertPropertyValue(...)`:
```csharp
    private void AssignValue(IContent owner, ContentType? contentType, PropertyData property, string? value)
    {
        var pd = contentType?.PropertyDefinitions.FirstOrDefault(d => d.Name == property.Name);
        var ctx = pd != null
            ? PropertyHandlerContext.ForProperty(property, pd, contentType!.ModelType)
            : new PropertyHandlerContext { Definition = null!, Property = property };
        var handler = _handlers.Resolve(ctx);
        if (handler.TryParse(value, ctx, out var parsed))
            property.Value = parsed;
        else
            throw new InvalidOperationException($"Value not accepted for property '{property.Name}'.");
    }
```
In `SaveAsync`: load `var contentType = _contentTypeRepository.Load(content.ContentTypeID);` then `AssignValue((IContent)writable, contentType, property, request.Value);`.
In `BulkSaveAsync`: load the content type once per item and call `AssignValue(...)` inside the `PropertyChanges` loop. Keep the existing try/catch that records a generic error (do **not** surface `ex.Message` to the client — preserve the security-review behavior).
Then delete the now-unused `ConvertPropertyValue`, `IsEditableType`, `GetPropertyTypeName`, `GetRuntimePropertyTypeName` methods.

- [ ] **Step 6: Write an integration test**

```csharp
using System.Linq;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class BulkPropertyEditorIntegrationTests
{
    public BulkPropertyEditorIntegrationTests() => EpiServerTestSetup.EnsureInitialized();

    private static PropertyTypeHandlerRegistry BuildRegistry(IContentLoader loader) =>
        new(new IPropertyTypeHandler[]
        {
            new FallbackHandler(),
            new StringHandler(), new NumberHandler(), new FloatHandler(),
            new BooleanHandler(), new DateHandler(), new UrlHandler(),
            new ContentReferenceHandler(loader),
        });

    [Fact]
    public void Registry_ResolvesStringPropertyToTextEditor()
    {
        var registry = BuildRegistry(Mock.Of<IContentLoader>());
        var ctx = new PropertyHandlerContext { Definition = null!, Property = new PropertyString("x") };

        var handler = registry.Resolve(ctx);
        handler.GetEditor(ctx)!.Kind.Should().Be("text");
        handler.GetDisplay(ctx).Should().Be("x");
    }
}
```

- [ ] **Step 7: Run tests + build**

Run: `dotnet test src/EditorPowertools.Tests --filter BulkPropertyEditorIntegrationTests`
Then: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: PASS + Build succeeded. Fix any call sites of the deleted methods.

- [ ] **Step 8: Commit**

```bash
git add src/EditorPowertools/Tools/BulkPropertyEditor src/EditorPowertools.Tests/PropertyTypes/BulkPropertyEditorIntegrationTests.cs
git commit -m "refactor(bulkedit): resolve display/edit/parse via property-type registry"
```

---

## Task 6: Wire Content Importer to the registry

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentImporter/ContentImporterService.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/ContentImporterParseTests.cs`

> Goal: route `SetPropertyValue` through the registry while keeping the importer-specific behaviors (image download, content-area-from-blocks, `PropertyList`/`XhtmlString`) working. `PropertyList`/`XhtmlString` move to handlers in Task 7; for now keep their existing branches and add a registry fallback for everything else.

- [ ] **Step 1: Inject the registry**

Add `using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;`. Add `private readonly PropertyTypeHandlerRegistry _handlers;` + constructor parameter (mirror the existing assignments). The constructor already gained `IPrincipalAccessor` on the security branch — add `PropertyTypeHandlerRegistry handlers` alongside it.

- [ ] **Step 2: Thread the content type into `SetPropertyValue`**

Change the signature from `SetPropertyValue(PropertyData prop, string? value)` to `SetPropertyValue(PropertyData prop, string? value, ContentType? contentType)` and update its three call sites (in `CreateContentFromRow` and `SetContentAreaFromInlineBlocks`) to pass the content type already in scope (`ct` / `blockType`).

- [ ] **Step 3: Delegate the default branch to the registry**

In `SetPropertyValue`, keep the `XhtmlString` and `PropertyList<T>` branches as they are for now, then replace the final `prop.Value = ConvertValue(value, typeName);` with:
```csharp
        var pd = contentType?.PropertyDefinitions.FirstOrDefault(d => d.Name == prop.Name);
        var ctx = pd != null
            ? PropertyHandlerContext.ForProperty(prop, pd, contentType!.ModelType)
            : new PropertyHandlerContext { Definition = null!, Property = prop };
        var handler = _handlers.Resolve(ctx);
        if (handler.TryParse(value, ctx, out var parsed))
            prop.Value = parsed;
        // else: leave unset; the row warning is already recorded by the caller on failure.
```
Leave `ConvertValue` in place for now (still referenced by nothing after this change — delete it only once Task 7 confirms parity, to avoid a large diff here). Actually: if `ConvertValue` becomes unused, delete it in this step and remove `using System.Globalization;` if no longer needed.

- [ ] **Step 4: Write a parse test**

```csharp
using EPiServer.Core;
using FluentAssertions;
using Moq;
using EPiServer;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class ContentImporterParseTests
{
    [Fact]
    public void Registry_ParsesNumberForImport()
    {
        var registry = new PropertyTypeHandlerRegistry(new IPropertyTypeHandler[]
        {
            new FallbackHandler(), new NumberHandler(),
        });
        var ctx = new PropertyHandlerContext { Definition = null!, Property = new PropertyNumber() };

        registry.Resolve(ctx).TryParse("99", ctx, out var v).Should().BeTrue();
        v.Should().Be(99);
    }
}
```

- [ ] **Step 5: Run tests + build**

Run: `dotnet test src/EditorPowertools.Tests --filter ContentImporterParseTests`
Then: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: PASS + Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/EditorPowertools/Tools/ContentImporter/ContentImporterService.cs src/EditorPowertools.Tests/PropertyTypes/ContentImporterParseTests.cs
git commit -m "refactor(import): route property value-setting through the registry"
```

---

## Task 7: XhtmlString + PropertyList handlers (display everywhere)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/XhtmlStringHandler.cs`
- Create: `src/EditorPowertools/PropertyTypes/Handlers/PropertyListHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Modify: `src/EditorPowertools/Tools/ContentImporter/ContentImporterService.cs` (remove the now-duplicated XhtmlString/PropertyList branches)
- Test: `src/EditorPowertools.Tests/PropertyTypes/CollectionHandlerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections;
using EPiServer.Core;
using EPiServer.SpecializedProperties;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class CollectionHandlerTests
{
    private static PropertyHandlerContext Ctx(PropertyData prop) => new() { Definition = null!, Property = prop };

    [Fact]
    public void Xhtml_DisplaysPlainText()
    {
        var h = new XhtmlStringHandler();
        var prop = new PropertyXhtmlString { Value = new XhtmlString("<p>Hello <b>world</b></p>") };
        h.CanHandle(Ctx(prop)).Should().BeTrue();
        h.GetDisplay(Ctx(prop)).Should().Contain("Hello").And.NotContain("<");
        h.GetEditor(Ctx(prop))!.Kind.Should().Be("textarea");
        h.TryParse("plain", Ctx(prop), out var v).Should().BeTrue();
        v.Should().BeOfType<XhtmlString>();
    }

    [Fact]
    public void PropertyList_DisplaysJoined_AndParsesDelimited()
    {
        var h = new PropertyListHandler();
        var prop = new PropertyStringList();
        h.CanHandle(Ctx(prop)).Should().BeTrue();
        h.TryParse("a;b;c", Ctx(prop), out var v).Should().BeTrue();
        ((IEnumerable)v!).Cast<string>().Should().BeEquivalentTo("a", "b", "c");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter CollectionHandlerTests`
Expected: FAIL — classes do not exist.

- [ ] **Step 3: Implement the handlers**

`XhtmlStringHandler.cs`:
```csharp
using System.Text.RegularExpressions;
using EPiServer.Core;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>XhtmlString: readable plain-text display + plain-text edit. Priority 15.</summary>
public sealed class XhtmlStringHandler : IPropertyTypeHandler
{
    public int Priority => 15;
    public bool CanHandle(PropertyHandlerContext ctx) =>
        ctx.PropertyClrType.Name.Contains("XhtmlString", StringComparison.OrdinalIgnoreCase);

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        var html = (ctx.Value as XhtmlString)?.ToHtmlString() ?? ctx.Value?.ToString();
        if (string.IsNullOrEmpty(html)) return string.Empty;
        return Regex.Replace(html, "<.*?>", string.Empty).Trim();
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "textarea" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        value = string.IsNullOrEmpty(input) ? null : new XhtmlString(input);
        return true;
    }
}
```

`PropertyListHandler.cs` (move the importer's list logic here):
```csharp
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

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new() { Kind = "text" };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        var itemType = GetItemType(ctx.PropertyClrType);
        if (itemType == null || input == null) { value = null; return false; }
        value = ParseListValue(input, itemType);
        return true;
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
```

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter CollectionHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register + remove the duplicated importer branches**

DI:
```csharp
        services.AddSingleton<IPropertyTypeHandler, XhtmlStringHandler>();
        services.AddSingleton<IPropertyTypeHandler, PropertyListHandler>();
```
In `ContentImporterService.SetPropertyValue`, delete the `XhtmlString` and `PropertyList<T>` (`GetPropertyListItemType`/`ParseListValue`) branches so all non-image types now go through the registry. Delete the importer's now-unused `GetPropertyListItemType` and `ParseListValue` private methods (their logic lives in `PropertyListHandler`). Keep the image-download path.

- [ ] **Step 6: Run full test suite + build**

Run: `dotnet test`
Then: `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: all PASS + Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add src/EditorPowertools/PropertyTypes/Handlers/XhtmlStringHandler.cs src/EditorPowertools/PropertyTypes/Handlers/PropertyListHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools/Tools/ContentImporter/ContentImporterService.cs src/EditorPowertools.Tests/PropertyTypes/CollectionHandlerTests.cs
git commit -m "feat(propertytypes): xhtml + property-list handlers; importer reuses them"
```

---

## Task 8: SelectionHandler (single + multi)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/SelectionHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/SelectionHandlerTests.cs`

> Background: a selection property is a `PropertyString`/`PropertyLongString` whose **model property** carries `[SelectOne(typeof(Factory))]` or `[SelectMany(typeof(Factory))]`. The factory implements `EPiServer.Shell.ObjectEditing.SelectionFactories.ISelectionFactory` with `IEnumerable<ISelectItem> GetSelections(ExtendedMetadata metadata)` where each item has `Text` + `Value`. SelectMany stores values comma-separated. We detect via reflection on `ctx.ModelProperty`'s attributes, so no editor-descriptor framework dependency is needed.

- [ ] **Step 1: Write the failing test**

```csharp
using System.ComponentModel.DataAnnotations;
using EPiServer.Core;
using EPiServer.Shell.ObjectEditing;
using EPiServer.Shell.ObjectEditing.SelectionFactories;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class SelectionHandlerTests
{
    public sealed class ColorFactory : ISelectionFactory
    {
        public IEnumerable<ISelectItem> GetSelections(ExtendedMetadata metadata) => new ISelectItem[]
        {
            new SelectItem { Text = "Red", Value = "r" },
            new SelectItem { Text = "Green", Value = "g" },
        };
    }

    private sealed class Model
    {
        [SelectOne(SelectionFactoryType = typeof(ColorFactory))]
        public string? Single { get; set; }

        [SelectMany(SelectionFactoryType = typeof(ColorFactory))]
        public string? Multi { get; set; }
    }

    private static PropertyHandlerContext Ctx(string modelProp, PropertyData prop) => new()
    {
        Definition = null!,
        ModelType = typeof(Model),
        ModelProperty = typeof(Model).GetProperty(modelProp),
        Property = prop
    };

    [Fact]
    public void SelectOne_ProducesSelectEditorWithOptions()
    {
        var h = new SelectionHandler();
        var ctx = Ctx(nameof(Model.Single), new PropertyString());
        h.CanHandle(ctx).Should().BeTrue();
        var ed = h.GetEditor(ctx)!;
        ed.Kind.Should().Be("select");
        ed.Multiple.Should().BeFalse();
        ed.Options.Should().ContainSingle(o => o.Value == "r" && o.Label == "Red");
    }

    [Fact]
    public void SelectMany_IsMultiselect_AndDisplaysLabels()
    {
        var h = new SelectionHandler();
        var ctx = Ctx(nameof(Model.Multi), new PropertyString("r,g"));
        h.GetEditor(ctx)!.Kind.Should().Be("multiselect");
        h.GetDisplay(ctx).Should().Be("Red, Green");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter SelectionHandlerTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Implement SelectionHandler**

```csharp
using System.Reflection;
using EPiServer.Shell.ObjectEditing;
using EPiServer.Shell.ObjectEditing.SelectionFactories;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>Properties decorated with [SelectOne]/[SelectMany]. Priority 30 (beats string scalar).</summary>
public sealed class SelectionHandler : IPropertyTypeHandler
{
    public int Priority => 30;

    public bool CanHandle(PropertyHandlerContext ctx) => GetFactory(ctx, out _) != null;

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
        var factory = GetFactory(ctx, out var many);
        if (factory == null) return null;
        return new PropertyEditorDescriptor
        {
            Kind = many ? "multiselect" : "select",
            Multiple = many,
            Options = LoadOptions(ctx)
        };
    }

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        // Single = the value; multi = comma-joined values (SelectMany storage format).
        value = input ?? string.Empty;
        return true;
    }

    private static object? GetFactory(PropertyHandlerContext ctx, out bool many)
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
        var attr = GetFactory(ctx, out _);
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
```

> Note for the implementer: some selection factories dereference `metadata` and will throw on `null!`. The `catch` degrades gracefully (editor still renders as a free-text-ish multiselect with no options). If a real site has such a factory and you want options, that's a follow-up — out of scope here.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter SelectionHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register in DI + commit**

```csharp
        services.AddSingleton<IPropertyTypeHandler, SelectionHandler>();
```
```bash
git add src/EditorPowertools/PropertyTypes/Handlers/SelectionHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/SelectionHandlerTests.cs
git commit -m "feat(propertytypes): selection (SelectOne/SelectMany) handler"
```

---

## Task 9: CategoryListHandler (native EPiServer categories)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/CategoryListHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/CategoryListHandlerTests.cs`

> Background: native categories use `PropertyCategory`, whose value is an `EPiServer.DataAbstraction.CategoryList` (an `IList<int>` of category IDs). Resolve names + the option list via `EPiServer.DataAbstraction.CategoryRepository` (`Get(int)` → `Category` with `.Name`; `GetRoot()` → root `Category` whose `.Categories` are the children, recursively). Inject `CategoryRepository`.

- [ ] **Step 1: Write the failing test**

```csharp
using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class CategoryListHandlerTests
{
    public CategoryListHandlerTests() => EpiServerTestSetup.EnsureInitialized();

    private static PropertyHandlerContext Ctx(PropertyData prop) => new() { Definition = null!, Property = prop };

    [Fact]
    public void CanHandle_MatchesPropertyCategory()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.CanHandle(Ctx(new PropertyCategory())).Should().BeTrue();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
    }

    [Fact]
    public void GetDisplay_ResolvesCategoryNames()
    {
        var repo = new Mock<CategoryRepository>();
        repo.Setup(r => r.Get(3)).Returns(new Category { Name = "News" });
        repo.Setup(r => r.Get(4)).Returns(new Category { Name = "Sports" });

        var h = new CategoryListHandler(repo.Object);
        var prop = new PropertyCategory { Value = new CategoryList(new[] { 3, 4 }) };

        h.GetDisplay(Ctx(prop)).Should().Be("News, Sports");
    }

    [Fact]
    public void TryParse_IdsToCategoryList()
    {
        var h = new CategoryListHandler(Mock.Of<CategoryRepository>());
        h.TryParse("3;4", Ctx(new PropertyCategory()), out var v).Should().BeTrue();
        ((CategoryList)v!).Should().BeEquivalentTo(new[] { 3, 4 });
    }
}
```

> If `CategoryRepository`/`Category` members are not virtual and Moq cannot override them, fall back to wrapping category lookups behind a tiny internal `ICategoryNameResolver` interface this handler depends on, and test that. Decide at implementation time based on the actual class shape (check `CategoryRepository.Get` is virtual).

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter CategoryListHandlerTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Implement CategoryListHandler**

```csharp
using System.Collections;
using System.Globalization;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>Native EPiServer categories (PropertyCategory / CategoryList). Priority 40.</summary>
public sealed class CategoryListHandler : IPropertyTypeHandler
{
    private readonly CategoryRepository _categories;
    public CategoryListHandler(CategoryRepository categories) => _categories = categories;

    public int Priority => 40;

    public bool CanHandle(PropertyHandlerContext ctx) =>
        ctx.PropertyClrType.Name.Equals("PropertyCategory", StringComparison.Ordinal)
        || ctx.Value is CategoryList;

    public string GetDisplay(PropertyHandlerContext ctx)
    {
        if (ctx.Value is not CategoryList list || list.Count == 0) return string.Empty;
        var names = list.Select(id =>
        {
            try { return _categories.Get(id)?.Name ?? id.ToString(CultureInfo.InvariantCulture); }
            catch { return id.ToString(CultureInfo.InvariantCulture); }
        });
        return string.Join(", ", names);
    }

    public PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx) => new()
    {
        Kind = "category",
        Multiple = true,
        Options = LoadAllCategories()
    };

    public bool TryParse(string? input, PropertyHandlerContext ctx, out object? value)
    {
        if (string.IsNullOrWhiteSpace(input)) { value = new CategoryList(); return true; }
        var sep = input.Contains(';') ? ';' : input.Contains('|') ? '|' : ',';
        var ids = new List<int>();
        foreach (var part in input.Split(sep).Select(p => p.Trim()).Where(p => p.Length > 0))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) { value = null; return false; }
            ids.Add(id);
        }
        value = new CategoryList(ids.ToArray());
        return true;
    }

    private IReadOnlyList<EditorOption> LoadAllCategories()
    {
        try
        {
            var root = _categories.GetRoot();
            var result = new List<EditorOption>();
            void Walk(Category c)
            {
                foreach (var child in c.Categories ?? Enumerable.Empty<Category>())
                {
                    result.Add(new EditorOption(child.ID.ToString(CultureInfo.InvariantCulture), child.Name));
                    Walk(child);
                }
            }
            if (root != null) Walk(root);
            return result;
        }
        catch { return Array.Empty<EditorOption>(); }
    }
}
```

> Verify `CategoryList` is enumerable as `int` and constructible from `int[]`, and that `Category.Categories` exists, against the referenced EPiServer.CMS.Core version. Adjust the enumeration if the API differs across CMS 12/13 (Tier-1 `#if` inside this file if needed).

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter CategoryListHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register in DI + commit**

```csharp
        services.AddSingleton<IPropertyTypeHandler, CategoryListHandler>();
```
```bash
git add src/EditorPowertools/PropertyTypes/Handlers/CategoryListHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/CategoryListHandlerTests.cs
git commit -m "feat(propertytypes): native category-list handler"
```

---

## Task 10: Bulk-Edit JS — render editors from the descriptor

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js`

> Read the file first. Today it builds inline editors by guessing from a type-name string. The API now returns `editor: { kind, options, multiple }` per column (from `PropertyColumnInfo.Editor`). Switch the inline-editor builder to read `column.editor`.

- [ ] **Step 1: Locate the inline-editor creation**

Find where the editor `<input>`/`<select>` is created for a cell (search for `createElement('input'`, `type-name`, or where it decides editable). Note the function name and the variable holding the column metadata.

- [ ] **Step 2: Add an editor-builder helper**

Add a function that maps a descriptor to a DOM control. Escape all option labels/values with `EPT.escHtml`:
```javascript
// Build an inline editor element from a server-supplied descriptor.
function buildEditorControl(descriptor, currentValue) {
    var kind = (descriptor && descriptor.kind) || 'text';
    if (kind === 'bool') {
        var sel = document.createElement('select');
        ['', 'true', 'false'].forEach(function (v) {
            var o = document.createElement('option');
            o.value = v; o.textContent = v === '' ? '—' : v;
            if (String(currentValue) === v) o.selected = true;
            sel.appendChild(o);
        });
        return sel;
    }
    if (kind === 'select' || kind === 'multiselect' || kind === 'category') {
        var s = document.createElement('select');
        if (kind !== 'select') s.multiple = true;
        (descriptor.options || []).forEach(function (opt) {
            var o = document.createElement('option');
            o.value = opt.value;
            o.textContent = opt.label;            // textContent escapes automatically
            s.appendChild(o);
        });
        return s;
    }
    if (kind === 'textarea') {
        var ta = document.createElement('textarea');
        ta.value = currentValue == null ? '' : String(currentValue);
        return ta;
    }
    var input = document.createElement('input');
    input.type = (kind === 'number') ? 'number' : (kind === 'date') ? 'date' : 'text';
    input.value = currentValue == null ? '' : String(currentValue);
    return input;
}

// Read the edited value back out of a control built by buildEditorControl.
function readEditorControl(el) {
    if (el.tagName === 'SELECT' && el.multiple) {
        return Array.prototype.filter.call(el.options, function (o) { return o.selected; })
            .map(function (o) { return o.value; }).join(',');
    }
    return el.value;
}
```

- [ ] **Step 3: Use the helper where inline edit starts**

Replace the existing input-creation in the cell-edit code path with `buildEditorControl(column.editor, currentRawValue)`, and replace the existing value read-back with `readEditorControl(theControl)`. Keep the existing save call (which posts the string value to `BulkSave`/`Save`). For `category`/`multiselect`, the comma-joined string the handler's `TryParse` expects is exactly what `readEditorControl` returns.

- [ ] **Step 4: Manual verification (no JS unit harness in repo)**

Run the sample site: `dotnet run --project src/EditorPowertools.SampleSite`. In Bulk Property Editor:
- A category property shows names (not a raw object) and edits via a multi-select of categories.
- A `[SelectOne]` property edits via a dropdown; `[SelectMany]` via a multi-select.
- A plain string still edits via a text box; bool via the true/false dropdown.
- An unsupported type shows a readable value and is not editable.

- [ ] **Step 5: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js
git commit -m "feat(bulkedit): render inline editors from server editor descriptor"
```

---

## Task 11: GetaCategoryHandler (reflection — full edit)

**Files:**
- Create: `src/EditorPowertools/PropertyTypes/Handlers/GetaCategoryHandler.cs`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/EditorPowertools.Tests/PropertyTypes/GetaCategoryHandlerTests.cs`

> Background: GETA's `Geta.Optimizely.Categories.CategoryList` is its own type (NOT the native
> `CategoryList`); its values are `ContentReference`s to category content (instances of a model
> deriving from `Geta.Optimizely.Categories.Category`). We take **no compile-time dependency** on
> Geta — everything is reflection over loaded assemblies. The handler is **inert** (read-only,
> `TryParse` false, editor null) when the Geta type is not present. v1 provides **full edit**: a
> multi-select category picker (options = all Geta category content) and reflective write-back
> that constructs Geta's `CategoryList` from selected content IDs.
>
> **Testability seam:** the type-detection uses an injectable `Func<Type,bool>` predicate and an
> injectable value-type full name, so tests exercise matching + reflective construction with a
> stand-in type — no Geta install needed in CI.

- [ ] **Step 0: Verification spike (do this first; adjust the code below to match findings)**

Because we can't validate Geta's API from CI, confirm these assumptions against a real install
(or by inspecting the loaded `Geta.Optimizely.Categories` assembly) BEFORE implementing, and
adjust Step 3 if they differ. Record findings in the commit message.
1. The property value type is `Geta.Optimizely.Categories.CategoryList` and it implements
   `IList` (so `Add(ContentReference)` works) and/or has a `ctor(IEnumerable<ContentReference>)`.
2. Category content models derive from `Geta.Optimizely.Categories.Category`.
3. Enumerating a `CategoryList` value yields `ContentReference` items (used for display).
If any differs, change `valueTypeFullName` / `categoryModelBaseFullName` / the construction
branch accordingly. The structure (predicate + reflective build) stays the same.

- [ ] **Step 1: Write the failing tests (match, fail-closed, reflective build, display)**

```csharp
using System.Collections;
using System.Collections.Generic;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes;
using UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;
using Xunit;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.PropertyTypes;

public class GetaCategoryHandlerTests
{
    // Minimal PropertyData stand-in with a settable Value.
    private sealed class TestProp : PropertyData
    {
        public override PropertyDataType Type => PropertyDataType.LongString;
        public override object? Value { get; set; }
        protected override void SetDefaultValue() { }
        public override EPiServer.PageReference OwnerPage { get => default!; set { } }
    }

    // Stand-in for Geta's CategoryList: an IList of ContentReference, resolvable by full name.
    public sealed class FakeGetaList : List<ContentReference> { }

    private static PropertyHandlerContext Ctx(PropertyData prop) => new() { Definition = null!, Property = prop };

    private static GetaCategoryHandler Handler(
        IContentLoader? loader = null, string valueTypeFullName = "No.Such.Type", Func<Type, bool>? match = null) =>
        new(loader ?? Mock.Of<IContentLoader>(), Mock.Of<IContentTypeRepository>(), Mock.Of<IContentModelUsage>(),
            valueTypeFullName: valueTypeFullName, isGetaCategoryType: match);

    [Fact]
    public void CanHandle_FalseForNonGetaTypes()
    {
        var h = Handler();
        h.CanHandle(Ctx(new PropertyString())).Should().BeFalse();
        h.CanHandle(Ctx(new PropertyContentReference())).Should().BeFalse();
    }

    [Fact]
    public void CanHandle_TrueWhenPredicateMatches()
    {
        var h = Handler(match: t => t.Name == nameof(TestProp));
        h.CanHandle(Ctx(new TestProp())).Should().BeTrue();
    }

    [Fact]
    public void TryParse_FailsClosed_WhenGetaTypeAbsent()
    {
        var h = Handler(valueTypeFullName: "Not.A.Real.Type");
        h.TryParse("3;4", Ctx(new TestProp()), out _).Should().BeFalse();
    }

    [Fact]
    public void TryParse_BuildsListType_WhenResolvable()
    {
        var h = Handler(valueTypeFullName: typeof(FakeGetaList).FullName!);
        h.TryParse("3;4", Ctx(new TestProp()), out var v).Should().BeTrue();
        ((IList)v!).Count.Should().Be(2);
    }

    [Fact]
    public void GetDisplay_ResolvesCategoryNames()
    {
        var loader = new Mock<IContentLoader>();
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns("Cat A");
        IContent? oc = content.Object;
        loader.Setup(l => l.TryGet(new ContentReference(3), out oc)).Returns(true);

        var h = Handler(loader.Object);
        var prop = new TestProp { Value = new FakeGetaList { new ContentReference(3) } };

        h.GetDisplay(Ctx(prop)).Should().Be("Cat A");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test src/EditorPowertools.Tests --filter GetaCategoryHandlerTests`
Expected: FAIL — class does not exist.

- [ ] **Step 3: Implement GetaCategoryHandler**

```csharp
using System.Collections;
using System.Globalization;
using EPiServer;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.PropertyTypes.Handlers;

/// <summary>
/// GETA categories (Geta.Optimizely.Categories.CategoryList) via reflection — no compile-time
/// dependency on Geta. Full edit (multi-select picker + reflective write-back). Inert (read-only,
/// no parse) when Geta is not installed. Priority 50.
/// </summary>
public sealed class GetaCategoryHandler : IPropertyTypeHandler
{
    private readonly IContentLoader _contentLoader;
    private readonly IContentTypeRepository _contentTypeRepository;
    private readonly IContentModelUsage _modelUsage;
    private readonly Func<Type, bool> _isGetaCategoryType;
    private readonly Lazy<Type?> _valueType;
    private readonly Lazy<Type?> _categoryModelBaseType;

    public GetaCategoryHandler(
        IContentLoader contentLoader,
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage modelUsage,
        string valueTypeFullName = "Geta.Optimizely.Categories.CategoryList",
        string categoryModelBaseFullName = "Geta.Optimizely.Categories.Category",
        Func<Type, bool>? isGetaCategoryType = null)
    {
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
        // Without the Geta value type we cannot round-trip a written value → read-only.
        if (_valueType.Value == null) return null;
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
        catch { /* value-type shape differs from the spike assumptions */ }
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
        catch { return Array.Empty<EditorOption>(); }
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
            catch { /* unresolvable assembly — skip */ }
        }
        return null;
    }
}
```

> **Note:** editing requires the Geta value type to be loadable (otherwise the handler degrades to
> read-only — still no raw-object cells). `LoadCategoryOptions` discovers categories via content
> types whose model derives from Geta's `Category`, using only standard EPiServer services, so it
> does not need Geta's configured categories root. Adjust per the Step 0 spike if Geta's value
> type isn't `IList`-shaped or lacks an `IEnumerable<ContentReference>` constructor.

- [ ] **Step 4: Run to verify it passes**

Run: `dotnet test src/EditorPowertools.Tests --filter GetaCategoryHandlerTests`
Expected: PASS.

- [ ] **Step 5: Register in DI**

```csharp
        services.AddSingleton<IPropertyTypeHandler>(sp =>
            new GetaCategoryHandler(
                sp.GetRequiredService<IContentLoader>(),
                sp.GetRequiredService<IContentTypeRepository>(),
                sp.GetRequiredService<IContentModelUsage>()));
```

- [ ] **Step 6: Full suite + build + commit**

Run: `dotnet test` then `dotnet build src/EditorPowertools/EditorPowertools.csproj`
Expected: all PASS + Build succeeded (both TFMs).
```bash
git add src/EditorPowertools/PropertyTypes/Handlers/GetaCategoryHandler.cs src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/PropertyTypes/GetaCategoryHandlerTests.cs
git commit -m "feat(propertytypes): GETA category handler (reflection, full edit)"
```

---

## Final verification

- [ ] Run the full suite on both TFMs: `dotnet test` → all green.
- [ ] Build: `dotnet build` → 0 errors.
- [ ] Manual sample-site pass (Task 10 Step 4 checklist) for Bulk Edit; import a CSV mapping a category/selection/list column and confirm values land via the registry.
- [ ] Confirm no `ConvertPropertyValue` / `ConvertValue` / `IsEditableType` remnants remain (`grep -rn "ConvertPropertyValue\|ConvertValue\|IsEditableType" src/EditorPowertools` should return nothing).

## Notes on scope decisions captured during planning

- **GETA editing** is full edit in v1 (Task 11) via reflective write-back + a category picker. It degrades to read-only display if the Geta assembly isn't loaded (no raw-object cells either way). A Step 0 spike verifies Geta's value-type shape before implementation, since it can't be validated in CI.
- **ContentArea / blocks / LinkItemCollection** fall through to the `FallbackHandler` → readable summary, read-only (per spec non-goals).
- The plan assumes EPiServer category + selection APIs are stable across CMS 12/13; any divergence is a Tier-1 `#if` inside the single affected handler (no new conditional package refs).
