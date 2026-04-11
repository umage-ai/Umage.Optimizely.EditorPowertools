# CMS 13 / .NET 10 Multi-Targeting Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `UmageAI.Optimizely.EditorPowerTools` to compile and run against both CMS 12 (.NET 8) and CMS 13 (.NET 10) from a single NuGet package, plus add a CMS 13 Alloy sample site.

**Architecture:** Multi-target `EditorPowertools.csproj` to `net8.0;net10.0` with conditional `EPiServer.CMS` package refs and `OPTIMIZELY_CMS12`/`OPTIMIZELY_CMS13` compile symbols. Breaking changes fixed with Tier 1 `#if` blocks; the Razor layout uses Tier 2 (separate cshtml per TFM). A new `EditorPowertools.SampleSiteCms13` project is scaffolded and wired up like the existing CMS 12 sample site.

**Tech Stack:** .NET 8 / .NET 10, EPiServer.CMS 12.* / 13.*, MSBuild multi-targeting, Razor SDK

---

## Files Modified / Created

| File | Action |
|---|---|
| `src/EditorPowertools/EditorPowertools.csproj` | Modify — multi-target, conditional packages, symbols |
| `src/EditorPowertools/build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets` | Create — copy of net8.0 targets |
| `src/EditorPowertools/Services/UnifiedContentAnalysisJob.cs` | Modify — `#if` ScheduledPlugIn |
| `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs` | Modify — `#if` ScheduledPlugIn + SiteDefinition |
| `src/EditorPowertools/Tools/ContentDetails/ContentDetailsService.cs` | Modify — `FilteredItems` → `Items` |
| `src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs` | Modify — `FilteredItems` → `Items` |
| `src/EditorPowertools/Views/Shared/Cms13/_PowertoolsLayout.cshtml` | Create — CMS 13 nav TagHelper version |
| `src/EditorPowertools.Tests/EditorPowertools.Tests.csproj` | Modify — multi-target |
| `src/EditorPowertools.SampleSiteCms13/` | Create — new project via template or scaffold |
| `EditorPowertools.sln` | Modify — add new sample site project |

---

## Task 1: Find the CMS 13 package version

**Files:** None (discovery only)

- [ ] **Step 1: Find the latest CMS 13 package version**

```bash
dotnet package search EPiServer.CMS --source https://api.nuget.optimizely.com/v3/index.json --prerelease | grep "^EPiServer.CMS " | head -5
```

Expected output: a line like `EPiServer.CMS   13.0.0-preview4   ...`

Note the exact version number (e.g. `13.0.0-preview4`). Also do:

```bash
dotnet package search EPiServer.CMS.UI.Core --source https://api.nuget.optimizely.com/v3/index.json --prerelease | grep "^EPiServer.CMS.UI.Core " | head -5
```

If `dotnet package search` is unavailable (older SDK), use:
```bash
curl -s "https://api.nuget.optimizely.com/v3/registration/episerver.cms/index.json" | grep -o '"version":"13[^"]*"' | head -5
```

- [ ] **Step 2: Record the versions**

You will use these in Task 2. They will look like `13.0.0-preview4` or `13.0.0` if GA has released.

---

## Task 2: Multi-target the library csproj

**Files:**
- Modify: `src/EditorPowertools/EditorPowertools.csproj`

- [ ] **Step 1: Replace TargetFramework and add conditional package refs**

