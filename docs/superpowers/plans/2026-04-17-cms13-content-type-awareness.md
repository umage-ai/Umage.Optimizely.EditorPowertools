# CMS 13 content-type awareness — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make ContentTypeAudit, ContentStatistics, BulkPropertyEditor, and ContentAudit aware of CMS 13's new content-type concepts (Contract, CompositionBehaviors Section/Element, applied Contracts), rename the misleading term "Orphaned" to "Code-less", and update project documentation — all gated strictly to CMS 13.

**Architecture:** A single `IContentTypeMetadataProvider` service (Tier 2 multi-target pattern) exposes `IsContract`, `Contracts`, and `CompositionBehaviors` via a version-neutral record. The `Cms12…` implementation returns empty values; the `Cms13…` implementation reads the real CMS 13 APIs. Shared services and DTOs never use `#if`. JS treats absence of new nullable DTO fields as "feature not available" and renders nothing CMS 13-specific under CMS 12.

**Tech Stack:** .NET 8 (CMS 12) / .NET 10 (CMS 13), EPiServer.CMS 12.29 / 13.0.1, xUnit + Moq + FluentAssertions for tests, vanilla JS for UI, Optimizely XML localization.

**Spec:** `docs/superpowers/specs/2026-04-17-cms13-content-type-awareness-design.md`

---

## File Structure

**New files:**
- `src/EditorPowertools/Abstractions/IContentTypeMetadataProvider.cs` — interface (shared)
- `src/EditorPowertools/Abstractions/ContentTypeMetadata.cs` — `ContentTypeMetadata` + `ContractRef` records (shared)
- `src/EditorPowertools/Cms12/Cms12ContentTypeMetadataProvider.cs` — empty-values impl (net8.0 only)
- `src/EditorPowertools/Cms13/Cms13ContentTypeMetadataProvider.cs` — real impl (net10.0 only)
- `src/EditorPowertools.Tests/Tools/ContentTypeMetadataProviderTests.cs` — TFM-gated tests
- `src/EditorPowertools.Tests/Tools/BulkPropertyEditor/ResolveTargetTypesTests.cs`
- `docs/cms13-support.md` — consolidated CMS 13 support page

