# EditorPowertools

[![NuGet](https://img.shields.io/nuget/v/UmageAI.Optimizely.EditorPowerTools)](https://www.nuget.org/packages/UmageAI.Optimizely.EditorPowerTools/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A collection of power tools for Optimizely CMS 12 and CMS 13 editors and admins. Distributed as the NuGet package `UmageAI.Optimizely.EditorPowerTools`.

![Overview Dashboard](docs/screenshots/01-overview.png)

## Tools

Tools are grouped in the navigation menu for easy access.

### Content & Editorial

| Tool | Description |
|------|-------------|
| **Content Statistics** | Dashboard with content counts, type distribution, publishing trends, and storage metrics. |
| **Activity Timeline** | Dual-column timeline of editorial activities with comments, version comparison, and infinite scroll. |
| **Bulk Property Editor** | Inline-edit property values across multiple content items. Filter, sort, paginate, bulk save/publish. |
| **Content Importer** | Import content from CSV, Excel, or JSON with field mapping, preview, and validation. |

### Audits & Analysis

| Tool | Description |
|------|-------------|
| **Content Audit** | Comprehensive content inventory with configurable columns, filters, and export. |
| **Content Type Audit** | Audit all content types, usage counts, properties (inherited/defined/orphaned), inheritance tree. CSV export. |
| **Personalization Audit** | Find where audiences are used across the site - access rights, content areas, XHTML fields. Scheduled job. |
| **Language Audit** | Analyze translation coverage across languages, find missing translations, and identify stale content. |
| **Security Audit** | Review access rights across the content tree, find overly permissive settings, and audit role assignments. |
| **Link Audit** | Scan content for broken internal and external links. Friendly URLs, status codes, easy edit-mode access. |

### Configuration & Admin

| Tool | Description |
|------|-------------|
| **Content Type Recommendations** | Define rules for which content types are suggested when creating content under specific parents. Hooks into the CMS create dialog via `IContentTypeAdvisor`. |
| **Audience Manager** | Manage audiences with search, category filtering, criteria details, and usage statistics. |
| **CMS Doctor** | Pluggable health check dashboard with auto-fix capabilities. |
| **Active Editors** | Real-time editor presence, see who's editing what, team chat, and CMS notifications via SignalR. |
| **Scheduled Jobs Gantt** | Interactive Gantt chart of scheduled job execution history with zoom, scroll, and planned future runs. |

### CMS Edit Mode Integrations

| Tool | Description |
|------|-------------|
| **Power Content Details** | Assets panel widget showing detailed info about the currently selected content item. |
| **Manage Children** | Bulk operations on child content items from the navigation tree. |
| **Visitor Group Tester** | Floating toolbar on the public site for testing personalization rules and inspecting personalized content. |

## Screenshot Gallery

### Overview Dashboard
The main dashboard showing all available tools with quick-access cards.

![Overview](docs/screenshots/01-overview.png)

### Content Type Audit
Audit all content types with usage counts, property details (inherited, defined, orphaned), and inheritance tree visualization. Includes CSV export and drill-down dialogs.

![Content Type Audit](docs/screenshots/02-content-type-audit.png)

### Personalization Audit
Discover where visitor groups (audiences) are used across the site, including access rights, content areas, and XHTML fields.

![Personalization Audit](docs/screenshots/03-personalization-audit.png)

### Audience Manager
Enhanced visitor group management with search, category filtering, criteria details, and usage statistics.

![Audience Manager](docs/screenshots/04-audience-manager.png)

### Activity Timeline
Full timeline of editorial activities with dual-column layout, comments, version comparison, and infinite scroll.

![Activity Timeline](docs/screenshots/05-activity-timeline.png)

### Scheduled Jobs Gantt
Interactive Gantt chart of scheduled job execution history with zoom, scroll, duration display, and planned future runs.

![Scheduled Jobs Gantt](docs/screenshots/06-scheduled-jobs-gantt.png)

### Bulk Property Editor
Inline-edit property values across multiple content items with filtering, sorting, pagination, and bulk save/publish.

![Bulk Property Editor](docs/screenshots/07-bulk-property-editor.png)

### CMS Doctor
Pluggable health check dashboard with grouped checks, status indicators, and auto-fix capabilities.

![CMS Doctor](docs/screenshots/08-cms-doctor.png)

### Link Audit
Scan content for broken internal and external links with status codes, friendly URLs, and easy navigation to edit mode.

![Link Audit](docs/screenshots/09-link-checker.png)

### Active Editors
Real-time editor presence awareness showing who is editing what, with team chat and CMS notifications via SignalR.

![Active Editors](docs/screenshots/10-active-editors.png)

### Content Importer
Import content from CSV, Excel, or JSON with field mapping, preview, validation, and dry-run mode.

![Content Importer](docs/screenshots/11-content-importer.png)

### Content Audit
Comprehensive content inventory with configurable columns, multi-column filtering, sorting, and Excel/CSV export.

![Content Audit](docs/screenshots/12-content-audit.png)

### Content Type Recommendations
Define rules for which content types are suggested when creating content under specific parent pages. Hooks into the CMS create dialog via `IContentTypeAdvisor`.

![Content Type Recommendations](docs/screenshots/13-content-type-recommendations.png)

### Content Statistics
Dashboard with content counts, type distribution, publishing trends, and storage metrics.

### Language Audit
Analyze translation coverage across languages, find missing translations, and identify stale content.

### Security Audit
Review access rights across the content tree, find overly permissive settings, and audit role assignments.

### Visitor Group Tester
Floating toolbar on the public site for testing personalization rules and inspecting personalized content.

### CMS Edit Mode Integration
Power Content Details widget, Manage Children dialog, and Visitor Group Tester integrated directly into the CMS edit mode interface.

![CMS Edit Mode](docs/screenshots/14-cms-edit-mode.png)

## Installation

```
dotnet add package UmageAI.Optimizely.EditorPowerTools
```

### Registration

In your `Startup.cs`:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // ... other services

    services.AddEditorPowertools(options =>
    {
        // Optional: configure roles with full access (default: WebAdmins, Administrators)
        options.AuthorizedRoles = ["WebAdmins", "Administrators"];

        // Optional: enable per-tool access control via "Permissions For Functions"
        options.CheckPermissionForEachFeature = true;

        // Optional: disable specific tools
        options.Features.BulkPropertyEditor = false;
    });
}

public void Configure(IApplicationBuilder app)
{
    // ... other middleware

    app.UseEditorPowertools();

    app.UseEndpoints(endpoints =>
    {
        endpoints.MapContent();
        endpoints.MapEditorPowertools(); // Required: maps SignalR hubs and tool endpoints
    });
}
```

Or configure via `appsettings.json`:

```json
{
  "UmageAI": {
    "EditorPowerTools": {
      "authorizedRoles": ["WebAdmins", "Administrators"],
      "checkPermissionForEachFeature": true,
      "features": {
        "contentTypeAudit": true,
        "personalizationUsageAudit": true,
        "bulkPropertyEditor": true,
        "contentStatistics": true,
        "languageAudit": true,
        "securityAudit": true,
        "visitorGroupTester": true
      }
    }
  }
}
```

## Permissions

Three-layer permission model:

1. **Feature Toggles** - Enable/disable individual tools via configuration
2. **Role-Based Access** - `AuthorizedRoles` grants full access (default: WebAdmins, Administrators)
3. **Permissions For Functions** - When `CheckPermissionForEachFeature = true`, individual tools can be granted to specific users/roles via the CMS admin UI under "Permissions For Functions"

## Scheduled Jobs

Several tools require a scheduled job to collect data:

| Job | Tools |
|-----|-------|
| **Aggregate Content Type Statistics** | Content Type Audit |
| **Analyze Personalization Usage** | Personalization Audit, Audience Manager |
| **Link Audit** | Link Audit |

Run these from the CMS admin Scheduled Jobs page, or trigger them from each tool's "Run now" button.

## Documentation

- [Getting Started Guide](docs/getting-started.md) - Step-by-step installation and setup
- [Configuration Reference](docs/configuration.md) - All options, feature toggles, and settings
- [Extending CMS Doctor](docs/extending-cms-doctor.md) - Create custom health checks
- [Coding Guidelines](docs/coding-guidelines.md) - Architecture patterns and coding standards
- [Backlog](docs/backlog.md) - Planned features and improvements

## Tech Stack

- .NET 8 / .NET 10 / C# / Optimizely CMS 12 and CMS 13 (single multi-targeting NuGet package)
- Vanilla JavaScript (no framework dependencies)
- Razor SDK class library with embedded views and static assets
- DynamicDataStore (DDS) for persistence
- Protected module integration with CMS shell
- 11 language files included for localization

## Contributing

See [docs/coding-guidelines.md](docs/coding-guidelines.md) for architecture patterns and coding standards.

## License

[MIT](LICENSE) - Copyright (c) 2026 Allan Thraen / UmageAI
