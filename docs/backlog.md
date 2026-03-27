# EditorPowertools - Tool Backlog

Tools carried over from the old project (re-implemented with new UI) and new additions.

## Carried Over (from old Blazor project - re-implement)

- [x] **Content Type Audit** - Audit all content types, usage counts, properties (inherited/defined/orphaned), inheritance tree, CSV export. Includes drill-down dialogs for content of type and soft link references.
- [x] **Personalization Usage Audit** - Report where visitor groups are used (access rights, content areas, XHTML). Requires scheduled job to analyze.
- [x] **Content Type Recommendations** - Rules engine suggesting content types when creating content under specific parents. Rules stored in DynamicDataStore.
- [x] **Audience Manager** - Enhanced visitor group management with usage statistics and criteria counts.
- [x] **Power Content Details** - Assets panel widget showing detailed info about the currently selected content item (soft links, references, language versions).

## Activity & Timeline

- [x] **Site Activity Timeline** - Full timeline of all activities on the site using ActivityLog. Show details about specific changes/versions. Filterable by date range, user, content type.
- [ ] **Content Item Timeline** - Per-item timeline showing the full history of a single content item. Based on ActivityLog + IContentVersionRepository. Shows who did what, when, and what changed.

## Scheduled Jobs

- [x] **Scheduled Jobs Gantt Diagram** - Interactive Gantt chart showing scheduled job execution history and planned execution. Uses job log/history to show when jobs ran, duration, success/failure status. Visual overview of job scheduling and overlaps.

## Bulk Operations

- [x] **Bulk Property Editor** - Edit a property value across multiple content items at once. (Details TBD - existing implementation to be provided.)
- [ ] **Content Importer** - Upload Excel, CSV, or JSON files, then map fields to properties on a chosen content type. Pick a target location in the content tree and import. Supports preview before import, validation, and dry-run mode.

## Editor Collaboration

- [ ] **Active Editors Widget** - Shows who is currently logged in as editors, what content they're working on, and provides a way to message them. Real-time presence awareness for the editorial team.

## Personalization Tools

- [ ] **Personalization Usage Audit** *(carried over, see above)*
- [ ] **Audience Manager** *(carried over, see above)*
- [ ] **Visitor Group Tester** - Test/preview personalization rules for a specific user profile without impersonating.
- [ ] **Personalization Coverage Report** - Overview of which pages/content areas have personalization applied and which don't.
- [ ] **Visitor Group Dependency Map** - Visualize which visitor groups are used where and how they relate to each other.

## Visualization

- [ ] **Content Connection Visualizer** - Interactive D3.js visualization of page connections, interlinks, and blocks-in-blocks within a page. Shows how content items relate to each other through links, content areas, and block hierarchies. Dynamic, beautiful, and interactive graph exploration.

## Health & Diagnostics