**Modified files (C#):**
- `src/EditorPowertools/EditorPowertools.csproj` — add Cms12/Cms13 compile conditions
- `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs` — register provider
- `src/EditorPowertools/Tools/ContentTypeAudit/Models/ContentTypeDto.cs` — new nullable fields, rename IsOrphaned
- `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs` — populate metadata, rename
- `src/EditorPowertools/Tools/ContentStatistics/Models/ContentStatisticsDtos.cs` — new fields
- `src/EditorPowertools/Tools/ContentStatistics/ContentStatisticsService.cs` — use metadata
- `src/EditorPowertools/Tools/BulkPropertyEditor/Models/BulkPropertyEditorDtos.cs` — new fields
- `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorService.cs` — ResolveTargetTypes
- `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorController.cs` — expose new data
- `src/EditorPowertools/Tools/ContentAudit/Models/ContentAuditDtos.cs` — filter params
- `src/EditorPowertools/Tools/ContentAudit/ContentAuditService.cs` — apply filters

**Modified files (JS):**
- `modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`
- `modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js`
- `modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js`
- `modules/_protected/EditorPowertools/ClientResources/js/content-audit.js`

**Modified files (localization):** all 11 files under `src/EditorPowertools/lang/`:
`da.xml`, `de.xml`, `en.xml`, `es.xml`, `fi.xml`, `fr.xml`, `ja.xml`, `nl.xml`, `no.xml`, `sv.xml`, `zh-CN.xml`

**Modified files (docs):**
- `README.md`
- `docs/getting-started.md`
- `docs/coding-guidelines.md`

**Out of scope — intentionally NOT changed:**
- `src/EditorPowertools/Tools/CmsDoctor/...` — uses the word "orphaned" for a separate concept (orphan property definitions). Preserve existing wording.
- `modules/_protected/EditorPowertools/ClientResources/js/link-checker.js` uses the CSS class `ept-row--orphaned` purely for danger-row styling. We will keep the CSS class name unchanged (it's a styling class, not a user-facing label); only JS field names and user-facing strings are renamed.
- The CSS rule `.ept-table tr.ept-row--orphaned td { … }` in `editorpowertools.css` stays. This is noted in the rename task.

---

## Task 1: Foundation — shared metadata provider + records

**Files:**
- Create: `src/EditorPowertools/Abstractions/IContentTypeMetadataProvider.cs`
- Create: `src/EditorPowertools/Abstractions/ContentTypeMetadata.cs`
- Create: `src/EditorPowertools/Cms12/Cms12ContentTypeMetadataProvider.cs`
- Create: `src/EditorPowertools/Cms13/Cms13ContentTypeMetadataProvider.cs`
- Modify: `src/EditorPowertools/EditorPowertools.csproj`
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`
- Test: `src/EditorPowertools.Tests/Tools/ContentTypeMetadataProviderTests.cs`

- [ ] **Step 1.1: Create the shared interface**

Write `src/EditorPowertools/Abstractions/IContentTypeMetadataProvider.cs`:

```csharp
using EPiServer.DataAbstraction;

namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

/// <summary>
/// Exposes CMS-version-specific ContentType metadata through a stable shape.
/// Under CMS 12 all values are empty/false. Under CMS 13 values reflect real data.
/// Consumers never branch on CMS version themselves.
/// </summary>
public interface IContentTypeMetadataProvider
{
    ContentTypeMetadata Get(ContentType contentType);
}
```

- [ ] **Step 1.2: Create the shared records**

Write `src/EditorPowertools/Abstractions/ContentTypeMetadata.cs`:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

public sealed record ContentTypeMetadata(
    bool IsContract,
    IReadOnlyList<ContractRef> Contracts,
    IReadOnlyList<string> CompositionBehaviors)
{
    public static readonly ContentTypeMetadata Empty = new(
        IsContract: false,
        Contracts: Array.Empty<ContractRef>(),
        CompositionBehaviors: Array.Empty<string>());
}

public sealed record ContractRef(int Id, Guid Guid, string Name, string? DisplayName);
```

- [ ] **Step 1.3: Create the CMS 12 provider (empty implementation)**

Write `src/EditorPowertools/Cms12/Cms12ContentTypeMetadataProvider.cs`:

```csharp
using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Cms12;

/// <summary>CMS 12 has no Contracts or CompositionBehaviors; always returns Empty.</summary>
internal sealed class Cms12ContentTypeMetadataProvider : IContentTypeMetadataProvider
{
    public ContentTypeMetadata Get(ContentType contentType) => ContentTypeMetadata.Empty;
}
```

- [ ] **Step 1.4: Create the CMS 13 provider (real implementation)**

Write `src/EditorPowertools/Cms13/Cms13ContentTypeMetadataProvider.cs`:

```csharp
using EPiServer.DataAbstraction;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;

namespace UmageAI.Optimizely.EditorPowerTools.Cms13;

internal sealed class Cms13ContentTypeMetadataProvider : IContentTypeMetadataProvider
{
    private readonly IContentTypeRepository _contentTypeRepository;

    public Cms13ContentTypeMetadataProvider(IContentTypeRepository contentTypeRepository)
    {
        _contentTypeRepository = contentTypeRepository;
    }

    public ContentTypeMetadata Get(ContentType contentType)
    {
        var contracts = (contentType.Contracts ?? Enumerable.Empty<ContentTypeReference>())
            .Select(ResolveContract)
            .Where(c => c is not null)
            .Cast<ContractRef>()
            .ToList();

        var behaviors = (contentType.CompositionBehaviors ?? Enumerable.Empty<CompositionBehavior>())
            .Select(cb => cb.ToString())
            .ToList();

        return new ContentTypeMetadata(
            IsContract: contentType.IsContract,
            Contracts: contracts,
            CompositionBehaviors: behaviors);
    }

    private ContractRef? ResolveContract(ContentTypeReference reference)
    {
        var ct = _contentTypeRepository.Load(reference.ID);
        if (ct == null) return null;
        return new ContractRef(ct.ID, ct.GUID, ct.Name, ct.DisplayName);
    }
}
```

- [ ] **Step 1.5: Add Cms12/Cms13 compile conditions to the csproj**

Edit `src/EditorPowertools/EditorPowertools.csproj`. Locate the existing TFM-conditional items (around the CMS 13 Razor layout block) and add immediately after:

```xml
  <!-- Version-specific compile folders -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <Compile Remove="Cms13\**\*.cs" />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <Compile Remove="Cms12\**\*.cs" />
  </ItemGroup>
```

- [ ] **Step 1.6: Register the provider in DI**

Edit `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`. Add `using UmageAI.Optimizely.EditorPowerTools.Abstractions;` to the using list. Inside `AddEditorPowertools(this IServiceCollection services, Action<EditorPowertoolsOptions> configureOptions)`, right after the `services.AddOptions()` / configuration setup and before tool registrations, add:

```csharp
#if OPTIMIZELY_CMS13
        services.AddSingleton<IContentTypeMetadataProvider, Cms13.Cms13ContentTypeMetadataProvider>();
#else
        services.AddSingleton<IContentTypeMetadataProvider, Cms12.Cms12ContentTypeMetadataProvider>();
#endif
```

Exact placement: find the first `services.AddTransient<...Service>()` call (around line 69 where `ContentTypeAuditService` is registered) and insert the block on the lines above it.

- [ ] **Step 1.7: Write the failing test for CMS 12 provider**

Write `src/EditorPowertools.Tests/Tools/ContentTypeMetadataProviderTests.cs`:

```csharp
using EPiServer.DataAbstraction;
using FluentAssertions;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
#if OPTIMIZELY_CMS12
using UmageAI.Optimizely.EditorPowerTools.Cms12;
#endif
#if OPTIMIZELY_CMS13
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Cms13;
#endif

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools;

public class ContentTypeMetadataProviderTests
{
    public ContentTypeMetadataProviderTests()
    {
        EpiServerTestSetup.EnsureInitialized();
    }

#if OPTIMIZELY_CMS12
    [Fact]
    public void Cms12Provider_AlwaysReturnsEmpty()
    {
        var provider = new Cms12ContentTypeMetadataProvider();
        var ct = new ContentType { ID = 1, Name = "Test" };

        var result = provider.Get(ct);

        result.Should().Be(ContentTypeMetadata.Empty);
        result.IsContract.Should().BeFalse();
        result.Contracts.Should().BeEmpty();
        result.CompositionBehaviors.Should().BeEmpty();
    }
#endif

#if OPTIMIZELY_CMS13
    [Fact]
    public void Cms13Provider_ReadsRealMembers()
    {
        var repo = new Mock<IContentTypeRepository>();
        var provider = new Cms13ContentTypeMetadataProvider(repo.Object);

        var ct = new ContentType { ID = 1, Name = "Test" };
        // CompositionBehaviors setter — use reflection since property is public get but setter may be protected
        typeof(ContentType).GetProperty("CompositionBehaviors")!
            .SetValue(ct, new[] { CompositionBehavior.SectionEnabled });
        typeof(ContentType).GetProperty("IsContract")!
            .SetValue(ct, false);
        typeof(ContentType).GetProperty("Contracts")!
            .SetValue(ct, Array.Empty<ContentTypeReference>());

        var result = provider.Get(ct);

        result.IsContract.Should().BeFalse();
        result.CompositionBehaviors.Should().ContainSingle().Which.Should().Be("SectionEnabled");
        result.Contracts.Should().BeEmpty();
    }
#endif
}
```

- [ ] **Step 1.8: Run the test — expect FAIL for CMS 12 (compile error) because setup helper not ready, then PASS after it compiles**

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0 --filter ContentTypeMetadataProviderTests`
Expected: PASS for CMS 12 — the empty provider is trivial.

Run: `dotnet test src/EditorPowertools.Tests --framework net10.0 --filter ContentTypeMetadataProviderTests`
Expected: PASS for CMS 13 — reflection-driven setup exercises real providers.

If reflection setters fail under the real CMS 13 assembly (internal setters), fall back to using `ContentType.CreateWritableClone()` and its internal setters via the existing public property (`ContentType.CompositionBehaviors = ...` if settable) or an `InternalsVisibleTo` workaround. Document that fallback in the test file if needed.

- [ ] **Step 1.9: Build both TFMs to confirm compile conditions work**

Run: `dotnet build src/EditorPowertools --framework net8.0`
Expected: SUCCEEDS. No reference to CMS 13 types.

Run: `dotnet build src/EditorPowertools --framework net10.0`
Expected: SUCCEEDS. No reference to CMS 12-specific stubs.

- [ ] **Step 1.10: Commit**

```bash
git add src/EditorPowertools/Abstractions/ src/EditorPowertools/Cms12/ src/EditorPowertools/Cms13/ src/EditorPowertools/EditorPowertools.csproj src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs src/EditorPowertools.Tests/Tools/ContentTypeMetadataProviderTests.cs
git commit -m "feat(core): add IContentTypeMetadataProvider multi-target abstraction"
```

---

## Task 2: Extend ContentTypeDto and PropertyOrigin with CMS 13 fields (no rename yet)

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentTypeAudit/Models/ContentTypeDto.cs`

- [ ] **Step 2.1: Add nullable CMS 13 fields to `ContentTypeDto`**

Edit `ContentTypeDto.cs`. Add the following properties immediately after the existing `StatisticsUpdated` property, keeping the using import at the top:

```csharp
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
```

Inside `public class ContentTypeDto` add:

```csharp
    // CMS 13 metadata — null on CMS 12, populated on CMS 13
    public bool? IsContract { get; set; }
    public string[]? CompositionBehaviors { get; set; }
    public ContractRef[]? Contracts { get; set; }
```

- [ ] **Step 2.2: Build to confirm**

Run: `dotnet build src/EditorPowertools --framework net8.0`
Expected: SUCCEEDS.

Run: `dotnet build src/EditorPowertools --framework net10.0`
Expected: SUCCEEDS.

- [ ] **Step 2.3: Commit**

```bash
git add src/EditorPowertools/Tools/ContentTypeAudit/Models/ContentTypeDto.cs
git commit -m "feat(content-type-audit): add nullable CMS 13 metadata fields to ContentTypeDto"
```

---

## Task 3: Rename Orphaned → Codeless (C# symbols only)

This is a pure rename. No behavior changes. JS and i18n follow in later tasks so the build can be verified step by step.

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentTypeAudit/Models/ContentTypeDto.cs`
- Modify: `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs`
- Modify: `src/EditorPowertools.Tests/Tools/ContentTypeAudit/ContentTypeAuditServiceTests.cs` (if it references the old name)

- [ ] **Step 3.1: Rename `PropertyOrigin.Orphaned` → `PropertyOrigin.Codeless`**

Edit `src/EditorPowertools/Tools/ContentTypeAudit/Models/ContentTypeDto.cs`. Change the enum:

```csharp
public enum PropertyOrigin
{
    Defined,
    Inherited,
    /// <summary>Exists in database but not on the model (code-less).</summary>
    Codeless
}
```

Also update the summary comment on `PropertyDefinitionDto.Origin`:

```csharp
    /// <summary>Code-defined, inherited from parent, or code-less (only in DB).</summary>
    public PropertyOrigin Origin { get; set; }
```

- [ ] **Step 3.2: Rename `ContentTypeDto.IsOrphaned` → `ContentTypeDto.IsCodeless`**

In the same file, rename:

```csharp
    public bool IsCodeless { get; set; }
```

- [ ] **Step 3.3: Rename `ContentTypeTreeNodeDto.IsOrphaned` → `ContentTypeTreeNodeDto.IsCodeless`**

In the same file, rename:

```csharp
public class ContentTypeTreeNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public int? ContentCount { get; set; }
    public bool IsCodeless { get; set; }
    public List<ContentTypeTreeNodeDto> Children { get; set; } = new();
}
```

- [ ] **Step 3.4: Update all usages in `ContentTypeAuditService.cs`**

Edit `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs`:

In `GetProperties`, change the origin assignment block from:
```csharp
                if (!pd.ExistsOnModel)
                    origin = PropertyOrigin.Orphaned;
```
to:
```csharp
                if (!pd.ExistsOnModel)
                    origin = PropertyOrigin.Codeless;
```

In `BuildTreeNode`, change:
```csharp
            IsOrphaned = contentType.ModelType == null,
```
to:
```csharp
            IsCodeless = contentType.ModelType == null,
```

In `MapToDto`, change:
```csharp
            IsOrphaned = ct.ModelType == null,
```
to:
```csharp
            IsCodeless = ct.ModelType == null,
```

- [ ] **Step 3.5: Update test file if it references the old names**

Run: `grep -n "IsOrphaned\|PropertyOrigin.Orphaned" src/EditorPowertools.Tests/Tools/ContentTypeAudit/ContentTypeAuditServiceTests.cs`

If matches exist, rename them exactly (`IsOrphaned` → `IsCodeless`, `PropertyOrigin.Orphaned` → `PropertyOrigin.Codeless`).

- [ ] **Step 3.6: Build both TFMs**

Run: `dotnet build src/EditorPowertools --framework net8.0`
Expected: SUCCEEDS. 0 errors.

Run: `dotnet build src/EditorPowertools --framework net10.0`
Expected: SUCCEEDS. 0 errors.

- [ ] **Step 3.7: Run existing tests**

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0 --filter ContentTypeAudit`
Expected: PASS (same tests as before, field-renamed only).

- [ ] **Step 3.8: Commit**

```bash
git add src/EditorPowertools/Tools/ContentTypeAudit/ src/EditorPowertools.Tests/Tools/ContentTypeAudit/
git commit -m "refactor(content-type-audit): rename Orphaned -> Codeless in C# symbols"
```

---

## Task 4: Rename Orphaned → Codeless in JS (content-type-audit.js)

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`

We keep the CSS class `ept-row--orphaned` unchanged because it's reused by `link-checker.js` for an unrelated concept. Only the JS property-name references are updated.

- [ ] **Step 4.1: Rename `t.isOrphaned` property references and stat-label keys**

Find-and-replace inside `content-type-audit.js`:

- Replace `t.isOrphaned` with `t.isCodeless` (4 occurrences expected — in `renderStats`, row class-name resolver, tree badge, columns list).
- Replace `r.isOrphaned` with `r.isCodeless` (2 occurrences in row helpers).
- Replace `node.isOrphaned` with `node.isCodeless` (1 occurrence in `renderTree`).
- Replace `origin === 2` comments (the `Orphaned` enum was value 2) — leave the integer as-is since the enum still returns 2 at index position for `Codeless`, but update the adjacent string literal:

  Change:
  ```js
  const label = r.origin === 0 ? EPT.s('contenttypeaudit.origin_defined', 'Defined') : r.origin === 1 ? EPT.s('contenttypeaudit.origin_inherited', 'Inherited') : EPT.s('contenttypeaudit.origin_orphaned', 'Orphaned');
  ```
  to:
  ```js
  const label = r.origin === 0 ? EPT.s('contenttypeaudit.origin_defined', 'Defined') : r.origin === 1 ? EPT.s('contenttypeaudit.origin_inherited', 'Inherited') : EPT.s('contenttypeaudit.origin_codeless', 'Code-less');
  ```

- Update every localization key:
  - `contenttypeaudit.stat_orphaned` → `contenttypeaudit.stat_codeless`
  - `contenttypeaudit.badge_orphaned` → `contenttypeaudit.badge_codeless`
  - `contenttypeaudit.legend_orphaneddesc` → `contenttypeaudit.legend_codelessdesc`
  - `contenttypeaudit.origin_orphaned` → `contenttypeaudit.origin_codeless`

- The inline-label fallback strings (second arg to `EPT.s(...)`) change from `'Orphaned'` to `'Code-less'`, and `'Orphaned (not in code)'` to `'Code-less (not in code)'`.

- Replace the `{ key: 'isOrphaned', label: 'Orphaned' }` CSV-export column descriptor with `{ key: 'isCodeless', label: 'Code-less' }`.

- Do **NOT** change any `ept-row--orphaned` or `ept-badge--danger` CSS class references. They stay as-is.

- [ ] **Step 4.2: Manual grep to verify no stale references**

Run: `grep -nE "isOrphaned|stat_orphaned|badge_orphaned|origin_orphaned|legend_orphaneddesc" src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`
Expected: no output.

- [ ] **Step 4.3: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js
git commit -m "refactor(content-type-audit): rename Orphaned -> Code-less in UI JS"
```

---

## Task 5: Rename Orphaned → Codeless in all 11 localization files

**Files:**
- Modify: `src/EditorPowertools/lang/en.xml`
- Modify: `src/EditorPowertools/lang/da.xml`
- Modify: `src/EditorPowertools/lang/sv.xml`
- Modify: `src/EditorPowertools/lang/no.xml`
- Modify: `src/EditorPowertools/lang/de.xml`
- Modify: `src/EditorPowertools/lang/fi.xml`
- Modify: `src/EditorPowertools/lang/fr.xml`
- Modify: `src/EditorPowertools/lang/es.xml`
- Modify: `src/EditorPowertools/lang/nl.xml`
- Modify: `src/EditorPowertools/lang/ja.xml`
- Modify: `src/EditorPowertools/lang/zh-CN.xml`

**Do NOT touch the `orphanedpropertycheck` block** — that belongs to CmsDoctor and refers to a different concept (property definitions, not content types).

- [ ] **Step 5.1: Rename keys and values in `en.xml`**

In `src/EditorPowertools/lang/en.xml`, rename the following keys (exact matches only, inside the `contenttypeaudit` area):

```
<stat_orphaned>Orphaned</stat_orphaned>
→ <stat_codeless>Code-less</stat_codeless>

<badge_orphaned>Orphaned</badge_orphaned>
→ <badge_codeless>Code-less</badge_codeless>

<legend_orphaneddesc>Orphaned (not in code)</legend_orphaneddesc>
→ <legend_codelessdesc>Code-less (not in code)</legend_codelessdesc>

<origin_orphaned>Orphaned</origin_orphaned>
→ <origin_codeless>Code-less</origin_codeless>
```

- [ ] **Step 5.2: Rename the same four keys in each other language file**

For each of `da, sv, no, de, fi, fr, es, nl, ja, zh-CN`:

1. Find the `<stat_orphaned>`, `<badge_orphaned>`, `<legend_orphaneddesc>`, `<origin_orphaned>` elements inside the `contenttypeaudit` block.
2. Rename the element names to the `_codeless` forms.
3. Replace the inner text with an equivalent native-language term for "content type that exists in the database but has no matching .NET model class". Suggested replacements below — adjust to match existing translation conventions in each file:

| File | Old value | New value (suggested) |
|------|-----------|-----------------------|
| `da.xml` | "Forældreløs" | "Kodeløs" |
| `sv.xml` | "Föräldralös" | "Kodlös" |
| `no.xml` | "Foreldreløs" | "Kodeløs" |
| `de.xml` | "Verwaist" | "Ohne Code" |
| `fi.xml` | "Orpo" | "Koodittomat" |
| `fr.xml` | "Orpheline" | "Sans code" |
| `es.xml` | "Huérfano" | "Sin código" |
| `nl.xml` | "Verweesd" | "Codeloos" |
| `ja.xml` | "孤立" | "コードなし" |
| `zh-CN.xml` | "孤立" | "无代码" |

For the two-word legend entry (e.g. "Orphaned (not in code)"), the "(not in code)" part stays as the parenthetical equivalent in each language; only the leading noun changes.

- [ ] **Step 5.3: Search for stragglers**

Run:
```bash
grep -rn "stat_orphaned\|badge_orphaned\|origin_orphaned\|legend_orphaneddesc" src/EditorPowertools/lang/
```
Expected: no output.

- [ ] **Step 5.4: Commit**

```bash
git add src/EditorPowertools/lang/
git commit -m "i18n: rename Orphaned -> Code-less in content-type-audit strings (11 languages)"
```

---

## Task 6: Populate CMS 13 metadata in ContentTypeAuditService

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs`
- Modify: `src/EditorPowertools.Tests/Tools/ContentTypeAudit/ContentTypeAuditServiceTests.cs`

- [ ] **Step 6.1: Write the failing test — CMS 13 metadata flows into DTO**

Add to `ContentTypeAuditServiceTests.cs` (using the existing test-class conventions):

```csharp
    [Fact]
    public void GetAllContentTypes_PopulatesMetadataFromProvider()
    {
        var ct = CreateContentType(42, "Promo", "Promo Block", "Blocks");
        _contentTypeRepo.Setup(r => r.List()).Returns(new[] { ct });
        _statisticsRepo.Setup(r => r.GetAll())
            .Returns(Array.Empty<ContentTypeStatisticsRecord>());

        var metadataProvider = new Mock<IContentTypeMetadataProvider>();
        metadataProvider.Setup(p => p.Get(ct))
            .Returns(new ContentTypeMetadata(
                IsContract: false,
                Contracts: new[] { new ContractRef(7, Guid.Empty, "IHasSeo", "HasSeo") },
                CompositionBehaviors: new[] { "SectionEnabled" }));

        var service = new ContentTypeAuditService(
            _contentTypeRepo.Object,
            _contentModelUsage.Object,
            _contentLoader.Object,
            _softLinkRepo.Object,
            _propertyDefinitionRepo.Object,
            _statisticsRepo.Object,
            metadataProvider.Object,
            Mock.Of<ILogger<ContentTypeAuditService>>());

        var dtos = service.GetAllContentTypes().ToList();

        dtos.Single().IsContract.Should().BeFalse();
        dtos.Single().CompositionBehaviors.Should().ContainSingle().Which.Should().Be("SectionEnabled");
        dtos.Single().Contracts.Should().ContainSingle().Which.Name.Should().Be("IHasSeo");
    }
```

(Adjust the existing constructor call in the class-level `_service` setup to pass a default `Mock.Of<IContentTypeMetadataProvider>()` so other tests keep compiling.)

- [ ] **Step 6.2: Run test — expect compile error on service constructor**

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0 --filter ContentTypeAudit`
Expected: BUILD FAILURE ("no overload of `ContentTypeAuditService` accepts 8 args").

- [ ] **Step 6.3: Create the feature-flag helper FIRST (used by the service edits below)**

Create `src/EditorPowertools/Abstractions/CmsFeatureFlags.cs`:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Abstractions;

internal static class CmsFeatureFlags
{
#if OPTIMIZELY_CMS13
    public const bool ContractsAvailable = true;
#else
    public const bool ContractsAvailable = false;
#endif
}
```

This is the single `#if` in shared code, localized to one file. Services read `CmsFeatureFlags.ContractsAvailable` without branching.

- [ ] **Step 6.4: Modify `ContentTypeAuditService` to inject provider**

Edit `src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs`:

At the top, add:
```csharp
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
```

In the class, add field:
```csharp
    private readonly IContentTypeMetadataProvider _metadataProvider;
```

Update constructor signature and body to take the provider as the last-but-one argument (keep `ILogger` last):

```csharp
    public ContentTypeAuditService(
        IContentTypeRepository contentTypeRepository,
        IContentModelUsage contentModelUsage,
        IContentLoader contentLoader,
        IContentSoftLinkRepository softLinkRepository,
        IPropertyDefinitionRepository propertyDefinitionRepository,
        ContentTypeStatisticsRepository statisticsRepository,
        IContentTypeMetadataProvider metadataProvider,
        ILogger<ContentTypeAuditService> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentModelUsage = contentModelUsage;
        _contentLoader = contentLoader;
        _softLinkRepository = softLinkRepository;
        _propertyDefinitionRepository = propertyDefinitionRepository;
        _statisticsRepository = statisticsRepository;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }
```

Change `MapToDto` from a `static` method to an instance method and populate the new fields. Replace the existing method with:

```csharp
    private ContentTypeDto MapToDto(ContentType ct, ContentTypeStatisticsRecord? stats)
    {
        var metadata = _metadataProvider.Get(ct);
        return new ContentTypeDto
        {
            Id = ct.ID,
            Guid = ct.GUID,
            Name = ct.Name,
            DisplayName = ct.DisplayName,
            Description = ct.LocalizedDescription ?? ct.Description,
            GroupName = ct.GroupName,
            Base = ct.Base.ToString(),
            ModelType = ct.ModelTypeString,
            ParentTypeName = ct.ModelType?.BaseType?.Name,
            DefaultController = ct.DefaultMvcController?.Name,
            EditUrl = $"{Paths.ToResource("EPiServer.Cms.UI.Admin", "default")}#/ContentType/{ct.GUID}",
            PropertyCount = ct.PropertyDefinitions.Count,
            IsSystemType = IsSystemType(ct),
            IsCodeless = ct.ModelType == null,
            IconUrl = GetIconUrl(ct),
            Created = ct.Created,
            Saved = ct.Saved,
            SavedBy = ct.SavedBy,
            ContentCount = stats?.ContentCount,
            PublishedCount = stats?.PublishedCount,
            ReferencedCount = stats?.ReferencedCount,
            UnreferencedCount = stats?.UnreferencedCount,
            StatisticsUpdated = stats?.LastUpdated,
            IsContract = CmsFeatureFlags.ContractsAvailable ? metadata.IsContract : null,
            CompositionBehaviors = CmsFeatureFlags.ContractsAvailable ? metadata.CompositionBehaviors.ToArray() : null,
            Contracts = CmsFeatureFlags.ContractsAvailable ? metadata.Contracts.ToArray() : null
        };
    }
```

Also update the call in `GetAllContentTypes` — it already calls `MapToDto(ct, stats)` so no change required, but remove the `static` keyword from `MapToDto`.

- [ ] **Step 6.5: Run test — expect PASS**

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0 --filter GetAllContentTypes_PopulatesMetadataFromProvider`
Expected: PASS. Under CMS 12, `IsContract/CompositionBehaviors/Contracts` are null (because `ContractsAvailable == false`).

Run: `dotnet test src/EditorPowertools.Tests --framework net10.0 --filter GetAllContentTypes_PopulatesMetadataFromProvider`
Expected: PASS. Fields are populated.

The test assertion for CMS 12 needs adjustment — split into two facts or use `#if`:

```csharp
#if OPTIMIZELY_CMS13
        dtos.Single().IsContract.Should().BeFalse();
        dtos.Single().CompositionBehaviors.Should().ContainSingle().Which.Should().Be("SectionEnabled");
        dtos.Single().Contracts.Should().ContainSingle().Which.Name.Should().Be("IHasSeo");
#else
        dtos.Single().IsContract.Should().BeNull();
        dtos.Single().CompositionBehaviors.Should().BeNull();
        dtos.Single().Contracts.Should().BeNull();
#endif
```

- [ ] **Step 6.6: Also update `BuildTreeNode` to set `IsCodeless` only (no contract fields yet in tree node)**

Verify no change needed — `IsCodeless` remains `contentType.ModelType == null`. Contracts are not part of the tree DTO in this design.

- [ ] **Step 6.7: Run full test suite**

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0`
Expected: all pass.

Run: `dotnet test src/EditorPowertools.Tests --framework net10.0`
Expected: all pass.

- [ ] **Step 6.8: Commit**

```bash
git add src/EditorPowertools/Tools/ContentTypeAudit/ContentTypeAuditService.cs src/EditorPowertools/Abstractions/CmsFeatureFlags.cs src/EditorPowertools.Tests/Tools/ContentTypeAudit/ContentTypeAuditServiceTests.cs
git commit -m "feat(content-type-audit): populate CMS 13 metadata (IsContract, CompositionBehaviors, Contracts) in DTO"
```

---

## Task 7: ContentTypeAudit UI — badges + "Contracts" stat card

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 7.1: Add badge rendering in `renderRow` / `buildBadges` area**

Locate the existing badge-building block in `content-type-audit.js` (around `else if (r.isOrphaned)` — now `r.isCodeless`). Immediately after that `else if`, add:

```js
        if (r.isContract) {
            badges.push(`<span class="ept-badge ept-badge--info">${EPT.s('contenttypeaudit.badge_contract', 'Contract')}</span>`);
        }
        if (r.compositionBehaviors?.includes('SectionEnabled')) {
            badges.push(`<span class="ept-badge ept-badge--accent">${EPT.s('contenttypeaudit.badge_section', 'Section')}</span>`);
        }
        if (r.compositionBehaviors?.includes('ElementEnabled')) {
            badges.push(`<span class="ept-badge ept-badge--accent">${EPT.s('contenttypeaudit.badge_element', 'Element')}</span>`);
        }
```

- [ ] **Step 7.2: Add "Contracts" stat card (CMS 13 only)**

In `renderStats`, after the existing `orphaned` stat calc (now `codeless`), add:

```js
        const hasCms13 = allTypes.some(t => t.isContract != null);
        const contracts = hasCms13 ? allTypes.filter(t => t.isContract).length : null;
```

And in the returned `innerHTML` template, add just before the `Showing` card, guarded by non-null contracts:

```js
${contracts != null ? `<div class="ept-stat"><div class="ept-stat__value">${contracts}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_contracts', 'Contracts')}</div></div>` : ''}
```

- [ ] **Step 7.3: Add 4 new localization keys to `en.xml` inside the `contenttypeaudit` block**

```xml
<stat_contracts>Contracts</stat_contracts>
<badge_contract>Contract</badge_contract>
<badge_section>Section</badge_section>
<badge_element>Element</badge_element>
```

- [ ] **Step 7.4: Add the same 4 keys to all other 10 language files**

Use these translations (adjust to match existing file conventions):

| Lang | stat_contracts | badge_contract | badge_section | badge_element |
|------|----------------|----------------|---------------|---------------|
| `da` | Kontrakter | Kontrakt | Sektion | Element |
| `sv` | Kontrakt | Kontrakt | Sektion | Element |
| `no` | Kontrakter | Kontrakt | Seksjon | Element |
| `de` | Verträge | Vertrag | Abschnitt | Element |
| `fi` | Sopimukset | Sopimus | Osio | Elementti |
| `fr` | Contrats | Contrat | Section | Élément |
| `es` | Contratos | Contrato | Sección | Elemento |
| `nl` | Contracten | Contract | Sectie | Element |
| `ja` | コントラクト | コントラクト | セクション | 要素 |
| `zh-CN` | 合约 | 合约 | 区块 | 元素 |

- [ ] **Step 7.5: Manual check — build and browse**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

Manual verification note: launch the CMS 13 sample site (`dotnet run --project src/EditorPowertools.SampleSiteCms13`) and visit the Content Type Audit page. Confirm new badges render on any block with CompositionBehaviors or Contract flag set. Under CMS 12 sample site, confirm UI is unchanged.

- [ ] **Step 7.6: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js src/EditorPowertools/lang/
git commit -m "feat(content-type-audit): render Contract/Section/Element badges + Contracts stat card (CMS 13)"
```

---

## Task 8: ContentTypeAudit UI — Kind + Composition filters

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 8.1: Add two filter state variables near the existing filter-state block**

Locate the existing `let showSystem = false; let baseFilter = ''; let searchQuery = '';` block and add:

```js
    let kindFilter = '';      // '' | 'contract' | 'non-contract'
    let compositionFilter = ''; // '' | 'section' | 'element' | 'both' | 'plain'
```

- [ ] **Step 8.2: Update `getFiltered` to honor the new filters**

Replace the existing `getFiltered` with:

```js
    function getFiltered() {
        return allTypes.filter(t => {
            if (!showSystem && t.isSystemType) return false;
            if (baseFilter && t.base !== baseFilter) return false;
            if (kindFilter === 'contract' && !t.isContract) return false;
            if (kindFilter === 'non-contract' && t.isContract) return false;
            if (compositionFilter) {
                const has = (name) => t.compositionBehaviors?.includes(name);
                if (compositionFilter === 'section' && !has('SectionEnabled')) return false;
                if (compositionFilter === 'element' && !has('ElementEnabled')) return false;
                if (compositionFilter === 'both' && !(has('SectionEnabled') && has('ElementEnabled'))) return false;
                if (compositionFilter === 'plain' &&
                    (has('SectionEnabled') || has('ElementEnabled'))) return false;
            }
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                if (q.startsWith('property:')) return false;
                return (t.name?.toLowerCase().includes(q) ||
                    t.displayName?.toLowerCase().includes(q) ||
                    t.description?.toLowerCase().includes(q) ||
                    t.groupName?.toLowerCase().includes(q) ||
                    t.modelType?.toLowerCase().includes(q) ||
                    String(t.id) === q);
            }
            return true;
        });
    }
```

- [ ] **Step 8.3: Render the new filter controls in `renderToolbar` (CMS 13 only)**

Inside `renderToolbar`, after the existing `<select id="audit-base-filter">` block and before the `<label class="ept-toggle">`, insert:

```js
            ${hasCms13 ? `
            <select id="audit-kind-filter" class="ept-select">
                <option value="">${EPT.s('contenttypeaudit.opt_allkinds', 'All kinds')}</option>
                <option value="contract">${EPT.s('contenttypeaudit.opt_contractsonly', 'Contract types only')}</option>
                <option value="non-contract">${EPT.s('contenttypeaudit.opt_noncontract', 'Non-contract types')}</option>
            </select>
            <select id="audit-composition-filter" class="ept-select">
                <option value="">${EPT.s('contenttypeaudit.opt_anycomposition', 'Any composition')}</option>
                <option value="section">${EPT.s('contenttypeaudit.opt_sectiononly', 'Section-enabled')}</option>
                <option value="element">${EPT.s('contenttypeaudit.opt_elementonly', 'Element-enabled')}</option>
                <option value="both">${EPT.s('contenttypeaudit.opt_both', 'Both section & element')}</option>
                <option value="plain">${EPT.s('contenttypeaudit.opt_plain', 'Plain block')}</option>
            </select>` : ''}
```

Before that template string, compute `const hasCms13 = allTypes.some(t => t.isContract != null);`.

- [ ] **Step 8.4: Wire up the select change handlers**

In the toolbar-setup block (after `renderToolbar`), locate existing event wiring (around `document.getElementById('audit-base-filter').addEventListener`), and add:

```js
        const kindEl = document.getElementById('audit-kind-filter');
        if (kindEl) kindEl.addEventListener('change', (e) => { kindFilter = e.target.value; renderStats(); renderTable(); });
        const compEl = document.getElementById('audit-composition-filter');
        if (compEl) compEl.addEventListener('change', (e) => { compositionFilter = e.target.value; renderStats(); renderTable(); });
```

- [ ] **Step 8.5: Add 7 new localization keys (opt_allkinds, opt_contractsonly, opt_noncontract, opt_anycomposition, opt_sectiononly, opt_elementonly, opt_both, opt_plain) to `en.xml`**

Inside `contenttypeaudit`:

```xml
<opt_allkinds>All kinds</opt_allkinds>
<opt_contractsonly>Contract types only</opt_contractsonly>
<opt_noncontract>Non-contract types</opt_noncontract>
<opt_anycomposition>Any composition</opt_anycomposition>
<opt_sectiononly>Section-enabled</opt_sectiononly>
<opt_elementonly>Element-enabled</opt_elementonly>
<opt_both>Both section &amp; element</opt_both>
<opt_plain>Plain block</opt_plain>
```

- [ ] **Step 8.6: Add equivalent keys in the other 10 language files**

Translate each value equivalently, matching the file's existing style. English fallbacks are acceptable if a short local term is unavailable.

- [ ] **Step 8.7: Build and manual browser check**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

Manual: under CMS 13 sample site, confirm the two new dropdowns appear and filter correctly. Under CMS 12 sample site, confirm the dropdowns do NOT render.

- [ ] **Step 8.8: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js src/EditorPowertools/lang/
git commit -m "feat(content-type-audit): Kind + Composition filters (CMS 13 only)"
```

---

## Task 9: ContentTypeAudit UI — "Applied contracts" detail panel

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 9.1: Add the panel renderer**

In `content-type-audit.js`, locate the type-detail rendering function (the function that renders the properties panel when a type is clicked). After the properties panel rendering, add a new sibling panel:

```js
        if (type.contracts && type.contracts.length > 0) {
            const contractsHtml = type.contracts.map(c =>
                `<li><a href="#id=${c.id}">${c.displayName || c.name}</a></li>`
            ).join('');
            detailContainer.insertAdjacentHTML('beforeend', `
                <section class="ept-panel">
                    <h4>${EPT.s('contenttypeaudit.panel_appliedcontracts', 'Applied contracts')}</h4>
                    <ul class="ept-list">${contractsHtml}</ul>
                </section>
            `);
        }
```

Adjust the exact insertion point to match the current DOM structure — the intent is that the Applied Contracts panel renders at the end of the type-detail view, only when the types array has items.

- [ ] **Step 9.2: Add the panel-title localization key**

In `en.xml` inside `contenttypeaudit`:

```xml
<panel_appliedcontracts>Applied contracts</panel_appliedcontracts>
```

And equivalent in each of the 10 other language files:

| Lang | Value |
|------|-------|
| `da` | Anvendte kontrakter |
| `sv` | Tillämpade kontrakt |
| `no` | Anvendte kontrakter |
| `de` | Angewendete Verträge |
| `fi` | Sovelletut sopimukset |
| `fr` | Contrats appliqués |
| `es` | Contratos aplicados |
| `nl` | Toegepaste contracten |
| `ja` | 適用された契約 |
| `zh-CN` | 已应用合约 |

- [ ] **Step 9.3: Build and verify**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

Manual check: on the CMS 13 sample site, open a content type that implements at least one contract; the "Applied contracts" panel appears in the detail view, linking each contract to its own type detail page.

- [ ] **Step 9.4: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js src/EditorPowertools/lang/
git commit -m "feat(content-type-audit): Applied Contracts detail panel (CMS 13)"
```

---

## Task 10: ContentStatistics backend — TotalContracts + Contracts distribution + Block breakdown

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentStatistics/Models/ContentStatisticsDtos.cs`
- Modify: `src/EditorPowertools/Tools/ContentStatistics/ContentStatisticsService.cs`
- Modify: `src/EditorPowertools.Tests/...` (add new test file if Stats tests exist; otherwise skip-per-user TODO)

- [ ] **Step 10.1: Extend the DTOs**

Edit `src/EditorPowertools/Tools/ContentStatistics/Models/ContentStatisticsDtos.cs`. Add to `SummaryStatsDto`:

```csharp
    // CMS 13 only — null on CMS 12
    public int? TotalContracts { get; set; }
```

Append a new DTO at the bottom of the file:

```csharp
public class BlockBreakdownDto
{
    public int Sections { get; set; }
    public int Elements { get; set; }
    public int Plain { get; set; }
}
```

And in `ContentStatisticsDashboardDto`:

```csharp
    public BlockBreakdownDto? BlockBreakdown { get; set; }  // null on CMS 12
```

- [ ] **Step 10.2: Update `ContentStatisticsService` to inject the metadata provider and emit new fields**

Edit the service. Add:

```csharp
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
```

Add a private field `_metadataProvider` and accept `IContentTypeMetadataProvider` in the constructor signature (insert parameter immediately before `ILogger`).

In `BuildSummary`, replace the foreach body (the `baseType == "Page"` / `Block` / `Media` cascade) with:

```csharp
            var baseType = ct.Base.ToString();
            var metadata = _metadataProvider.Get(ct);

            if (CmsFeatureFlags.ContractsAvailable && metadata.IsContract)
                totalContracts += stat.ContentCount;
            else if (baseType == "Page")
                totalPages += stat.ContentCount;
            else if (baseType == "Block")
                totalBlocks += stat.ContentCount;
            else if (baseType == "Media" || baseType == "Image" || baseType == "Video")
                totalMedia += stat.ContentCount;
```

Declare `int totalContracts = 0;` at the top of the method. Include `TotalContracts = CmsFeatureFlags.ContractsAvailable ? totalContracts : null` in the returned `SummaryStatsDto`.

In `BuildTypeDistribution`, add `"Contracts"` to the initial categories dict:

```csharp
        var categories = new Dictionary<string, int>
        {
            ["Pages"] = 0,
            ["Blocks"] = 0,
            ["Media"] = 0,
            ["Contracts"] = 0,
            ["Other"] = 0
        };
```

Replace the category switch with:

```csharp
            var metadata = _metadataProvider.Get(ct);
            var category = (CmsFeatureFlags.ContractsAvailable && metadata.IsContract)
                ? "Contracts"
                : baseType switch
                {
                    "Page" => "Pages",
                    "Block" => "Blocks",
                    "Media" or "Image" or "Video" => "Media",
                    _ => "Other"
                };
            categories[category] += stat.ContentCount;
```

(Contracts bucket stays zero under CMS 12 and is filtered by the existing `.Where(kv => kv.Value > 0)`.)

Add a new private method:

```csharp
    private BlockBreakdownDto? BuildBlockBreakdown(
        List<ContentTypeStatisticsRecord> allStats,
        Dictionary<int, ContentType> contentTypeMap)
    {
        if (!CmsFeatureFlags.ContractsAvailable) return null;

        int sections = 0, elements = 0, plain = 0;
        foreach (var stat in allStats)
        {
            if (!contentTypeMap.TryGetValue(stat.ContentTypeId, out var ct)) continue;
            if (ct.Base.ToString() != "Block") continue;

            var m = _metadataProvider.Get(ct);
            var hasSection = m.CompositionBehaviors.Contains("SectionEnabled");
            var hasElement = m.CompositionBehaviors.Contains("ElementEnabled");

            if (hasSection) sections += stat.ContentCount;
            if (hasElement) elements += stat.ContentCount;
            if (!hasSection && !hasElement) plain += stat.ContentCount;
        }

        return new BlockBreakdownDto { Sections = sections, Elements = elements, Plain = plain };
    }
```

Wire `BlockBreakdown = BuildBlockBreakdown(allStats, contentTypeMap)` into whichever method builds the dashboard DTO (commonly `GetDashboard()` or similar).

- [ ] **Step 10.3: Update DI registration if constructor changed**

In `Infrastructure/ServiceCollectionExtensions.cs`, DI resolves constructors automatically; no explicit change needed because the provider is already registered (Task 1.6).

- [ ] **Step 10.4: Build both TFMs**

Run: `dotnet build src/EditorPowertools --framework net8.0`
Expected: SUCCEEDS.

Run: `dotnet build src/EditorPowertools --framework net10.0`
Expected: SUCCEEDS.

- [ ] **Step 10.5: Commit**

```bash
git add src/EditorPowertools/Tools/ContentStatistics/
git commit -m "feat(content-statistics): add TotalContracts, Contracts category, and block breakdown (CMS 13)"
```

---

## Task 11: ContentStatistics UI — contracts slice + block-breakdown sub-card

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 11.1: Render `totalContracts` summary stat when non-null**

Locate the summary-rendering function in `content-statistics.js`. After the existing media stat card, insert:

```js
${data.summary.totalContracts != null ? `
    <div class="ept-stat">
        <div class="ept-stat__value">${data.summary.totalContracts}</div>
        <div class="ept-stat__label">${EPT.s('contentstatistics.stat_contracts', 'Contracts')}</div>
    </div>` : ''}
```

- [ ] **Step 11.2: Render the block-breakdown sub-card**

Below the main distribution chart in the render function, add:

```js
if (data.blockBreakdown) {
    const bb = data.blockBreakdown;
    detailContainer.insertAdjacentHTML('beforeend', `
        <section class="ept-panel">
            <h4>${EPT.s('contentstatistics.panel_blockbreakdown', 'Block breakdown')}</h4>
            <ul class="ept-kv">
                <li><span>${EPT.s('contentstatistics.bb_sections', 'Sections')}</span><strong>${bb.sections}</strong></li>
                <li><span>${EPT.s('contentstatistics.bb_elements', 'Elements')}</span><strong>${bb.elements}</strong></li>
                <li><span>${EPT.s('contentstatistics.bb_plain', 'Plain blocks')}</span><strong>${bb.plain}</strong></li>
            </ul>
        </section>
    `);
}
```

Adjust the selector/insert point to match the actual structure in `content-statistics.js`.

- [ ] **Step 11.3: Add 5 new localization keys (`stat_contracts`, `panel_blockbreakdown`, `bb_sections`, `bb_elements`, `bb_plain`) to `en.xml` under a `contentstatistics` area**

```xml
<stat_contracts>Contracts</stat_contracts>
<panel_blockbreakdown>Block breakdown</panel_blockbreakdown>
<bb_sections>Sections</bb_sections>
<bb_elements>Elements</bb_elements>
<bb_plain>Plain blocks</bb_plain>
```

- [ ] **Step 11.4: Add same keys to 10 other languages**

Translations:

| Lang | panel_blockbreakdown | bb_sections | bb_elements | bb_plain |
|------|----------------------|-------------|-------------|----------|
| `da` | Blokopdeling | Sektioner | Elementer | Almindelige blokke |
| `sv` | Blockfördelning | Sektioner | Element | Vanliga block |
| `no` | Blokkoversikt | Seksjoner | Elementer | Vanlige blokker |
| `de` | Block-Aufschlüsselung | Abschnitte | Elemente | Normale Blöcke |
| `fi` | Lohkojen jaottelu | Osiot | Elementit | Tavalliset lohkot |
| `fr` | Répartition des blocs | Sections | Éléments | Blocs simples |
| `es` | Desglose de bloques | Secciones | Elementos | Bloques simples |
| `nl` | Blokverdeling | Secties | Elementen | Standaardblokken |
| `ja` | ブロック内訳 | セクション | 要素 | 標準ブロック |
| `zh-CN` | 区块明细 | 区块 | 元素 | 普通区块 |

(Use the same `stat_contracts` value as in ContentTypeAudit if already localized; otherwise reuse the one you added in Task 7.3.)

- [ ] **Step 11.5: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js src/EditorPowertools/lang/
git commit -m "feat(content-statistics): render Contracts stat and Block breakdown panel (CMS 13)"
```

---

## Task 12: BulkPropertyEditor backend — ResolveTargetTypes + expose CMS 13 metadata

**Files:**
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorService.cs`
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/Models/BulkPropertyEditorDtos.cs`
- Modify: `src/EditorPowertools/Tools/BulkPropertyEditor/BulkPropertyEditorController.cs`
- Test: `src/EditorPowertools.Tests/Tools/BulkPropertyEditor/ResolveTargetTypesTests.cs` (new file)

- [ ] **Step 12.1: Write the failing test**

Create `src/EditorPowertools.Tests/Tools/BulkPropertyEditor/ResolveTargetTypesTests.cs`:

```csharp
using EPiServer.DataAbstraction;
using FluentAssertions;
using Moq;
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
using UmageAI.Optimizely.EditorPowerTools.Tests.Helpers;
using UmageAI.Optimizely.EditorPowerTools.Tools.BulkPropertyEditor;

namespace UmageAI.Optimizely.EditorPowerTools.Tests.Tools.BulkPropertyEditor;

public class ResolveTargetTypesTests
{
    public ResolveTargetTypesTests()
    {
        EpiServerTestSetup.EnsureInitialized();
    }

    [Fact]
    public void IdenticalWhenNoContractsSelected()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();
        var concrete1 = new ContentType { ID = 10, Name = "Promo" };
        var concrete2 = new ContentType { ID = 11, Name = "Hero" };
        repo.Setup(r => r.List()).Returns(new[] { concrete1, concrete2 });
        meta.Setup(m => m.Get(It.IsAny<ContentType>())).Returns(ContentTypeMetadata.Empty);

        var service = CreateService(repo.Object, meta.Object);

        var result = service.ResolveTargetTypes(new[] { 10, 11 });
        result.Should().BeEquivalentTo(new[] { 10, 11 });
    }

#if OPTIMIZELY_CMS13
    [Fact]
    public void ContractSelectionExpandsToImplementingTypes()
    {
        var repo = new Mock<IContentTypeRepository>();
        var meta = new Mock<IContentTypeMetadataProvider>();

        var contract = new ContentType { ID = 100, Name = "IHasSeo" };
        var impl1 = new ContentType { ID = 10, Name = "Promo" };
        var impl2 = new ContentType { ID = 11, Name = "Hero" };
        var other = new ContentType { ID = 12, Name = "Footer" };

        repo.Setup(r => r.List()).Returns(new[] { contract, impl1, impl2, other });

        var contractRef = new ContractRef(100, Guid.Empty, "IHasSeo", null);
        meta.Setup(m => m.Get(contract)).Returns(new ContentTypeMetadata(
            IsContract: true, Contracts: Array.Empty<ContractRef>(), CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl1)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(impl2)).Returns(new ContentTypeMetadata(
            IsContract: false, Contracts: new[] { contractRef }, CompositionBehaviors: Array.Empty<string>()));
        meta.Setup(m => m.Get(other)).Returns(ContentTypeMetadata.Empty);

        var service = CreateService(repo.Object, meta.Object);

        var result = service.ResolveTargetTypes(new[] { 100 });

        result.Should().BeEquivalentTo(new[] { 10, 11 });
    }
#endif

    private static BulkPropertyEditorService CreateService(
        IContentTypeRepository repo, IContentTypeMetadataProvider meta)
    {
        return new BulkPropertyEditorService(
            repo,
            Mock.Of<EPiServer.IContentRepository>(),
            Mock.Of<IContentModelUsage>(),
            Mock.Of<ILanguageBranchRepository>(),
            meta,
            Mock.Of<Microsoft.Extensions.Logging.ILogger<BulkPropertyEditorService>>());
    }
}
```

- [ ] **Step 12.2: Run test — expect compile error**

Run: `dotnet test src/EditorPowertools.Tests --filter ResolveTargetTypes`
Expected: BUILD FAILURE (no such method).

- [ ] **Step 12.3: Add `ResolveTargetTypes` to the service**

Edit `BulkPropertyEditorService.cs`. Add using:

```csharp
using UmageAI.Optimizely.EditorPowerTools.Abstractions;
```

Add private field `_metadataProvider` and constructor parameter (insert before `ILogger`):

```csharp
    private readonly IContentTypeMetadataProvider _metadataProvider;

    public BulkPropertyEditorService(
        IContentTypeRepository contentTypeRepository,
        IContentRepository contentRepository,
        IContentModelUsage contentModelUsage,
        ILanguageBranchRepository languageBranchRepository,
        IContentTypeMetadataProvider metadataProvider,
        ILogger<BulkPropertyEditorService> logger)
    {
        _contentTypeRepository = contentTypeRepository;
        _contentRepository = contentRepository;
        _contentModelUsage = contentModelUsage;
        _languageBranchRepository = languageBranchRepository;
        _metadataProvider = metadataProvider;
        _logger = logger;
    }
```

Append the new method:

```csharp
    public IReadOnlyList<int> ResolveTargetTypes(IEnumerable<int> requestedIds)
    {
        var requested = requestedIds?.Distinct().ToList() ?? new List<int>();
        if (!CmsFeatureFlags.ContractsAvailable || requested.Count == 0)
            return requested;

        var all = _contentTypeRepository.List().ToList();
        var byId = all.ToDictionary(ct => ct.ID);
        var result = new HashSet<int>();

        foreach (var id in requested)
        {
            if (!byId.TryGetValue(id, out var ct)) continue;
            var metadata = _metadataProvider.Get(ct);

            if (metadata.IsContract)
            {
                // Expand: every non-contract type whose Contracts includes this one.
                foreach (var candidate in all)
                {
                    var cm = _metadataProvider.Get(candidate);
                    if (!cm.IsContract && cm.Contracts.Any(c => c.Id == id))
                        result.Add(candidate.ID);
                }
            }
            else
            {
                result.Add(id);
            }
        }

        return result.ToList();
    }
```

- [ ] **Step 12.4: Run the test — expect PASS**

Run: `dotnet test src/EditorPowertools.Tests --framework net10.0 --filter ResolveTargetTypes`
Expected: PASS on both cases.

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0 --filter ResolveTargetTypes`
Expected: PASS on `IdenticalWhenNoContractsSelected` (contract-specific test is `#if OPTIMIZELY_CMS13`).

- [ ] **Step 12.5: Extend `GetContentTypes` result with metadata fields**

Edit `BulkPropertyEditorService.cs` `GetContentTypes` to return the new DTO shape:

```csharp
    public List<ContentTypeListItem> GetContentTypes()
    {
        return _contentTypeRepository.List()
            .Where(ct => ct.ModelType != null || _metadataProvider.Get(ct).IsContract)
            .Select(ct =>
            {
                var m = _metadataProvider.Get(ct);
                return new ContentTypeListItem(
                    ct.ID,
                    ct.LocalizedName ?? ct.Name,
                    GetBaseType(ct.ModelType),
                    CmsFeatureFlags.ContractsAvailable ? (bool?)m.IsContract : null,
                    CmsFeatureFlags.ContractsAvailable ? m.CompositionBehaviors.ToArray() : null);
            })
            .OrderBy(ct => ct.Name)
            .ToList();
    }
```

Update the DTO:

```csharp
public record ContentTypeListItem(
    int Id,
    string Name,
    string BaseType,
    bool? IsContract = null,
    string[]? CompositionBehaviors = null);
```

- [ ] **Step 12.6: Call `ResolveTargetTypes` at the top of every "apply-to-types" API path**

Grep for every place `request.ContentTypeId` flows into an existing `GetContentAsync` / bulk-save method. For a single content-type ID (the current shape), wrap it:

```csharp
        var effectiveIds = ResolveTargetTypes(new[] { request.ContentTypeId });
```

Iterate the loop over `effectiveIds` where the request expects a single ID. Keep the single-type path identical when `effectiveIds.Count == 1 && effectiveIds[0] == request.ContentTypeId` (no behavior change for the CMS 12 path).

*If the existing service only supports a single content-type-per-request, keep it single-type but expose the expanded set in the preview response as `ResolvedTypes`. That path is the minimum change to satisfy the spec. Otherwise, extend the filter method signature to accept a collection.*

- [ ] **Step 12.7: Add `ResolvedTypes` to the preview response**

In `Models/BulkPropertyEditorDtos.cs`, extend `ContentFilterResponse`:

```csharp
public record ContentFilterResponse(
    List<ContentItemRow> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    int[]? ResolvedTypes = null);
```

In the service, populate `ResolvedTypes = effectiveIds.ToArray()` when the request selects a contract; otherwise leave null.

- [ ] **Step 12.8: Build**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

- [ ] **Step 12.9: Commit**

```bash
git add src/EditorPowertools/Tools/BulkPropertyEditor/ src/EditorPowertools.Tests/Tools/BulkPropertyEditor/
git commit -m "feat(bulk-property-editor): support Contract selection with expansion to implementing types (CMS 13)"
```

---

## Task 13: BulkPropertyEditor UI — tabs, badges, expansion preview

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 13.1: Render a tab selector when CMS 13 data is present**

Inside the content-type-picker render function in `bulk-property-editor.js`:

```js
const hasCms13 = contentTypes.some(t => t.isContract != null);
let activeTab = 'types'; // or 'contracts'

const tabsHtml = hasCms13 ? `
    <div class="ept-tabs">
        <button data-tab="types" class="${activeTab === 'types' ? 'active' : ''}">
            ${EPT.s('bulkpropertyeditor.tab_types', 'Content types')}
        </button>
        <button data-tab="contracts" class="${activeTab === 'contracts' ? 'active' : ''}">
            ${EPT.s('bulkpropertyeditor.tab_contracts', 'Contracts')}
        </button>
    </div>
` : '';
```

Wire click handlers to swap `activeTab` and re-render the list.

- [ ] **Step 13.2: Filter the list by tab**

```js
const visibleTypes = hasCms13
    ? (activeTab === 'contracts'
        ? contentTypes.filter(t => t.isContract)
        : contentTypes.filter(t => !t.isContract))
    : contentTypes;
```

- [ ] **Step 13.3: Render composition badges on block rows**

For each row, if `t.compositionBehaviors`:

```js
const composition = [];
if (t.compositionBehaviors?.includes('SectionEnabled'))
    composition.push(`<span class="ept-badge ept-badge--accent">${EPT.s('bulkpropertyeditor.badge_section', 'Section')}</span>`);
if (t.compositionBehaviors?.includes('ElementEnabled'))
    composition.push(`<span class="ept-badge ept-badge--accent">${EPT.s('bulkpropertyeditor.badge_element', 'Element')}</span>`);
```

Append `composition.join('')` after the type name in the row HTML.

- [ ] **Step 13.4: Show expansion preview when a Contract is selected**

In the preview area, after loading a filter response:

```js
if (response.resolvedTypes && response.resolvedTypes.length > 0) {
    const labels = response.resolvedTypes.map(id => {
        const t = contentTypes.find(ct => ct.id === id);
        return t ? t.name : `#${id}`;
    });
    previewHeader.insertAdjacentHTML('beforeend',
        `<div class="ept-note">${EPT.s('bulkpropertyeditor.note_expansion', 'Contract expands to')}: ${labels.join(', ')}</div>`);
}
```

- [ ] **Step 13.5: Add 5 localization keys to `en.xml` under `bulkpropertyeditor`**

```xml
<tab_types>Content types</tab_types>
<tab_contracts>Contracts</tab_contracts>
<badge_section>Section</badge_section>
<badge_element>Element</badge_element>
<note_expansion>Contract expands to</note_expansion>
```

- [ ] **Step 13.6: Add translations in 10 other language files**

Reuse translations for badge_section / badge_element from Task 7.4.

| Lang | tab_types | tab_contracts | note_expansion |
|------|-----------|---------------|----------------|
| `da` | Indholdstyper | Kontrakter | Kontrakt udvides til |
| `sv` | Innehållstyper | Kontrakt | Kontrakt utökas till |
| `no` | Innholdstyper | Kontrakter | Kontrakten utvides til |
| `de` | Inhaltstypen | Verträge | Vertrag erweitert sich zu |
| `fi` | Sisältötyypit | Sopimukset | Sopimus laajenee |
| `fr` | Types de contenu | Contrats | Le contrat s'étend à |
| `es` | Tipos de contenido | Contratos | El contrato se expande a |
| `nl` | Inhoudstypen | Contracten | Contract breidt uit naar |
| `ja` | コンテンツタイプ | コントラクト | コントラクトは次に展開されます |
| `zh-CN` | 内容类型 | 合约 | 合约扩展到 |

- [ ] **Step 13.7: Build and manual verify**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

Manual: CMS 13 site — tabs appear. Select a Contract, run a filter, see "Contract expands to: X, Y". CMS 12 site — no tabs.

- [ ] **Step 13.8: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js src/EditorPowertools/lang/
git commit -m "feat(bulk-property-editor): Content-type vs Contract tabs + expansion preview (CMS 13)"
```

