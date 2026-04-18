# CMS 13 content-type awareness across EditorPowertools

**Date:** 2026-04-17
**Status:** Approved (design phase)

## Background

EditorPowertools multi-targets Optimizely CMS 12 (.NET 8) and CMS 13 (.NET 10). CMS 13 introduces three new content-modelling concepts that several tools surface or filter by but currently ignore:

- **Contracts** — content types declared as `.NET` interfaces inheriting `IContentData` and flagged with `ContentTypeAttribute`. Exposed as `ContentType.IsContract` (`bool`) and `ContentType.Contracts` (`IEnumerable<ContentTypeReference>` of contracts a type implements).
- **Composition behaviors** — blocks can be `SectionEnabled`, `ElementEnabled`, both, or neither. Exposed as `ContentType.CompositionBehaviors` (`IEnumerable<CompositionBehavior>`) and authored via `ContentTypeAttribute.CompositionBehaviors` (`string[]`).
- The existing `ContentTypeBase` struct is **unchanged** between CMS 12 and CMS 13 (values: `Undefined / Page / Block / Folder / Media / Image / Video`). There is no new Base enum member — the new concepts are additional flags, not a new base.

None of these APIs exist in CMS 12 assemblies, so every addition must be gated strictly to the `net10.0` / `OPTIMIZELY_CMS13` build.

The term **"Orphaned"** — currently used to mean "content type exists in the database but has no matching .NET model class" — will also be renamed to **"Code-less"** as part of this work, because "Orphaned" was never a precise description of that state and is ambiguous now that Contracts introduce additional code↔DB relationships.

## Goals

