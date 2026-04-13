# CMS 13 / .NET 10 Multi-Targeting Design

**Date:** 2026-04-11  
**Branch:** `features/cms13`  
**Status:** Approved

---

## Goal

Extend `UmageAI.Optimizely.EditorPowerTools` to support both **Optimizely CMS 12 (.NET 8)** and **Optimizely CMS 13 (.NET 10)** from a single NuGet package, without forking the codebase. Add a CMS 13 sample site for local development and testing.

---

## Architecture

### Single multi-target library

The library (`EditorPowertools.csproj`) changes from `net8.0` to `net8.0;net10.0`. CMS-version-specific logic is isolated using compile-time symbols `OPTIMIZELY_CMS12` (net8.0) and `OPTIMIZELY_CMS13` (net10.0). The resulting NuGet package contains both `lib/net8.0/` and `lib/net10.0/` outputs automatically.

The **tiered approach** from `CLAUDE.md` applies throughout:
- **Tier 1** — `#if OPTIMIZELY_CMS13` for small API differences (< ~10 lines per site)
- **Tier 2** — Separate TFM-specific files for larger divergence (e.g., Razor views)
- **Tier 3** — Internal interface + two adapters for architectural differences (not needed here; `DynamicDataStore` is not removed in CMS 13)

---

## Project Changes

### `EditorPowertools.csproj`

```xml
<!-- Multi-target both frameworks -->
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>

<!-- Conditional Optimizely package references -->
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="EPiServer.CMS" Version="12.*" />
  <PackageReference Include="EPiServer.CMS.UI.Core" Version="12.*" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="EPiServer.CMS" Version="13.*" />
  <PackageReference Include="EPiServer.CMS.UI.Core" Version="13.*" />
</ItemGroup>

<!-- Compile-time symbols -->
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS12</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS13</DefineConstants>
</PropertyGroup>
```

The `_PowertoolsLayout.cshtml` (Razor) cannot use `#if`, so it is handled via Tier 2: a separate `Views/Shared/Cms13/_PowertoolsLayout.cshtml` file included only for `net10.0`, with the existing file remaining the `net8.0` default.

Two `.targets` files (one per TFM) are packed into the NuGet package:
- `build/net8.0/UmageAI.Optimizely.EditorPowerTools.targets` (existing)
- `build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets` (new, identical content)

### `EditorPowertools.Tests.csproj`

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

No other changes — tests contain no Optimizely API calls directly.

### `EditorPowertools.SampleSite.csproj`

Unchanged. Stays on `net8.0` / CMS 12. This is the development sample site for CMS 12.

### New: `EditorPowertools.SampleSiteCms13/`

Scaffolded via `dotnet new alloy-epi-mvc` targeting `net10.0`. Configured to mirror the CMS 12 sample site:
- Project reference to `EditorPowertools.csproj`
- `services.AddEditorPowertools()` + `app.UseEditorPowertools()` in startup
- Equivalent `appsettings.json` / connection string configuration
- Added to `EditorPowertools.sln`

---

## Breaking Change Fixes

### HIGH — Will cause compile errors

| Change | Affected Files | Fix |
|---|---|---|
| `[ScheduledPlugIn]` → `[ScheduledJobAttribute]` | `UnifiedContentAnalysisJob.cs`, `ContentAuditExportJob.cs` | Tier 1 `#if OPTIMIZELY_CMS13` |
| `SiteDefinition.Current.GlobalAssetsRoot` removed | `ContentAuditExportJob.cs` | Tier 1 `#if` — inject `IApplicationResolver` for CMS 13 |
| `Html.CreatePlatformNavigationMenu()` removed | `Views/Shared/_PowertoolsLayout.cshtml` | Tier 2 — separate CMS 13 view using `<platform-navigation />` TagHelper |
| `Html.ApplyPlatformNavigation()` removed | `Views/Shared/_PowertoolsLayout.cshtml` | Tier 2 — same as above |