---

## Task 14: ContentAudit backend — filter parameters

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentAudit/Models/ContentAuditDtos.cs`
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditService.cs`

- [ ] **Step 14.1: Add filter fields to the audit-run input DTO**

Edit `ContentAuditDtos.cs`. On whichever class represents the audit-run input (likely named `ContentAuditRunRequest` or similar — confirm by reading the file), add:

```csharp
    public string? ContractFilter { get; set; }    // null | "include" | "exclude" | "only"
    public string? CompositionFilter { get; set; } // null | "section" | "element"
```

- [ ] **Step 14.2: Apply the filters in the service's type-filter stage**

Edit `ContentAuditService.cs`. Inject `IContentTypeMetadataProvider` (add using, field, constructor parameter — same pattern as Task 12.3).

Locate the section where the service enumerates content types to audit (likely within a method that iterates `_contentTypeRepository.List()` or builds a type allowlist). Before the actual audit work begins, add:

```csharp
        var filteredTypes = allTypes.Where(ct =>
        {
            if (!CmsFeatureFlags.ContractsAvailable) return true;
            var m = _metadataProvider.Get(ct);

            if (request.ContractFilter == "only" && !m.IsContract) return false;
            if (request.ContractFilter == "exclude" && m.IsContract) return false;

            if (!string.IsNullOrEmpty(request.CompositionFilter) && ct.Base.ToString() == "Block")
            {
                if (request.CompositionFilter == "section" && !m.CompositionBehaviors.Contains("SectionEnabled")) return false;
                if (request.CompositionFilter == "element" && !m.CompositionBehaviors.Contains("ElementEnabled")) return false;
            }
            return true;
        }).ToList();
```

