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