1. Make ContentTypeAudit fully aware of Contract / Section / Element / applied Contracts.
2. Let ContentStatistics, BulkPropertyEditor, and ContentAudit surface those concepts where it is useful.
3. Keep CMS 12 behavior and UI identical — no new labels, columns, filters, or stats visible under CMS 12.
4. Minimize `#if` blocks in shared services by localizing version-specific code in a metadata provider (Tier 2 multi-target pattern).
5. Rename "Orphaned" to "Code-less" throughout the codebase (C#, JS, all 11 language files).
6. Update project documentation so CMS 13 support is discoverable.

## Non-goals

- Changes to CMS 12-only behavior.
- Changes to tools not listed above.
- New permissions or feature toggles — existing toggles per tool cover the new surface.
- Supporting Contracts authored outside the conventions in the CMS 13 release (e.g. dynamic runtime registration).

## Architecture

### Metadata provider (Tier 2 pattern)

All CMS 13-specific reads go through a single tiny service:

```
src/EditorPowertools/Abstractions/
    IContentTypeMetadataProvider.cs     // shared interface, always compiled
    ContentTypeMetadata.cs              // shared immutable record, always compiled

src/EditorPowertools/Cms12/
    Cms12ContentTypeMetadataProvider.cs // compiled for net8.0 only

src/EditorPowertools/Cms13/
    Cms13ContentTypeMetadataProvider.cs // compiled for net10.0 only
```

Public surface:

```csharp
public interface IContentTypeMetadataProvider
{
    ContentTypeMetadata Get(ContentType contentType);
}

public sealed record ContentTypeMetadata(
    bool IsContract,
    IReadOnlyList<ContractRef> Contracts,
    IReadOnlyList<string> CompositionBehaviors);

public sealed record ContractRef(int Id, Guid Guid, string Name, string? DisplayName);
```

The CMS 12 provider always returns `new ContentTypeMetadata(false, Array.Empty<ContractRef>(), Array.Empty<string>())`. The CMS 13 provider reads the real `ContentType` members. Shared services **never** use `#if` to access these concepts — they call the provider unconditionally.

DI registration lives in `ServiceCollectionExtensions.AddEditorPowertools` with a single `#if OPTIMIZELY_CMS13` / `#else` block choosing the implementation.

Csproj wiring (already the project pattern):

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <Compile Remove="Cms13\**\*.cs" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <Compile Remove="Cms12\**\*.cs" />
</ItemGroup>
```

### DTO changes

`ContentTypeDto` gains three nullable properties. On CMS 12 they serialize as `null` / absent; the JS treats absence as "feature not available" and renders nothing CMS 13-specific.

```csharp
public bool? IsContract { get; set; }
public string[]? CompositionBehaviors { get; set; } // e.g. ["SectionEnabled", "ElementEnabled"]
public ContractRef[]? Contracts { get; set; }
```

`PropertyDefinitionDto.Origin` enum value is renamed `Orphaned` → `Codeless`. The DTO shape and JSON key changes accordingly. This is an internal API with no external consumers; the C# and JS ship together so the rename can be atomic with no shim.

`ContentTypeDto.IsOrphaned` is renamed to `IsCodeless` on the same terms.

**Feature-detection convention:** the JS decides whether to render CMS 13-specific UI by checking whether the relevant nullable field is present on the DTO (`if (t.isContract != null)`, `if (stats.totalContracts != null)`, etc.). No separate `cms13Available` flag is introduced — each tool's response is self-describing.

## Tool-by-tool changes

### 4.1 ContentTypeAudit

**Service (`ContentTypeAuditService`)**
- `MapToDto` calls `IContentTypeMetadataProvider.Get(ct)` and copies values onto `ContentTypeDto`.
- `GetContentOfType` returns unchanged content — contracts are declared at type level and identical for every instance, so per-instance display would only add noise.
- `IsOrphaned` / `IsCodeless` — ModelType-null detection unchanged; only the field name changes.

**API controller**
- No new endpoints. Existing `GetTypes`, `GetProperties`, `GetContentOfType` carry the new fields.

**UI (`content-type-audit.js`)**
- Toolbar gets two new filter controls, rendered **only** when `allTypes.some(t => t.isContract != null)` (i.e. CMS 13):
  - "Kind": *All / Contract types only / Non-contract types*
  - "Composition" (visible when Base=Block or All): *Any / Section-enabled / Element-enabled / Both / Plain*
- Row rendering: if `t.isContract`, render a `Contract` pill; if `t.compositionBehaviors?.includes('SectionEnabled')`, render `Section`; same for `Element`. All pills use existing `ept-tag` / `ept-tag--*` styles.
- Stats strip: new card "Contracts" counts `allTypes.filter(t => t.isContract).length`, visible only when CMS 13 data is present.
- Type detail view: new collapsible panel "Applied contracts" listing `type.contracts` with each row linking to that contract's own detail view (same audit, `id=<contractId>`).
- Content-of-type list: unchanged — contracts are a type-level property and already visible in the type detail panel.
- All new strings localized under `/editorpowertools/contenttypeaudit/` paths and added to all 11 language files.

### 4.2 ContentStatistics

**Service (`ContentStatisticsService`)**
- `BuildSummary`: when a type's metadata shows `IsContract`, count its content toward a new `TotalContracts` bucket instead of `TotalBlocks`.
- `BuildTypeDistribution`: add a fourth first-class category `Contracts` (CMS 13 only; always zero on CMS 12 so it's filtered out by the existing `Where(kv => kv.Value > 0)`).
- Add a new method `BuildBlockBreakdown()` returning `{ Sections, Elements, Plain }` counts. Only populated when `OPTIMIZELY_CMS13` is defined (guarded via the metadata provider returning empty behaviors on CMS 12, so the three buckets are all zero and the API response is `null` rather than all-zeros — JS uses that null to hide the card).

**DTO (`SummaryStatsDto`, new `BlockBreakdownDto`)**
```csharp
public int? TotalContracts { get; set; } // null on CMS 12
public BlockBreakdownDto? BlockBreakdown { get; set; }
```

**UI**
- Main distribution pie/chart unchanged in layout; adds a "Contracts" slice only if non-null.
- New "Block breakdown" sub-card below the chart, rendered only when `blockBreakdown` is present.

### 4.3 BulkPropertyEditor

CMS 13 introduces a qualitative upgrade: users can select **a contract** as the target and the tool **expands** it to every content type implementing that contract at execution time.

**Service (`BulkPropertyEditorService`)**
- New method `ResolveTargetTypes(int[] typeIds)` returns the effective list of content-type IDs. For CMS 12 this is the identity function. For CMS 13: if any selected ID is a Contract, replace it with the set of non-contract types whose `Contracts` collection contains a reference to it.
- All existing "apply change to types" methods call `ResolveTargetTypes` first.

**API**
- `ListContentTypes` adds the CMS 13 metadata fields to each item in the picker response.
- New field `ResolvedTypes` on the preview/dry-run response so the UI can show "Contract X → expands to 4 types" before the user commits.

**UI**
- Picker gets a **Tabs** control (CMS 13 only): *Content types* / *Contracts*. Under CMS 12 the tabs are not rendered (single list as today).
- Section-enabled / Element-enabled badges show on block rows in the picker.
- When a Contract is selected, the preview panel shows the expanded concrete-type list.

### 4.4 ContentAudit

Lightweight — filter-only changes.

**Service**
- Add two optional filter parameters to the audit-run input: `ContractFilter` (`null | "include" | "exclude" | "only"`), `CompositionFilter` (`null | "section" | "element"`). Apply in the existing type-filter stage after loading content-type metadata.

**UI**
- Filter drawer gains two new `<select>` controls under a collapsible "CMS 13" section. Rendered only when the content-type list returned for the picker contains at least one type with `isContract != null` (same self-describing convention used elsewhere in this design).

### 4.5 Rename "Orphaned" → "Code-less"

Scope:
- C# symbols: `PropertyOrigin.Orphaned` → `PropertyOrigin.Codeless`, `ContentTypeDto.IsOrphaned` → `ContentTypeDto.IsCodeless`, `ContentTypeTreeNodeDto.IsOrphaned` → `ContentTypeTreeNodeDto.IsCodeless`.
- JS: every `t.isOrphaned` → `t.isCodeless`, class names `ept-row--orphaned` → `ept-row--codeless`.
- Localization keys:
  - `/editorpowertools/contenttypeaudit/stat_orphaned` → `/editorpowertools/contenttypeaudit/stat_codeless`
  - any other `orphaned` keys in `lang/*.xml` follow the same rename.
- English label becomes **"Code-less"**. Translations in the other 10 language files (da, sv, no, de, fi, fr, es, nl, ja, zh-CN) are provided during implementation — each translator chooses an equivalent native-language term for "content type that exists in the database but has no matching .NET model class".
- No shims, no backwards-compatibility aliases.

## Documentation updates

- `README.md` — add a short "CMS 13" subsection under Features listing: contract types surfaced in ContentTypeAudit, contract selection in BulkPropertyEditor, contract category in ContentStatistics.
- `docs/getting-started.md` — install note: CMS 13 requires .NET 10.
- `docs/coding-guidelines.md` — new "Multi-targeting" section summarizing the CLAUDE.md rules so contributors see them in repo docs too.
- New `docs/cms13-support.md` — single consolidated page: which tools know about Contracts / Section / Element, how contract expansion works in BulkPropertyEditor, how "Code-less" differs from Contract. Linked from README.

## Testing

- Unit test `Cms12ContentTypeMetadataProvider` returns empty metadata regardless of input.
- Unit test `Cms13ContentTypeMetadataProvider` against a fake `ContentType` with each combination of IsContract/CompositionBehaviors/Contracts. Lives under `Cms13/` so it only compiles for `net10.0`.
- Integration test for `BulkPropertyEditorService.ResolveTargetTypes`: contract → expansion, content type → identity, mixed selection → deduplicated union.
- JS: no dedicated test harness exists in the project — manual verification via the sample site on both CMS 12 and CMS 13 (once a CMS 13 sample site is available).

## Rollout

All changes ship together in one release since Spec A (foundation) → Spec B (audit UI) → Spec C (other tools) are internally coherent. Each tool's changes are behind its existing feature toggle.

## Out of scope

- Changes to SecurityAudit, LanguageAudit, PersonalizationAudit, ActiveEditors, LinkChecker, ScheduledJobsGantt, ManageChildren, ContentTypeRecommendations, VisitorGroupTester, ActivityTimeline, ContentDetails, ContentImporter, CmsDoctor, AudienceManager, Overview.
- Any UI or behavior change under CMS 12.
- Any new permission types or feature toggles.
