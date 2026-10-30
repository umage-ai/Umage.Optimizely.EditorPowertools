# Release Notes

## Unreleased

### Fixes

- **Power Slice looked empty** — a newly added Power Slice gadget got the shell's default height of 200 pixels, and its two-row toolbar left the list no room, so the slice appeared to have no content. The gadget now asks for 400 pixels, which also enlarges gadgets already added at the old height.

### Documentation

- Screenshots of PowerSearch and Power Slice in the README and the [Graph Add-On guide](docs/graph.md), and how editors add Power Slice to the assets pane.

## v0.11.0

### New add-on

- **Graph add-on** (`UmageAI.Optimizely.EditorPowerTools.Graph`) — PowerSearch (Graph-backed editor search with facets; an empty box browses newest first), Power Slice (assets-pane content lists) and Graph-backed search providers and content picker search. Queries run with the editor's roles. Localized in all 11 languages. See [docs/graph.md](docs/graph.md). Verified against live Graph instances on CMS 12 and CMS 13.

### New packages

- **CMS 12 on .NET 10** — `UmageAI.Optimizely.EditorPowerTools.Cms12`, `.Forms.Cms12` and `.Graph.Cms12`: the CMS 12 build on its own, for CMS 12 sites running .NET 10 (#62). The main packages are unchanged. See [Moving a CMS 12 site to .NET 10](docs/getting-started.md#moving-a-cms-12-site-to-net-10).

### Fixes

- **Aggregation job** — since v0.10.0 every run failed to initialize four analyzers (Personalization, Link Checker, Language Audit, Security Audit), so their stores were not cleared before re-analysis. The batched Dynamic Data Store clear asked the store for item ids, which it cannot do; it now loads whole items in batches (#79).
- **Notification bell on tool pages** — every Powertools page showed a broken notification bell in the CMS header (its request failed with 400 and threw a script error), because the page did not carry the antiforgery token the CMS navigation reads.
- **Active Editors in the CMS 13 edit UI** — the edit-UI scripts fell back to a hard-coded `/episerver/` path, so on CMS 13 (mounted at `/Optimizely`) the Active Editors connection and feature switches failed. The path is now derived from where the shell serves the module.
- **Module files with add-ons only** — when a site referenced only an add-on (for example Forms), the core package's module zip was not copied into the site. The `.targets` files now ship in `buildTransitive/`.

### Improvements

- **`IUiStringsContributor`** — add-ons can now contribute translated strings to the JavaScript string table.
- **Forms add-on** — JavaScript strings are now translated through the new contributor mechanism.
- **CI** — the release workflow now also builds, tests and packs the Graph add-on. Pull requests also build both sample sites.
- **Dependency ranges** — packages now declare bounded CMS ranges (CMS 12: `[12.34.4, 13.0.0)`, CMS 13: `[13.2.0, 14.0.0)`; Forms 5.10.1+ / 6.x). Previously the minimum CMS version silently rose to whatever was newest when each release was packed.

## v0.10.0

- Shared property-type handlers for bulk edit and import, with bulk-edit and import fixes; batched Dynamic Data Store clears (#54, #55, #61).
- **Content Audit** — one corrupt content item no longer aborts the whole export (#63).

## v0.9.1

- **Forms add-on** — form-language filter in Forms Overview and Submissions Timeline; system columns removed from submission details; documentation and screenshots.
- **CI** — publish workflow moved to Node 24 actions.

## v0.9.0

- **Forms add-on** (`UmageAI.Optimizely.EditorPowerTools.Forms`) — Forms overview, submissions timeline, and privacy and duplicate diagnostics.
- **Scheduled Jobs Gantt** — row alignment fixed; jobs with duplicate names are told apart.

## v0.8.1

- **Packaging** — `EditorPowertools.zip` is now copied to the consuming site's publish output.

## v0.8.0

- Opti-ID routing fixes, performance clean-up, and a pre-release security pass.

## v0.7.0

- **Permissions** — localized descriptions on the admin Permissions for Functions page; removed two permissions for tools that no longer exist.
- **Audience Manager** — the Usage column now fills in.
- **Content Audit** — the CMS 13 filter row is hidden on CMS 12.

## v0.6.1

- **Packaging** — corrected the repository URL.

## v0.6.0

- **Packaging** — Excel export moved from EPPlusFree to ClosedXML; added SECURITY.md.

## v0.5.1

- **Packaging** — CMS dependencies relaxed from exact versions so sites on other patch versions can install; patched transitive System.* security dependencies; release notes generated from merged pull requests.

## v0.5.0

- **CMS 13 support** — one package now supports CMS 12 (.NET 8) and CMS 13 (.NET 10).
- **Content Type Audit** — Contract, Section and Element badges, kind and composition filters, and an applied-contracts panel (CMS 13).
- **Content Statistics** — contracts summary and block breakdown (CMS 13).
- **Bulk Property Editor** — content types and contracts tabs; choosing a contract selects every type that implements it.
- **Content Audit** — CMS 13 contract and composition filters.
- "Orphaned" renamed to "Code-less" in the UI and all 11 languages.

## v0.2.0-preview.1

### New Tools

- **Content Statistics** — Dashboard with content counts, type distribution, publishing trends, and storage metrics.

- **Language Audit** — Analyze translation coverage across languages, find missing translations, and identify stale content.

- **Security Audit** — Review access rights across the content tree, find overly permissive settings, and audit role assignments.

- **Visitor Group Tester** — Floating toolbar on the public site for testing personalization rules and inspecting personalized content.

### Improvements

- **Link Checker renamed to Link Audit** — The tool formerly known as "Link Checker" is now "Link Audit" for consistency with other audit tools.

- **Content Type Recommendations** — Now hooks into the CMS create dialog via `IContentTypeAdvisor`, providing type suggestions directly in the editor workflow.

- **Menu reordering** — Tools are now grouped by category: Content & Editorial, Audits & Analysis, and Configuration & Admin.

- **EPiServer.CMS.UI.Core** — Added as a package dependency for deeper CMS shell integration.

- **Localization** — 11 language files now included for multilingual admin UI support.

- **New feature toggles** — `ContentStatistics`, `LanguageAudit`, `SecurityAudit`, and `VisitorGroupTester` can be individually enabled/disabled.

---

## v0.1.0-preview.1

First public preview of Editor Powertools for Optimizely CMS 12.

### Tools

- **Content Type Audit** — Audit all content types with usage counts, property analysis (inherited/defined/orphaned), inheritance tree visualization, and CSV export. Drill into content of a specific type and explore soft link references.

- **Personalization Audit** — Discover where visitor groups are used across your site: access rights, content areas, and XHTML fields. Powered by a scheduled job that analyzes all content.

- **Audience Manager** — Enhanced visitor group management with search, category filtering, criteria details, and usage statistics.

- **Content Type Recommendations** — Define rules that suggest content types when editors create content under specific parents. Rules stored in DynamicDataStore with a management UI.

- **Bulk Property Editor** — Inline-edit property values across multiple content items at once. Filter by type, language, and property values. Bulk save and publish.

- **Content Importer** — Upload CSV, Excel, or JSON files and map fields to properties on a chosen content type. Preview before import, validation, and dry-run mode.

- **Activity Timeline** — Dual-column timeline of all editorial activities with version comparison, comments, and infinite scroll. Filter by user, action, content type, date range. Content Item Timeline for per-item history.

- **Scheduled Jobs Gantt** — Interactive Gantt chart showing scheduled job execution history and planned future runs. Zoom, scroll, and visual overlap detection.

- **Link Audit** (originally "Link Checker") — Comprehensive link health monitoring. Scheduled job crawls all content for internal and external links, checks status codes, and tracks history. UI with filters and direct edit-mode links.

- **CMS Doctor** — Pluggable health check dashboard. Built-in checks for content types, orphaned properties, scheduled jobs, draft content, version bloat, memory usage, broken links, missing alt text, and unused content. Extensible via `IDoctorCheck`.

- **Content Audit** — Content inventory with configurable columns, multi-column filtering and sorting, and export to Excel/CSV/JSON.

- **Power Content Details** — Assets panel widget showing detailed info about the selected content item: references, usage, content tree, versions, personalizations, and language sync.

- **Active Editors** — Real-time editor presence via SignalR. See who's online, what they're editing, and collaborate with team chat. Send CMS notifications to other editors via Optimizely's `INotifier`. Assets panel widget with Editors and Chat tabs.

- **Manage Children** — Bulk operations on child content from the navigation tree: sort, move, delete, publish, unpublish.

### Infrastructure

- Unified scheduled job that traverses content once and calls all registered `IContentAnalyzer` plugins
- Three-layer permission model: feature toggles, role-based auth policy, and per-tool CMS permissions
- Shared CSS design system with cards, tables, dialogs, badges, buttons, and tree views
- NuGet packaging with protected module zip (same pattern as official Optimizely add-ons)
- 162 unit tests covering all core business logic

### Requirements

- .NET 8
- Optimizely CMS 12 (EPiServer.CMS 12.29.0+)

### Installation

```
dotnet add package CodeArt.Optimizely.EditorPowertools
```

See [README](README.md) for setup instructions.
