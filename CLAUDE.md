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

## Key Patterns

- **Registration**: `services.AddEditorPowertools(options => ...)` + `app.UseEditorPowertools()`
- **Options**: `EditorPowertoolsOptions` bound from `CodeArt:EditorPowertools` config section
- **Permissions**: Three-layer (feature toggles + auth policy + optional EPiServer PermissionTypes)
- **Tool structure**: Each tool in `Tools/{ToolName}/` with Controller + Service + ViewModel
- **Menu**: `EditorPowertoolsMenuProvider` registers tools in CMS global navigation
- **Data**: DynamicDataStore for persistence, in-memory cache for expensive queries

## Conventions

- Follow `docs/coding-guidelines.md` for all tool implementations
- No static state - everything via DI
- Nullable reference types enabled
- Controllers return JSON APIs; UI is vanilla JS or React, not server-rendered
- Each tool has a corresponding PermissionType and FeatureToggle
