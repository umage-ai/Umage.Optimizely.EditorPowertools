# Local Development Guide

## Prerequisites

- .NET 8 SDK
- SQL Server LocalDB (included with Visual Studio, or install separately)
- A code editor (VS Code, Visual Studio, Rider)

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/CodeArtDK/EditorPowertools.git
cd EditorPowertools
```

### 2. Set up the sample site database and blobs

The sample site (Alloy demo) ships with a pre-built database and media blobs as zip files in `App_Data`. Unzip them before running:

```bash
cd src/EditorPowertools.SampleSite/App_Data
unzip -o database.zip
unzip -o blobs.zip
cd ../../..
```

This creates:
- `EditorPowertools.SampleSite.mdf` and `EditorPowertools.SampleSite_log.ldf` - SQL Server LocalDB database files
- `blobs/` - Media files (images, videos) used by the demo content

### 3. Build and run

```bash
dotnet build
dotnet run --project src/EditorPowertools.SampleSite
```

The site starts at **https://localhost:5000/**

### 4. Log in

Navigate to https://localhost:5000/EPiServer/CMS and use:

| | |
|---|---|
| **URL** | https://localhost:5000/EPiServer/CMS |
| **Username** | Admin |
| **Password** | (set on first login via the admin registration page) |

The sample site uses `AddAdminUserRegistration()` which presents a registration form on first visit. Create an admin user with any password you choose.

### 5. Access Editor Powertools

After logging in, find **Editor Powertools** in the top navigation menu (under the global menu). Or navigate directly:

- **Overview**: https://localhost:5000/EPiServer/EditorPowertools/EditorPowertools/Overview
- **Content Type Audit**: https://localhost:5000/EPiServer/EditorPowertools/EditorPowertools/ContentTypeAudit

## Project Structure

```
EditorPowertools/
  src/
    EditorPowertools/                    # Plugin class library (the NuGet package)
      Configuration/                     # Options, feature toggles
      Infrastructure/                    # DI registration, middleware
      Menu/                              # CMS menu provider
      Permissions/                       # Permission types, access checker
      Services/                          # Shared services (DDS stores, preferences, jobs)
      Components/                        # Shared UI components (content picker, preferences API)
      Tools/                             # One folder per tool
        ContentTypeAudit/                # Service + Controller + Models
        PersonalizationAudit/            # Service + Controller + Job + Models
        AudienceManager/                 # ...
        ContentTypeRecommendations/
        BulkPropertyEditor/
        ScheduledJobsGantt/
        ActivityTimeline/
        LinkChecker/
        ContentDetails/
        Overview/                        # Main controller for page views
      Views/                             # Razor views per tool
      modules/_protected/EditorPowertools/
        ClientResources/
          css/editorpowertools.css        # Shared design system
          js/editorpowertools.js          # Shared JS utilities (EPT namespace)
          js/components.js                # Content picker, content type picker
          js/content-type-audit.js        # Per-tool JS files
          js/personalization-audit.js
          js/audience-manager.js
          js/content-type-recommendations.js
          js/bulk-property-editor.js
          js/scheduled-jobs-gantt.js
          js/activity-timeline.js
          js/link-checker.js
          js/ContentDetailsWidget.js      # Dojo AMD widget for assets panel
        module.config                     # Optimizely module configuration

    EditorPowertools.SampleSite/         # Alloy demo site for development
      App_Data/                          # Database + blobs (unzip first)
      Models/                            # Alloy content types
      Business/                          # Site-specific code
      Views/                             # Site templates
      Startup.cs                         # Site configuration

  docs/
    backlog.md                           # Feature backlog
    coding-guidelines.md                 # Architecture and coding standards
    local-development.md                 # This file
```

## Running Scheduled Jobs

Several tools require scheduled jobs to collect data. In the CMS admin:

1. Go to **Admin** > **Scheduled Jobs**
2. Find and run:
   - **[EditorPowertools] Aggregate Content Type Statistics** - for Content Type Audit
   - **[EditorPowertools] Analyze Personalization Usage** - for Personalization Audit and Audience Manager
   - **[EditorPowertools] Link Audit** - for Link Audit

Or use the "Run now" button on each tool's page.

## Making Changes

### Adding a new tool

Follow [docs/coding-guidelines.md](coding-guidelines.md). In short:

1. Create `Tools/{ToolName}/` with Service, Controller, Models
2. Create `Views/{ToolName}/Index.cshtml`
3. Create `modules/_protected/.../js/{tool-name}.js`
4. Add feature toggle in `FeatureToggles.cs`
5. Add permission in `EditorPowertoolsPermissions.cs`
6. Add menu item in `EditorPowertoolsMenuProvider.cs`
7. Add controller action in `OverviewController.cs`
8. Add card on the Overview page
9. Register services in `ServiceCollectionExtensions.cs`

### Static files

Static files (JS, CSS) go in `modules/_protected/EditorPowertools/ClientResources/`, NOT in `wwwroot/`. They are served by the Optimizely protected module system.

After changing static files, you need to **restart the site** for the Razor SDK to pick up changes (they are compiled into the assembly on build).

### Design system

All tools use the shared design system in `editorpowertools.css`. CSS classes are prefixed with `ept-`. Shared JS utilities are in the `EPT` global object (defined in `editorpowertools.js`).

## Troubleshooting

### Database not found
Make sure you unzipped `database.zip` in `App_Data/`. The connection string uses LocalDB with `AttachDbFilename`.

### Blobs/images missing
Unzip `blobs.zip` in `App_Data/`.

### JS/CSS changes not showing
The Razor SDK embeds static files at build time. Restart the site after changing files in `ClientResources/`.

### "Access denied" when accessing tools
Check that your user is in the `WebAdmins` or `Administrators` role. Or enable `CheckPermissionForEachFeature` and grant access via Admin > Permissions For Functions.