Replace the downstream iteration to use `filteredTypes` instead of `allTypes`.

- [ ] **Step 14.3: Build**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

- [ ] **Step 14.4: Commit**

```bash
git add src/EditorPowertools/Tools/ContentAudit/
git commit -m "feat(content-audit): ContractFilter + CompositionFilter inputs (CMS 13)"
```

---

## Task 15: ContentAudit UI — new filter drawer controls

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js`
- Modify: `src/EditorPowertools/lang/en.xml` (+ 10 others)

- [ ] **Step 15.1: Add controls to the filter drawer**

In `content-audit.js`, find the filter-drawer render function. Append, gated on whether the content-type list contains CMS 13 data:

```js
const hasCms13 = (contentTypes || []).some(t => t.isContract != null);
const cms13Section = hasCms13 ? `
    <fieldset class="ept-filter-group">
        <legend>${EPT.s('contentaudit.filter_cms13', 'CMS 13')}</legend>
        <label>${EPT.s('contentaudit.filter_contract', 'Contracts')}:
            <select name="contractFilter">
                <option value="">${EPT.s('contentaudit.any', 'Any')}</option>
                <option value="include">${EPT.s('contentaudit.include', 'Include')}</option>
                <option value="exclude">${EPT.s('contentaudit.exclude', 'Exclude')}</option>
                <option value="only">${EPT.s('contentaudit.only', 'Contracts only')}</option>
            </select>
        </label>
        <label>${EPT.s('contentaudit.filter_composition', 'Composition')}:
            <select name="compositionFilter">
                <option value="">${EPT.s('contentaudit.any', 'Any')}</option>
                <option value="section">${EPT.s('contentaudit.section', 'Section')}</option>
                <option value="element">${EPT.s('contentaudit.element', 'Element')}</option>
            </select>
        </label>
    </fieldset>
` : '';
```

Insert into the drawer HTML at the natural position for the existing filters.

- [ ] **Step 15.2: Wire values into the audit-run request payload**

In the handler that POSTs the audit run, add:

```js
payload.contractFilter = form.contractFilter.value || null;
payload.compositionFilter = form.compositionFilter.value || null;
```

- [ ] **Step 15.3: Add 8 localization keys to `en.xml` under `contentaudit`**

```xml
<filter_cms13>CMS 13</filter_cms13>
<filter_contract>Contracts</filter_contract>
<filter_composition>Composition</filter_composition>
<any>Any</any>
<include>Include</include>
<exclude>Exclude</exclude>
<only>Contracts only</only>
<section>Section</section>
<element>Element</element>
```

(9 keys — count accordingly when adding to other files.)

- [ ] **Step 15.4: Add translations in 10 other languages**

Use the same conventions established in earlier tasks. English fallback via `EPT.s(key, 'fallback')` is acceptable in the JS — the XML entry just needs to exist.

- [ ] **Step 15.5: Build and manual verify**

Run: `dotnet build src/EditorPowertools`
Expected: SUCCEEDS.

Manual: run an audit on CMS 13 with "Contracts only" — only contract-type content (or zero, since contracts have no instances) should be audited. Verify drawer absent under CMS 12.

- [ ] **Step 15.6: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js src/EditorPowertools/lang/
git commit -m "feat(content-audit): CMS 13 filter drawer (Contracts + Composition)"
```

