# Graph Add-On

Optimizely Graph tooling for Editor Powertools ships as a **separate, optional NuGet package**, `UmageAI.Optimizely.EditorPowerTools.Graph`, so only sites that run [Optimizely Graph](https://docs.developers.optimizely.com/platform-optimizely/docs/optimizely-graph) take the extra dependency.

Like the base package it is multi-targeted: **Optimizely CMS 12 (.NET 8)** and **CMS 13 (.NET 10)** from a single package. It depends on the base `UmageAI.Optimizely.EditorPowerTools` package (pinned to the same version) and on the Graph client for the CMS version (`Optimizely.ContentGraph.Core` 4.x on CMS 12, `Optimizely.Graph.Core` 13.x on CMS 13).

> **Screenshots: pending live verification.** Screenshots of PowerSearch, Power Slice and the quick search will be added once the add-on has been verified against live Graph instances.

## What it does

| Tool | Description |
|------|-------------|
| **PowerSearch** | A full-page editor search tool backed by Graph: full-text search, tabs per content kind, facets (content type, section, language), drafts shown first, and "load more" paging. With an empty search box it lists the tab newest first, so editors can browse by facets alone. |
| **Power Slice** | An assets-pane widget with ready-made content lists (my content, recently changed, stale content, unused blocks, and so on). The list loads more rows as you scroll. Each slice is an `IContentQuery`, so you can add your own. |
| **Search providers** | Replaces or supplements the CMS built-in Pages, Blocks and Media search providers (quick search, page-tree filter, assets pane) and the content picker search with Graph-backed results. |

It also adds a **Graph** check to the base **CMS Doctor** dashboard.

## Prerequisites

- An Optimizely Graph instance that your CMS site is **synchronizing content to** (the Graph client package installed, `AddContentGraph()` (CMS 12) or the CMS 13 equivalent registered, and the content synchronization job having run at least once).
- **`Secret` configured** under `Optimizely:ContentGraph`. Without the secret the platform client falls back to public single-key access, which cannot see restricted content. The add-on treats this as "not configured" rather than returning a silently partial index.
- **Inheritance in the content type field** enabled (`IncludeInheritanceInContentType = true` on CMS 12), so that filters such as `ContentType: { in: ["PageData"] }` match derived types.

## Installation

```bash
dotnet add package UmageAI.Optimizely.EditorPowerTools.Graph
```

The add-on declares a dependency on the base package, so a package manager pulls it in automatically, but you register each explicitly.

## Registration

Register the Graph add-on **after** the base package and after the Graph client:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... existing Optimizely services

    services.AddContentGraph();              // Optimizely Graph client (CMS 12 name; see the Graph docs for CMS 13)
    services.AddEditorPowertools();          // base package
    services.AddEditorPowertoolsGraph();     // Graph add-on
}

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // ... existing middleware

    app.UseEditorPowertools();
    app.UseEditorPowertoolsGraph();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapContent();
        endpoints.MapEditorPowertools();
        endpoints.MapEditorPowertoolsGraph();   // maps the Graph tool endpoints
    });
}
```

Graph connection settings are the platform client's own, under `Optimizely:ContentGraph` (`GatewayAddress`, `AppKey`, `Secret`, `SingleKey`). Keep them in user-secrets or environment variables, never in source control.

## Configuration

Options are bound from the `CodeArt:EditorPowertools:Graph` section (separate from the base package's section). Values below are the defaults.

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "Graph": {
        "features": { "powerSearch": true, "powerSlice": true, "searchProviders": true },
        "search": { "includeDrafts": true, "powerSearchPageSize": 20 },
        "powerSlice": { "recentlyChangedDays": 14 }
      }
    }
  }
}
```

Or in code:

```csharp
services.AddEditorPowertoolsGraph(options =>
{
    options.Search.DisableBuiltInProviders = true;
    options.PowerSlice.StaleContentMonths = 12;
});
```

| Option | Default | Description |
|--------|---------|-------------|
| `Features.PowerSearch` | `true` | Enables the PowerSearch tool. |
| `Features.PowerSlice` | `true` | Enables the Power Slice widget. |
| `Features.SearchProviders` | `true` | Enables the Graph-backed search providers and content picker search. |
| `Search.DisableBuiltInProviders` | `false` | Hide the CMS built-in page/block/media search providers while Graph is available. |
| `Search.IncludeDrafts` | `true` | Merge the editor's own drafts (read from the CMS, not Graph) into results, shown first. |
| `Search.OverfetchFactor` | `5` | Graph is asked for this multiple of the requested page size, so post-filtering does not leave short pages. |
| `Search.MaxOverfetch` | `100` | Upper bound on the overfetch. Graph accepts at most 100 per request, so higher values are capped. |
| `Search.ContentTypeFacetLimit` | `100` | Maximum content type facet values. |
| `Search.SectionFacetLimit` | `200` | Maximum section facet values. |
| `Search.PowerSearchPageSize` | `20` | Results per page in PowerSearch. |
| `PowerSlice.RecentlyChangedDays` | `14` | Window for the "recently changed" slice. |
| `PowerSlice.StaleContentMonths` | `18` | Age after which content counts as stale. |
| `PowerSlice.MaxVersions` | `2000` | Maximum versions read by version-based slices. |
| `PowerSlice.MaxUnusedBlocksScanned` | `5000` | Maximum blocks examined by the "unused blocks" slice. |

## Choosing search providers