Replace the entire contents of `src/EditorPowertools/EditorPowertools.csproj` with the following. Substitute `CMS13_VERSION` and `CMS13_UI_VERSION` with the versions found in Task 1 (e.g. `13.0.0-preview4`):

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>

    <!-- NuGet package properties -->
    <PackageId>UmageAI.Optimizely.EditorPowerTools</PackageId>
    <Version>0.1.0-local</Version>
    <Authors>UmageAI</Authors>
    <Company>UmageAI</Company>
    <Description>A collection of power tools for Optimizely CMS 12 and CMS 13 editors and admins. Includes Content Type Audit, Activity Timeline, Bulk Property Editor, Scheduled Jobs Gantt, Link Checker, Active Editors with real-time chat, and more.</Description>
    <PackageTags>Optimizely;CMS;EPiServer;Editor;Tools;Admin;Audit;PowerTools</PackageTags>
    <PackageProjectUrl>https://github.com/UmageAI/EditorPowerTools</PackageProjectUrl>
    <RepositoryUrl>https://github.com/UmageAI/EditorPowerTools</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageIcon>icon.png</PackageIcon>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
  </PropertyGroup>

  <!-- Compile-time version symbols -->
  <PropertyGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS12</DefineConstants>
  </PropertyGroup>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <DefineConstants>$(DefineConstants);OPTIMIZELY_CMS13</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="EPiServer" />
    <Using Include="EPiServer.Core" />
    <Using Include="EPiServer.DataAbstraction" />
    <Using Include="EPiServer.DataAnnotations" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

  <!-- CMS 12 packages (net8.0) -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <PackageReference Include="EPiServer.CMS" Version="12.29.0" />
    <PackageReference Include="EPiServer.CMS.UI.Core" Version="12.29.0" />
  </ItemGroup>

  <!-- CMS 13 packages (net10.0) — substitute exact version from Task 1 -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <PackageReference Include="EPiServer.CMS" Version="CMS13_VERSION" />
    <PackageReference Include="EPiServer.CMS.UI.Core" Version="CMS13_UI_VERSION" />
  </ItemGroup>

  <!-- Packages compatible with both TFMs -->
  <ItemGroup>
    <PackageReference Include="CsvHelper" Version="33.1.0" />
    <PackageReference Include="EPPlusFree" Version="4.5.3.8" />
  </ItemGroup>

  <!-- Localization language files (Optimizely XML format) -->
  <ItemGroup>
    <EmbeddedResource Include="lang\*.xml" />
  </ItemGroup>

  <!-- CMS 12 Razor layout (default, net8.0) -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0'">
    <Content Include="Views\Shared\_PowertoolsLayout.cshtml" />
  </ItemGroup>

  <!-- CMS 13 Razor layout — mapped to the same virtual path as the CMS 12 version -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <Content Remove="Views\Shared\_PowertoolsLayout.cshtml" />
    <Content Include="Views\Shared\Cms13\_PowertoolsLayout.cshtml">
      <Link>Views\Shared\_PowertoolsLayout.cshtml</Link>
    </Content>
  </ItemGroup>

  <!-- Include README, icon, and BOTH .targets files in the NuGet package -->
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="" />
    <None Include="icon.png" Pack="true" PackagePath="" />
    <None Include="build\net8.0\UmageAI.Optimizely.EditorPowerTools.targets" Pack="true" PackagePath="build\net8.0\" />
    <None Include="build\net10.0\UmageAI.Optimizely.EditorPowerTools.targets" Pack="true" PackagePath="build\net10.0\" />
  </ItemGroup>

  <!--
    Module zip: created during build, packed as contentFiles with BuildAction=None.
    The .targets file copies it to the consuming project's modules/_protected/ on first build.
  -->
  <Target Name="CreateModuleZip" BeforeTargets="Build">
    <MakeDir Directories="$(IntermediateOutputPath)module-zip" />
    <ZipDirectory SourceDirectory="modules\_protected\EditorPowertools" DestinationFile="$(IntermediateOutputPath)module-zip\EditorPowertools.zip" Overwrite="true" />
  </Target>

  <ItemGroup>
    <Content Include="$(IntermediateOutputPath)module-zip\EditorPowertools.zip" Pack="true" PackagePath="contentFiles\any\any\modules\_protected\EditorPowertools" BuildAction="None" CopyToPublishDirectory="Never" CopyToOutputDirectory="Never">
      <Link>EditorPowertools.zip</Link>
    </Content>
  </ItemGroup>

  <!-- Exclude the source modules/_protected folder from build output -->
  <ItemGroup>
    <Content Remove="modules\_protected\**" />
    <None Remove="modules\_protected\**" />
  </ItemGroup>

  <!-- Keep source files visible in IDE -->
  <ItemGroup>
    <None Include="modules\_protected\EditorPowertools\**\*" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Attempt build to see all CMS 13 compile errors**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net10.0 2>&1 | grep -E "error|warning" | head -50
