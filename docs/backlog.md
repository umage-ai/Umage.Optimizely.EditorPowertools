# EditorPowertools - Tool Backlog

Tools carried over from the old project (re-implemented with new UI) and new additions.

## Carried Over (from old Blazor project - re-implement)

- [ ] **Content Type Audit** - Audit all content types: name, base type, group, property count, usage count. Filterable, CSV export.
- [ ] **Personalization Usage Audit** - Report where visitor groups are used (access rights, content areas, XHTML). Requires scheduled job to analyze.
- [ ] **Content Type Recommendations** - Rules engine suggesting content types when creating content under specific parents. Rules stored in DynamicDataStore.
- [ ] **Audience Manager** - Enhanced visitor group management with usage statistics and criteria counts.
- [ ] **Power Content Details** - Assets panel widget showing detailed info about the currently selected content item (soft links, references, language versions).

## Activity & Timeline

- [ ] **Site Activity Timeline** - Full timeline of all activities on the site using ActivityLog. Show details about specific changes/versions. Filterable by date range, user, content type.
- [ ] **Content Item Timeline** - Per-item timeline showing the full history of a single content item. Based on ActivityLog + IContentVersionRepository. Shows who did what, when, and what changed.

## Scheduled Jobs

- [ ] **Scheduled Jobs Gantt Diagram** - Interactive Gantt chart showing scheduled job execution history and planned execution. Uses job log/history to show when jobs ran, duration, success/failure status. Visual overview of job scheduling and overlaps.

## Bulk Operations

- [ ] **Bulk Property Editor** - Edit a property value across multiple content items at once. (Details TBD - existing implementation to be provided.)

## Editor Collaboration

- [ ] **Active Editors Widget** - Shows who is currently logged in as editors, what content they're working on, and provides a way to message them. Real-time presence awareness for the editorial team.

## Personalization Tools

- [ ] **Personalization Usage Audit** *(carried over, see above)*
- [ ] **Audience Manager** *(carried over, see above)*
- [ ] **Visitor Group Tester** - Test/preview personalization rules for a specific user profile without impersonating.
- [ ] **Personalization Coverage Report** - Overview of which pages/content areas have personalization applied and which don't.
- [ ] **Visitor Group Dependency Map** - Visualize which visitor groups are used where and how they relate to each other.

## Health & Diagnostics

- [ ] **CMS Health Check Dashboard** - Pluggable health check system showing how the CMS is doing. Each check reports status (healthy/warning/critical) with actionable suggestions. Extensible so custom checks can be added. Example checks:
  - Orphaned content detection
  - Missing media references
  - Unused content types
  - Database size / blob storage usage
  - Scheduled job failures
  - Content publish queue status
  - Cache hit ratios
  - Configuration issues (missing settings, deprecated features)

## Content Tools

- [ ] **Orphaned Content Finder** - Find content not linked from anywhere
- [ ] **Content Tree Exporter** - Export content tree structure to CSV
- [ ] **Missing Alt Text Report** - Find images missing alt text
- [ ] **Broken Link Checker** - Scan content for broken internal/external links

## Media Tools

- [ ] **Unused Media Cleaner** - Find and optionally remove media files not referenced by any content
- [ ] **Media Usage Report** - Show where each media item is used
- [ ] **Duplicate Media Finder** - Find duplicate media files (by hash or name)

## Admin Tools

- [ ] **Cache Inspector** - View and manage CMS cache entries
- [ ] **Content Type Diff** - Compare content types between environments
- [ ] **Environment Info Panel** - CMS version, loaded assemblies, config summary

## Editor UX

- [ ] **Quick Publish Widget** - Batch publish selected content items
- [ ] **Editor Bookmarks** - Bookmark frequently edited pages
- [ ] **Content Version Diff** - Side-by-side diff of content versions

## SEO Tools

- [ ] **SEO Checklist** - Per-page checklist for SEO best practices
- [ ] **Redirect Manager** - UI for managing URL redirects
- [ ] **Sitemap Validator** - Validate sitemap entries against actual content

---

*Priority: Carried-over tools first, then Activity Timeline, Scheduled Jobs Gantt, Bulk Edit, Active Editors, Health Check.*
