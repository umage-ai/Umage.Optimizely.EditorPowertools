# EditorPowertools

An Optimizely CMS 12/13 plugin providing a collection of power tools for editors and admins.
Distributed as NuGet package `UmageAI.Optimizely.EditorPowerTools`.

## Project Structure

- `src/EditorPowertools/` - Plugin class library (Razor SDK)
- `src/EditorPowertools.SampleSite/` - Alloy demo site for development/testing
- `docs/backlog.md` - Tool backlog
- `docs/coding-guidelines.md` - Architecture and coding standards

## Tech Stack

- C# on .NET 8 / CMS 12 and .NET 10 / CMS 13 (see Multi-targeting)
- UI: Vanilla JS or React (NOT Blazor - the old project was Blazor, this is a rewrite)
- Razor class library for embedded views and static assets

## Build & Run

```bash
dotnet build                                           # Build solution
dotnet run --project src/EditorPowertools.SampleSite   # Run sample site
bash scripts/check-conventions.sh                      # Mechanical rules (CI runs it)
```

Before the first run, unzip the sample site database and blobs: `docs/local-development.md` §2.

## Release & distribution

- Pushing a tag matching `v*` triggers `.github/workflows/publish.yml`, which builds, tests (both TFMs), packs, and **attaches the `.nupkg` to a GitHub release**. Example: `git tag -a v0.7.0 -m "..." && git push origin v0.7.0`.
- **Distribution channel is the Optimizely NuGet feed**, which pulls from the GitHub release — **we do not push to nuget.org directly**. Don't suggest a nuget.org push step.
- Version numbers come from the tag name (the workflow strips the leading `v`). Stick to `vMAJOR.MINOR.PATCH` (preview variants like `v0.1.0-preview.5` exist in the history but aren't the current pattern).
- If a tag-triggered run fails before the publish step, the tag is safe to delete + recreate at a new commit — no package got out. (Tag delete is destructive on shared state, so confirm first.)
- Pre-release local verification: `dotnet test` (no `--no-build` — that flag silently uses stale test DLLs and can miss compile errors introduced by signature changes; `--no-build` burned us once already).

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
- **Module paths — NEVER hardcode `/EPiServer/` (or any other prefix) anywhere.** The CMS Shell mount point is configurable and the protected-module virtual path differs between CMS 12 and CMS 13. Always resolve via `EPiServer.Shell.Paths.ToResource(typeof(SomeMenuProvider), "...")` (server) or `window.EPT_BASE_URL` / `window.EPT_FORMS_BASE_URL` (client). `scripts/check-conventions.sh` enforces this for `.cs`, `.js` and `.cshtml`; use the same resolution in test scripts and curl probes.

## Localization

All user-facing strings MUST go through Optimizely's localization system — never hardcode display text in C#.

- **Language files**: `src/EditorPowertools/lang/*.xml` (11 languages: en, da, sv, no, de, fi, fr, es, nl, ja, zh-CN)
- **String path convention**: `/editorpowertools/{area}/{key}` — e.g. `/editorpowertools/menu/contenttypeaudit`, `/editorpowertools/cmsdoctor/checks/memorycheck/name`
- **In services/controllers**: Inject `LocalizationService` and call `_localization.GetString("/editorpowertools/path/key")`
- **In components**: Set `LanguagePath = "/editorpowertools/components/yourcomponent"` — Optimizely auto-resolves `/title` and `/description`
- **In menu items**: Use `_localization.GetString()` for menu item names
- **Adding new strings**: Add to ALL 11 language files under the appropriate path. English in `en.xml` is the base; translate others accordingly.
- **Fallback**: If a key is missing, `GetString()` returns the key path — always provide English as the base language


# Multi-targeting: CMS 12 (.NET 8) + CMS 13 (.NET 10)

One codebase and one NuGet package per project, built as `net8.0;net10.0` (library and test projects). Symbols `OPTIMIZELY_CMS12` / `OPTIMIZELY_CMS13` are defined per TFM; copy the csproj wiring from `src/EditorPowertools.Graph/EditorPowertools.Graph.csproj`. Details and CMS 13 API differences: `docs/cms13-support.md`.

Resolve each version difference at the lowest tier:

1. **`#if OPTIMIZELY_CMS13`** (CMS 13 first, `#else` CMS 12) for 1–10 lines in one place; at most ~3 blocks per method.
2. **TFM-specific files** (`Cms12/`, `Cms13/`, wired by csproj `Compile Include`) once a file is over ~30% `#if`.
3. **Internal interface** (`Abstractions/I…`, registered via `#if` in the DI extension) when a subsystem changed architecture. Example: `IGraphQueryExecutor` in the Graph add-on.

Rules:
- Gate version-specific package references with a TFM condition, using the version-range properties in `src/Directory.Build.props` (never `12.*`).
- Releases also ship net8.0-only `.Cms12` packages for CMS 12 on .NET 10 (`EptCmsFlavor`, see `docs/cms13-support.md` §Packaging); `scripts/pack-smoke.sh` is the check.
- Verify a CMS 13 API against the package assemblies and XML docs in `~/.nuget/packages` before using it.
- Build and test both TFMs before committing: `dotnet test` (no `--no-build`).
