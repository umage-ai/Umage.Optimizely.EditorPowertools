# Graph PowerTools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `UmageAI.Optimizely.EditorPowerTools.Graph` — a companion package that adds Graph-backed CMS search providers, a Power Slice assets-pane widget, and a faceted PowerSearch page, on CMS 12 (net8.0) and CMS 13 (net10.0).

**Architecture:** A Razor class library cloned from the `EditorPowertools.Forms` packaging pattern. Graph is reached through the platform's own authenticated query client behind two Tier 3 seams: `IGraphQueryExecutor` (client adapter) and `IGraphQueryDialect` (query text + parsing), each with a `Cms12/` and `Cms13/` implementation. Everything user-facing is gated by the same three-layer access model as the rest of the power tools and localized in 11 languages.

**Tech Stack:** .NET 8/10, Optimizely CMS 12/13, `Optimizely.ContentGraph.Core` 4.* (net8.0), `Optimizely.Graph.Core` 13.* (net10.0), vanilla JS + Dojo AMD, xunit 2.5.3, Moq 4.20.72, FluentAssertions 6.12.2.

**Spec:** `docs/superpowers/specs/2026-09-30-graph-powertools-design.md` — read it before starting any task.

## Global Constraints

- Root namespace `UmageAI.Optimizely.EditorPowerTools.Graph`; AssemblyName `EditorPowertools.Graph`; PackageId `UmageAI.Optimizely.EditorPowerTools.Graph`; protected module name `EditorPowertoolsGraph`; dojo package name `editorpowertools-graph`.
- TFMs `net8.0;net10.0`; symbols `OPTIMIZELY_CMS12` / `OPTIMIZELY_CMS13`; `Cms12\**` compiled only for net8.0, `Cms13\**` only for net10.0. Prefer `#if OPTIMIZELY_CMS13` as the leading branch.
- Config section `CodeArt:EditorPowertools:Graph`. Graph credentials are **never** read or logged by this package — the platform client owns them.
- Every controller: `[Authorize(Policy = "codeart:editorpowertools")]`. Every action: `_accessChecker.HasAccess(...)` → `Forbid()`. API controllers: `[RequireAjax]`. Error responses never contain `ex.Message`.
- Content access uses `AccessLevel.Read` (never `NoAccess`). No static mutable state; everything via DI. Nullable enabled.
- All user-facing text via `LocalizationService` / `EPT.s(...)`, keys under `/editorpowertools/...`, present in all 11 lang files (en, da, sv, no, de, fi, fr, es, nl, ja, zh-CN). No hardcoded role names, no hardcoded English in C# except `GetStringByCulture` fallbacks.
- Never hardcode `/EPiServer/` or any shell prefix. Server: `Paths.ToResource(typeof(EditorPowertoolsGraphMenuProvider), ...)` / `Paths.ToClientResource(...)`. Client: `window.EPT_GRAPH_BASE_URL`.
- GraphQL: user input only as **variables**. Only compile-time constants, integers from options, content-type names validated against `IContentTypeRepository`, and `Guid` values formatted with `:D` may appear in query text.
- Tests: `dotnet test` from repo root, **never** `--no-build`.
- Do not edit anything under `C:\Github\nti-website-upgrade-release` (read-only reference). Do not commit `modules/_protected/OpalChat/` or other CMS build artifacts.
- Commit after every task with Conventional Commits (`feat(graph): …`, `test(graph): …`, `docs(graph): …`) ending with the session's attribution trailer.

## Review Focus

