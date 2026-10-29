# Graph PowerTools — Design Spec

Issue: #65. Package: `UmageAI.Optimizely.EditorPowerTools.Graph`. Status: design approved 2026-09-30.

## 1. Goal

A companion package (like `EditorPowertools.Forms`) that improves **editorial search** using Optimizely Graph:

1. **Graph search providers** — CMS UI `ISearchProvider`s (global quick search, page-tree filter, assets pane blocks/media) plus the core `IContentSearchProvider` seam used by core's content picker.
2. **Power Slice** — a dojo Assets-pane widget that works like the old content slices (predefined, filterable, sortable content lists).
3. **PowerSearch** — a full-page editorial search with facets.

Core gets no Graph dependency. The package references the core project (NuGet dependency at same version).

## 2. Non-goals (v1)

Replacing CMS 13 Content Manager search, Commerce catalog providers, indexing/schema-push jobs, semantic/AI search (see #67), write operations on content.

## 3. Key decisions

| Topic | Decision |
|---|---|
| Graph client | Reuse the platform's own authenticated query client — no custom crypto. CMS 12: `Optimizely.ContentGraph.Core.IClient.QueryAsync(query, variables, headers, queryStringParams, ct)` (registered by the host's `AddContentGraph()`, HMAC-signed when `Secret` is set). CMS 13: `Optimizely.Graph.Core.Client.IGraphClient.QueryAsync(query, variables, customHeaders, ct)` (package family `Optimizely.Graph.*` 13.x; both bind `Optimizely:ContentGraph`). Both return the raw JSON string. All user input passed as GraphQL **variables**; only allow-listed identifiers (content type names, GUIDs) are ever interpolated. |
| CMS 12 / 13 | Both TFMs from day one (`net8.0;net10.0`). Tier 3: `IGraphQueryExecutor` (client adapter) and `IGraphQueryDialect` (query text + response parsing), each with `Cms12/` and `Cms13/` implementations. The CMS 12 dialect ports query shapes proven in production. The CMS 13 schema differs and **must be verified by introspection** against the CMS 13 test instance; until verified the CMS 13 dialect reports "not verified" through the health check instead of guessing. Tier 1 `#if OPTIMIZELY_CMS13` for `ISearchProvider.Key`. |
| Built-in search | Complement by default (register alongside). Option `DisableBuiltInProviders` (default false). |
| Reach | Global search, page-tree filter, assets pane (blocks, media), core content picker via `IContentSearchProvider`. Other selector dialogs only if verified in sample site. |
| Graph credentials | Reuse `Optimizely:ContentGraph` (GatewayAddress, AppKey, Secret, SingleKey) read via own options binding; no `Optimizely.ContentGraph.Cms` package reference unless required. |

## 4. Project layout

```
src/EditorPowertools.Graph/
  EditorPowertools.Graph.csproj        (Razor SDK, net8.0;net10.0, conditional refs, pack metadata, module zip target)
  Infrastructure/  ServiceCollectionExtensions (AddEditorPowertoolsGraph / UseEditorPowertoolsGraph / MapEditorPowertoolsGraph)
                   GraphMenuProvider, GraphPermissionTypes, GraphFeatureAccessChecker
  Configuration/   EditorPowertoolsGraphOptions, GraphFeatureToggles
  Abstractions/    IGraphQueryExecutor, IGraphQueryDialect, IGraphSearchService, query/result models
  Cms12/ Cms13/    executor (platform client adapter) + dialect per CMS version
  Search/          GraphSearchService, GraphLocaleMapper, GraphAccessHeaders, GraphAvailability, GraphHealthCheck (IDoctorCheck)
  Providers/       GraphSearchProviderBase, Page/Block/File providers, DraftSearch, SearchResultFactory, ContentPickerSearchProvider
  Tools/PowerSlice/   PowerSliceComponent ([Component]), PowerSliceBase : ContentQueryBase, built-in slices
  Tools/PowerSearch/  PowerSearchService, PowerSearchApiController, models
  Views/           PowerSearch/Index.cshtml (uses core _PowertoolsLayout)
  lang/*.xml       11 languages (embedded)
  modules/_protected/EditorPowertoolsGraph/  module.config, ClientResources/{css,js}
  build/net8.0, build/net10.0  .targets copying module zip
src/EditorPowertools.Tests.Graph/  (xunit 2.5.3, FluentAssertions 6.12.2, Moq 4.20.72; net8.0;net10.0)
docs/graph.md
```

