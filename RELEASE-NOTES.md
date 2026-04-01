# Release Notes

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

- **Link Checker** — Comprehensive link health monitoring. Scheduled job crawls all content for internal and external links, checks status codes, and tracks history. UI with filters and direct edit-mode links.

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
