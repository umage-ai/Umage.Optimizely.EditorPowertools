# Shared property-type handlers for Bulk Property Editor & Content Importer

**Date:** 2026-06-16
**Issues:** #55 (pluggable property-type support — umbrella), #54 (GETA Categories — first concrete request)
**Status:** Approved design, pending implementation plan

## Problem

Both the **Bulk Property Editor** and the **Content Importer** independently convert between
string representations and Optimizely property values using private, type-name-matching
`switch` blocks:

- `BulkPropertyEditorService`: `IsEditableType` (a whitelist), `GetPropertyTypeName`,
  `ConvertPropertyValue`. Display is mostly `prop.Value?.ToString()`, so collection-backed
  properties (categories, lists) render as raw objects (e.g. `System.Collections...`) and are
  flagged not-editable — the exact #54 complaint.
- `ContentImporterService`: `SetPropertyValue` → `ConvertValue` (slightly richer; handles
  `PropertyList<T>` and `XhtmlString`).

This duplicates logic, special-cases types inconsistently, and gives no extension point for new
or third-party property types.

## Goal

One **pluggable property-type-handler** abstraction in the core package, consumed by **both**
tools, where each supported type provides:

- **Display** — a readable cell/preview value (resolved names, never a raw object dump).
- **Edit** — an inline-editor descriptor for the Bulk Editor (text / number / bool / date /
  url / reference / select / multiselect / category), with correct write-back.
- **Parse** — string → property value (used by both tools).
- A guaranteed **fallback** — unknown types render read-only with a sensible string.

New types light up in both tools at once. The fallback structurally guarantees no raw-object
dumps.

## Non-goals (v1 / YAGNI)

- ContentArea / block editing (read-only display only).
- `LinkItemCollection` editing (readable display only).
- Rich `XhtmlString` editing (plain-text edit only).
- A settings UI to enable/disable handlers.

## Architecture

New core namespace `UmageAI.Optimizely.EditorPowerTools.PropertyTypes`:

| Type | Purpose |
|---|---|
| `IPropertyTypeHandler` | The handler contract (one class per property type). |
| `PropertyHandlerContext` | What a handler receives. |
| `PropertyEditorDescriptor` + `EditorOption` | Describes the Bulk-Edit inline editor. |
| `PropertyTypeHandlerRegistry` | Resolves the handler for a given property. |
| Built-in handlers | Scalars, selection, category (native), GETA (reflection), property-list, content-reference, xhtml, fallback. |

Both `BulkPropertyEditorService` and `ContentImporterService` take the registry via DI and
delegate display/parse/editor decisions to it, replacing their private switches.

### The handler contract

```csharp
public interface IPropertyTypeHandler
{
    int  Priority { get; }                                    // higher = matched first
    bool CanHandle(PropertyHandlerContext ctx);
    string GetDisplay(PropertyHandlerContext ctx);            // readable cell value
    PropertyEditorDescriptor? GetEditor(PropertyHandlerContext ctx);  // null = read-only
    bool TryParse(string? input, PropertyHandlerContext ctx, out object? value);
}
```

- `GetDisplay` reads `ctx.Property.Value`.
- `GetEditor` returns `null` for read-only types (the editor concept is Bulk-Edit-only; the
  importer simply never calls it).
- `TryParse` returns `false` on unparseable input (callers record a generic warning; no
  exception leakage — consistent with the security-review error-handling rule).

### Context

```csharp
public sealed class PropertyHandlerContext
{
    public PropertyDefinition Definition { get; init; }   // always present
    public Type? ModelType { get; init; }                 // owning content-type CLR model
    public PropertyInfo? ModelProperty { get; init; }     // model property — for [SelectOne]/[SelectMany] attrs
    public PropertyData? Property { get; init; }          // instance value; present for display/parse, absent when listing columns
    // convenience accessors: PropertyDataType, PropertyData CLR type
}
```

`CanHandle` keys off `Definition` / `ModelProperty` (type + attributes) so it works during
column listing (no instance). `GetDisplay`/`TryParse` use `Property`/`Definition`.

### Editor descriptor

```csharp
public sealed class PropertyEditorDescriptor
{
    public string Kind { get; init; }                      // text|number|bool|date|url|reference|select|multiselect|category
    public IReadOnlyList<EditorOption>? Options { get; init; } // for select/multiselect/category
    public bool Multiple { get; init; }
}
public record EditorOption(string Value, string Label);
```

### Registry + matching

```csharp
public sealed class PropertyTypeHandlerRegistry
{
    private readonly IReadOnlyList<IPropertyTypeHandler> _handlers;
    public PropertyTypeHandlerRegistry(IEnumerable<IPropertyTypeHandler> handlers)
        => _handlers = handlers.OrderByDescending(h => h.Priority).ToList();
    public IPropertyTypeHandler Resolve(PropertyHandlerContext ctx)
        => _handlers.First(h => h.CanHandle(ctx)); // FallbackHandler guarantees a match
}
```

Handlers are registered in DI as `IEnumerable<IPropertyTypeHandler>`; the registry is a
singleton. The `FallbackHandler` has the lowest priority and always matches.

## v1 handlers (priority high → low)

1. **`CategoryListHandler`** — native EPiServer `PropertyCategory` / `CategoryList`. Display:
   category names via `CategoryRepository`. Editor: `category` (multiple) with options = all
   categories `{id, name}`. Parse: ids → `CategoryList`.
