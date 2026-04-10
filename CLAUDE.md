# EditorPowertools

An Optimizely CMS 12 plugin providing a collection of power tools for editors and admins.
Distributed as NuGet package `CodeArt.Optimizely.EditorPowertools`.

## Project Structure

- `src/EditorPowertools/` - Plugin class library (Razor SDK)
- `src/EditorPowertools.SampleSite/` - Alloy demo site for development/testing
- `docs/backlog.md` - Tool backlog
- `docs/coding-guidelines.md` - Architecture and coding standards

## Tech Stack

- .NET 8 / C# / Optimizely CMS 12 (EPiServer.CMS 12.29.0)
- UI: Vanilla JS or React (NOT Blazor - the old project was Blazor, this is a rewrite)
- Razor class library for embedded views and static assets

## Build & Run

```bash
dotnet build                                           # Build solution
dotnet run --project src/EditorPowertools.SampleSite   # Run sample site
```

## Optimizely Module Integration Reference

- Docs: https://docs.developers.optimizely.com/content-management-system/docs/user-interface
- Client resources: https://docs.developers.optimizely.com/content-management-system/docs/client-resources
- Add-ons: https://docs.developers.optimizely.com/content-management-system/docs/add-ons
- React components: https://docs.developers.optimizely.com/content-management-system/docs/creating-a-react-component
- Blog example: https://world.optimizely.com/blogs/Ben-McKernan/Dates/2018/11/a-react-gadget-in-episerver-cms-revisited/

## Key Patterns

- **Registration**: `services.AddEditorPowertools(options => ...)` + `app.UseEditorPowertools()`
- **Options**: `EditorPowertoolsOptions` bound from `CodeArt:EditorPowertools` config section
- **Permissions**: Three-layer (feature toggles + auth policy + optional EPiServer PermissionTypes)
- **Tool structure**: Each tool in `Tools/{ToolName}/` with Service + ViewModel. API controllers per tool, page views via central `EditorPowertoolsController`
- **Menu**: `EditorPowertoolsMenuProvider` uses `Paths.ToResource()` for controller routes
- **Static files**: Go in `modules/_protected/EditorPowertools/ClientResources/`, referenced via `Paths.ToClientResource()`. NOT `/_content/` path.
- **CMS Shell integration**: Layout uses `@ClientResources.RenderResources("ShellCore")`, `@Html.CreatePlatformNavigationMenu()`, `@Html.ApplyPlatformNavigation()`
- **Data**: DynamicDataStore for persistence, in-memory cache for expensive queries

## Conventions

- Follow `docs/coding-guidelines.md` for all tool implementations
- No static state - everything via DI
- Nullable reference types enabled
- Controllers return JSON APIs; UI is vanilla JS or React, not server-rendered
- Each tool has a corresponding PermissionType and FeatureToggle
- **JS paths**: Never hardcode API paths. Use `window.EPT_API_URL + '/endpoint'` and `window.EPT_HUB_URL + '/hub'`
- **Security**: All controllers must have `[Authorize(Policy = "codeart:editorpowertools")]`, all actions must call `_accessChecker.HasAccess()`, POST/DELETE endpoints must have `[RequireAjax]`, content operations must use proper `AccessLevel` (never `NoAccess`), error responses must not expose `ex.Message`

## Localization

All user-facing strings MUST go through Optimizely's localization system — never hardcode display text in C#.

- **Language files**: `src/EditorPowertools/lang/*.xml` (11 languages: en, da, sv, no, de, fi, fr, es, nl, ja, zh-CN)
- **String path convention**: `/editorpowertools/{area}/{key}` — e.g. `/editorpowertools/menu/contenttypeaudit`, `/editorpowertools/cmsdoctor/checks/memorycheck/name`
- **In services/controllers**: Inject `LocalizationService` and call `_localization.GetString("/editorpowertools/path/key")`
- **In components**: Set `LanguagePath = "/editorpowertools/components/yourcomponent"` — Optimizely auto-resolves `/title` and `/description`
- **In menu items**: Use `_localization.GetString()` for menu item names
- **Adding new strings**: Add to ALL 11 language files under the appropriate path. English in `en.xml` is the base; translate others accordingly.
- **Fallback**: If a key is missing, `GetString()` returns the key path — always provide English as the base language


# Multi-targeting: Optimizely CMS 12 (.NET 8) + CMS 13 (.NET 10)

## Context

This project is a NuGet add-on for Optimizely CMS. It currently targets **CMS 12 / .NET 8**
and must be extended to **also support CMS 13 / .NET 10** — without splitting into two
codebases. Consumers should get a single NuGet package that works with both CMS versions.