Which search providers editors see is the site owner's choice. The add-on registers its own providers, **Pages (Graph)**, **Blocks (Graph)** and **Media (Graph)**, next to the ones already there:

- the CMS built-in providers (**Pages**, **Blocks**, **Media**), which search the CMS database;
- the providers from Optimizely's own Graph package (**Pages (from Graph)** and so on), when that package is installed.

To choose, open **Search Configuration** (under Admin on CMS 12, Settings on CMS 13). Tick the providers editors should get and drag them into the order their results should appear in. For example, to switch fully to this add-on's providers, enable the three **(Graph)** providers and clear the others.

Two options in this add-on also help:

- `Search.DisableBuiltInProviders` hides the CMS built-in providers while Graph is available, and brings them back automatically when Graph is not. It does not affect Optimizely's own Graph providers; use Search Configuration for those.
- `Features.SearchProviders = false` removes this add-on's providers and content picker search entirely.

## Permissions

The Graph tools use the same three-layer model as the base package (feature toggle, then `AuthorizedRoles`, then optional per-function permissions). The add-on registers three permission entries, `PowerSearch`, `PowerSlice` and `SearchProviders`, which appear under **Admin, Permissions For Functions** when `CheckPermissionForEachFeature` is enabled on the base package.

`AuthorizedRoles` defaults to `WebAdmins` and `Administrators`. To give ordinary editors Graph search and slices, **add `WebEditors` to `AuthorizedRoles`** in the base package's configuration.

## Security model

- Every Graph query runs **as the current editor**: the add-on sends the editor's username and roles as the `cg-username` and `cg-roles` access headers, so Graph applies the same access rights the editor has in the CMS. An anonymous caller sends empty identity headers and gets nothing.
- Drafts are not in Graph. They are read from the CMS and filtered by the editor's read access before being merged in.
- Nothing restricted is returned: results that the editor cannot read are dropped, and the Graph `Secret` is required so the index is never queried with public-only access.
- All controllers require the `codeart:editorpowertools` policy, check the feature access on every action, and never return exception messages to clients. Failures are logged server-side only.
- Power Slice lists re-check read access on every item, because slices read the version store directly.

## Adding a custom slice

Subclass `PowerSliceBase` and register the class as an `IContentQuery`:

```csharp
public class MyTeamSlice : PowerSliceBase
{
    public MyTeamSlice(
        IContentRepository contentRepository,
        IContentQueryHelper queryHelper,
        GraphFeatureAccessChecker access,
        IHttpContextAccessor http,
        LocalizationService localization)
        : base(contentRepository, queryHelper, access, http, localization) { }

    // Implement the abstract members to name the slice and return its content.
}

services.AddTransient<IContentQuery, MyTeamSlice>();
```

The widget discovers every `IContentQuery` whose `PlugInAreas` contains `PowerSliceBase.PlugInArea`. The built-in slices in `Tools/PowerSlice/BuiltInSlices.cs` are good templates.

## CMS 12 and CMS 13 notes

Both versions are verified against a live Optimizely Graph instance. The add-on picks the right query dialect for the target framework.

| | CMS 12 (.NET 8) | CMS 13 (.NET 10) |
|---|---|---|
| Graph client package | `Optimizely.ContentGraph.Core` 4.x | `Optimizely.Graph.Core` 13.x |
| Graph schema | `Content` root, PascalCase fields (`Name`, `ContentType`, `Ancestors`) | `_Content` root, fields under `_metadata` and `_itemMetadata` (shared with SaaS CMS) |
| Access-rights changes | Reach Graph on the next synchronization job run | Reach Graph immediately |
| Asset folders in ancestor paths | Included | Not included, so the block and media providers check "shared assets only" in the CMS instead |

Behaviour common to both:

- Only **published** versions are searched in Graph. Graph also indexes checked-out drafts, which would otherwise double-count totals and facets. Drafts come from the CMS version store instead, with CMS access checks.
- The **content type facet** uses the item's own type, so filtering on "Standard Page" does not also return page types that inherit from it.
- Graph accepts at most 100 items per request, so page sizes and overfetch are capped at 100.
- Optimizely's own Graph package registers its own Graph search providers ("Pages (from Graph)" and so on). They appear next to this add-on's providers ("Pages (Graph)"); see [Choosing search providers](#choosing-search-providers).
- An empty PowerSearch box lists content newest first: by `Saved` on CMS 12 and by `_metadata.lastModified` on CMS 13. Drafts are not merged in while browsing, because drafts are found by name.

## Troubleshooting

The **Graph** check in CMS Doctor reports one of these states:

| State | Meaning | Fix |
|-------|---------|-----|
| Client not registered | The platform Graph client is not in DI. | Install the Graph client package and call its registration (`AddContentGraph()` on CMS 12). |
| Not configured | `GatewayAddress`, `AppKey` or `Secret` is empty. | Set all three under `Optimizely:ContentGraph` (user-secrets or environment). |
| Available | Ready. | |

- **New or changed content is missing:** Graph is updated by the content synchronization job and has some indexing lag. Run the job and retry. On CMS 12 this also applies to access-rights changes.
- **Results are empty for an editor but not an administrator:** the editor's roles lack read access to the content, or `WebEditors` is not in `AuthorizedRoles`.
- **Derived page types are not found (CMS 12):** enable `IncludeInheritanceInContentType` on the Graph client.

## Screenshots

Screenshots: pending live verification.