### MEDIUM — Obsolete warnings / behavioural differences

| Change | Affected Files | Fix |
|---|---|---|
| `ContentArea.FilteredItems` obsolete | `ContentDetailsService.cs`, `GetDescendentsContentAuditProvider.cs` | Direct fix: replace with `Items` in both versions (same behaviour, removes warning) |
| `IContentVersionRepository.List()` no longer auto-populates `totalCount` | `LanguageAuditAnalyzer.cs`, `ContentStatisticsService.cs`, `ContentDetailsService.cs` | Tier 1 `#if` — set `VersionFilter.IncludeTotalCount = true` for CMS 13 where `totalCount` is consumed |
| `IUrlResolver` deprecated (replacement TBD until CMS 13 GA) | `LinkCheckerAnalyzer.cs`, `GetDescendentsContentAuditProvider.cs` | Tier 1 `#if` — swap injection type when confirmed |

### LOW — Patterns deprecated but still functional

| Change | Affected Files | Fix |
|---|---|---|
| `ServiceLocator.Current.GetInstance<T>()` anti-pattern | `EditorPowertoolsMenuProvider.cs`, `ContentExtensions.cs` | Refactor to constructor injection (improves both versions) |

---

## Razor Layout — Tier 2 Detail

**CMS 12 (`Views/Shared/_PowertoolsLayout.cshtml`)** — unchanged:
```html
@Html.CreatePlatformNavigationMenu()
<div id="app-container">@RenderBody()</div>
@Html.ApplyPlatformNavigation()
```

**CMS 13 (`Views/Shared/Cms13/_PowertoolsLayout.cshtml`)** — new file:
```html
<platform-navigation />
<div id="app-container" class="epi-navigation--no-padding">@RenderBody()</div>
```

The `.csproj` selects which file to include in the Razor compilation output per TFM using `<Content>` conditions.

---

## NuGet Package Impact

- Single package `UmageAI.Optimizely.EditorPowerTools`, same version number
- `lib/net8.0/` — compiled against CMS 12
- `lib/net10.0/` — compiled against CMS 13
- `contentFiles/any/any/modules/_protected/EditorPowertools/` — module zip (shared, unchanged)
- `build/net8.0/` and `build/net10.0/` — both `.targets` files packed

Consumers on CMS 12 (.NET 8) get the CMS 12 assembly; consumers on CMS 13 (.NET 10) get the CMS 13 assembly. NuGet resolves this automatically.

---

## Per-Tool CMS 13 Disable Escape Hatch

If a tool cannot be made CMS 13-compatible without significant effort (e.g., deep API dependency on a removed subsystem), it can be disabled for CMS 13 using a compile-time guard in `ServiceCollectionExtensions.cs` and its feature toggle:

```csharp
#if !OPTIMIZELY_CMS13
    // Register tool X — not yet supported in CMS 13
    services.AddTransient<ToolXService>();
#endif
```

The feature toggle system already supports disabling individual tools at runtime. A compile-time guard prevents registration entirely for the affected TFM. This is a fallback — prefer fixing over disabling — but it keeps the CMS 13 build green while work is ongoing.

---

## Out of Scope

- No Tier 3 abstraction needed — `DynamicDataStore` is not removed in CMS 13
- `CsvHelper` and `EPPlusFree` versions are assumed compatible with both TFMs; confirm at build time
- CMS 13 sample site does not need to demonstrate every feature — basic wiring sufficient
- No public API surface changes between TFMs (same public types, same method signatures)

---

## Success Criteria

1. `dotnet build` succeeds for both `net8.0` and `net10.0` targets with zero errors
2. CMS 12 sample site runs and all tools function as before
3. CMS 13 sample site starts and the plugin loads without errors
4. `dotnet test` passes for both TFMs
5. `dotnet pack` produces a single `.nupkg` with both TFM folders