```

Expected: Several errors about `ScheduledPlugIn`, `SiteDefinition`, and potentially others. These are fixed in Tasks 3–6.

---

## Task 3: Add net10.0 build targets file

**Files:**
- Create: `src/EditorPowertools/build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets`

- [ ] **Step 1: Create the net10.0 targets directory and file**

```bash
mkdir -p C:/Github/EditorPowertools/src/EditorPowertools/build/net10.0
```

Create `src/EditorPowertools/build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets` with identical content to the net8.0 version:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003" ToolsVersion="4.0">
    <ItemGroup>
        <SourceScripts
                Include="$(MSBuildThisFileDirectory)..\..\contentFiles\any\any\modules\_protected\**\*.zip"/>
    </ItemGroup>

    <Target Name="CopyModule" BeforeTargets="Build">
        <Copy
                SourceFiles="@(SourceScripts)"
                DestinationFolder="$(MSBuildProjectDirectory)\modules\_protected\%(RecursiveDir)"
        />
    </Target>
</Project>
```

- [ ] **Step 2: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools/build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets
git add src/EditorPowertools/EditorPowertools.csproj
git commit -m "feat(cms13): multi-target library csproj and add net10.0 targets file"
```

---

## Task 4: Fix ScheduledPlugIn → ScheduledJobAttribute

**Files:**
- Modify: `src/EditorPowertools/Services/UnifiedContentAnalysisJob.cs`
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs`

In CMS 13, `[ScheduledPlugIn]` (from `EPiServer.PlugIn`) is replaced by `[ScheduledJobAttribute]` (from `EPiServer.Scheduler`). The properties `DisplayName`, `Description`, `LanguagePath`, and `SortIndex` remain the same.

- [ ] **Step 1: Fix UnifiedContentAnalysisJob.cs**

In `src/EditorPowertools/Services/UnifiedContentAnalysisJob.cs`, replace:

```csharp
using EPiServer.PlugIn;
using EPiServer.Scheduler;
```

with:

```csharp
#if OPTIMIZELY_CMS13
using EPiServer.Scheduler;
#else
using EPiServer.PlugIn;
using EPiServer.Scheduler;
#endif
```

Then replace the attribute:

```csharp
[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis",
    SortIndex = 10000)]
```

with:

```csharp
#if OPTIMIZELY_CMS13
[ScheduledJobAttribute(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis",
    SortIndex = 10000)]
#else
[ScheduledPlugIn(
    DisplayName = "[EditorPowertools] Content Analysis",
    Description = "Unified job that traverses all content once and runs all registered analyzers (content type stats, personalization, link checking, etc.).",
    LanguagePath = "/editorpowertools/jobs/contentanalysis",
    SortIndex = 10000)]
#endif
```

- [ ] **Step 2: Fix ContentAuditExportJob.cs**

In `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs`, apply the same pattern.

Replace the existing using directives block that includes `EPiServer.PlugIn`:

```csharp
using EPiServer.PlugIn;
using EPiServer.Scheduler;
```

with:

```csharp
#if OPTIMIZELY_CMS13
using EPiServer.Scheduler;
#else
using EPiServer.PlugIn;
using EPiServer.Scheduler;
#endif
```

Replace the attribute on `ContentAuditExportJob`:

```csharp
[ScheduledPlugIn(
    DisplayName    = "[EditorPowertools] Content Audit Export",
    Description    = "Generates a Content Audit export file and saves it to the CMS media library.",
    LanguagePath   = "/editorpowertools/jobs/contentauditexport",
    SortIndex      = 10001)]
```

with:

```csharp
#if OPTIMIZELY_CMS13
[ScheduledJobAttribute(
    DisplayName    = "[EditorPowertools] Content Audit Export",
    Description    = "Generates a Content Audit export file and saves it to the CMS media library.",
    LanguagePath   = "/editorpowertools/jobs/contentauditexport",
    SortIndex      = 10001)]
#else
[ScheduledPlugIn(
    DisplayName    = "[EditorPowertools] Content Audit Export",
    Description    = "Generates a Content Audit export file and saves it to the CMS media library.",
    LanguagePath   = "/editorpowertools/jobs/contentauditexport",
    SortIndex      = 10001)]
#endif
```