---

## Task 16: Documentation updates

**Files:**
- Modify: `README.md`
- Modify: `docs/getting-started.md`
- Modify: `docs/coding-guidelines.md`
- Create: `docs/cms13-support.md`

- [ ] **Step 16.1: Add "CMS 13 support" subsection to `README.md`**

After the existing Features section, insert:

```markdown
## CMS 13 support

EditorPowertools is multi-targeted: the same NuGet package supports both CMS 12 (.NET 8) and CMS 13 (.NET 10). CMS 13 unlocks the following additional capabilities:

- **Content Type Audit** — Contract / Section / Element badges, dedicated filters, and an "Applied contracts" detail panel.
- **Content Statistics** — Contracts summary card and block breakdown (Sections / Elements / Plain blocks).
- **Bulk Property Editor** — select a Contract to apply changes across every content type implementing it.
- **Content Audit** — filter by contract type or composition behavior.

The user-facing term "Orphaned" (content type in DB with no matching .NET class) is renamed to "Code-less" across the UI. See [docs/cms13-support.md](docs/cms13-support.md) for details.
```

- [ ] **Step 16.2: Update `docs/getting-started.md` with a runtime note**

Near the "Installation" section, add:

```markdown
### Runtime requirements

- **Optimizely CMS 12** — requires .NET 8.
- **Optimizely CMS 13** — requires .NET 10.

The NuGet package ships both target frameworks and picks the correct one automatically based on your host project.
```