Follow `EditorPowertools.Forms` csproj/packaging exactly (module zip, `InternalsVisibleTo`, `ProjectReference` to core, symbols). Add both projects to `EditorPowertools.slnx`, both SampleSites (registration + module copy target), and `.github/workflows/publish.yml` (restore/build/test/pack/attach). Check `.gitignore` so `modules/` assets are tracked (`git add -f` if needed); never commit the untracked CMS `OpalChat` build artifacts.

## 5. Configuration

`CodeArt:EditorPowertools:Graph` → `EditorPowertoolsGraphOptions`:
- `Enabled`-style feature toggles in `GraphFeatureToggles` (`PowerSearch`, `PowerSlice`, `SearchProviders`), default true, mirroring `FormsFeatureToggles`.
- `DisableBuiltInProviders` (false), `SearchRoots` fallbacks, `MaxResults`, `OverfetchFactor` (5), `MaxOverfetch` (250), `DraftSearchEnabled` (true), `RequestTimeoutSeconds`.
- Graph credentials from `Optimizely:ContentGraph`; secrets never logged.
- `AddEditorPowertoolsGraph()` overloads with and without options lambda; registers `ModuleDetails { Name = "EditorPowertoolsGraph" }` in `ProtectedModuleOptions`; `IPowertoolDescriptor`s for Overview tiles; `IDoctorCheck` for Graph health.

## 6. Permissions and security (same three-layer model as core/Forms)

- Layer 1: feature toggles. Layer 2: `[Authorize(Policy = "codeart:editorpowertools")]` on every controller. Layer 3: `PermissionType("EditorPowertools", "PowerSearch" / "PowerSlice")` via `GraphFeatureAccessChecker` (delegates `HasPermission` to core `FeatureAccessChecker`).
- Every API action calls `_accessChecker.HasAccess(HttpContext, nameof(GraphFeatureToggles.X), Permissions.X)` and returns `Forbid()` on failure; POST/DELETE carry `[RequireAjax]`.
- Content operations use proper `AccessLevel` (never `NoAccess`). Error responses never expose `ex.Message`.
- **ACL:** all editor queries are executed with the current editor's `cg-username` and `cg-roles` (HMAC/Basic) so Graph filters on `RolesWithReadAccess`/`UsersWithReadAccess` before ranking. Never run editor queries with a role-less admin credential or the public single key. Missing `Secret` → health check error and disabled state, never silent empty/unauthenticated results.
- Injection: phrase, locale, limit, skip, selected types/sections always GraphQL variables; type names validated against `IContentTypeRepository`; section ids parsed as GUIDs. Dedicated injection tests.
- Hits are re-checked against CMS (`IContentLoader` with read access, search roots via parsed `ContentReference`) before being returned.
- No static state; everything via DI. Nullable enabled.
- Localization: every user-facing string through `LocalizationService` under `/editorpowertools/graph/...` in all 11 language files (`en` base). JS strings via `window.EPT_STRINGS` / `WidgetStrings`. Today `UiStringsProvider` only serves core keys, so add-on JS strings silently fall back to English (Forms has this bug). Core gets a small extension point, `IUiStringsContributor` (section name + key list under `/editorpowertools/ui/{section}/`), merged into `UiStringsProvider.GetAll()`; Graph and Forms both register one. No hardcoded role names or English text.
- JS never hardcodes paths: `window.EPT_API_URL`, `window.EPT_GRAPH_BASE_URL` (Forms-style `EPT_FORMS_BASE_URL` analogue), resolved from `Paths.ToResource()`/`Paths.ToClientResource()`. Never type `/EPiServer/`.