1. **Editor without read access to restricted content searches for it** → must not appear in providers, PowerSearch or picker. Pinned in Task 3 (headers carry the real user's roles, anonymous sends empty roles) and Task 7 (every hit re-loaded through CMS and `QueryDistinctAccess(AccessLevel.Read)` checked).
2. **Search phrase containing GraphQL syntax / quotes / `%` / `_` / `\`** → treated literally, query shape unchanged. Pinned in Task 5 (injection tests).
3. **Graph not configured, unreachable, or returning `errors`** → CMS edit UI keeps working with built-in results; PowerSearch shows a localized "not available" state; no exception text leaves the server. Pinned in Task 4 (availability), Task 8 (provider swallows), Task 11 (API returns problem code without message).
4. **Region-qualified language (`fr-CH`, `en-GB`, `zh-CN`)** → mapped by rule `xx-YY` → `xx_YY` (never truncated to `xx`, which would silently search another market); neutral cultures unchanged. Pinned in Task 5 (locale mapper tests). CMS 13 enum values confirmed in Task 16.
5. **Facet selection that would empty its own group / zero-count and `total == -1` values** → group keeps alternatives, zero counts dropped, unknown total hides the badge. Pinned in Task 5 (parser) and Task 12 (JS render rules).

---

## File map

```
src/EditorPowertools/Localization/IUiStringsContributor.cs            (new, Task 2)
src/EditorPowertools/Localization/UiStringsProvider.cs                (modify, Task 2)
src/EditorPowertools.Forms/Localization/FormsUiStringsContributor.cs  (new, Task 2)
src/EditorPowertools.Graph/
  EditorPowertools.Graph.csproj
  Configuration/EditorPowertoolsGraphOptions.cs
  Permissions/EditorPowertoolsGraphPermissions.cs
  Permissions/GraphFeatureAccessChecker.cs
  Menu/EditorPowertoolsGraphMenuProvider.cs
  Infrastructure/ServiceCollectionExtensions.cs
  Infrastructure/ApplicationBuilderExtensions.cs
  Infrastructure/GraphSvgIcons.cs
  Localization/GraphUiStringsContributor.cs
  Abstractions/IGraphQueryExecutor.cs
  Abstractions/IGraphQueryDialect.cs
  Abstractions/GraphSearchModels.cs
  Search/GraphAccessHeaders.cs
  Search/GraphQueryException.cs
  Search/GraphAvailability.cs
  Search/GraphLocaleMapper.cs
  Search/GraphSearchService.cs
  Search/GraphHealthCheck.cs
  Cms12/Cms12GraphQueryExecutor.cs
  Cms12/Cms12GraphQueryDialect.cs
  Cms13/Cms13GraphQueryExecutor.cs
  Cms13/Cms13GraphQueryDialect.cs
  Providers/SearchRoots.cs
  Providers/DraftSearch.cs
  Providers/SearchResultFactory.cs
  Providers/GraphSearchProviderBase.cs
  Providers/GraphPageSearchProvider.cs
  Providers/GraphBlockSearchProvider.cs
  Providers/GraphFileSearchProvider.cs
  Providers/GraphContentPickerSearchProvider.cs
  Tools/PowerSlice/PowerSliceBase.cs
  Tools/PowerSlice/SliceSortOption.cs
  Tools/PowerSlice/SliceVersionHelper.cs
  Tools/PowerSlice/BuiltInSlices.cs
  Tools/PowerSlice/PowerSliceComponent.cs
  Tools/PowerSearch/PowerSearchModels.cs
  Tools/PowerSearch/PowerSearchService.cs
  Tools/PowerSearch/PowerSearchApiController.cs
  Tools/PowerSearch/EditorPowertoolsGraphController.cs
  Views/_ViewImports.cshtml
  Views/PowerSearch/Index.cshtml
  lang/{en,da,sv,no,de,fi,fr,es,nl,ja,zh-CN}.xml
  build/net8.0/UmageAI.Optimizely.EditorPowerTools.Graph.targets
  build/net10.0/UmageAI.Optimizely.EditorPowerTools.Graph.targets
  modules/_protected/EditorPowertoolsGraph/module.config
  modules/_protected/EditorPowertoolsGraph/ClientResources/js/PowerSliceWidget.js
  modules/_protected/EditorPowertoolsGraph/ClientResources/js/templates/PowerSliceWidget.html
  modules/_protected/EditorPowertoolsGraph/ClientResources/js/powersearch.js
  modules/_protected/EditorPowertoolsGraph/ClientResources/css/editorpowertools-graph.css
src/EditorPowertools.Tests.Graph/ (mirrors the folders above + Helpers/)
docs/graph.md, README.md, RELEASE-NOTES.md, docs/backlog.md, docs/configuration.md, docs/local-development.md
EditorPowertools.slnx, .github/workflows/publish.yml, both SampleSite projects
```

---

### Task 1: Project scaffold, options, permissions, access checker, menu, registration

**Files:**
- Create: everything under `src/EditorPowertools.Graph/` listed for Configuration, Permissions, Menu, Infrastructure, `Views/_ViewImports.cshtml`, `build/*/…targets`, `modules/_protected/EditorPowertoolsGraph/module.config`, `lang/en.xml` (skeleton only; full strings in Task 13).
- Create: `src/EditorPowertools.Tests.Graph/EditorPowertools.Tests.Graph.csproj`, `Helpers/GraphTestSetup.cs`, `Permissions/GraphFeatureAccessCheckerTests.cs`, `Infrastructure/ServiceRegistrationTests.cs`
- Modify: `EditorPowertools.slnx`, `src/EditorPowertools/Abstractions/PowertoolDescriptor.cs` (add group constant), `.gitignore`

**Interfaces:**
- Produces: `EditorPowertoolsGraphOptions` (+ `GraphFeatureToggles`, `GraphSearchOptions`, `PowerSliceOptions`), `EditorPowertoolsGraphPermissions.{PowerSearch,PowerSlice,SearchProviders}`, `GraphFeatureAccessChecker.IsFeatureEnabled(string)`, `.HasAccess(HttpContext, string, PermissionType)`, `EditorPowertoolsGraphMenuProvider`, `services.AddEditorPowertoolsGraph()`, `app.UseEditorPowertoolsGraph()`, `endpoints.MapEditorPowertoolsGraph()`, `PowertoolGroups.Search`.

- [ ] **Step 1: Create the csproj** — copy `src/EditorPowertools.Forms/EditorPowertools.Forms.csproj` and change: RootNamespace/AssemblyName/PackageId per Global Constraints; Description `"Optimizely Graph add-on for UmageAI Editor Powertools. Graph-backed editor search providers, Power Slice content lists and faceted PowerSearch. Supports Optimizely CMS 12 and CMS 13."`; PackageTags add `Graph;Search`; `InternalsVisibleTo EditorPowertools.Tests.Graph`; all `Forms` in target/zip/module names → `Graph`; package references:

```xml
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <PackageReference Include="EPiServer.CMS" Version="12.*" />
    <PackageReference Include="EPiServer.CMS.UI.Core" Version="12.*" />
    <PackageReference Include="Optimizely.ContentGraph.Core" Version="4.*" />
  </ItemGroup>
  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <PackageReference Include="EPiServer.CMS" Version="13.*" />
    <PackageReference Include="EPiServer.CMS.UI.Core" Version="13.*" />
    <PackageReference Include="Optimizely.Graph.Core" Version="13.*" />
  </ItemGroup>
```

  Build targets: `build/net8.0/UmageAI.Optimizely.EditorPowerTools.Graph.targets` and the net10.0 twin are copies of the Forms targets with target name `CopyEditorPowertoolsGraphModule`.

- [ ] **Step 2: Options** — `Configuration/EditorPowertoolsGraphOptions.cs`:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Graph.Configuration;

/// <summary>Feature toggles for the Graph add-on. All default to enabled.</summary>
public class GraphFeatureToggles
{
    public bool PowerSearch { get; set; } = true;
    public bool PowerSlice { get; set; } = true;
    public bool SearchProviders { get; set; } = true;
}

public class GraphSearchOptions
{
    /// <summary>Hide the CMS built-in page/block/media search providers when Graph is available.</summary>
    public bool DisableBuiltInProviders { get; set; }
    public bool IncludeDrafts { get; set; } = true;
    public int OverfetchFactor { get; set; } = 5;
    public int MaxOverfetch { get; set; } = 250;
    public int ContentTypeFacetLimit { get; set; } = 100;
    public int SectionFacetLimit { get; set; } = 200;
    public int PowerSearchPageSize { get; set; } = 20;
}

public class PowerSliceOptions
{
    public int RecentlyChangedDays { get; set; } = 14;
    public int StaleContentMonths { get; set; } = 18;
    public int MaxVersions { get; set; } = 2000;
    public int MaxUnusedBlocksScanned { get; set; } = 5000;
}

/// <summary>Bound from <c>CodeArt:EditorPowertools:Graph</c>.</summary>
public class EditorPowertoolsGraphOptions
{
    public GraphFeatureToggles Features { get; set; } = new();
    public GraphSearchOptions Search { get; set; } = new();
    public PowerSliceOptions PowerSlice { get; set; } = new();
}
```

- [ ] **Step 3: Permissions + access checker** — `Permissions/EditorPowertoolsGraphPermissions.cs` mirrors `EditorPowertoolsFormsPermissions` with `PowerSearch`, `PowerSlice`, `SearchProviders` (`new("EditorPowertools", "<Name>")`). `Permissions/GraphFeatureAccessChecker.cs` mirrors `FormsFeatureAccessChecker` exactly (reads `GraphFeatureToggles`, delegates `HasPermission` to core `FeatureAccessChecker`) **plus** a policy-aware overload used by CMS-UI surfaces (search providers, slices) that are not behind an `[Authorize]` controller:

```csharp
/// <summary>
/// For surfaces invoked by the CMS UI rather than our controllers (search providers, slices):
/// applies the same "codeart:editorpowertools" policy the controllers carry, then the toggle and
/// permission. Keeps these surfaces on exactly the same audience as every other tool.
/// </summary>
public bool HasUiAccess(HttpContext? context, string featureName, PermissionType permissionType)
{
    if (context?.User?.Identity?.IsAuthenticated != true) return false;
    var auth = context.RequestServices.GetService<IAuthorizationService>();
    if (auth is null) return false;
    var policy = auth.AuthorizeAsync(context.User, "codeart:editorpowertools").GetAwaiter().GetResult();
    return policy.Succeeded && HasAccess(context, featureName, permissionType);
}
```

- [ ] **Step 4: Write failing tests** — `Permissions/GraphFeatureAccessCheckerTests.cs`:

```csharp
public class GraphFeatureAccessCheckerTests
{
    private static GraphFeatureAccessChecker Create(Action<GraphFeatureToggles>? toggles = null)
    {
        var graph = new EditorPowertoolsGraphOptions();
        toggles?.Invoke(graph.Features);
        var core = new FeatureAccessChecker(
            Options.Create(new EditorPowertoolsOptions { CheckPermissionForEachFeature = false }),
            permissionService: null!);
        return new GraphFeatureAccessChecker(Options.Create(graph), core);
    }

    [Theory]
    [InlineData(nameof(GraphFeatureToggles.PowerSearch))]
    [InlineData(nameof(GraphFeatureToggles.PowerSlice))]
    [InlineData(nameof(GraphFeatureToggles.SearchProviders))]
    public void Features_default_to_enabled(string feature) =>
        Create().IsFeatureEnabled(feature).Should().BeTrue();

    [Fact]
    public void Disabled_toggle_denies_access()
    {
        var checker = Create(t => t.PowerSearch = false);
        checker.HasAccess(new DefaultHttpContext(), nameof(GraphFeatureToggles.PowerSearch),
            EditorPowertoolsGraphPermissions.PowerSearch).Should().BeFalse();
    }

    [Fact]
    public void Unknown_feature_fails_closed() => Create().IsFeatureEnabled("Nope").Should().BeFalse();

    [Fact]
    public void Ui_access_denies_anonymous() =>
        Create().HasUiAccess(new DefaultHttpContext(), nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeFalse();

    [Fact]
    public void Ui_access_requires_policy()
    {
        var auth = new Mock<IAuthorizationService>();
        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "codeart:editorpowertools"))
            .ReturnsAsync(AuthorizationResult.Failed());
        var ctx = AuthenticatedContext(auth.Object);
        Create().HasUiAccess(ctx, nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeFalse();

        auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, "codeart:editorpowertools"))
            .ReturnsAsync(AuthorizationResult.Success());
        Create().HasUiAccess(ctx, nameof(GraphFeatureToggles.SearchProviders),
            EditorPowertoolsGraphPermissions.SearchProviders).Should().BeTrue();
    }

    private static HttpContext AuthenticatedContext(IAuthorizationService auth)
    {
        var services = new ServiceCollection().AddSingleton(auth).BuildServiceProvider();
        return new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "ed") }, "test"))
        };
    }
}
```

  Note: `IAuthorizationService.AuthorizeAsync(user, resource, policyName)` is the interface method; the string-only overload is an extension that calls it with `resource: null`, which is why the mock matches `null`.

- [ ] **Step 5: Test project** — copy `src/EditorPowertools.Tests.Forms/EditorPowertools.Tests.Forms.csproj`, rename to `EditorPowertools.Tests.Graph`, reference `..\EditorPowertools.Graph\EditorPowertools.Graph.csproj`. `Helpers/GraphTestSetup.cs` is a copy of `FormsTestSetup` (namespace `UmageAI.Optimizely.EditorPowerTools.Tests.Graph.Helpers`).

- [ ] **Step 6: Menu provider** — copy `EditorPowertoolsFormsMenuProvider`, class `EditorPowertoolsGraphMenuProvider`, one item:

```csharp
yield return new UrlMenuItem(
    L("/editorpowertools/menu/powersearch", "PowerSearch"),
    BaseMenuPath + "/powersearch",
    GetResourcePath("EditorPowertoolsGraph/PowerSearch"))
{
    SortIndex = 370,
    IsAvailable = ctx => IsFeatureEnabled(ctx, nameof(GraphFeatureToggles.PowerSearch))
};
```

- [ ] **Step 7: Registration** — `Infrastructure/ServiceCollectionExtensions.cs` mirrors Forms: `AddEditorPowertoolsGraph()` / `AddEditorPowertoolsGraph(Action<EditorPowertoolsGraphOptions>)` binding `CodeArt:EditorPowertools:Graph`; registers `GraphFeatureAccessChecker` (singleton), `ProtectedModuleOptions` entry `EditorPowertoolsGraph`, and one `IPowertoolDescriptor` (`Id = "PowerSearch"`, title/description `/editorpowertools/tools/powersearch/title|description`, `Url = GraphUrl("EditorPowertoolsGraph/PowerSearch")`, `IconSvgPath = GraphSvgIcons.PowerSearch`, `Group = PowertoolGroups.Search`, `SortIndex = 370`, availability via toggle). Leave a clearly separated block where Tasks 3–11 add their registrations. `ApplicationBuilderExtensions` copies Forms (`UseEditorPowertoolsGraph` no-op, `MapEditorPowertoolsGraph` route name `EditorPowertoolsGraph`). `GraphSvgIcons.PowerSearch` is a 24×24 magnifier-with-funnel path string (same format as `FormsSvgIcons`). In core `PowertoolGroups` add `public const string Search = "Search";` and add its Overview heading key `/editorpowertools/overview/groups/search` to core `en.xml` and the 10 other core lang files ("Search" + translations) — find how existing group headings are rendered with `grep -rn "PowertoolGroups.Forms" src/EditorPowertools` and follow it.

- [ ] **Step 8: module.config** — copy Forms' and change assembly to `EditorPowertools.Graph`, CSS resource `editorpowertools-graph.css` at `ClientResources/css/editorpowertools-graph.css`, dojo path `editorpowertools-graph` → `ClientResources/js`, and add `<requiredResources><add name="editorpowertools-graph.css" /></requiredResources>` inside `clientModule` (the slice widget runs in the edit shell, so its CSS must load there).

- [ ] **Step 9: Registration test** — `Infrastructure/ServiceRegistrationTests.cs`:

```csharp
[Fact]
public void Binds_options_from_configuration_section()
{
    var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["CodeArt:EditorPowertools:Graph:Features:PowerSlice"] = "false",
        ["CodeArt:EditorPowertools:Graph:Search:OverfetchFactor"] = "3"
    }).Build();
    var services = new ServiceCollection().AddSingleton<IConfiguration>(config);
    services.AddEditorPowertoolsGraph();
    var options = services.BuildServiceProvider().GetRequiredService<IOptions<EditorPowertoolsGraphOptions>>().Value;
    options.Features.PowerSlice.Should().BeFalse();
    options.Search.OverfetchFactor.Should().Be(3);
}

[Fact]
public void Registers_protected_module()
{
    var services = new ServiceCollection().AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
    services.AddEditorPowertoolsGraph();
    services.BuildServiceProvider().GetRequiredService<IOptions<ProtectedModuleOptions>>().Value
        .Items.Should().Contain(m => m.Name == "EditorPowertoolsGraph");
}
```

- [ ] **Step 10: Solution + gitignore** — add both projects to `EditorPowertools.slnx` (after the Forms entries). Append to `.gitignore` the same CMS-artifact ignores Forms has, for `/src/EditorPowertools.Graph/modules/_protected/{CMS,EPiServer.*,Profile,Settings,Shell,TinyMce,OpalChat}/` and `/src/EditorPowertools.Tests.Graph/modules/`. Also add `/src/EditorPowertools.Forms/modules/_protected/OpalChat/`, `/src/EditorPowertools/modules/_protected/OpalChat/`, `/src/EditorPowertools.Tests.Forms/modules/` (currently untracked noise).

- [ ] **Step 11: Build + test**

Run: `dotnet build EditorPowertools.slnx` then `dotnet test src/EditorPowertools.Tests.Graph`
Expected: build succeeds for net8.0 and net10.0; all tests PASS on both TFMs.

- [ ] **Step 12: Commit** — `feat(graph): scaffold Graph add-on project, options, permissions and menu`

---

### Task 2: Core `IUiStringsContributor` (add-on JS strings get translated)

**Files:**
- Create: `src/EditorPowertools/Localization/IUiStringsContributor.cs`
- Modify: `src/EditorPowertools/Localization/UiStringsProvider.cs`
- Create: `src/EditorPowertools.Forms/Localization/FormsUiStringsContributor.cs`; Modify Forms `ServiceCollectionExtensions` (register it)
- Test: `src/EditorPowertools.Tests/Localization/UiStringsProviderTests.cs`, `src/EditorPowertools.Tests.Forms/Localization/FormsUiStringsContributorTests.cs`

**Interfaces:**
- Produces: `IUiStringsContributor { string Section { get; } IEnumerable<string> Keys { get; } }`; `UiStringsProvider.GetAll()` returns `IDictionary<string, object>` whose keys are the existing top-level sections plus each contributor's `Section`, value `IDictionary<string,string>` of key → `GetString("/editorpowertools/ui/{Section}/{key}")`.

- [ ] **Step 1: Failing test** (`UiStringsProviderTests`):

```csharp
public class UiStringsProviderTests
{
    private sealed class TestContributor : IUiStringsContributor
    {
        public string Section => "graphtest";
        public IEnumerable<string> Keys => new[] { "hello", "nested/key" };
    }

    [Fact]
    public void Merges_contributor_sections_with_core_sections()
    {
        var loc = new MemoryLocalizationService();
        loc.AddString(CultureInfo.InvariantCulture, "/editorpowertools/ui/graphtest/hello", "Hi");
        var provider = new UiStringsProvider(loc, new[] { new TestContributor() });

        var all = provider.GetAll();

        all.Should().ContainKey("shared");
        var section = all["graphtest"].Should().BeAssignableTo<IDictionary<string, string>>().Subject;
        section["hello"].Should().Be("Hi");
        section.Should().ContainKey("nested_key");
    }

    [Fact]
    public void Contributor_cannot_overwrite_core_section()
    {
        var hijack = new Mock<IUiStringsContributor>();
        hijack.SetupGet(c => c.Section).Returns("shared");
        hijack.SetupGet(c => c.Keys).Returns(new[] { "loading" });
        var all = new UiStringsProvider(new MemoryLocalizationService(), new[] { hijack.Object }).GetAll();
        all["shared"].Should().NotBeAssignableTo<IDictionary<string, string>>();
    }
}
```

  (Check `MemoryLocalizationService.AddString` signature in the CMS assembly; if the culture-less overload exists use it. JS keys: `/` in a key becomes `_` so `EPT.s('graphtest.nested_key')` works.)

- [ ] **Step 2: Run** `dotnet test src/EditorPowertools.Tests --filter UiStringsProviderTests` → FAIL (constructor/type missing).

- [ ] **Step 3: Implement.** `IUiStringsContributor.cs`:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Localization;

/// <summary>
/// Lets add-on packages publish their JavaScript UI strings through <c>window.EPT_STRINGS</c>.
/// Each key resolves from <c>/editorpowertools/ui/{Section}/{key}</c>; JS reads it as
/// <c>EPT.s('{Section}.{key with / replaced by _}')</c>.
/// </summary>
public interface IUiStringsContributor
{
    string Section { get; }
    IEnumerable<string> Keys { get; }
}
```

  `UiStringsProvider`: change to `public class UiStringsProvider(LocalizationService loc, IEnumerable<IUiStringsContributor> contributors)`. Keep the existing anonymous object as `private object Core() => new { ...unchanged... };`. New `GetAll()`:

```csharp
public IDictionary<string, object> GetAll()
{
    var result = new Dictionary<string, object>(StringComparer.Ordinal);
    foreach (var prop in Core().GetType().GetProperties())
        result[prop.Name] = prop.GetValue(Core())!;   // evaluate Core() once into a local; shown inline for brevity

    foreach (var c in contributors)
    {
        if (result.ContainsKey(c.Section)) continue; // core sections win
        result[c.Section] = c.Keys.Distinct(StringComparer.Ordinal).ToDictionary(
            k => k.Replace('/', '_'),
            k => loc.GetString($"/editorpowertools/ui/{c.Section}/{k}"),
            StringComparer.Ordinal);
    }
    return result;
}
```

  (Evaluate `Core()` once into a local variable in the real code.) Both layouts serialize `UiStrings.GetAll()` with `JsonSerializer` — a dictionary serializes to the same JSON shape, so no layout change is needed; `JsonSerializer` default naming keeps keys verbatim, matching the current anonymous-object output. Verify by diffing the JSON of `/EditorPowertools/WidgetStrings` before/after in the sample site (Task 15).

- [ ] **Step 4: Forms contributor** — `FormsUiStringsContributor : IUiStringsContributor` with `Section => "formsoverview"` and `Keys` = every element name under `<ui><formsoverview>` in `src/EditorPowertools.Forms/lang/en.xml`; add a second contributor class `SubmissionsTimelineUiStringsContributor` for `<ui><submissionstimeline>` if that section exists (check en.xml). Register with `services.AddSingleton<IUiStringsContributor, …>()` in `AddEditorPowertoolsForms`. Test (`FormsUiStringsContributorTests`): load `en.xml` from the Forms assembly's embedded resources, collect the element names under the section, and assert `contributor.Keys` equals that set — this keeps the contributor from drifting from the lang file.

- [ ] **Step 5: Run** `dotnet test` (repo root) → all PASS on both TFMs.

- [ ] **Step 6: Commit** — `feat(localization): let add-ons contribute translated JS strings; wire Forms`

---

### Task 3: Graph query execution layer (platform clients, access headers)

**Files:**
- Create: `Abstractions/IGraphQueryExecutor.cs`, `Search/GraphAccessHeaders.cs`, `Search/GraphQueryException.cs`, `Cms12/Cms12GraphQueryExecutor.cs`, `Cms13/Cms13GraphQueryExecutor.cs`
- Test: `Search/GraphAccessHeadersTests.cs`, `Search/GraphResponseReaderTests.cs`

**Interfaces:**
- Produces:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public sealed record GraphRequest(string Query, IReadOnlyDictionary<string, object?> Variables);

public interface IGraphQueryExecutor
{
    /// <summary>True when the platform Graph client is registered in DI.</summary>
    bool IsClientAvailable { get; }

    /// <summary>Runs the request as the current editor and returns a clone of the <c>data</c> element.</summary>
    /// <exception cref="GraphQueryException">Transport failure or GraphQL <c>errors</c>.</exception>
    Task<JsonElement> ExecuteAsync(GraphRequest request, CancellationToken cancellationToken);
}
```

  `GraphAccessHeaders.Build(ClaimsPrincipal? user) : Dictionary<string,string>` (keys `cg-username`, `cg-roles`); `GraphResponseReader.ReadData(string json) : JsonElement` (static, internal); `GraphQueryException : Exception` (message is for logs only, never returned to clients).

- [ ] **Step 1: Failing tests** — `GraphAccessHeadersTests`:

```csharp
[Fact]
public void Anonymous_user_sends_empty_identity_so_graph_returns_nothing()
{
    var headers = GraphAccessHeaders.Build(null);
    headers["cg-username"].Should().BeEmpty();
    headers["cg-roles"].Should().BeEmpty();
}

[Fact]
public void Sends_username_and_distinct_roles_from_role_and_declared_role_claim_types()
{
    var identity = new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Name, "anna"),
        new Claim("role", "WebEditors"),
        new Claim(ClaimTypes.Role, "WebAdmins"),
        new Claim(ClaimTypes.Role, "WebEditors"),
        new Claim(ClaimTypes.Role, " "),
    }, "test", ClaimTypes.Name, ClaimTypes.Role);

    var headers = GraphAccessHeaders.Build(new ClaimsPrincipal(identity));

    headers["cg-username"].Should().Be("anna");
    headers["cg-roles"].Split(',').Should().BeEquivalentTo("WebEditors", "WebAdmins");
}
```

  `GraphResponseReaderTests`:

```csharp
[Fact]
public void Returns_cloned_data_element() =>
    GraphResponseReader.ReadData("""{"data":{"Content":{"total":3}}}""")
        .GetProperty("Content").GetProperty("total").GetInt32().Should().Be(3);

[Fact]
public void Throws_on_graphql_errors_without_leaking_to_callers()
{
    var act = () => GraphResponseReader.ReadData("""{"errors":[{"message":"Field 'X' not found"}]}""");
    act.Should().Throw<GraphQueryException>().Which.Message.Should().Contain("Field 'X' not found");
}

[Fact]
public void Throws_on_invalid_json() =>
    ((Action)(() => GraphResponseReader.ReadData("<html>"))).Should().Throw<GraphQueryException>();

[Fact]
public void Missing_data_returns_undefined_element() =>
    GraphResponseReader.ReadData("{}").ValueKind.Should().Be(JsonValueKind.Undefined);
```

- [ ] **Step 2: Run** → FAIL (types missing).

- [ ] **Step 3: Implement** `GraphAccessHeaders` (port of the reference `EditorGraphExecutor.BuildAccessHeaders/ResolveRoles`: role claim types = `"role"` ∪ each identity's `RoleClaimType`; distinct ordinal; skip whitespace). `GraphResponseReader.ReadData`: parse with `JsonDocument`, wrap `JsonException` in `GraphQueryException("Graph returned a non-JSON response.")`, throw `GraphQueryException($"Graph query failed: {first error message}")` if `errors` is a non-empty array, return `data.Clone()` or `default`.

  `Cms12/Cms12GraphQueryExecutor.cs` (net8.0 only):

```csharp
using Optimizely.ContentGraph.Core;

internal sealed class Cms12GraphQueryExecutor(IServiceProvider services, IHttpContextAccessor http) : IGraphQueryExecutor
{
    // Resolved lazily: IClient exists only if the host called AddContentGraph().
    private IClient? Client => services.GetService<IClient>();

    public bool IsClientAvailable => Client is not null;

    public async Task<JsonElement> ExecuteAsync(GraphRequest request, CancellationToken ct)
    {
        var client = Client ?? throw new GraphQueryException("Optimizely Graph client is not registered.");
        string json;
        try
        {
            json = await client.QueryAsync(request.Query, request.Variables,
                GraphAccessHeaders.Build(http.HttpContext?.User), queryStringParams: null!, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new GraphQueryException("Graph request failed.", ex);
        }
        return GraphResponseReader.ReadData(json);
    }
}
```

  Check the `QueryAsync` parameter names/nullability on the real `IClient` (it is `QueryAsync(string, object, Dictionary<string,string>, Dictionary<string,string>, CancellationToken)` returning `Task<string>` per the package XML docs) and pass `new Dictionary<string,string>()` if null is rejected. `Cms13/Cms13GraphQueryExecutor.cs` is identical but uses `Optimizely.Graph.Core.Client.IGraphClient.QueryAsync(string query, object variables, IDictionary<string,string> customHeaders, CancellationToken)`.

  Register in `AddEditorPowertoolsGraph` with `#if OPTIMIZELY_CMS13 services.AddScoped<IGraphQueryExecutor, Cms13GraphQueryExecutor>(); #else … Cms12 … #endif` and `services.AddHttpContextAccessor();`.

- [ ] **Step 4: Run** `dotnet test src/EditorPowertools.Tests.Graph` → PASS (both TFMs).

- [ ] **Step 5: Commit** — `feat(graph): execute Graph queries as the current editor via the platform client`

---

### Task 4: Availability + CMS Doctor health check

**Files:**
- Create: `Search/GraphAvailability.cs`, `Search/GraphHealthCheck.cs`
- Test: `Search/GraphAvailabilityTests.cs`, `Search/GraphHealthCheckTests.cs`

**Interfaces:**
- Consumes: `IGraphQueryExecutor.IsClientAvailable` (Task 3), `IGraphQueryDialect.IsVerified` (Task 5). **Execute Task 5 before Task 4.**
- Produces:

```csharp
public enum GraphAvailabilityState { Available, ClientNotRegistered, NotConfigured, DialectNotVerified }

public interface IGraphAvailability
{
    GraphAvailabilityState State { get; }
    bool IsAvailable => State == GraphAvailabilityState.Available;
}
```

  `GraphAvailability(IGraphQueryExecutor executor, IGraphQueryDialect dialect, IConfiguration configuration)`: `ClientNotRegistered` if `!executor.IsClientAvailable`; `NotConfigured` if `configuration["Optimizely:ContentGraph:GatewayAddress"]`, `…:AppKey` or `…:Secret` is empty (the Secret is required: without it the platform client falls back to public single-key access, which cannot see restricted content and is not what editors expect — fail loudly instead of returning a silently partial index); `DialectNotVerified` if `!dialect.IsVerified`; otherwise `Available`. The package only checks presence — never reads values into logs or responses.

  `GraphHealthCheck : DoctorCheckBase` — Name `/editorpowertools/cmsdoctor/checks/graphhealthcheck/name` ("Optimizely Graph for editor search"), Group `/editorpowertools/cmsdoctor/groups/graph` ("Graph"), Tags `Graph`, `Search`, `Configuration`. `PerformCheck()` maps: `Available` → then run a smoke query `query { Content(limit: 1) { total } }` via the dialect-independent executor (catch `GraphQueryException` → `Fault` with localized "Graph did not respond" and **no** exception text in `Details`; log the exception) → `Ok`; `ClientNotRegistered` → `Warning` "Optimizely Graph is not installed (call AddContentGraph())"; `NotConfigured` → `Warning` "Graph gateway, app key or secret is missing"; `DialectNotVerified` → `Warning` "Graph search is not yet verified for this CMS version". Register as `services.AddTransient<IDoctorCheck, GraphHealthCheck>()` and `services.AddScoped<IGraphAvailability, GraphAvailability>()`.

- [ ] **Step 1: Failing tests** — Theory over the four states with a mocked executor/dialect and in-memory configuration; health-check tests assert `HealthStatus` per state and that a throwing executor yields `Fault` whose `Details` does not contain the exception message (`"secret-marker"`). Use `GraphTestSetup.EnsureInitialized()` (DoctorCheckBase needs the ServiceLocator localization).
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement.** **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `feat(graph): Graph availability state and CMS Doctor health check`

---

### Task 5: Query model, locale mapper, CMS 12 dialect (ported, proven query shapes)

**Files:**
- Create: `Abstractions/GraphSearchModels.cs`, `Abstractions/IGraphQueryDialect.cs`, `Search/GraphLocaleMapper.cs`, `Cms12/Cms12GraphQueryDialect.cs`, `Cms13/Cms13GraphQueryDialect.cs` (unverified placeholder that reports `IsVerified = false`)
- Test: `Search/GraphLocaleMapperTests.cs`, `Cms12/Cms12GraphQueryDialectTests.cs` (net8.0 only — wrap the file in `#if OPTIMIZELY_CMS12`)

**Interfaces:**
- Produces:

```csharp
namespace UmageAI.Optimizely.EditorPowerTools.Graph.Abstractions;

public sealed record GraphSearchScope(
    IReadOnlyList<string> ContentTypes,   // validated type names, e.g. "PageData", "BlockData", "MediaData"
    IReadOnlyList<Guid> AncestorGuids,    // search roots
    bool FilterByLocale);

public sealed record GraphSearchFilters(
    IReadOnlyList<string> ContentTypes,   // facet selections (validated)
    IReadOnlyList<string> Languages,      // CMS culture names, e.g. "fr-CH"
    IReadOnlyList<Guid> Sections)         // ancestor GUIDs
{
    public static GraphSearchFilters None { get; } = new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Guid>());
}

public sealed record GraphSearchRequest(
    string Phrase, GraphSearchScope Scope, string? Language, GraphSearchFilters Filters,
    int Limit, int Skip, bool IncludeFacets);

public sealed record GraphSearchHit(Guid ContentGuid, string Name, string Language);
public sealed record GraphFacetValue(string Name, int Count);
public sealed record GraphFacets(
    IReadOnlyList<GraphFacetValue> ContentTypes, IReadOnlyList<GraphFacetValue> Languages, IReadOnlyList<GraphFacetValue> Sections)
{
    public static GraphFacets Empty { get; } = new(Array.Empty<GraphFacetValue>(), Array.Empty<GraphFacetValue>(), Array.Empty<GraphFacetValue>());
}

/// <param name="Total">-1 when Graph did not report a total (unknown, not zero).</param>
public sealed record GraphSearchResult(IReadOnlyList<GraphSearchHit> Hits, int Total, GraphFacets Facets)
{
    public static GraphSearchResult Empty { get; } = new(Array.Empty<GraphSearchHit>(), 0, GraphFacets.Empty);
}

public interface IGraphQueryDialect
{
    /// <summary>False until the dialect's field names have been verified against a real schema.</summary>
    bool IsVerified { get; }
    GraphRequest BuildSearch(GraphSearchRequest request);
    GraphSearchResult ParseSearch(JsonElement data, bool includeFacets);
}
```

  `GraphLocaleMapper.ToGraphLocale(string? cultureName) : string?` — null/empty/invariant → null; `"xx"` → `"xx"`; `"xx-YY"` → `"xx_YY"` (case preserved as `lower_UPPER`); scripts like `"zh-Hans"` → `"zh_Hans"`; never truncates.

- [ ] **Step 1: Failing tests** — `GraphLocaleMapperTests`:

```csharp
[Theory]
[InlineData("en", "en")]
[InlineData("EN", "en")]
[InlineData("en-GB", "en_GB")]
[InlineData("fr-ch", "fr_CH")]
[InlineData("zh-CN", "zh_CN")]
[InlineData("zh-Hans", "zh_Hans")]
[InlineData("", null)]
[InlineData(null, null)]
public void Maps_culture_names_without_truncating_regions(string? input, string? expected) =>
    GraphLocaleMapper.ToGraphLocale(input).Should().Be(expected);
```

  `Cms12GraphQueryDialectTests` (port of the reference repo's pinned-query assertions):

```csharp
private static readonly Cms12GraphQueryDialect Dialect = new(Options.Create(new EditorPowertoolsGraphOptions()));
private static GraphSearchRequest Req(string phrase = "bim", GraphSearchFilters? f = null, bool facets = false) =>
    new(phrase, new GraphSearchScope(new[] { "PageData" }, Array.Empty<Guid>(), true), "en-GB",
        f ?? GraphSearchFilters.None, Limit: 10, Skip: 0, IncludeFacets: facets);

[Fact]
public void Phrase_travels_only_as_variable()
{
    var r = Dialect.BuildSearch(Req("\"} ) { __schema { types { name } } } #"));
    r.Query.Should().NotContain("__schema");
    r.Variables["phrase"].Should().Be("\"} ) { __schema { types { name } } } #");
}

[Fact]
public void Uses_field_specific_operators()
{
    var q = Dialect.BuildSearch(Req()).Query;
    q.Should().Contain("Name: { contains: $phrase, boost: 10 }");
    q.Should().Contain("Name: { contains: $phrase, fuzzy: true, boost: 3 }");
    q.Should().Contain("RouteSegment: { like: $pattern, boost: 5 }");
    q.Should().Contain("_fulltext: { match: $phrase }");
    q.Should().NotContain("RouteSegment: { contains");
    q.Should().Contain("orderBy: { _ranking: RELEVANCE }");
}

[Theory]
[InlineData("50%", "%50\\%%")]
[InlineData("a_b", "%a\\_b%")]
[InlineData("c:\\x", "%c:\\\\x%")]
public void Escapes_like_wildcards(string phrase, string pattern) =>
    Dialect.BuildSearch(Req(phrase)).Variables["pattern"].Should().Be(pattern);

[Fact]
public void Maps_locale_variable() =>
    Dialect.BuildSearch(Req()).Variables["locale"].Should().BeEquivalentTo(new[] { "en_GB" });

[Fact]
public void Without_facets_query_has_no_facets_or_total()
{
    var q = Dialect.BuildSearch(Req()).Query;
    q.Should().NotContain("facets").And.NotContain("total");
}

[Fact]
public void Selected_types_and_sections_are_variables_and_declared_only_when_used()
{
    var none = Dialect.BuildSearch(Req(facets: true)).Query;
    none.Should().NotContain("$selectedTypes").And.NotContain("$selectedSections").And.NotContain("TypeFacet");

    var g = Guid.NewGuid();
    var r = Dialect.BuildSearch(Req(facets: true, f: new GraphSearchFilters(new[] { "ArticlePage" }, Array.Empty<string>(), new[] { g })));
    r.Query.Should().Contain("$selectedTypes: [String!]").And.Contain("TypeFacet:").And.Contain("SectionFacet:");
    r.Query.Should().NotContain("ArticlePage");
    r.Variables["selectedTypes"].Should().BeEquivalentTo(new[] { "ArticlePage" });
    r.Variables["selectedSections"].Should().BeEquivalentTo(new[] { g.ToString("D") });
}

[Fact]
public void Explicit_language_selection_overrides_scope_locale() =>
    Dialect.BuildSearch(Req(f: new GraphSearchFilters(Array.Empty<string>(), new[] { "de", "fr-CH" }, Array.Empty<Guid>())))
        .Variables["locale"].Should().BeEquivalentTo(new[] { "de", "fr_CH" });

[Fact]
public void Scope_without_locale_filter_sends_null_locale() =>
    Dialect.BuildSearch(Req() with { Scope = new GraphSearchScope(new[] { "MediaData" }, Array.Empty<Guid>(), false) })
        .Variables["locale"].Should().BeNull();

[Fact]
public void Parses_hits_total_and_facets_dropping_zero_counts_and_preferring_aliases()
{
    var data = JsonDocument.Parse("""
    {"Content":{"total":42,"items":[
        {"Name":"A","ContentLink":{"GuidValue":"6a4b3f3e-7f0e-4a7c-9d0b-0a2c3e4f5a6b"},"Language":{"Name":"en"}},
        {"Name":"broken","ContentLink":{"GuidValue":"not-a-guid"}}],
     "facets":{"ContentType":[{"name":"PageData","count":42},{"name":"Dead","count":0}],
               "Language":{"Name":[{"name":"en","count":40}]},
               "Ancestors":[{"name":"11111111-1111-1111-1111-111111111111","count":5}]}},
     "TypeFacet":{"facets":{"ContentType":[{"name":"ArticlePage","count":7}]}}}
    """).RootElement;

    var result = Dialect.ParseSearch(data, includeFacets: true);

    result.Total.Should().Be(42);
    result.Hits.Should().ContainSingle().Which.Name.Should().Be("A");
    result.Facets.ContentTypes.Should().ContainSingle().Which.Should().Be(new GraphFacetValue("ArticlePage", 7));
    result.Facets.Languages.Should().ContainSingle();
    result.Facets.Sections.Should().ContainSingle();
}

[Fact]
public void Missing_total_is_unknown_not_zero() =>
    Dialect.ParseSearch(JsonDocument.Parse("""{"Content":{"items":[]}}""").RootElement, true).Total.Should().Be(-1);
```

- [ ] **Step 2: Run** `dotnet test src/EditorPowertools.Tests.Graph --framework net8.0` → FAIL.

- [ ] **Step 3: Implement `Cms12GraphQueryDialect`** by porting `C:\Github\nti-website-upgrade-release\src\NTI.Backend\Features\GraphSearch\GraphEditorSearchQuery.cs` (read-only reference — copy the `BuildQuery`, `BuildWhere`, `BuildRouteSegmentPattern`, `ReadHits`, `ReadTotal`, `ReadFacets`, `ReadAliasFacet`, `ReadFacet` logic) with these changes:
  - `IsVerified => true`; constructor takes `IOptions<EditorPowertoolsGraphOptions>` and uses `Search.ContentTypeFacetLimit` / `SectionFacetLimit` instead of the constants.
  - One `BuildSearch(GraphSearchRequest)` entry point: `IncludeFacets == false` emits the plain query (no `total`, no `facets`, no `$skip`); `true` emits the faceted query with `skip`, `total`, facets and the aliases. Keep `$skip` declared only in the faceted query (an undeclared/unused variable is a GraphQL error).
  - Locale via `GraphLocaleMapper`; `Filters.Languages` non-empty → mapped distinct list; else `Scope.FilterByLocale` → `[mapped Language]` (or null if the mapper returns null); else null.
  - `selectedSections` variable values are `Guid.ToString("D")`; scope ancestors are interpolated as `{g:D}` (Guid formatting cannot inject).
  - `ReadTotal` returns **-1** when `total` is absent (the reference returned 0; spec requires unknown).
  - Scope content-type names are interpolated; this is safe only because callers pass validated names — add `Debug.Assert`-free runtime guard: throw `ArgumentException` if any scope type name fails `^[A-Za-z_][A-Za-z0-9_]*$`. Add a test: `BuildSearch` with scope type `"Page\") {"` throws.
  - Keep the reference's explanatory comments where they carry a non-obvious reason (operator vocabulary, facet-narrowing, variable-only rule); trim narrative.

  `Cms13/Cms13GraphQueryDialect.cs`: `IsVerified => false`; `BuildSearch`/`ParseSearch` throw `NotSupportedException("CMS 13 Graph dialect not verified yet")`. Nothing calls them while `IGraphAvailability` reports `DialectNotVerified`. (Replaced in Task 16.) Register the dialect with the same `#if OPTIMIZELY_CMS13` switch as the executor (singleton).

- [ ] **Step 4: Run** → PASS (net8.0 dialect tests; locale tests on both TFMs).
- [ ] **Step 5: Commit** — `feat(graph): search query model, locale mapping and CMS 12 Graph dialect`

---

### Task 6: `GraphSearchService` (single entry point for all search surfaces)

**Files:**
- Create: `Abstractions/IGraphSearchService.cs` (interface in the same file as nothing else), `Search/GraphSearchService.cs`
- Test: `Search/GraphSearchServiceTests.cs`

**Interfaces:**
- Consumes: `IGraphQueryDialect`, `IGraphQueryExecutor`, `IGraphAvailability`, `IContentTypeRepository`.
- Produces:

```csharp
public interface IGraphSearchService
{
    /// <summary>Returns <see cref="GraphSearchResult.Empty"/> when unavailable or the phrase is blank.</summary>
    /// <exception cref="GraphQueryException">Graph failed; callers decide how to degrade.</exception>
    Task<GraphSearchResult> SearchAsync(GraphSearchRequest request, CancellationToken ct);

    /// <summary>Keeps only names of registered content types (plus the base types PageData/BlockData/MediaData/ContentFolder), preserving order, distinct.</summary>
    IReadOnlyList<string> ValidateContentTypes(IEnumerable<string>? names);
}
```

- [ ] **Step 1: Failing tests**: blank phrase → Empty without calling executor; unavailable → Empty without calling executor; `ValidateContentTypes(["ArticlePage","Evil\") {","PageData"])` → `["ArticlePage","PageData"]` given a repository mock that lists `ArticlePage`; `SearchAsync` clamps `Limit` to 1..250 and `Skip` ≥ 0 before building; filters' content types are re-validated inside `SearchAsync` (defence in depth: a caller passing an unvalidated name gets it dropped, not interpolated).
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** (scoped). **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `feat(graph): Graph search service with content-type allow-listing`

---

### Task 7: Search roots, draft search and CMS result factory

**Files:**
- Create: `Providers/SearchRoots.cs`, `Providers/DraftSearch.cs`, `Providers/SearchResultFactory.cs`
- Test: `Providers/SearchRootsTests.cs`, `Providers/DraftSearchTests.cs`, `Providers/SearchResultFactoryTests.cs`

**Interfaces:**
- Produces:

```csharp
internal static class SearchRoots
{
    /// <summary>Parses shell search roots into version-less references; ignores unparseable/empty.</summary>
    public static IReadOnlySet<ContentReference> Parse(IEnumerable<string>? roots);
    /// <summary>True when no roots, or content is a root, or any ancestor is a root.</summary>
    public static bool Contains(IContent content, IReadOnlySet<ContentReference> roots, IContentLoader loader);
    /// <summary>GUIDs of loadable roots (for the Graph Ancestors constraint).</summary>
    public static IReadOnlyList<Guid> ToGuids(IReadOnlySet<ContentReference> roots, IContentLoader loader);
}

public class DraftSearch   // registered scoped
{
    public DraftSearch(IContentVersionRepository versions, ILogger<DraftSearch> logger);
    /// <summary>Drafts whose name matches; excludes ids in alreadyFound; never throws.</summary>
    public virtual IReadOnlyList<ContentReference> FindDrafts(string phrase, string? language, int candidateLimit, ISet<int> alreadyFound);
}

public class SearchResultFactory   // registered scoped
{
    public SearchResultFactory(IContentTypeRepository types, UIDescriptorRegistry descriptors, LocalizationService loc);
    public virtual SearchResult Create(IContent content, bool isDraft, string? requestedLanguage, string iconCssClass);
    internal static string BuildTitle(string? name, string? detail);          // HTML-encoded "name  —  detail"
    internal static string AppendLanguage(string editUrl, string? branch);    // inserts ?language= before '#'
}
```

- [ ] **Step 1: Failing tests**
  - `SearchRootsTests`: `Parse(["5", "5_12", "", "garbage", "-1073741823__CatalogContent"])` contains `new ContentReference(5)` once (version stripped) and whatever the catalog ref parses to only if `ContentReference.TryParse` accepts it; `Contains` true with empty set; true when an ancestor (mock `IContentLoader.GetAncestors`) matches; false otherwise.
  - `DraftSearchTests`: verifies the `VersionFilter` passed to `List` has `ProviderName == string.Empty`, `ExcludeDeleted`, statuses `{CheckedOut, CheckedIn, Rejected, DelayedPublish, AwaitingApproval}`, sort Saved desc, `Languages` set for `"da"`; de-duplicates by id and skips `alreadyFound`; an invalid culture `"xx-invalid-!!"` or a repository exception returns empty (no throw).
  - `SearchResultFactoryTests`: `BuildTitle("<b>A</b>", "Page")` == `"&lt;b&gt;A&lt;/b&gt;  —  Page"`; `AppendLanguage("/ui/cms/#context=epi.cms.contentdata:///5", "sv")` == `"/ui/cms/?language=sv#context=epi.cms.contentdata:///5"`; `AppendLanguage("/x", "")` == `"/x"`. For `Create` assert Metadata keys exactly `Id` (= `ContentLink.ToString()`), `ParentId`, `IsOnCurrentHost`, `TypeId`, `TypeIdentifier`, `ThumbnailUrl`, `LanguageBranch`, `IsUntranslatedContent` — PascalCase — using a stub `IContent`+`ILocalizable` page. If `GetTypeIdentifier`/`PageEditing.GetEditUrl` cannot run without a CMS host, extract them behind two `protected virtual` methods (`ResolveTypeIdentifier`, `ResolveEditUrl`) and override in a test subclass.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement** by porting the reference `GraphEditorSearchProviderBase.CreateResult/BuildEditUrl/BuildResultTitle/IsWithinSearchRoots/ParseSearchRoots` and `DraftSearch` (read-only reference files under `C:\Github\nti-website-upgrade-release\src\NTI.Web\Business\ContentGraph\Search\`). Differences: the detail text is localized — draft marker `/editorpowertools/graph/search/unpublisheddraft` ("Unpublished draft"), type = `ContentType.LocalizedName ?? DisplayName ?? Name`; join with `" · "`. `IsUntranslatedContent` = requested language non-empty and ≠ branch. Remove every Commerce/catalog branch (`EditViewPath` etc.).
- [ ] **Step 4: Run** → PASS (both TFMs). **Step 5: Commit** — `feat(graph): search roots, draft search and CMS UI result metadata`

---

### Task 8: CMS UI search providers + content picker provider

**Files:**
- Create: `Providers/GraphSearchProviderBase.cs`, `Providers/GraphPageSearchProvider.cs`, `Providers/GraphBlockSearchProvider.cs`, `Providers/GraphFileSearchProvider.cs`, `Providers/GraphContentPickerSearchProvider.cs`
- Modify: `Infrastructure/ServiceCollectionExtensions.cs`
- Test: `Providers/GraphSearchProviderBaseTests.cs`, `Providers/GraphContentPickerSearchProviderTests.cs`

**Interfaces:**
- Consumes: `IGraphSearchService`, `IGraphAvailability`, `DraftSearch`, `SearchResultFactory`, `SearchRoots`, `GraphFeatureAccessChecker.HasUiAccess`, `IHttpContextAccessor`, `IContentLoader`, `IContentRepository`, `IOptions<EditorPowertoolsGraphOptions>`, `ILogger`.
- Produces: providers with `Area`/`Category`:
  - Pages: `Area = "cms/pages"`, scope types `["PageData"]`, `FilterByLocale = true`, icon `epi-iconObjectPage`, `IsInScope = content is PageData`.
  - Blocks: `Area = "cms/blocks"`, types `["BlockData"]`, `FilterByLocale = true`, icon `epi-iconObjectSharedBlock`, `IsInScope = content is BlockData`; default root = `SiteDefinition.Current.GlobalAssetsRoot` (and `SiteAssetsRoot` if different) when the query has no roots — so inline "for this page" blocks don't flood results.
  - Media: `Area = "cms/files"`, types `["MediaData"]`, `FilterByLocale = false`, icon `epi-iconObjectImage`, `IsInScope = content is MediaData`.
  - `Category` = `L("/editorpowertools/graph/search/category/{pages|blocks|media}")` ("Pages (Graph)" etc.).
  - CMS 13: add `public string Key => "editorpowertools-graph-" + <pages|blocks|files>;` under `#if OPTIMIZELY_CMS13` (Tier 1). Confirm the exact member name/type against `EPiServer.Shell.Search.ISearchProvider` in the CMS 13 `EPiServer.Shell.UI` assembly — compile errors will tell.
  - `GraphContentPickerSearchProvider : IContentSearchProvider` (core seam): Search → `IGraphSearchService` with types `["PageData","BlockData","MediaData","ContentFolder"]`, `FilterByLocale = false`, ancestor = `root`'s GUID if given; maps to `ContentSearchHit { ContentId, Name, TypeName = ContentType.Name, HasChildren = loader.GetChildren<IContent>(ref).Any() }` after `QueryDistinctAccess(AccessLevel.Read)`; any exception → empty.

- [ ] **Step 1: Failing tests** (`GraphSearchProviderBaseTests`, using a `TestProvider : GraphSearchProviderBase` with mocks):
  - blank query → empty, service never called;
  - `HasUiAccess` false (feature toggle off) → empty, service never called;
  - availability not Available → empty;
  - service throws `GraphQueryException` → empty and logger called with `LogLevel.Error`;
  - hit whose GUID no longer loads → skipped; hit the user cannot read (`QueryDistinctAccess` false via an `IContent` mock implementing `ISecurable`) → skipped;
  - roots given: first call carries ancestor GUIDs and `Limit == min(max*OverfetchFactor, MaxOverfetch)`; zero hits → second call without ancestors; out-of-root hits removed;
  - drafts: listed before published, capped at `max(1, MaxResults/3)`, skipped when `Search.IncludeDrafts == false`;
  - results never exceed `query.MaxResults`.
  - Picker tests: maps fields, filters unreadable, swallows exceptions.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.** `GraphSearchProviderBase.Search(Query query)` flow (port of the reference `Search`, `ApplySearchRoots`, `FindDraftResults`): validate → access (`HasUiAccess(http.HttpContext, nameof(GraphFeatureToggles.SearchProviders), EditorPowertoolsGraphPermissions.SearchProviders)`) → availability → build `GraphSearchRequest` (`Language = query.FilterOnCulture ? ContentLanguage.PreferredCulture.Name : null` — check `Query` property names in the Shell assembly; the reference used a `ResolveLanguage(query)` helper, read it in the reference file) → `SearchAsync(...).GetAwaiter().GetResult()` (the interface is synchronous; ASP.NET Core has no sync context) → load each hit with `IContentRepository.TryGet(Guid, …)` (language-agnostic) then the branch via `IContentLoader.TryGet(ref, new LanguageSelector(hit.Language))` when `hit.Language` is set → access + scope + roots checks → drafts → `SearchResultFactory.Create`. Wrap everything after validation in `try/catch (Exception ex)` → log + empty.
  - Registration: `services.TryAddEnumerable(ServiceDescriptor.Scoped<ISearchProvider, GraphPageSearchProvider>())` ×3; `services.AddScoped<IContentSearchProvider, GraphContentPickerSearchProvider>()` (core resolves it optionally). `DisableBuiltInProviders`: register an `IConfigureOptions`/startup hook that, when true, removes the built-in CMS providers for `cms/pages`, `cms/blocks`, `cms/files`. **Verify the mechanism before implementing:** on CMS 13 use `SearchProviderRegistry.Unregister` (type exists in `EPiServer.Shell.UI` 13); on CMS 12 inspect `SearchProvidersManager` / `SearchProviderSetting` in `EPiServer.Shell` for an enable/disable API. If no supported mechanism exists on a version, leave the option documented as "CMS 13 only" (or no-op with a CMS Doctor warning) instead of reflection hacks, and record the finding in `docs/graph.md`.
- [ ] **Step 4: Run** → PASS. **Step 5: Commit** — `feat(graph): Graph-backed CMS search providers and content picker provider`

---

### Task 9: Power Slice server side (content queries + component)

**Files:**
- Create: `Tools/PowerSlice/SliceSortOption.cs`, `Tools/PowerSlice/PowerSliceBase.cs`, `Tools/PowerSlice/SliceVersionHelper.cs`, `Tools/PowerSlice/BuiltInSlices.cs`, `Tools/PowerSlice/PowerSliceComponent.cs`
- Modify: `Infrastructure/ServiceCollectionExtensions.cs`
- Test: `Tools/PowerSlice/PowerSliceBaseTests.cs`, `Tools/PowerSlice/BuiltInSlicesTests.cs`, `Tools/PowerSlice/PowerSliceComponentTests.cs`

**Interfaces:**
- Produces:

```csharp
public sealed record SliceSortOption(string Key, string LabelPath)
{
    public const string Name = "name", Changed = "changed", ChangedAsc = "changed-asc", Published = "published";
    public static SliceSortOption ByName { get; } = new(Name, "/editorpowertools/graph/powerslice/sort/name");
    public static SliceSortOption Newest { get; } = new(Changed, "/editorpowertools/graph/powerslice/sort/changed");
    public static SliceSortOption Oldest { get; } = new(ChangedAsc, "/editorpowertools/graph/powerslice/sort/changedasc");
    public static SliceSortOption RecentlyPublished { get; } = new(Published, "/editorpowertools/graph/powerslice/sort/published");
}

/// <summary>Base for Power Slice lists. Subclass and register as IContentQuery to add a slice.</summary>
public abstract class PowerSliceBase : ContentQueryBase
{
    public const string PlugInArea = "/editorpowertools/graph/powerslice";
    protected PowerSliceBase(IContentRepository repo, IContentQueryHelper helper,
        GraphFeatureAccessChecker access, IHttpContextAccessor http, LocalizationService loc);
    public override IEnumerable<string> PlugInAreas => new[] { PlugInArea };
    /// <summary>Localization path for the display name, e.g. "/editorpowertools/graph/powerslice/slices/mycontent".</summary>
    protected abstract string DisplayNamePath { get; }
    public override string DisplayName => Loc.GetString(DisplayNamePath);
    public virtual IReadOnlyList<SliceSortOption> SortOptions => new[] { SliceSortOption.ByName, SliceSortOption.Newest };
    // Filter(): access gate via HasUiAccess(PowerSlice) → [] if denied; "q" name filter (ordinal-ignore-case);
    //           per-item AccessLevel.Read; then base.Filter.  Sort(): "sortBy" param → ApplySort; else keep order.
}
```

  Built-in slices (`BuiltInSlices.cs`, each `[ServiceConfiguration(typeof(IContentQuery))]`-free — register explicitly in DI instead, see Step 3), names prefixed `ept-slice-`:
  | Class | Name | SortOrder | Source | Default sorts |
  |---|---|---|---|---|
  | `MyContentSlice` | `ept-slice-mycontent` | 10 | version window newest-first, `ChangedBy = current user` (in the store) | Newest, Name, Oldest |
  | `RecentlyChangedSlice` | `ept-slice-recentlychanged` | 20 | newest-first window, `Saved >= now - RecentlyChangedDays` | Newest, Name |
  | `DraftsSlice` | `ept-slice-drafts` | 30 | statuses CheckedOut, CheckedIn, Rejected | Newest, Oldest, Name |
  | `AwaitingApprovalSlice` | `ept-slice-awaitingapproval` | 40 | status AwaitingApproval | Oldest, Newest, Name |
  | `StaleContentSlice` | `ept-slice-stalecontent` | 50 | oldest-first window, `Saved <= now - StaleContentMonths`, published pages only | Oldest, Name |
  | `UnusedBlocksSlice` | `ept-slice-unusedblocks` | 60 | descendants of `SiteDefinition.Current.GlobalAssetsRoot` (breadth-first via `GetChildren`, stop after `MaxUnusedBlocksScanned` items), `BlockData`, no references (`IContentRepository.GetReferencesToContent(link, false)` empty) | Oldest, Name, Newest |

  `SliceVersionHelper` = port of the reference helper: filters pin `ProviderName = string.Empty`, `ExcludeDeleted = true`, `List(filter, 0, MaxVersions, out _)`, de-dup by id, load version-less content, drop `ContentFolder`, root, wastebasket, unloadable.

  `PowerSliceComponent : ComponentDefinitionBase` — `base("editorpowertools-graph/PowerSliceWidget")`, `LanguagePath = "/editorpowertools/components/powerslice"`, `Categories = new[] { "cms" }`, `PlugInAreas = new[] { PlugInArea.AssetsDefaultGroup }`, `SortOrder = 1100`; constructor (parameterless, resolving via `ServiceLocator` like the reference — component definitions are instantiated by the shell) adds `Settings.Add(new Setting("slices", …))` with `{ name, displayName, sortOptions: [{ key, label = Loc.GetString(LabelPath) }] }` for every `PowerSliceBase` in `IEnumerable<IContentQuery>`, ordered by `SortOrder`, then `DisplayName`. Also a `Setting("strings", …)` with the widget's toolbar strings (filter placeholder, reload, empty) so the widget needs no extra fetch. Set `IsAvailableForUserSelection`-style visibility via `AllowedRoles`? — **no**: keep it visible to shell users and let the queries return empty when `HasUiAccess` is false; the widget shows the localized "not available" string when every slice returns nothing because access is denied (Setting `"enabled"` = result of `HasUiAccess` for the current user at construction time; if false the widget renders only the message).

- [ ] **Step 1: Failing tests**: access denied → `Filter` returns empty; `q` filter case-insensitive; unreadable item removed; sort keys map (`name` ascending culture-insensitive, `changed`, `changed-asc`, `published`), unknown key keeps order; `MyContentSlice` passes `ChangedBy = "anna"` in the filter (capture via mock) and returns empty for anonymous; `RecentlyChangedSlice` drops older items (inject `TimeProvider` — use `TimeProvider.System` in DI and `FakeTimeProvider`-style manual subclass in tests; .NET 8 has `TimeProvider`); `UnusedBlocksSlice` stops scanning at the cap and excludes referenced blocks; component settings contain slices ordered by SortOrder with localized sort labels.
- [ ] **Step 2: Run** → FAIL.
- [ ] **Step 3: Implement.** Register each slice with `services.AddTransient<IContentQuery, MyContentSlice>()` etc. and `services.AddSingleton(TimeProvider.System)` via `TryAddSingleton`. Confirm the `ContentQueryBase` constructor/abstract members in the CMS 12 and 13 assemblies (`EPiServer.Cms.Shell.UI.Rest.ContentQuery.ContentQueryBase`): reference implementation overrides `Name`, `DisplayName`, `SortOrder`, `PlugInAreas`, `GetContent(ContentQueryParameters)`, `Filter`, `Sort`. If a member differs on CMS 13, use Tier 1 `#if`.
- [ ] **Step 4: Run** → PASS. **Step 5: Commit** — `feat(graph): Power Slice content queries and assets-pane component`

---

### Task 10: Power Slice dojo widget

**Files:**
- Create: `modules/_protected/EditorPowertoolsGraph/ClientResources/js/PowerSliceWidget.js`, `…/js/templates/PowerSliceWidget.html`, `…/css/editorpowertools-graph.css` (slice section)

**Interfaces:**
- Consumes: component settings `slices` (`[{name, displayName, sortOptions:[{key,label}]}]`), `strings` (`{filterPlaceholder, reload, notAvailable, empty}`), `enabled` (bool).

- [ ] **Step 1: Template** `PowerSliceWidget.html`:

```html
<div class="ept-slices">
    <div class="epi-gadgetInnerToolbar ept-slices__toolbar" data-dojo-attach-point="toolbar">
        <div class="ept-slices__row">
            <div data-dojo-type="dijit/form/Button" data-dojo-attach-point="reloadButton"
                 data-dojo-attach-event="onClick: _reloadClicked"
                 class="epi-chromelessButton epi-iconReload epi-input-margin--left dijitInline dijitReset"></div>
        </div>
        <div class="ept-slices__row" data-dojo-attach-point="filterRow"></div>
    </div>
    <div data-dojo-type="epi-cms/component/ContentQueryGrid" data-dojo-attach-point="contentQuery"></div>
</div>
```

- [ ] **Step 2: Widget** — port `C:\Github\nti-website-upgrade-release\src\NTI.Web\modules\_protected\NTI.WebExtensions\Scripts\slices\SlicesComponent.js` (read-only) with: module id `editorpowertools-graph/PowerSliceWidget`, `declare("editorpowertools-graph.PowerSliceWidget", [_Container, _LayoutWidget, _TemplatedMixin, _WidgetsInTemplateMixin], …)`, template `dojo/text!./templates/PowerSliceWidget.html`, CSS classes `ept-slices__*`, `placeHolder: this.strings.filterPlaceholder`, reload button `title: this.strings.reload`, sort option labels from `option.label`. When `this.enabled === false` or `slices` is empty: in `buildRendering` hide the toolbar and grid (`domStyle.set(..., "display", "none")`) and place `<div class="ept-slices__message">` with `this.strings.notAvailable` (text via `textContent`, never innerHTML). Keep: toolbar sibling of grid, `resize` fallback to `this._contentBox`, parameters before `queryName`, 300 ms debounce, `removeOption` on slice switch, `clearTimeout` in `destroy`.
- [ ] **Step 3: CSS** — `.ept-slices__row{display:flex;gap:4px;align-items:center;padding:2px 4px}` `.ept-slices__select,.ept-slices__filter,.ept-slices__sort{flex:1 1 auto;min-width:0}` `.ept-slices__message{padding:12px;color:#666}` (match the look of the core `ContentDetailsWidget` — check `editorpowertools.css` for `.ept-cd-empty` and reuse its colours).
- [ ] **Step 4: Manual verification (sample site, CMS 12 then CMS 13)** — `dotnet run --project src/EditorPowertools.SampleSite`; log in as admin; Assets pane → add gadget "Power Slice"; each of the six slices loads; switching slices resets filter and sort; filter debounces; drag a block from "Unused blocks" into a content area works; context menu works; resize the pane → grid height follows. Toggle `CodeArt:EditorPowertools:Graph:Features:PowerSlice=false` → widget shows the not-available message. Record results in the task report. Browser console must be free of errors.
- [ ] **Step 5: Commit** — `feat(graph): Power Slice assets-pane widget`

---

### Task 11: PowerSearch server side (service, API, page)

**Files:**
- Create: `Tools/PowerSearch/PowerSearchModels.cs`, `Tools/PowerSearch/PowerSearchService.cs`, `Tools/PowerSearch/PowerSearchApiController.cs`, `Tools/PowerSearch/EditorPowertoolsGraphController.cs`, `Views/PowerSearch/Index.cshtml`
- Test: `Tools/PowerSearch/PowerSearchServiceTests.cs`, `Tools/PowerSearch/PowerSearchApiControllerTests.cs`, `Security/ControllerConventionTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum PowerSearchTab { Pages, Blocks, Media, Other }

public sealed record PowerSearchQuery(
    string? Q, PowerSearchTab Tab, int Take, int Skip,
    IReadOnlyList<string> Types, IReadOnlyList<string> Languages, IReadOnlyList<Guid> Sections);

public sealed record PowerSearchFacetValueDto(string Value, string Label, int Count, bool Selected);
public sealed record PowerSearchFacetsDto(
    IReadOnlyList<PowerSearchFacetValueDto> Types, IReadOnlyList<PowerSearchFacetValueDto> Languages,
    IReadOnlyList<PowerSearchFacetValueDto> Sections);
public sealed record PowerSearchHitDto(
    int ContentId, string Name, string TypeName, string Language, string Breadcrumb,
    DateTime? Changed, string? ChangedBy, string Status, bool IsDraft, string EditUrl);
public sealed record PowerSearchTabCountDto(PowerSearchTab Tab, int Total);   // Total -1 = unknown
public sealed record PowerSearchResponseDto(
    IReadOnlyList<PowerSearchHitDto> Hits, int Total, int? NextSkip,
    PowerSearchFacetsDto Facets, IReadOnlyList<PowerSearchTabCountDto> Tabs, string State);
    // State: "ok" | "unavailable" | "error"

public interface IPowerSearchService
{
    Task<PowerSearchResponseDto> SearchAsync(PowerSearchQuery query, CancellationToken ct);
}
```

  Tab scopes: Pages `["PageData"]`, locale on; Blocks `["BlockData"]`, locale on; Media `["MediaData"]`, locale off; Other = every registered content type whose base is none of those (from `IContentTypeRepository.List()`, validated), locale on. Rules:
  - empty/whitespace `Q` → `State "ok"`, no hits, no Graph call; `Take` clamped 1–50 (default `Search.PowerSearchPageSize`); `Skip` ≥ 0.
  - unavailable → `State "unavailable"`; `GraphQueryException` → `State "error"` (logged; no message in DTO).
  - Active tab: faceted search with `Limit = min(Take * OverfetchFactor, MaxOverfetch)` (headroom for access/stale drops); tab totals for the other three: plain `limit: 0`-style count queries — implement by calling `SearchAsync` with `IncludeFacets = true, Limit = 1` and reading `Total`; run the four Graph calls concurrently with `Task.WhenAll`.
  - **CMS content loading strictly sequential** after the Graph calls complete (shared SQL connection); per hit: load by GUID + language branch, skip unloadable/unreadable (`QueryDistinctAccess(AccessLevel.Read)`), de-dup by content id, stop at `Take`; `NextSkip = Skip + rawHitsConsumed` if more raw hits remain or `Total > Skip + rawHitsConsumed`, else null.
  - Drafts: when `Search.IncludeDrafts` and `Skip == 0`, prepend up to `Take/3` draft matches (via `DraftSearch`, tab scope filter by type).
  - Facets: type labels = content type `LocalizedName ?? DisplayName ?? Name`; language labels = `CultureInfo.GetCultureInfo(code).NativeName` (Graph locale `fr_CH` → culture `fr-CH`; unknown → code); section values = GUIDs; **drop section values whose count == total** (shared ancestors like the root), resolve remaining GUIDs to content names via `IContentRepository.TryGet(Guid)` (skip unresolvable/unreadable), keep top 50; any facet value in the query's selections is always present (`Selected = true`, count 0 if Graph omitted it).
  - Hit DTO: breadcrumb = ancestor names (excluding root) joined `" › "`; `Status` = localized version status; `EditUrl` via `SearchResultFactory.AppendLanguage(PageEditing.GetEditUrl(link), branch)`.

  `PowerSearchApiController` — `[Authorize(Policy="codeart:editorpowertools")] [RequireAjax]`, one `[HttpGet] Search(string? q, PowerSearchTab tab = Pages, int take = 0, int skip = 0, [FromQuery(Name="type")] string[]? type, [FromQuery(Name="lang")] string[]? lang, [FromQuery(Name="section")] string[]? section, CancellationToken ct)`: `HasAccess(HttpContext, nameof(GraphFeatureToggles.PowerSearch), EditorPowertoolsGraphPermissions.PowerSearch)` else `Forbid()`; parse `section` with `Guid.TryParse` (drop invalid); `type` through `IGraphSearchService.ValidateContentTypes`; `lang` kept only if `CultureInfo.GetCultureInfo` succeeds (catch `CultureNotFoundException`) and max 20 entries; wrap in try/catch → `StatusCode(500, new { error = L("/editorpowertools/graph/powersearch/error") })`.

  `EditorPowertoolsGraphController` — mirrors `EditorPowertoolsFormsController`: `[HttpGet] PowerSearch()` → access check → `View("/Views/PowerSearch/Index.cshtml")`.

  `Index.cshtml` — mirrors `Views/FormsOverview/Index.cshtml`: layout `/Views/Shared/_PowertoolsLayout.cshtml`, NavItems link to Overview, header with title/description, `<div id="ept-powersearch-root"></div>`, `window.EPT_GRAPH_BASE_URL = '@Html.Raw(Paths.ToResource(typeof(EditorPowertoolsGraphMenuProvider), ""))';`, scripts section loading `editorpowertools-graph.css` and `powersearch.js` via `Paths.ToClientResource`.

- [ ] **Step 1: Failing tests**
  - Service: empty `Q` → no Graph call; clamps; unavailable/error states; de-dup; unreadable skipped; sequential loading — assert with a repository mock whose `TryGet` records `Interlocked.Increment` of an "in flight" counter and fails the test if it ever exceeds 1; section value with count == total dropped; selected value retained with count 0; `NextSkip` null at end; Other tab excludes Page/Block/Media types.
  - Controller: forbidden when toggle off; invalid section GUIDs dropped; unknown type names dropped; exception → 500 body has no exception text (throw `new InvalidOperationException("secret-marker")` from the mock and assert the serialized body lacks it).
  - `Security/ControllerConventionTests` (reflection over the Graph assembly): every `Controller` subclass has `[Authorize(Policy = "codeart:editorpowertools")]`; every class whose name ends in `ApiController` has `[RequireAjax]`; every public action method's IL is not asserted — instead assert each controller's constructor takes `GraphFeatureAccessChecker` (proxy for "calls HasAccess"), and add a Roslyn-free source scan test: read each `*Controller.cs` under `src/EditorPowertools.Graph` (path via `AppContext.BaseDirectory` + `../../../../EditorPowertools.Graph`) and assert every `public IActionResult`/`public async Task<IActionResult>` method body contains `HasAccess(` and no file contains `ex.Message`.
- [ ] **Step 2: Run** → FAIL. **Step 3: Implement** (register `IPowerSearchService` scoped). **Step 4: Run** → PASS.
- [ ] **Step 5: Commit** — `feat(graph): PowerSearch service, API and page`

---

### Task 12: PowerSearch UI (vanilla JS + CSS)

**Files:**
- Create: `modules/_protected/EditorPowertoolsGraph/ClientResources/js/powersearch.js`; extend `…/css/editorpowertools-graph.css`
- Create: `Localization/GraphUiStringsContributor.cs` (Section `"powersearch"`, keys listed below) and register it

**Interfaces:**
- Consumes: `GET {EPT_GRAPH_BASE_URL}PowerSearchApi/Search?q=&tab=&take=&skip=&type=…&lang=…&section=…` returning `PowerSearchResponseDto` (camelCase JSON).

- [ ] **Step 1: Implement** following `src/EditorPowertools.Forms/.../forms-overview.js` conventions (IIFE, `EPT.showLoading`, `EPT.s`, `EPT.escHtml`, `EPT.safeUrl`, `EPT.timeAgo`). Requests use `fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })`. Layout (`ept-*` classes, reuse core card/table/badge styles from `editorpowertools.css`):
  - search box (autofocus, 300 ms debounce, Enter submits, `?q=` synced to `location.hash` so a search is linkable/back-button friendly);
  - tab bar Pages / Blocks / Media / Other with count badges — hide badge when total is `-1`;
  - left facet rail with three groups (Type, Language, Section): checkbox per value, top 8 + "Show N more" toggle, selected values always visible, "Clear filters" link; changing a facet resets `skip`;
  - result list rows: name (link to `editUrl`, `target="_top"` so it opens in the CMS shell), type · language · status badge (draft badge when `isDraft`), breadcrumb, `EPT.timeAgo(changed)` by `changedBy`;
  - "Load more" button when `nextSkip != null` (appends);
  - states: initial hint, loading, no results, `unavailable` (message + link to CMS Doctor: `window.EPT_BASE_URL + 'EditorPowertools/CmsDoctor'`), `error`, plus a small "Results come from the Optimizely Graph index and may lag behind recent publishing" note;
  - all dynamic text through `EPT.escHtml`; URLs through `EPT.safeUrl`.
  - Strings (`EPT.s('powersearch.<key>', '<English fallback>')`), keys: `placeholder, search, tab_pages, tab_blocks, tab_media, tab_other, facet_type, facet_language, facet_section, show_more, show_less, clear_filters, load_more, no_results, hint, unavailable, unavailable_link, error, index_lag, draft, by, results_count`.
- [ ] **Step 2: Manual verification** in the CMS 12 sample site with Graph credentials (see Task 15): search, switch tabs, select/deselect facets (group keeps alternatives), load more, draft appears first, keyboard-only use (Tab/Enter/Space reach every control), narrow window (≤ 900 px) collapses the rail above results. Without credentials: page shows the unavailable state. Console free of errors.
- [ ] **Step 3: Commit** — `feat(graph): PowerSearch editor UI`

---

### Task 13: Localization — all strings in 11 languages + completeness test

**Files:**
- Create/complete: `src/EditorPowertools.Graph/lang/{en,da,sv,no,de,fi,fr,es,nl,ja,zh-CN}.xml`
- Test: `Localization/LangFileCompletenessTests.cs`, `Localization/GraphUiStringsContributorTests.cs`

- [ ] **Step 1: Collect keys** — `grep -rhoE '"/editorpowertools/[a-z0-9/_-]+"' src/EditorPowertools.Graph --include=*.cs --include=*.cshtml | sort -u` plus the `powersearch` UI keys (Task 12) plus permission descriptions (`<admin><permissiontype><groups><EditorPowertools><permissions><PowerSearch|PowerSlice|SearchProviders>`), component title/description (`/editorpowertools/components/powerslice/title|description`), CMS Doctor check strings, menu/tool strings. Structure `en.xml` exactly like `src/EditorPowertools.Forms/lang/en.xml` (`<languages><language name="English" id="en">…`). Use the `name`/`id` attributes from the matching Forms lang files for the other ten.
- [ ] **Step 2: Failing tests** — `LangFileCompletenessTests`: load every embedded `lang/*.xml` from the Graph assembly; assert exactly 11 files; for each non-English file the set of leaf element paths equals `en.xml`'s; no leaf is empty; every key referenced in C#/cshtml source (scan like Step 1 in the test, via source path relative to `AppContext.BaseDirectory`) exists in `en.xml`. `GraphUiStringsContributorTests`: contributor keys equal the element names under `<ui><powersearch>` in en.xml.
- [ ] **Step 3: Translate** — write natural translations for da, sv, no, de, fi, fr, es, nl, ja, zh-CN (product terms "PowerSearch" and "Power Slice" stay untranslated; "Optimizely Graph" untranslated).
- [ ] **Step 4: Run** → PASS. **Step 5: Commit** — `feat(graph): localize Graph add-on in all 11 languages`

---

### Task 14: Sample sites with Graph

**Files:**
- Modify: `src/EditorPowertools.SampleSite/{EditorPowertools.SampleSite.csproj,Startup.cs,appsettings.json}`, `src/EditorPowertools.SampleSiteCms13/{…csproj,Startup.cs,appsettings.json}`

- [ ] **Step 1: CMS 12 site** — add `<PackageReference Include="Optimizely.ContentGraph.Cms" Version="4.*" />` and `<ProjectReference Include="..\EditorPowertools.Graph\EditorPowertools.Graph.csproj" />`; add a `CopyEditorPowertoolsGraphModuleFiles` target cloned from the Forms one. In `Startup.ConfigureServices` after Forms: `services.AddContentGraph();` then `services.AddEditorPowertoolsGraph();` (check the reference site `C:\Github\nti-website-upgrade-release\src\NTI.Web\Startup\DependencyRegistration.Graph.cs` for the exact `AddContentGraph` call and options such as `IncludeInheritanceInContentType = true` — required so `ContentType: { in: ["PageData"] }` matches derived types). `app.UseEditorPowertoolsGraph();`, `endpoints.MapEditorPowertoolsGraph();`. In `appsettings.json` add `"Optimizely": { "ContentGraph": { "GatewayAddress": "", "AppKey": "", "Secret": "", "SingleKey": "", "AllowSendingLog": "true" } }` placeholders (merge with any existing `Optimizely` section).
- [ ] **Step 2: CMS 13 site** — same with `Optimizely.Graph.Cms` 13.*. Find its registration extension by inspecting `~/.nuget/packages/optimizely.graph.cms/13.2.0/lib/net10.0/Optimizely.Graph.Cms.xml` for `ServiceCollectionExtensions` (e.g. `AddGraph…`); do not guess. Confirm the options that include the inheritance chain in `ContentType` exist in CMS 13; if not, note it for Task 16.
- [ ] **Step 3: Verify without credentials** — both sites start (`dotnet run --project …`), every existing tool still works, CMS Doctor shows the Graph check as Warning "not configured", PowerSearch shows the unavailable state, quick search still returns built-in results. If `AddContentGraph()` throws on empty config at startup, guard the call in the sample `Startup` with `if (!string.IsNullOrEmpty(Configuration["Optimizely:ContentGraph:AppKey"]))` and document why.
- [ ] **Step 4: Commit** — `build(samplesites): wire Optimizely Graph and the Graph add-on`

---

### Task 15: CI, packaging, docs

**Files:**
- Modify: `.github/workflows/publish.yml`, `README.md`, `RELEASE-NOTES.md`, `docs/backlog.md`, `docs/configuration.md`, `docs/local-development.md`
- Create: `docs/graph.md`

- [ ] **Step 1: CI** — for every Forms step in `publish.yml` (restore ×2, build, test, pack, test-results upload paths) add the Graph equivalent (`src/EditorPowertools.Graph/EditorPowertools.Graph.csproj`, `src/EditorPowertools.Tests.Graph/EditorPowertools.Tests.Graph.csproj`, trx `test-results-graph.trx`). No nuget.org push step.
- [ ] **Step 2: Pack locally** — `dotnet pack src/EditorPowertools.Graph -c Release -p:Version=0.0.0-local -o ./artifacts/nupkg` then list the nupkg (`unzip -l`) and confirm: `lib/net8.0/EditorPowertools.Graph.dll`, `lib/net10.0/…`, `build/net8.0/…targets`, `build/net10.0/…targets`, `contentFiles/any/any/modules/_protected/EditorPowertoolsGraph/EditorPowertoolsGraph.zip`, dependency on `UmageAI.Optimizely.EditorPowerTools` (same version) and the Graph client packages per TFM. Delete `./artifacts` afterwards.
- [ ] **Step 3: `docs/graph.md`** (template: `docs/forms.md`): what the package does (three tools + picker provider), prerequisites (Graph instance, `Optimizely.ContentGraph.Cms` / `Optimizely.Graph.Cms` installed and syncing, **Secret configured**, `IncludeInheritanceInContentType`), installation + registration snippet, configuration table for every option in `EditorPowertoolsGraphOptions` with defaults, permissions (three PermissionTypes, `AuthorizedRoles` — note: add `WebEditors` to `AuthorizedRoles` to give ordinary editors Graph search/slices), security model (queries run with the editor's roles; drafts from CMS; nothing restricted leaks), how to add a custom slice (subclass `PowerSliceBase`, register as `IContentQuery`), CMS 12 vs 13 notes and verification status, troubleshooting (CMS Doctor states, index lag, empty results = sync/permissions), screenshots (from Task 16).
- [ ] **Step 4: Other docs** — README: add Graph add-on section next to Forms with install line and link; RELEASE-NOTES: new "Unreleased" entry (Graph add-on, `IUiStringsContributor`, Forms JS strings now translated); backlog: mark #65 in progress/done and link #67; configuration.md: Graph section; local-development.md: `dotnet user-secrets set "Optimizely:ContentGraph:AppKey" "<key>" --project src/EditorPowertools.SampleSite` (+ GatewayAddress, Secret, SingleKey, and the same for SampleSiteCms13 — separate instances per CMS version).
- [ ] **Step 5: Full verification** — `dotnet build EditorPowertools.slnx -c Release` and `dotnet test` (repo root) → all green on both TFMs.
- [ ] **Step 6: Commit** — `docs(graph): document the Graph add-on; build: CI pack and test`

---

### Task 16: Live verification against Graph test instances (requires credentials from the user)

**Blocked until** the user provides CMS 12 and CMS 13 Graph instance credentials (set via user-secrets as in `docs/local-development.md`; never committed, never printed in logs or reports).

- [ ] **Step 1: CMS 12 end-to-end** — run the sample site, run the Graph content synchronization job, then verify: quick search (pages), page-tree filter, assets pane Blocks and Media tabs, core content picker search, PowerSearch (all tabs, facets, load more, drafts). Verify ACL: create a page readable only by a test role, log in as an editor without it → absent everywhere; add the role → present. Record which selector dialogs call `ISearchProvider` (spec §13.4) in `docs/graph.md`.
- [ ] **Step 2: CMS 13 dialect** — run an introspection query through the executor (a temporary test in the sample site, removed afterwards; or `curl` with the credentials from user-secrets in a shell variable) for `Content`/root query field names, `where` input types for name/route/fulltext/content type/ancestors, facet types, `Locales` enum values, and item fields for GUID/name/language. Implement `Cms13GraphQueryDialect` for real (same public behaviour and the same test suite as the CMS 12 dialect, duplicated as `Cms13GraphQueryDialectTests` under `#if OPTIMIZELY_CMS13` with CMS 13 field names), set `IsVerified => true`, and confirm `IGraphClient` sends HMAC and honours `cg-username`/`cg-roles` (repeat the ACL check from Step 1 on CMS 13). If `cg-*` headers are not honoured on CMS 13, stop and report — do not ship an ACL-less search.
- [ ] **Step 3: CMS 13 UI** — repeat Step 1 checks and Task 10/12 manual checks on the CMS 13 sample site (`ContentQueryBase`, `[Component]`, `ISearchProvider.Key`).
- [ ] **Step 4: Screenshots** — PowerSearch with facets, Power Slice widget, quick search results; save to `docs/screenshots/22-powersearch.png`, `23-powerslice.png`, `24-graph-quicksearch.png` (next free numbers — check the folder) and reference them from `docs/graph.md` and README.
- [ ] **Step 5: Full test run** — `dotnet test` → green. **Commit** — `feat(graph): verified CMS 13 Graph dialect; docs screenshots`

---

## Final review (after all tasks)

Run an independent whole-branch review covering: security (ACL headers, injection, error leakage, access checks on every surface), CMS 12/13 parity, convention compliance (CLAUDE.md, `docs/coding-guidelines.md`), localization completeness, docs accuracy vs code. Fix findings, re-run `dotnet test`.