- [ ] **Step 16.3: Add a "Multi-targeting" section to `docs/coding-guidelines.md`**

Append:

```markdown
## Multi-targeting CMS 12 and CMS 13

This project follows the pattern documented in `CLAUDE.md`. In summary:

- `.csproj` multi-targets `net8.0;net10.0`.
- CMS-version-specific types live under `Cms12/` / `Cms13/` folders (see `IContentTypeMetadataProvider` for the canonical example).
- Shared services read only the abstraction; they never use `#if` directly.
- Feature flags are consolidated in a single file (`Abstractions/CmsFeatureFlags.cs`).
- New public DTO fields that only make sense on CMS 13 are declared as nullable so the CMS 12 build serializes them as absent.
- The JS detects CMS 13 features by checking `nullableField != null`, not a separate flag.
```

- [ ] **Step 16.4: Create `docs/cms13-support.md`**

```markdown
# CMS 13 support in EditorPowertools

This page lists every tool's CMS 13-specific behavior.

## Foundation — content-type metadata

All CMS 13 content-type reads go through `IContentTypeMetadataProvider`:

- `IsContract` — true when the type is declared as a .NET interface inheriting `IContentData`.
- `CompositionBehaviors` — array of `"SectionEnabled"` / `"ElementEnabled"`.
- `Contracts` — references to contract types the content type implements.