- [ ] **CMS Health Check Dashboard** - Pluggable health check system showing how the CMS is doing. Inspired by [CodeArt Optimizely Health Checker](https://www.codeart.dk/blog/2021/5/new-project-optimizely-episerver-health-checker/) and its accompanying repo. Each check reports status (healthy/warning/critical) with actionable suggestions. Extensible so custom checks can be added. Example checks:
  - Orphaned content detection
  - Missing media references
  - Unused content types
  - Database size / blob storage usage
  - Scheduled job failures
  - Content publish queue status
  - Cache hit ratios
  - Configuration issues (missing settings, deprecated features)

## Content Tools

- [ ] **Content Audit** - Comprehensive content inventory with configurable columns, filters, and Excel export. Inspired by the NTI export (75K+ items, 5 sheets). Must handle large sites with server-side pagination.

  **Sheet 1: Content Details** (main sheet, all content items)
  Columns (28 in reference export):
  - Content ID, Language, Content Type, Main Type (Page/Block/Media/Catalog/Other)
  - Url (friendly), Editmode Url, Editmode Url owner page (for "for this page" blocks)
  - Site Name (for multi-site), Breadcrumb, Name
  - DraftStatus (Published/Draft/Ready to Publish/etc.)
  - Created By, Created, Changed By, Changed, Saved By, Saved
  - Published (start publish date), Published until (stop publish date)
  - Master Language, Languages (all language versions)
  - Read ACL (who can read), Publish ACL (who can publish) - resolved role/user names
  - **Computed: How many places is it used** (reference count)
  - **Computed: Where is it used** (list of referencing pages with names)
  - **Computed: How many versions** (version count)
  - **Computed: List of reviewers** (approval flow info)
  - **Computed: Visitor Groups used** (personalization on this content)

  **Sheet 2: Visitor Group Details** (audience configuration dump)
  - Name, Notes, Criteria Operator (All/Any), Is Security Role, Enable Statistics
  - Points Threshold, Criteria Name, Criteria Type, Criteria Model (JSON), Required, Points

  **Sheet 3: Visitor Group Usage** (where personalization is used)
  - Page Id, Page Name, Visitor Group, Property Name, Content Name, Content Type, Content Id

  **Sheet 4: Form Usage** (requires Forms package - separate NuGet)
  - Form Id, Name, Edit link, Title, Description
  - Anonymous Submissions, Allow Data Feed, Allow Multiple Submissions, Store submission data
  - Show Summary, Show Navigation, No. of elements, No. of steps
  - Redirect on submission, Finalized/Partial Retention Period
  - Webhooks, Emails, Used on (page references)
  - No. of stored submissions, Last submission, First submission

  **Sheet 5: Invalid/Orphaned Content** (custom validation rules)
  - Content with missing required data, broken references, expired content, etc.

  **UI Features:**
  - Server-side pagination (essential for 75K+ items)
  - Column picker (choose which of the 28 columns to show)
  - Multi-column filtering with operators (contains, equals, startsWith, isEmpty, isNotEmpty)
  - Multi-column sorting
  - Export to Excel (.xlsx) with multiple sheets, or CSV for single sheet
  - Quick filters: "Show only blocks", "Show only pages", "Show unpublished", "Show unused content"
  - Saved filter presets (via user preferences)

  **Architecture:** Uses the unified scheduled job to pre-compute expensive columns (reference counts, usage, version counts). Direct columns (name, dates, ACL) loaded on-demand per page.
- [ ] **Content Statistics** - Dashboards showing content type distribution, content creation over time, content age analysis (oldest content), editor activity statistics, top 10 most active editors (per language). Some graphs can appear on the overview page.
- [ ] **Orphaned Content Finder** - Find content not linked from anywhere
- [ ] **Content Tree Exporter** - Export content tree structure to CSV
- [ ] **Missing Alt Text Report** - Find images missing alt text
- [x] **Link Checker** - Comprehensive link health monitoring. Scheduled job crawls all content to catalog internal and external (outbound) links. Checks link status (200/301/404/timeout/etc.), tracks history over time. UI shows broken links with filters by status code, content type, internal/external. Links to affected content items for easy fixing. Uses the shared aggregation scheduled job for link discovery, with a separate background check for outbound URL validation.
- [ ] **Content Cleanup Tool** - Overview of stale drafts, never-published content, expired content, and old versions. Helps editors and admins identify content that can be cleaned up. Supports bulk delete/publish/archive actions. (Existing implementation in another project to reference.)
- [ ] **Content Lifecycle Manager** - Define review intervals for content to ensure it stays relevant. Editors set review cadence per content item via an assets panel widget (e.g. "review every 6 months"). A full-page overview shows all content due for review, overdue items, and review history. Supports bulk actions (mark as reviewed, snooze, reassign). Data stored in DDS.

## Language & Translation

- [ ] **Language Audit** - Comprehensive language version analysis for multilingual sites. Key features:
  - Overview dashboard: total content per language, publish rates, coverage percentages
  - **Missing translations**: hierarchical view showing which branches/subtrees are missing specific languages. E.g. "Marketing > Campaigns: 45 pages in English, only 12 in German"
  - **Stale translations**: content where one language was updated significantly more recently than others. E.g. "English version updated 2 days ago, German version last updated 8 months ago" - configurable staleness threshold
  - **Last published per language**: when each language was last published, with timeline visualization
  - **Translation coverage by content type**: which content types have the best/worst translation coverage
  - **Translation queue**: exportable list of content needing translation, sorted by priority (most visited pages first, or most recently updated master language)
  - Filters: by language pair, content type, site, subtree, staleness threshold
  - Export to Excel with per-language sheets
  - Uses the unified scheduled job to pre-compute language statistics

## Media Tools

- [ ] **Unused Media Cleaner** - Find and optionally remove media files not referenced by any content
- [ ] **Media Usage Report** - Show where each media item is used
- [ ] **Duplicate Media Finder** - Find duplicate media files (by hash or name)

## Settings & Configuration

- [ ] **PowerTools Settings Area** - In-app settings UI for configuring EditorPowertools options. Toggle features on/off, configure permissions, set scheduled job intervals, and manage tool-specific settings without editing appsettings.json. Persisted via DDS or options pattern.

## Admin Tools

- [ ] **Content Type Visibility Manager** - Configure which content types are hidden from the normal content type selection dialog (e.g. accordion items that should only appear in accordion containers).
- [ ] **Cache Inspector** - View and manage CMS cache entries
- [ ] **Content Type Diff** - Compare content types between environments
- [ ] **Environment Info Panel** - CMS version, loaded assemblies, config summary

## Content Calendar

- [ ] **Content Calendar** - Calendar view showing when content was published and when it's scheduled to be published. Monthly/weekly view with content items placed on their publish dates. Click to navigate to the content item. Filter by content type, language, editor. Shows both past publications and future scheduled publishes.

## Fun & Easter Eggs

- [ ] **Konami Code Easter Egg** - Hidden feature toggled in PowerTools settings. When enabled, entering the Konami code (↑↑↓↓←→←→BA) in a text field in edit mode triggers a fun classic game popup (Breakout, Sudoku, or similar). A little surprise for editors who discover it.

## Editor UX

- [ ] **Quick Publish Widget** - Batch publish selected content items
- [ ] **Editor Bookmarks** - Bookmark frequently edited pages
- [ ] **Content Version Diff** - Side-by-side diff of content versions

## SEO Tools

- [ ] **SEO Checklist** - Per-page checklist for SEO best practices
- [ ] **Redirect Manager** - UI for managing URL redirects
- [ ] **Sitemap Validator** - Validate sitemap entries against actual content

## Forms Management (Separate NuGet: `CodeArt.Optimizely.EditorPowertools.Forms`)

*Separate project/NuGet package with dependency on EPiServer.Forms.*

- [ ] **Form Manager** - Comprehensive form management tool:
  - List all forms on the site with property counts
  - Detect forms with duplicate properties
  - Track which forms are used the most (submission counts)
  - List recent form submissions
  - GDPR data audit: identify where old personal data exists
  - GDPR cleanup: tools to purge old form submission data based on age/form

---

## Shared Infrastructure

- [x] **Unified Scheduled Job** - Merge the three current jobs (Content Type Statistics, Personalization Analysis, Link Checker) into a single pluggable job that traverses content once. Each "analyzer" plugin registers via DI and receives each content item during traversal. This avoids scanning all content 3+ times. Architecture: `IContentAnalyzer` interface with `Analyze(IContent, ContentReference)` method, registered as `IEnumerable<IContentAnalyzer>`. The job iterates all content once and calls each analyzer. Each analyzer stores its own results in its own DDS store.
- [x] **Aggregation Scheduled Job** (legacy) - Single scheduled job that traverses all content once and collects statistics for all tools (content type counts, personalization usage, etc.). Results stored in DDS.
- [x] **Shared UI Design System** - CSS design system with cards, tables, dialogs, badges, buttons, tree views. Defined in `wwwroot/css/editorpowertools.css`.
- [x] **Overview Page** - Dashboard showing all available tools with cards. Will later include content statistics graphs.
- [ ] **Module Packaging** - Wrap everything in the modules/_protected folder in a zip file (like Optimizely's own modules), for proper deployment packaging in the NuGet.
- [ ] **Protected Module API Routes** - All API routes and JS fetch URLs should go through the proper protected module path resolution instead of hardcoded `/editorpowertools/api/` paths.

*Priority: Carried-over tools first, then Activity Timeline, Scheduled Jobs Gantt, Bulk Edit, Active Editors, Health Check.*