## 7. Search providers

- `GraphSearchProviderBase : ISearchProvider` (CMS 13: adds `Key`, implements directly since `ContentSearchProviderBase` is absent). Registered with `TryAddEnumerable(Scoped<ISearchProvider,…>)`; honors `DisableBuiltInProviders`.
- Areas (literal strings): `cms/pages`, `cms/blocks`, `cms/files`. Matching case-insensitive.
- Query: `_ranking: RELEVANCE`, OR of `Name contains` (boost 10), `RouteSegment like` (5), `Name contains fuzzy` (3), `_fulltext match`. Operator vocabulary differs per field (e.g. `RouteSegment` has no `contains`); generated query text is unit-tested.
- Result metadata (silent-failure traps): `Id` (PascalCase, full `ContentLink`), `TypeIdentifier` (from `GetTypeIdentifier(UIDescriptorRegistry)`), `ParentId`, `IsOnCurrentHost`, `TypeId`, `ThumbnailUrl`, `LanguageBranch`, `IsUntranslatedContent`. Edit URL includes `?language=`. Disambiguator goes in HTML-encoded title (`PreviewText` isn't rendered).
- Search roots: `Query.SearchRoots` → `Ancestors` constraint (GUIDs); unexpressible roots → retry without constraint then post-filter; overfetch `min(max*factor, cap)`.
- Drafts: Graph is publish-only; draft name matches come from `IContentVersionRepository` (`ProviderName = string.Empty`), access-checked, listed first, capped at `maxResults/3`.
- Sync `Search` blocks on the async call; catch all exceptions → return empty + log.
- `ContentPickerSearchProvider : IContentSearchProvider` (core seam) registered by the package.
- Locale: `GraphLocaleMapper` maps `fr-CH`→`fr_CH` etc.; never truncate. Media/catalog have no language filter.

## 8. Power Slice

- Slices are CMS content queries: `PowerSliceBase : ContentQueryBase` (`EPiServer.Cms.Shell.UI.Rest.ContentQuery`, present in CMS 12 and 13). The widget renders them with the CMS's own `epi-cms/component/ContentQueryGrid`, so rows get platform icons, status, context menus and drag-and-drop for free — no custom slice API controller.
- `[Component] PowerSliceComponent : ComponentDefinitionBase`, widget `editorpowertools-graph/PowerSliceWidget`, `PlugInAreas = AssetsDefaultGroup`, `LanguagePath`, slice list (name, localized display name, sort options) passed through `Settings` so DI-registered slices (including third-party `PowerSliceBase` subclasses) appear without JS changes.
- AMD widget (`_Container`/`_LayoutWidget`/`_TemplatedMixin`/`_WidgetsInTemplateMixin`): toolbar is a **sibling** of the grid; `resize()` without args falls back to `_contentBox`; set `queryParameters` before `queryName`; debounce filter 300 ms; rebuild sort options per slice.
- Built-in slices, backed by CMS stores (version store, references) not Graph: My content, Recently changed, Drafts, Awaiting approval, Stale content, Unused blocks. Thresholds (days/months, max versions) configurable under `PowerSlice` options. Access = PowerSlice feature toggle + PermissionType (same checker as every other tool) plus per-item `AccessLevel.Read`; no hardcoded role names. Version-store filters pin `ProviderName = string.Empty`, filter `ChangedBy` in the store, exclude folders/root/wastebasket.
- Graph is not used by slices in v1; the package dependency is for search. Slices ship in the Graph package because they are part of the editorial-search story.

## 9. PowerSearch

- Menu item under `MenuPaths.Global + "/cms/editorpowertools"` (next free SortIndex after Forms), page via `_PowertoolsLayout`, vanilla JS in `ClientResources/js/powersearch.js` and `css/powersearch.css` (`ept-*` classes).
- API: `GET …/PowerSearchApi/Search?q&tab&language&take&type[]&lang[]&section[]&skip`; `take` clamped 1–50; empty `q` short-circuits. Tabs: Pages, Blocks, Media, Other; Graph queries in parallel via `Task.WhenAll`, **CMS content loading sequential** (shared SQL connection).
- Facets: content type, language, section (from `Ancestors`, levels shared by all hits dropped, GUIDs resolved to names). Each facet group is an aliased extra query with its own filter lifted (one round trip); zero-count values dropped; `total == -1` → unknown (hide badge). Facet rail shows top 8 with "Show N more"; selected value always visible.
- De-duplicate by content id; paging via server `NextSkip`; "index may lag" notice. Result rows show name, type, language, breadcrumb and changed date (loaded from CMS); Graph highlighting is deferred to a later version (its syntax is unverified for both schemas).
- Empty/unconfigured state driven by health check.

## 10. Sample sites with Graph

Both sample sites (`EditorPowertools.SampleSite` CMS 12, `EditorPowertools.SampleSiteCms13` CMS 13) get real Graph wiring so the package is exercised end to end:

- Add the Graph indexing/sync package to the **sample sites only**: `Optimizely.ContentGraph.Cms` 4.* (CMS 12) and `Optimizely.Graph.Cms` 13.* (CMS 13). Call its registration in `Startup` (`AddContentGraph()` on CMS 12; the CMS 13 registration method is confirmed from the package when wiring). The library itself references only the query client packages: `Optimizely.ContentGraph.Core` 4.* (net8.0) and `Optimizely.Graph.Core` 13.* (net10.0).
- Credentials (`Optimizely:ContentGraph:GatewayAddress/AppKey/Secret/SingleKey`) come from **user-secrets / environment variables**, never committed. `appsettings.json` contains only empty placeholders; `docs/local-development.md` documents the `dotnet user-secrets set` commands for each site. Separate test instances per CMS version (schemas differ).
- Sample sites call `AddEditorPowertoolsGraph()` / `UseEditorPowertoolsGraph()` / `MapEditorPowertoolsGraph()` and copy the module folder, same as Forms.
- Sites must still start and run without credentials: Graph tools show the disabled/health-check state; the rest of the powertools are unaffected.
- Once instances are available, sync the Alloy content and use the sites to resolve the verification items in §13 and to take screenshots.

## 11. Testing

xunit + Moq + FluentAssertions on both TFMs: query-text assertions, variable-binding/injection, locale mapper, metadata completeness, ACL header construction (roles/username, anonymous = none), roots fallback and post-filter, facet merging/zero-count/-1, draft search, slice queries, permission/feature-toggle matrix, controller attribute checks (`[Authorize]`, `[RequireAjax]`, `HasAccess` on every action, no `ex.Message`), lang files complete for all 11 languages, module.config validity. Run `dotnet test` without `--no-build`.

## 12. Docs

`docs/graph.md` (template: `docs/forms.md`), README section, `RELEASE-NOTES.md`, `docs/backlog.md`, configuration docs, screenshots taken from the SampleSite.

## 13. Open verification items (resolve against the sample-site Graph test instances, do not guess)

1. CMS 13 Graph schema field names, filter operators, facet shapes and locale enum (introspection).
2. That the CMS 13 `IGraphClient` is the authenticated (HMAC) client and honours `cg-username`/`cg-roles`; that CMS 12 `IClient` does (it does in production at the reference site).
3. `[Component]`, `ContentQueryBase` and `ISearchProvider.Key` behaviour in CMS 13.
4. Which selector dialogs / assets pane tabs call `ISearchProvider` in each sample site.
