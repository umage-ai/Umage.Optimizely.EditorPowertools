# Configuration Reference

Complete reference for all EditorPowertools configuration options.

## Configuration Methods

EditorPowertools can be configured in two ways. Both can be used together; code-based options take precedence.

### Code-Based Configuration

Configure options in `Startup.cs` via the `AddEditorPowertools` lambda:

```csharp
services.AddEditorPowertools(options =>
{
    options.AuthorizedRoles = ["WebAdmins", "Administrators", "PowerUsers"];
    options.CheckPermissionForEachFeature = true;
    options.Features.ContentImporter = false;
    options.Features.BulkPropertyEditor = false;
});
```

### appsettings.json Configuration

Settings are read from the `CodeArt:EditorPowertools` section:

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["WebAdmins", "Administrators"],
      "checkPermissionForEachFeature": true,
      "features": {
        "contentTypeAudit": true,
        "personalizationUsageAudit": true,
        "contentTypeRecommendations": true,
        "audienceManager": true,
        "contentDetails": true,
        "brokenLinkChecker": true,
        "orphanedContentFinder": true,
        "unusedMediaCleaner": true,
        "contentExporter": true,
        "bulkPropertyEditor": true,
        "scheduledJobsGantt": true,
        "activityTimeline": true,
        "contentImporter": true,
        "manageChildren": true,
        "contentAudit": true,
        "cmsDoctor": true,
        "activeEditors": true,
        "activeEditorsChat": true
      }
    }
  }
}
```

## EditorPowertoolsOptions Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `AuthorizedRoles` | `string[]` | `["WebAdmins", "Administrators"]` | Roles that have full access to all EditorPowertools features. Users in these roles bypass per-feature permission checks. |
| `CheckPermissionForEachFeature` | `bool` | `false` | When `true`, each tool also checks the user's Optimizely "Permissions For Functions" in addition to role-based access. This enables granular per-tool access control via the CMS admin UI. |
| `Features` | `FeatureToggles` | All `true` | Feature toggles to enable or disable individual tools. See below. |

## Feature Toggles

All features are **enabled by default** (`true`). Set a feature to `false` to disable it entirely -- it will not appear in the menu or dashboard, and its API endpoints will return 403.

| Property | JSON Key | Tool | Description |
|----------|----------|------|-------------|
| `ContentTypeAudit` | `contentTypeAudit` | Content Type Audit | Audit all content types with usage counts, properties, and inheritance. |
| `PersonalizationUsageAudit` | `personalizationUsageAudit` | Personalization Audit | Find where visitor groups are used across the site. |
| `ContentTypeRecommendations` | `contentTypeRecommendations` | Content Type Recommendations | Define rules for suggested content types under specific parents. |
| `AudienceManager` | `audienceManager` | Audience Manager | Enhanced visitor group management with usage statistics. |
| `ContentDetails` | `contentDetails` | Power Content Details | Assets panel widget with detailed content information. |
| `BrokenLinkChecker` | `brokenLinkChecker` | Link Checker | Scan for broken internal and external links. |
| `OrphanedContentFinder` | `orphanedContentFinder` | Orphaned Content Finder | Find content not linked from anywhere. |
| `UnusedMediaCleaner` | `unusedMediaCleaner` | Unused Media Cleaner | Find unused media files. |
| `ContentExporter` | `contentExporter` | Content Exporter | Export content tree structures. |
| `BulkPropertyEditor` | `bulkPropertyEditor` | Bulk Property Editor | Inline-edit properties across multiple content items. |
| `ScheduledJobsGantt` | `scheduledJobsGantt` | Scheduled Jobs Gantt | Interactive Gantt chart of job execution history. |
| `ActivityTimeline` | `activityTimeline` | Activity Timeline | Timeline of editorial activities. |
| `ContentImporter` | `contentImporter` | Content Importer | Import content from CSV, Excel, or JSON. |
| `ManageChildren` | `manageChildren` | Manage Children | Bulk operations on child content items. |
| `ContentAudit` | `contentAudit` | Content Audit | Comprehensive content inventory with filters and export. |
| `CmsDoctor` | `cmsDoctor` | CMS Doctor | Pluggable health check dashboard. |
| `ActiveEditors` | `activeEditors` | Active Editors | Real-time editor presence and activity. |
| `ActiveEditorsChat` | `activeEditorsChat` | Active Editors Chat | Team chat within the Active Editors widget. Requires `ActiveEditors` to also be enabled. |

## Permission Model

EditorPowertools uses a three-layer permission model. Each layer is checked in order; a user needs to pass all applicable layers to access a tool.

### Layer 1: Feature Toggles

If a feature is toggled off (set to `false`), it is completely disabled for everyone. The menu item is hidden, the page returns 403, and API endpoints are blocked.

### Layer 2: Role-Based Access (AuthorizedRoles)

Users in any of the `AuthorizedRoles` have full access to all enabled features. This is the primary access control mechanism.

Default roles: `WebAdmins` and `Administrators`.

```csharp
options.AuthorizedRoles = ["WebAdmins", "Administrators", "ContentEditors"];
```

```json
"authorizedRoles": ["WebAdmins", "Administrators", "ContentEditors"]
```

The authorization policy name is `codeart:editorpowertools`. All EditorPowertools controllers use this policy.

### Layer 3: Permissions For Functions (Optional)

When `CheckPermissionForEachFeature` is set to `true`, the plugin also checks Optimizely's built-in "Permissions For Functions" system. This allows you to grant access to individual tools for users or roles that are not in `AuthorizedRoles`.

To configure:

1. Set `CheckPermissionForEachFeature = true`
2. Go to **Admin** > **Access Rights** > **Permissions For Functions** in the CMS
3. Find the EditorPowertools entries (one per tool)
4. Grant access to specific users or roles

This is useful when you want most editors to see only a subset of tools. For example, you might grant all editors access to Activity Timeline and Content Audit, while restricting Bulk Property Editor and Content Importer to senior editors.

## Example Configurations

### Development (all features, no restrictions)

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["WebAdmins", "Administrators", "WebEditors"],
      "checkPermissionForEachFeature": false
    }
  }
}
```

### Production (restricted, per-tool permissions)

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["WebAdmins"],
      "checkPermissionForEachFeature": true,
      "features": {
        "contentImporter": false,
        "bulkPropertyEditor": false
      }
    }
  }
}
```

### Minimal (only diagnostics tools)

```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["Administrators"],
      "features": {
        "contentTypeAudit": true,
        "cmsDoctor": true,
        "scheduledJobsGantt": true,
        "activityTimeline": true,
        "contentAudit": true,
        "personalizationUsageAudit": false,
        "contentTypeRecommendations": false,
        "audienceManager": false,
        "contentDetails": false,
        "brokenLinkChecker": false,
        "orphanedContentFinder": false,
        "unusedMediaCleaner": false,
        "contentExporter": false,
        "bulkPropertyEditor": false,
        "contentImporter": false,
        "manageChildren": false,
        "activeEditors": false,
        "activeEditorsChat": false
      }
    }
  }
}
```

## Environment-Specific Overrides

Use the standard ASP.NET Core configuration layering to override per environment:

**appsettings.Development.json:**
```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["Everyone"]
    }
  }
}
```

**appsettings.Production.json:**
```json
{
  "CodeArt": {
    "EditorPowertools": {
      "authorizedRoles": ["WebAdmins"],
      "checkPermissionForEachFeature": true
    }
  }
}
```