Under CMS 12, all three are empty. Shared services never branch on version.

## Tool-by-tool

### Content Type Audit

- Contract / Section / Element badges per type.
- Two new filters: "Kind" (contracts only / non-contracts) and "Composition" (Section-enabled / Element-enabled / Both / Plain).
- Contracts stat card.
- "Applied contracts" panel in the detail view, linking each contract to its own detail page.

### Content Statistics

- Contracts summary card.
- Contracts slice in the type-distribution chart.
- "Block breakdown" sub-card showing Sections / Elements / Plain blocks.

### Bulk Property Editor

- *Content types* / *Contracts* tabs in the picker.
- Selecting a Contract expands at execution to every non-contract type whose `Contracts` collection contains it.
- The preview panel shows the expansion list before committing.

### Content Audit

- Filter drawer gains a "CMS 13" section with Contract-include/exclude/only and Section/Element filters.

## Terminology

Content types that exist in the database but have no matching .NET class are labeled **"Code-less"** (formerly "Orphaned"). This applies to all affected tools and all 11 supported UI languages.

## CmsDoctor

CmsDoctor's `OrphanedPropertyCheck` retains its name because it detects a different concern (property definitions missing from the model). No rename is applied there.
```

- [ ] **Step 16.5: Commit**

```bash
git add README.md docs/getting-started.md docs/coding-guidelines.md docs/cms13-support.md
git commit -m "docs: document CMS 13 support across tools + multi-targeting pattern"
```

---

## Final verification

- [ ] **Step F.1: Full build and test run on both TFMs**

Run: `dotnet build`
Expected: SUCCEEDS.

Run: `dotnet test src/EditorPowertools.Tests --framework net8.0`
Expected: all pass.

Run: `dotnet test src/EditorPowertools.Tests --framework net10.0`
Expected: all pass.

- [ ] **Step F.2: Manual smoke tests**

Under CMS 12 (`dotnet run --project src/EditorPowertools.SampleSite`):
- Content Type Audit renders exactly as it did before the change, with "Code-less" label replacing "Orphaned". No new filters, badges, or stat cards appear.
- Content Statistics unchanged.
- Bulk Property Editor unchanged.
- Content Audit unchanged.

Under CMS 13 (`dotnet run --project src/EditorPowertools.SampleSiteCms13`):
- Content Type Audit shows Contract/Section/Element badges on applicable types, Kind + Composition filters work, Contracts stat card renders, Applied-Contracts panel appears on contract-implementing types.
- Content Statistics shows Contracts summary card and Block Breakdown sub-card.
- Bulk Property Editor shows Content types / Contracts tabs; selecting a contract and running a filter shows the expansion preview.
- Content Audit shows the CMS 13 filter section in the drawer.

- [ ] **Step F.3: Final integration commit (if any outstanding uncommitted glue)**

```bash
git status
# Commit any outstanding glue/tests discovered during the smoke tests.
```

---

## Self-review notes (for plan authors)

- Every spec section has a corresponding task: Foundation → Task 1; DTO → Task 2; Rename → Tasks 3-5; ContentTypeAudit → Tasks 6-9; ContentStatistics → Tasks 10-11; BulkPropertyEditor → Tasks 12-13; ContentAudit → Tasks 14-15; Docs → Task 16. ✓
- Tests written before implementation: Task 1 (provider), Task 6 (service populates), Task 12 (ResolveTargetTypes). JS and XML tasks have no unit tests — verified manually. ✓
- `IContentTypeMetadataProvider`, `ContentTypeMetadata`, `ContractRef`, `CmsFeatureFlags` all defined in Task 1 and used consistently in every later task. ✓
- CSS class `ept-row--orphaned` deliberately preserved (see File Structure note) — link-checker uses it too. ✓
- CmsDoctor "orphaned property" wording preserved — explicitly called out in Task 5 and Task 16. ✓
- No `TBD` / `TODO` / "similar to Task N" placeholders anywhere in the plan. ✓