---

## Target project structure

All library `.csproj` files must be changed from single- to multi-target:

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

Conditional Optimizely package references must follow this pattern:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <PackageReference Include="EPiServer.CMS.Core" Version="12.*" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="EPiServer.CMS.Core" Version="13.*" />
</ItemGroup>
```

Define compile-time symbols for use in C# code:

```xml
<PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS12</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS13</DefineConstants>
</PropertyGroup>
```

---

## Handling code differences — apply in order

### Tier 1 — Small differences: use `#if`

For renamed types, changed method signatures, or small API differences spanning fewer than
~10 lines in a single location:

```csharp
#if OPTIMIZELY_CMS13
    // CMS 13 / .NET 10 code path
#else
    // CMS 12 / .NET 8 code path
#endif
```

Prefer `#if OPTIMIZELY_CMS13` as the leading condition (newer version first).

### Tier 2 — Medium differences: TFM-specific files

When a file accumulates many `#if` blocks, extract the divergent parts into version-specific
files and wire them conditionally in the `.csproj`:

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
  <Compile Include="Cms12\**\*.cs" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <Compile Include="Cms13\**\*.cs" />
</ItemGroup>
```

Use this folder layout:

```
MyAddon/
├── MyFeature.cs          ← shared (~80% of code)
├── Cms12/
│   └── Cms12Adapter.cs   ← CMS 12-specific implementation
└── Cms13/
    └── Cms13Adapter.cs   ← CMS 13-specific implementation
```

### Tier 3 — Larger API changes: internal abstraction

When a significant architectural difference exists between versions, introduce a thin internal
interface shared by both adapters:

```
/Abstractions/IMyFeatureAdapter.cs   ← internal interface
/Cms12/Cms12FeatureAdapter.cs        ← implements interface, compiled for net8.0 only
/Cms13/Cms13FeatureAdapter.cs        ← implements interface, compiled for net10.0 only
/MyFeature.cs                        ← public API, depends only on the interface
```

Register the correct adapter using `#if` in the DI extension method:

```csharp
public static IServiceCollection AddMyAddon(this IServiceCollection services)
{
#if OPTIMIZELY_CMS13
    services.AddSingleton<IMyFeatureAdapter, Cms13FeatureAdapter>();
#else
    services.AddSingleton<IMyFeatureAdapter, Cms12FeatureAdapter>();
#endif
    services.AddSingleton<MyFeature>();
    return services;
}
```

---

## Decision rules

| Situation | Action |
|---|---|
| 1–10 lines differ in one place | Tier 1 `#if` |
| A whole file is >30% `#if` blocks | Promote to Tier 2 (separate files) |
| A subsystem changed architecture between CMS 12 and 13 | Tier 3 (internal interface) |
| A dependency only exists in one version | Conditional `<PackageReference>` in csproj |
| Shared logic, no version difference | Leave as-is, no `#if` needed |

---

## Test projects

Multi-target test projects the same way:

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

Running `dotnet test` will then execute the full suite against both TFMs. Use the same
`OPTIMIZELY_CMS12` / `OPTIMIZELY_CMS13` symbols in test fixtures where needed.

---

## Migration workflow

When working on a file or feature:

1. Try building with `dotnet build` targeting both frameworks.
2. Fix each compiler error using the lowest applicable tier (prefer Tier 1, escalate only
   when the file becomes hard to read).
3. After fixing errors, check for runtime/behavioural differences that the compiler cannot
   catch — look for deprecated APIs, changed default behaviours, and removed types.
4. Ensure both `net8.0` and `net10.0` builds produce valid output before committing.

---

## What NOT to do

- Do not create a separate `MyAddon.Cms13` project that duplicates the full library.
- Do not use `#if` for more than ~3 blocks in a single method — extract to Tier 2 instead.
- Do not reference CMS-version-specific packages without a TFM condition.
- Do not add a `[Obsolete]` shim layer unless you have a specific deprecation plan.
- Do not guess at CMS 13 API changes — if uncertain, ask before modifying shared code.

---

## NuGet packaging notes

- Keep a single version number across both TFMs — the `.nupkg` will contain both
  `lib/net8.0/` and `lib/net10.0/` automatically.
- Set `<PackageValidationBaselineVersion>` if you want SDK Pack to enforce API compatibility
  between releases.
- If the public API surface must differ between CMS 12 and 13 (rare), document it clearly
  in the package release notes.