2. **`GetaCategoryHandler`** — reflection-only. `CanHandle` matches Geta's `CategoryList`
   PropertyData type by full type name; resolves names/options via Geta's category content
   through reflection. **Inert (`CanHandle=false`) if Geta types are not loaded — never throws,
   no compile-time dependency.**
3. **`SelectionHandler`** — model property carries `[SelectOne]`/`[SelectMany]`. Reads the
   `ISelectionFactory` items as options. Editor: `select` / `multiselect`. Parse: selected
   value(s) → string (single) or the SelectMany storage format (multi).
4. **`PropertyListHandler`** — `PropertyList<string>` / `PropertyList<int>`. Display: joined.
   Editor: `text` (delimited / JSON). Parse: reuse the importer's existing list-parse
   (JSON array or `;`/`|`/`,` delimited) → `IList<T>`.
5. **`ContentReferenceHandler`** — display `Name (ID: n)`; editor `reference` (picker); parse
   id → `ContentReference`. (Extracts the current special-case out of `BuildContentItemRow`.)
6. **Scalar handlers** — `StringHandler`, `NumberHandler`, `FloatHandler`, `BooleanHandler`,
   `DateHandler`, `UrlHandler`. Editors: text/number/bool/date/url. Parse mirrors today's
   `ConvertPropertyValue` / `ConvertValue`.
7. **`XhtmlStringHandler`** — display: stripped/plain text. Editor: `text` (plain-text edit).
8. **`FallbackHandler`** (lowest priority, always matches) — display: friendly summary —
   `""` for null, the string itself for strings, `"(n items)"` or a capped joined preview for
   `IEnumerable`, otherwise a friendly type label (never the raw `value.ToString()` type dump).
   Editor: `null` (read-only). `TryParse`: `false`.

## Tool integration

### Bulk Property Editor

- `GetProperties(contentTypeId)`: per `PropertyDefinition`, build a context (no instance),
  resolve the handler, set `Editable = GetEditor(ctx) != null`, and include the editor
  descriptor (`Kind` + `Options`) on `PropertyColumnInfo`. Replaces `IsEditableType` /
  `GetPropertyTypeName`.
- `BuildContentItemRow`: per cell, build a context with the instance; `display = GetDisplay(ctx)`,
  editability + type name from the handler. Replaces the inline `ContentReference` special-case
  and `prop.Value?.ToString()`.
- `SaveAsync` / `BulkSaveAsync`: `handler.TryParse(value, ctx, out var v)` → assign. Replaces
  `ConvertPropertyValue`. **Preserves the existing access checks and proper `AccessLevel` on
  writes (do not regress the security fixes).**
- **JS (`bulk-property-editor.js`)**: render inline editors from `descriptor.Kind` + `Options`
  instead of guessing from a type-name string. New control types: `select`, `multiselect`,
  `category` (checkbox/multi-select list of options).

### Content Importer

- `SetPropertyValue`: delegate to `handler.TryParse`. The `PropertyList<T>` and `XhtmlString`
  branches move into their handlers. **Image-download and content-area-from-blocks logic stays
  importer-specific — those are behaviors, not property types.**
- `GetContentTypeWithProperties`: mark a property as importable when a handler can parse it
  (`TryParse`-capable); the dry-run preview uses `GetDisplay`.
- Editor descriptors are not used by the importer (it maps columns → string values; the handler
  parses them).

## Error handling

- `TryParse` → `false` on bad input; the tool records a generic per-row/item warning. No
  exception messages surface to the client.
- Display/parse exceptions are caught at the registry call site → fall back to read-only display
  / skip, logged server-side.
- GETA reflection failures make the handler inert (`CanHandle=false`); they never throw.

## Multi-targeting (CMS 12 / 13)

- Handlers are shared code. `CategoryRepository` and `ISelectionFactory` are stable across CMS
  12/13; any divergence is a Tier-1 `#if` inside the single affected handler.
- GETA is never referenced (reflection only) → no conditional `<PackageReference>`.

## Testing

- **Per-handler unit tests:** `CanHandle` matching, `GetDisplay` for representative values,
  `TryParse` round-trip (valid + invalid), `GetEditor` descriptor shape.
- **Registry tests:** priority ordering; fallback always matches; `GetaCategoryHandler` inert
  when the Geta type is absent.
- **Per-tool integration tests:** a sample content type's properties resolve to the expected
  handlers and round-trip through both tools.
- Reuse the existing `EditorPowertools.Tests` patterns; run against both TFMs.

## Build order (suggested phases)

1. **Foundation** — interfaces, context, descriptor, registry, DI wiring, `FallbackHandler`,
   plus scalar + `ContentReference` handlers (behaviour-preserving extraction). Wire both tools
   to the registry. *No user-visible change yet beyond readable fallback display.*
2. **Display-everywhere** — `XhtmlStringHandler`, `PropertyListHandler` display, fallback
   collection summaries. Ship readable cells across all types.
3. **Editing set** — `SelectionHandler`, `CategoryListHandler` (native), `PropertyListHandler`
   edit, plus the Bulk-Edit JS for select/multiselect/category controls.
4. **GETA** — `GetaCategoryHandler` (reflection) + tests proving inert-when-absent. Closes #54.

## Dependencies / sequencing

This touches `BulkPropertyEditorService` and `ContentImporterService`, which are also modified
on `security/review-fixes-2026-06`. This work is branched from that branch
(`feature/property-type-handlers`) and should land after it, to avoid conflicts and to preserve
the security fixes (access checks, `AccessLevel`, SSRF guard, principal flow).