- [ ] **Step 3: Verify no remaining ScheduledPlugIn errors**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net10.0 2>&1 | grep "ScheduledPlugIn"
```

Expected: No output (no remaining errors about ScheduledPlugIn).

- [ ] **Step 4: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools/Services/UnifiedContentAnalysisJob.cs
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs
git commit -m "feat(cms13): replace [ScheduledPlugIn] with [ScheduledJobAttribute] for CMS 13"
```

---

## Task 5: Fix SiteDefinition.Current.GlobalAssetsRoot

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs`

`SiteDefinition.Current` is removed in CMS 13. In `ContentAuditExportJob.cs:168`, it is used to get `GlobalAssetsRoot` — a `ContentReference` to the global shared assets folder. In CMS 13 this is accessible as `ContentReference.GlobalBlockFolder`.

- [ ] **Step 1: Add the CMS 13 using directive**

In `ContentAuditExportJob.cs`, the `using EPiServer.Web;` import covers `SiteDefinition` for CMS 12. No additional using is needed for CMS 13 since `ContentReference` is in `EPiServer.Core` which is already a global using.

- [ ] **Step 2: Replace the GlobalAssetsRoot usage**

In `src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs`, find the `EnsureReportFolder` method (around line 165):

```csharp
private ContentReference EnsureReportFolder()
{
    var folderName = _options.ContentAudit.ReportFolderName;
    var globalAssets = SiteDefinition.Current.GlobalAssetsRoot;
```

Replace with:

```csharp
private ContentReference EnsureReportFolder()
{
    var folderName = _options.ContentAudit.ReportFolderName;
#if OPTIMIZELY_CMS13
    var globalAssets = ContentReference.GlobalBlockFolder;
#else
    var globalAssets = SiteDefinition.Current.GlobalAssetsRoot;
#endif
```

Also remove the `using EPiServer.Web;` import conditionally, since `SiteDefinition` lives there and is not needed in CMS 13:

```csharp
#if !OPTIMIZELY_CMS13
using EPiServer.Web;
#endif
```

- [ ] **Step 3: Verify the fix builds**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net10.0 2>&1 | grep "SiteDefinition"
```

Expected: No output.

> **Note:** If `ContentReference.GlobalBlockFolder` does not exist in the CMS 13 version you installed, the error will tell you the correct replacement. Common alternatives: inject `IApplicationResolver` and call `GetGlobalAssetsRoot()`, or use a different `ContentReference` static property. Fix accordingly.

- [ ] **Step 4: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools/Tools/ContentAudit/ContentAuditExportJob.cs
git commit -m "feat(cms13): replace SiteDefinition.Current.GlobalAssetsRoot with CMS 13 equivalent"
```

---

## Task 6: Replace ContentArea.FilteredItems with Items

**Files:**
- Modify: `src/EditorPowertools/Tools/ContentDetails/ContentDetailsService.cs`
- Modify: `src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs`

`ContentArea.FilteredItems` is marked obsolete in CMS 13 and will become a compile error in a future version. `Items` returns all items without access-level filtering — appropriate for admin tools. This is a direct fix (no `#if`) since the API exists in both versions.

- [ ] **Step 1: Fix ContentDetailsService.cs**

In `src/EditorPowertools/Tools/ContentDetails/ContentDetailsService.cs`, make these three replacements:

Replace (line ~120):
```csharp
foreach (var item in contentArea.FilteredItems ?? Enumerable.Empty<ContentAreaItem>())
```
with:
```csharp
foreach (var item in contentArea.Items ?? Enumerable.Empty<ContentAreaItem>())
```

Replace (line ~278):
```csharp
var items = (contentArea.FilteredItems ?? Enumerable.Empty<ContentAreaItem>()).ToList();
```
with:
```csharp
var items = (contentArea.Items ?? Enumerable.Empty<ContentAreaItem>()).ToList();
```

Replace (line ~612):
```csharp
return $"[{ca.FilteredItems?.Count() ?? 0} items]";
```
with:
```csharp
return $"[{ca.Items?.Count() ?? 0} items]";
```

- [ ] **Step 2: Fix GetDescendentsContentAuditProvider.cs**

In `src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs`, replace (lines ~288-289):

```csharp
ca.FilteredItems != null &&
ca.FilteredItems.Any(i => i.AllowedRoles != null && i.AllowedRoles.Any())
```
with:
```csharp
ca.Items != null &&
ca.Items.Any(i => i.AllowedRoles != null && i.AllowedRoles.Any())
```

- [ ] **Step 3: Verify no remaining FilteredItems references**

```bash
cd C:/Github/EditorPowertools
grep -rn "FilteredItems" src/EditorPowertools/
```

Expected: No output.

- [ ] **Step 4: Build both TFMs to confirm no regressions**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | grep -E "^.*error"
```

Expected: No errors.

- [ ] **Step 5: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools/Tools/ContentDetails/ContentDetailsService.cs
git add src/EditorPowertools/Tools/ContentAudit/GetDescendentsContentAuditProvider.cs
git commit -m "fix: replace deprecated ContentArea.FilteredItems with Items"
```

---

## Task 7: Create CMS 13 Razor layout (Tier 2)

**Files:**
- Create: `src/EditorPowertools/Views/Shared/Cms13/_PowertoolsLayout.cshtml`

The CMS 13 navigation API changed: `@Html.CreatePlatformNavigationMenu()` is removed (use `<platform-navigation />` TagHelper) and `@Html.ApplyPlatformNavigation()` is removed (add CSS class `epi-navigation--no-padding` to the content div instead).

- [ ] **Step 1: Create the Cms13 directory and layout file**

Create `src/EditorPowertools/Views/Shared/Cms13/_PowertoolsLayout.cshtml` with the following content:

```html
@using EPiServer.Framework.Web.Resources
@using EPiServer.Shell
@using EPiServer.Shell.Navigation
@using System.Text.Json
@addTagHelper *, EPiServer.Cms.Shell.UI
@inject UmageAI.Optimizely.EditorPowerTools.Localization.UiStringsProvider UiStrings
@inject LocalizationService Loc
@{
    var title = ViewData["Title"]?.ToString() ?? "Editor Powertools";
    // For controller routes (menu links, page navigation)
    string ActionPath(string path) => Paths.ToResource(typeof(UmageAI.Optimizely.EditorPowerTools.Menu.EditorPowertoolsMenuProvider), path);
    // For static files (CSS, JS) served from the module's ClientResources folder
    string ClientResource(string path) => Paths.ToClientResource(typeof(UmageAI.Optimizely.EditorPowerTools.Menu.EditorPowertoolsMenuProvider), path);
}
<!DOCTYPE html>
<html lang="en">
<head>
    <title>@title - Editor Powertools</title>
    <meta http-equiv="X-UA-Compatible" content="IE=Edge" />

    @* CMS Shell styles - makes the page look like part of the CMS *@
    @ClientResources.RenderResources("ShellCore")
    @ClientResources.RenderResources("ShellCoreLightTheme")

    <link rel="stylesheet" href="@ClientResource("ClientResources/css/editorpowertools.css")" />
    @RenderSection("Styles", required: false)
</head>
<body class="Sleek">
    @* CMS 13 top navigation bar - replaces @Html.CreatePlatformNavigationMenu() *@
    <platform-navigation />

    @* epi-navigation--no-padding replaces @Html.ApplyPlatformNavigation() *@
    <div class="epi-navigation--no-padding">
        <div class="ept-shell">
            <header class="ept-header">
                <a href="@ActionPath("EditorPowertools/Overview")" class="ept-header__logo">
                    <svg class="ept-header__icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"/>
                    </svg>
                    Editor Powertools
                </a>
                <nav class="ept-header__nav">
                    @RenderSection("NavItems", required: false)
                </nav>
                <a href="@ActionPath("EditorPowertools/About")" class="ept-header__about">
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>
                    @Loc.GetString("/editorpowertools/menu/about")
                </a>
            </header>
            <main class="ept-main">
                @RenderBody()
            </main>
        </div>
    </div>

    <div id="ept-dialog-container"></div>
    <script>
        window.EPT_BASE_URL = '@Html.Raw(Paths.ToResource(typeof(UmageAI.Optimizely.EditorPowerTools.Menu.EditorPowertoolsMenuProvider), ""))';
        window.EPT_HUB_URL = '@Html.Raw(Paths.ToResource(typeof(UmageAI.Optimizely.EditorPowerTools.Menu.EditorPowertoolsMenuProvider), "hubs"))';
        window.EPT_CMS_URL = '@Html.Raw(Paths.ToResource("CMS", ""))';
        window.EPT_ADMIN_URL = '@Html.Raw(Paths.ToResource("EPiServer.Cms.UI.Admin", "default"))';
        window.EPT_VG_URL = '@Html.Raw(Paths.ToResource("EPiServer.Cms.UI.VisitorGroups", "ManageVisitorGroups"))';
        window.EPT_STRINGS = @Html.Raw(JsonSerializer.Serialize(UiStrings.GetAll()));
    </script>
    <script src="@ClientResource("ClientResources/js/editorpowertools.js")"></script>
    <script src="@ClientResource("ClientResources/js/components.js")"></script>
    @RenderSection("Scripts", required: false)
</body>
</html>
```

> **Note:** The `@addTagHelper` line registers the `<platform-navigation />` TagHelper. If the assembly name differs in your CMS 13 version, the compiler will tell you — check the error and substitute the correct assembly name.

- [ ] **Step 2: Build the net10.0 target and check for remaining errors**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net10.0 2>&1 | grep -E "error"
```

Expected: Zero errors. If there are still errors, they will be new CMS 13 API issues — fix them using Tier 1 `#if` following the same pattern as Tasks 4–5.

- [ ] **Step 3: Also verify net8.0 still builds**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net8.0 2>&1 | grep -E "error"
```

Expected: Zero errors.

- [ ] **Step 4: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools/Views/Shared/Cms13/_PowertoolsLayout.cshtml
git commit -m "feat(cms13): add CMS 13 Razor layout using platform-navigation TagHelper"
```

---

## Task 8: Handle any remaining net10.0 compiler errors

**Files:** Whichever files still produce errors after Tasks 4–7.

- [ ] **Step 1: Run a full build and collect all remaining errors**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj --framework net10.0 2>&1 | grep "error CS"
```

- [ ] **Step 2: For each remaining error, apply Tier 1 fix**

For each compiler error, apply the `#if OPTIMIZELY_CMS13` / `#else` / `#endif` pattern shown in Task 4. Common patterns:

**Renamed type (e.g. `IUrlResolver` → `IContentUrlResolver`):**
```csharp
#if OPTIMIZELY_CMS13
using EPiServer.Core.Routing;  // adjust namespace to what CMS 13 actually uses
#else
using EPiServer.Web.Routing;
#endif

// In the class:
#if OPTIMIZELY_CMS13
private readonly IContentUrlResolver _urlResolver;
public MyService(IContentUrlResolver urlResolver) { _urlResolver = urlResolver; }
#else
private readonly IUrlResolver _urlResolver;
public MyService(IUrlResolver urlResolver) { _urlResolver = urlResolver; }
#endif
```

**Removed static member:**
```csharp
#if OPTIMIZELY_CMS13
var x = SomeClass.NewWayToGetThis();
#else
var x = SomeClass.OldStaticMember;
#endif
```

If a tool would require more than 3 `#if` blocks in a single file to fix, disable it for CMS 13 in `ServiceCollectionExtensions.cs`:
```csharp
#if !OPTIMIZELY_CMS13
    services.AddTransient<TheProblematicService>();
#endif
```

- [ ] **Step 3: Verify zero errors on both TFMs**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | grep "error CS"
```

Expected: No output.

- [ ] **Step 4: Commit all fixes**

```bash
cd C:/Github/EditorPowertools
git add -A
git commit -m "feat(cms13): fix remaining CMS 13 compile errors"
```

---

## Task 9: Multi-target the tests project

**Files:**
- Modify: `src/EditorPowertools.Tests/EditorPowertools.Tests.csproj`

- [ ] **Step 1: Add net10.0 target**

In `src/EditorPowertools.Tests/EditorPowertools.Tests.csproj`, replace:

```xml
<TargetFramework>net8.0</TargetFramework>
```

with:

```xml
<TargetFrameworks>net8.0;net10.0</TargetFrameworks>
```

- [ ] **Step 2: Run tests on both TFMs**

```bash
cd C:/Github/EditorPowertools
dotnet test src/EditorPowertools.Tests/EditorPowertools.Tests.csproj --framework net8.0
dotnet test src/EditorPowertools.Tests/EditorPowertools.Tests.csproj --framework net10.0
```

Expected: All tests pass on both frameworks.

- [ ] **Step 3: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools.Tests/EditorPowertools.Tests.csproj
git commit -m "feat(cms13): multi-target test project to net8.0 and net10.0"
```

---

## Task 10: Create the CMS 13 sample site

**Files:**
- Create: `src/EditorPowertools.SampleSiteCms13/` (new project directory)

- [ ] **Step 1: Check if the Alloy template supports CMS 13**

```bash
dotnet new --list | grep -i alloy
dotnet new --list | grep -i optimizely
```

If you see `alloy-epi-mvc` or similar, note its short name.

If the template is not installed, install the Optimizely templates:
```bash
dotnet new install EPiServer.Net.Templates --nuget-source https://api.nuget.optimizely.com/v3/index.json --prerelease
dotnet new --list | grep -i alloy
```

- [ ] **Step 2a: If an Alloy template for net10.0 is available, scaffold it**

```bash
cd C:/Github/EditorPowertools/src
dotnet new alloy-epi-mvc -n EditorPowertools.SampleSiteCms13 --output EditorPowertools.SampleSiteCms13
```

Then verify it targets net10.0:
```bash
grep TargetFramework src/EditorPowertools.SampleSiteCms13/*.csproj
```

If it targets net8.0, change it to net10.0 in the csproj manually.

- [ ] **Step 2b: If no CMS 13 template is available, scaffold a bare web project**

```bash
cd C:/Github/EditorPowertools/src
dotnet new web -n EditorPowertools.SampleSiteCms13 --framework net10.0 --output EditorPowertools.SampleSiteCms13
```

Then manually edit `src/EditorPowertools.SampleSiteCms13/EditorPowertools.SampleSiteCms13.csproj` to add:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="EPiServer" />
    <Using Include="EPiServer.Core" />
    <Using Include="EPiServer.DataAbstraction" />
    <Using Include="EPiServer.DataAnnotations" />
  </ItemGroup>

  <ItemGroup>
    <!-- Use the version found in Task 1 -->
    <PackageReference Include="EPiServer.CMS" Version="CMS13_VERSION" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\EditorPowertools\EditorPowertools.csproj" />
  </ItemGroup>

  <!-- Copy module files from the addon project during build (dev only) -->
  <Target Name="CopyEditorPowertoolsModuleFiles" BeforeTargets="Build">
    <ItemGroup>
      <EditorPowertoolsModuleFiles Include="..\EditorPowertools\modules\_protected\EditorPowertools\**\*" />
    </ItemGroup>
    <Copy
      SourceFiles="@(EditorPowertoolsModuleFiles)"
      DestinationFiles="@(EditorPowertoolsModuleFiles->'$(MSBuildProjectDirectory)\modules\_protected\EditorPowertools\%(RecursiveDir)%(Filename)%(Extension)')"
      SkipUnchangedFiles="true" />
  </Target>
</Project>
```

- [ ] **Step 3: Commit the scaffolded project**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools.SampleSiteCms13/
git commit -m "feat(cms13): scaffold CMS 13 sample site project"
```

---

## Task 11: Configure the CMS 13 sample site

**Files:**
- Modify: `src/EditorPowertools.SampleSiteCms13/Program.cs` (or `Startup.cs`)
- Create/Modify: `src/EditorPowertools.SampleSiteCms13/appsettings.json`
- Modify: `src/EditorPowertools.SampleSiteCms13/EditorPowertools.SampleSiteCms13.csproj` (if using alloy template)

Reference the CMS 12 sample site at `src/EditorPowertools.SampleSite/` for the pattern to follow. The goal is to wire up EditorPowertools so it loads when the CMS 13 site runs.

- [ ] **Step 1: Wire up EditorPowertools in Program.cs**

Open `src/EditorPowertools.SampleSiteCms13/Program.cs`. It will have either the minimal API pattern (for bare `dotnet new web`) or the Alloy startup pattern. Add the EditorPowertools registration.

For minimal API / bare scaffold, make `Program.cs` look like:

```csharp
using EPiServer.Cms.UI.AspNetIdentity;
using UmageAI.Optimizely.EditorPowerTools.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCmsAspNetIdentity<ApplicationUser>();
builder.Services.AddCms();
builder.Services.AddEditorPowertools();
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseEditorPowertools();
app.UseCmsApplicationBuilderExtensions();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapDefaultControllerRoute();

app.Run();
```

If the Alloy template already has a more complete setup, add only `builder.Services.AddEditorPowertools()` and `app.UseEditorPowertools()` in the appropriate places (after authentication middleware, before endpoint mapping).

- [ ] **Step 2: Configure appsettings.json**

Copy and adapt from `src/EditorPowertools.SampleSite/appsettings.json`. Minimum required:

```json
{
  "ConnectionStrings": {
    "EPiServerDB": "Data Source=.;Initial Catalog=EPiServerCms13;Integrated Security=True;TrustServerCertificate=True"
  },
  "EPiServer": {
    "CmsLicenseKey": ""
  },
  "CodeArt": {
    "EditorPowertools": {
      "AuthorizedRoles": ["WebAdmins", "Administrators"]
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 3: Build the sample site**

```bash
cd C:/Github/EditorPowertools
dotnet build src/EditorPowertools.SampleSiteCms13/ 2>&1 | grep -E "error"
```

Expected: Zero errors.

- [ ] **Step 4: Commit**

```bash
cd C:/Github/EditorPowertools
git add src/EditorPowertools.SampleSiteCms13/
git commit -m "feat(cms13): configure CMS 13 sample site with EditorPowertools integration"
```

---

## Task 12: Add the CMS 13 sample site to the solution

**Files:**
- Modify: `EditorPowertools.sln`

- [ ] **Step 1: Add project to solution**

```bash
cd C:/Github/EditorPowertools
dotnet sln add src/EditorPowertools.SampleSiteCms13/EditorPowertools.SampleSiteCms13.csproj
```

- [ ] **Step 2: Verify solution builds completely**

```bash
cd C:/Github/EditorPowertools
dotnet build 2>&1 | grep -E "^.*error"
```

Expected: Zero errors across all projects and TFMs.

- [ ] **Step 3: Run tests on both TFMs one final time**

```bash
cd C:/Github/EditorPowertools
dotnet test 2>&1 | tail -10
```

Expected: All tests pass.

- [ ] **Step 4: Commit and summarise**

```bash
cd C:/Github/EditorPowertools
git add EditorPowertools.sln
git commit -m "feat(cms13): add CMS 13 sample site to solution"
```

---

## Task 13: Verify NuGet pack produces both TFMs

**Files:** None (verification only)

- [ ] **Step 1: Pack the library**

```bash
cd C:/Github/EditorPowertools
dotnet pack src/EditorPowertools/EditorPowertools.csproj -o /tmp/ept-pack 2>&1 | tail -5
```

Expected: `Successfully created package`.

- [ ] **Step 2: Inspect the package contents**

```bash
# List TFM-specific lib folders
unzip -l /tmp/ept-pack/*.nupkg | grep "^.*lib/"
```

Expected output includes both:
```
...  lib/net8.0/UmageAI.Optimizely.EditorPowerTools.dll
...  lib/net10.0/UmageAI.Optimizely.EditorPowerTools.dll
```

Also verify both targets files are included:
```bash
unzip -l /tmp/ept-pack/*.nupkg | grep "build/"
```

Expected:
```
...  build/net8.0/UmageAI.Optimizely.EditorPowerTools.targets
...  build/net10.0/UmageAI.Optimizely.EditorPowerTools.targets
```

- [ ] **Step 3: Final commit**

```bash
cd C:/Github/EditorPowertools
git log --oneline -10
```

Confirm all tasks have been committed cleanly. The branch `features/cms13` is now ready for review.
