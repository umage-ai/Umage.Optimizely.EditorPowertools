# JS Localization Design

**Date:** 2026-04-03  
**Status:** Approved

## Problem

All user-facing text inside the JavaScript UI (column labels, button text, stat labels, tab names, dialog prompts, status badges, empty states, alerts) is hardcoded English. The Optimizely XML localization system that already works for server-side strings does not automatically cover browser-side rendering.

## Solution

Inject all JS UI strings server-side into the page via `window.EPT_STRINGS`, populated from `LocalizationService` in the Razor layout. Zero additional HTTP requests. JS files read from this object instead of using hardcoded literals.

---

## Architecture

### 1. XML Structure

A new `<ui>` section is added under `<editorpowertools>` in all 11 language files (`en.xml`, `da.xml`, `sv.xml`, `no.xml`, `de.xml`, `fi.xml`, `fr.xml`, `es.xml`, `nl.xml`, `ja.xml`, `zh-cn.xml`).

Path convention: `/editorpowertools/ui/{tool}/{key}`

```xml
<editorpowertools>
  <!-- ...existing sections... -->
  <ui>
    <shared>
      <loading>Loading...</loading>
      <noresults>No results found</noresults>
      <cancel>Cancel</cancel>
      <apply>Apply</apply>
      <close>Close</close>
      <runnow>Run now</runnow>
      <refresh>Refresh</refresh>
      <starting>Starting...</starting>
      <export>Export</export>
      <search_placeholder>Search...</search_placeholder>
      <prev>Prev</prev>
      <next>Next</next>
      <yes>Yes</yes>
      <no>No</no>
      <open>Open</open>
      <all>All</all>
      <error_prefix>Error: {0}</error_prefix>
    </shared>
    <contentaudit>
      <!-- Column labels -->
      <col_contentid>Content ID</col_contentid>
      <col_name>Name</col_name>
      <col_language>Language</col_language>
      <col_contenttype>Content Type</col_contenttype>
      <col_maintype>Main Type</col_maintype>
      <col_url>URL</col_url>
      <col_editurl>Edit URL</col_editurl>
      <col_breadcrumb>Breadcrumb</col_breadcrumb>
      <col_status>Status</col_status>
      <col_createdby>Created By</col_createdby>
      <col_created>Created</col_created>
      <col_changedby>Changed By</col_changedby>
      <col_changed>Changed</col_changed>
      <col_published>Published</col_published>
      <col_publisheduntil>Published Until</col_publisheduntil>
      <col_masterlanguage>Master Language</col_masterlanguage>
      <col_alllanguages>All Languages</col_alllanguages>
      <col_referencecount>Reference Count</col_referencecount>
      <col_versioncount>Version Count</col_versioncount>
      <col_haspersonalizations>Has Personalizations</col_haspersonalizations>
      <!-- Quick filters -->
      <filter_all>All content</filter_all>
      <filter_pages>Pages only</filter_pages>
      <filter_blocks>Blocks only</filter_blocks>
      <filter_media>Media only</filter_media>
      <filter_unpublished>Unpublished</filter_unpublished>
      <filter_unused>Unused content</filter_unused>
      <!-- Filter operators -->
      <op_contains>Contains</op_contains>
      <op_equals>Equals</op_equals>
      <op_startswith>Starts with</op_startswith>
      <op_isempty>Is empty</op_isempty>
      <op_isnotempty>Is not empty</op_isnotempty>
      <!-- Toolbar buttons -->
      <btn_filter>Filter</btn_filter>
      <btn_columns>Columns</btn_columns>
      <btn_export>Export</btn_export>
      <btn_selectall>Select all</btn_selectall>
      <btn_reset>Reset</btn_reset>
      <btn_addfilter>Add filter</btn_addfilter>
      <btn_clearall>Clear all</btn_clearall>
      <!-- Dialog labels -->
      <dlg_choosecolumns>Choose Columns</dlg_choosecolumns>
      <dlg_addfilter>Add Filter</dlg_addfilter>
      <lbl_column>Column</lbl_column>
      <lbl_operator>Operator</lbl_operator>
      <lbl_value>Value</lbl_value>
      <lbl_filtervalue>Filter value...</lbl_filtervalue>
      <!-- Stats labels -->
      <stat_totalitems>Total items</stat_totalitems>
      <stat_pages>Pages</stat_pages>
      <stat_currentpage>Current page</stat_currentpage>
      <!-- Table states -->
      <empty_nodata>No data loaded.</empty_nodata>
      <empty_nofilter>No content matches the current filters.</empty_nofilter>
      <!-- Pagination -->
      <page_showing>Showing {0}-{1} of {2}</page_showing>
      <!-- Per-page selector -->
      <perpage>{0} per page</perpage>
      <!-- Export formats -->
      <fmt_excel>Excel (.xlsx)</fmt_excel>
      <fmt_csv>CSV (.csv)</fmt_csv>
      <fmt_json>JSON (.json)</fmt_json>
    </contentaudit>
    <languageaudit>
      <!-- Tab labels -->
      <tab_overview>Overview</tab_overview>
      <tab_missing>Missing Translations</tab_missing>
      <tab_stale>Stale Translations</tab_stale>
      <tab_queue>Translation Queue</tab_queue>
      <!-- Stat labels -->
      <stat_totalcontent>Total Content</stat_totalcontent>
      <stat_languages>Languages</stat_languages>
      <stat_missing>Missing Translations</stat_missing>
      <stat_stale>Stale (30+ days)</stat_stale>
      <stat_itemstotranslate>Items to Translate</stat_itemstotranslate>
      <stat_page>Page</stat_page>
      <!-- Overview card -->
      <card_coverage>Language Coverage</card_coverage>
      <empty_nodata>No language data available. Run the aggregation job to collect statistics.</empty_nodata>
      <!-- Toolbar labels -->
      <lbl_language>Language:</lbl_language>
      <lbl_threshold>Threshold (days):</lbl_threshold>
      <lbl_targetlang>Target Language:</lbl_targetlang>
      <lbl_contenttype>Content Type:</lbl_contenttype>
      <lbl_alllanguages>All languages</lbl_alllanguages>
      <lbl_alltypes>All types</lbl_alltypes>
      <!-- Buttons -->
      <btn_coveragetree>Coverage Tree</btn_coveragetree>
      <btn_apply>Apply</btn_apply>
      <btn_exportcsv>Export CSV</btn_exportcsv>
      <!-- Table columns -->
      <col_id>ID</col_id>
      <col_name>Name</col_name>
      <col_type>Type</col_type>
      <col_master>Master</col_master>
      <col_available>Available Languages</col_available>
      <col_location>Location</col_location>
      <col_masterlanguage>Master Language</col_masterlanguage>
      <col_stalelanguage>Stale Language</col_stalelanguage>
      <col_daysbehind>Days Behind</col_daysbehind>
      <col_lastupdated>Last Updated</col_lastupdated>
      <col_available_short>Available</col_available_short>
      <!-- Empty states (use {0} for language) -->
      <empty_all_translated>All content has been translated to {0}</empty_all_translated>
      <empty_no_stale>No stale translations found (threshold: {0} days)</empty_no_stale>
      <empty_no_queue>No content needs translation to {0}</empty_no_queue>
      <empty_no_tree>No coverage tree data available</empty_no_tree>
      <!-- Run job banner -->
      <banner_run_job>Run the [EditorPowertools] Content Analysis scheduled job to populate data.</banner_run_job>
      <btn_runnow>Run now</btn_runnow>
      <btn_started>Job started, please refresh in a few minutes.</btn_started>
      <btn_failed>Failed to start job</btn_failed>
      <!-- Stat labels with language substitution -->
      <stat_missing_lang>Missing {0}</stat_missing_lang>
      <stat_stale_count>Stale Translations</stat_stale_count>
      <!-- Language card -->
      <card_contentitems>{0} content items</card_contentitems>
      <card_published>{0} published</card_published>
      <!-- Queue pagination -->
      <page_info>Page {0} of {1}</page_info>
      <btn_previous>Previous</btn_previous>
      <btn_next>Next</btn_next>
    </languageaudit>
    <cmsdoctor>
      <!-- Header -->
      <header_title>CMS Doctor</header_title>
      <header_desc>Health checks for your Optimizely CMS. Extensible by third-party packages.</header_desc>
      <lbl_lastrun>Last run: {0}</lbl_lastrun>
      <btn_runall>Run All Checks</btn_runall>
      <btn_running>Running...</btn_running>
      <!-- Summary labels -->
      <sum_healthy>Healthy</sum_healthy>
      <sum_warnings>Warnings</sum_warnings>
      <sum_faults>Faults</sum_faults>
      <sum_notchecked>Not Checked</sum_notchecked>
      <!-- Tag filter -->
      <tag_all>All</tag_all>
      <!-- Status labels (matching check.status values) -->
      <status_ok>Healthy</status_ok>
      <status_warning>Warning</status_warning>
      <status_badpractice>Bad Practice</status_badpractice>
      <status_fault>Fault</status_fault>
      <status_performance>Performance</status_performance>
      <status_notchecked>Not Checked</status_notchecked>
      <!-- Card action buttons -->
      <btn_run>Run</btn_run>
      <btn_fix>Fix</btn_fix>
      <btn_details>Details</btn_details>
      <btn_dismiss>Dismiss</btn_dismiss>
      <btn_restore>Restore</btn_restore>
      <!-- Detail dialog -->
      <dlg_result>Result:</dlg_result>
      <dlg_details>Details:</dlg_details>
      <dlg_categories>Categories:</dlg_categories>
      <dlg_checked>Checked: {0}</dlg_checked>
      <btn_applyfix>Apply Fix</btn_applyfix>
      <btn_rerun>Re-run Check</btn_rerun>
      <btn_close>Close</btn_close>
      <!-- Confirm dialogs -->
      <confirm_applyfix>Apply fix for this check?</confirm_applyfix>
      <confirm_fix>Apply fix?</confirm_fix>
    </cmsdoctor>
    <contenttypeaudit>
      <!-- Job alerts -->
      <alert_running>Aggregation job is currently running. Content counts will be updated when it completes.</alert_running>
      <alert_notrun>Content statistics have not been collected yet. The "Content" column will show data after the aggregation job has been run.</alert_notrun>
      <alert_old>Statistics were last updated {0}. Consider running the aggregation job for fresh data.</alert_old>
      <btn_refresh>Refresh</btn_refresh>
      <btn_runnow>Run now</btn_runnow>
      <btn_starting>Starting...</btn_starting>
      <btn_failed>Failed</btn_failed>
      <!-- Toolbar -->
      <btn_showsystem>Show system types</btn_showsystem>
      <btn_hidesystem>Hide system types</btn_hidesystem>
      <lbl_base>Base type:</lbl_base>
      <opt_allbases>All bases</opt_allbases>
      <btn_table>Table</btn_table>
      <btn_hierarchy>Hierarchy</btn_hierarchy>
      <!-- Stats -->
      <stat_total>Total</stat_total>
      <stat_pages>Pages</stat_pages>
      <stat_blocks>Blocks</stat_blocks>
      <stat_media>Media</stat_media>
      <!-- Table columns -->
      <col_name>Name</col_name>
      <col_displayname>Display Name</col_displayname>
      <col_base>Base</col_base>
      <col_properties>Properties</col_properties>
      <col_content>Content</col_content>
      <col_description>Description</col_description>
    </contenttypeaudit>
    <linkchecker>
      <!-- Job alerts -->
      <alert_running>Link checker job is currently running. Results will be updated when it completes.</alert_running>
      <alert_notrun>Link checker has not been run yet. Run the scheduled job to scan content for links.</alert_notrun>
      <alert_lastran>Link check last ran {0}.</alert_lastran>
      <btn_refresh>Refresh</btn_refresh>
      <btn_runnow>Run now</btn_runnow>
      <btn_starting>Starting...</btn_starting>
      <!-- Stats -->
      <stat_total>Total Links</stat_total>
      <stat_broken>Broken</stat_broken>
      <stat_ok>OK</stat_ok>
      <stat_unchecked>Unchecked</stat_unchecked>
      <!-- Toolbar -->
      <lbl_search>Search...</lbl_search>
      <lbl_type>Link Type</lbl_type>
      <lbl_status>Status</lbl_status>
      <opt_alltypes>All types</opt_alltypes>
      <opt_allstatuses>All statuses</opt_allstatuses>
      <!-- Table columns -->
      <col_url>URL</col_url>
      <col_status>Status</col_status>
      <col_statuscode>Code</col_statuscode>
      <col_linktype>Type</col_linktype>
      <col_contentname>Content</col_contentname>
      <col_lasttested>Last Tested</col_lasttested>
      <!-- Status values -->
      <status_ok>OK</status_ok>
      <status_broken>Broken</status_broken>
      <status_unchecked>Unchecked</status_unchecked>
    </linkchecker>
    <securityaudit>
      <!-- Tabs -->
      <tab_tree>Tree</tab_tree>
      <tab_roles>Role Explorer</tab_roles>
      <tab_issues>Issues</tab_issues>
      <!-- Tree toolbar -->
      <lbl_search>Search content...</lbl_search>
      <lbl_highlightrole>Highlight role:</lbl_highlightrole>
      <lbl_allroles>All roles</lbl_allroles>
      <chk_issuesonly>Issues only</chk_issuesonly>
      <!-- Role explorer -->
      <lbl_selectrole>Select a role</lbl_selectrole>
      <lbl_accessfilter>Access filter:</lbl_accessfilter>
      <opt_allaccess>All access levels</opt_allaccess>
      <!-- Issues -->
      <lbl_type>Issue type:</lbl_type>
      <lbl_severity>Severity:</lbl_severity>
      <opt_alltypes>All types</opt_alltypes>
      <opt_allseverities>All severities</opt_allseverities>
      <!-- Table columns -->
      <col_content>Content</col_content>
      <col_role>Role</col_role>
      <col_access>Access</col_access>
      <col_inherited>Inherited</col_inherited>
      <col_type>Type</col_type>
      <col_severity>Severity</col_severity>
      <col_description>Description</col_description>
    </securityaudit>
    <bulkeditor>
      <!-- Setup toolbar -->
      <lbl_contenttype>Content Type:</lbl_contenttype>
      <lbl_language>Language:</lbl_language>
      <lbl_includereferences>Include references</lbl_includereferences>
      <btn_load>Load</btn_load>
      <!-- Table toolbar -->
      <btn_columns>Columns</btn_columns>
      <btn_savechanges>Save Changes</btn_savechanges>
      <btn_discardchanges>Discard Changes</btn_discardchanges>
      <lbl_pendingchanges>{0} pending change(s)</lbl_pendingchanges>
      <!-- Table columns (always present) -->
      <col_select>Select</col_select>
      <col_name>Name</col_name>
      <col_status>Status</col_status>
      <col_changed>Changed</col_changed>
      <!-- Pagination -->
      <stat_showing>Showing {0}-{1} of {2}</stat_showing>
      <perpage>{0} per page</perpage>
      <!-- Confirm -->
      <confirm_discard>Discard all pending changes?</confirm_discard>
      <confirm_save>Save {0} change(s) to {1} content item(s)?</confirm_save>
    </bulkeditor>
    <contentstatistics>
      <!-- Banner -->
      <banner_runjob>Run the [EditorPowertools] Content Analysis scheduled job to populate data.</banner_runjob>
      <btn_runnow>Run now</btn_runnow>
      <btn_starting>Starting...</btn_starting>
      <error_load>Failed to load statistics from API.</error_load>
      <error_render>Error rendering statistics dashboard.</error_render>
      <!-- Chart titles -->
      <chart_byctype>Content by Type</chart_byctype>
      <chart_bystatus>Content by Status</chart_bystatus>
      <chart_created>Content Created (last 12 months)</chart_created>
      <chart_staleness>Content Staleness</chart_staleness>
      <chart_editoractivity>Editor Activity (last 30 days)</chart_editoractivity>
      <!-- Stat labels -->
      <stat_totalcontent>Total Content</stat_totalcontent>
      <stat_published>Published</stat_published>
      <stat_draft>Draft</stat_draft>
      <stat_editors>Active Editors</stat_editors>
    </contentstatistics>
    <activitytimeline>
      <!-- Stats -->
      <stat_today>Activities Today</stat_today>
      <stat_activeeditors>Active Editors</stat_activeeditors>
      <stat_publishes>Publishes Today</stat_publishes>
      <!-- Toolbar -->
      <lbl_user>User:</lbl_user>
      <lbl_action>Action:</lbl_action>
      <lbl_contenttype>Content Type:</lbl_contenttype>
      <lbl_from>From:</lbl_from>
      <lbl_to>To:</lbl_to>
      <lbl_content>Content:</lbl_content>
      <opt_allusers>All users</opt_allusers>
      <opt_allactions>All actions</opt_allactions>
      <opt_alltypes>All types</opt_alltypes>
      <btn_clearfilter>Clear</btn_clearfilter>
      <!-- Load more -->
      <btn_loadmore>Load more</btn_loadmore>
      <!-- Empty -->
      <empty_noactivity>No activity found.</empty_noactivity>
    </activitytimeline>
    <personalizationaudit>
      <!-- Job alerts (same pattern as others) -->
      <alert_running>Personalization analysis job is currently running. Results will be updated when it completes.</alert_running>
      <alert_notrun>Personalization usage has not been analyzed yet. Run the analysis job to scan content for audience usage.</alert_notrun>
      <alert_old>Analysis was last run {0}. Consider running the job again for fresh data.</alert_old>
      <btn_runnow>Run now</btn_runnow>
      <btn_refresh>Refresh</btn_refresh>
      <!-- Stats -->
      <stat_total>Total Usages</stat_total>
      <stat_content>Content Items</stat_content>
      <stat_groups>Visitor Groups Used</stat_groups>
      <!-- Toolbar -->
      <lbl_search>Search...</lbl_search>
      <lbl_type>Usage Type:</lbl_type>
      <lbl_group>Visitor Group:</lbl_group>
      <opt_alltypes>All types</opt_alltypes>
      <opt_allgroups>All groups</opt_allgroups>
      <!-- Table columns -->
      <col_content>Content</col_content>
      <col_type>Usage Type</col_type>
      <col_groups>Visitor Groups</col_groups>
      <col_location>Location</col_location>
    </personalizationaudit>
    <audiencemanager>
      <!-- Stats -->
      <stat_audiences>Audiences</stat_audiences>
      <stat_withstats>With Statistics</stat_withstats>
      <stat_categories>Categories</stat_categories>
      <stat_totalcriteria>Total Criteria</stat_totalcriteria>
      <stat_showing>Showing</stat_showing>
      <!-- Toolbar -->
      <lbl_search>Search audiences...</lbl_search>
      <lbl_category>Category:</lbl_category>
      <opt_allcategories>All categories</opt_allcategories>
      <chk_statsonly>Statistics-enabled only</chk_statsonly>
      <!-- Table columns -->
      <col_name>Name</col_name>
      <col_category>Category</col_category>
      <col_criteria>Criteria</col_criteria>
      <col_statistics>Statistics</col_statistics>
      <col_actions>Actions</col_actions>
      <!-- Actions -->
      <btn_edit>Edit</btn_edit>
      <btn_viewinstats>View in Statistics</btn_viewinstats>
      <btn_manage>Manage in CMS</btn_manage>
    </audiencemanager>
    <contentimporter>
      <!-- Step labels -->
      <step_upload>Upload File</step_upload>
      <step_configure>Configure Import</step_configure>
      <step_preview>Preview</step_preview>
      <step_import>Import</step_import>
      <!-- Upload step -->
      <lbl_dropfile>Drop a CSV, JSON, or Excel file here, or click to browse</lbl_dropfile>
      <btn_browse>Browse...</btn_browse>
      <!-- Configure step -->
      <lbl_contenttype>Content Type:</lbl_contenttype>
      <lbl_language>Language:</lbl_language>
      <lbl_parent>Parent Location:</lbl_parent>
      <lbl_namecol>Name Column:</lbl_namecol>
      <lbl_publishafter>Publish after import</lbl_publishafter>
      <lbl_selectparent>Select parent...</lbl_selectparent>
      <btn_next>Next</btn_next>
      <btn_back>Back</btn_back>
      <!-- Mapping step -->
      <lbl_mapping>Field Mapping</lbl_mapping>
      <lbl_sourcecol>Source Column</lbl_sourcecol>
      <lbl_targetfield>Target Field</lbl_targetfield>
      <opt_skip>-- Skip --</opt_skip>
      <btn_dryrun>Dry Run</btn_dryrun>
      <!-- Preview step -->
      <lbl_dryresult>Dry Run Results</lbl_dryresult>
      <lbl_willcreate>Will create: {0} items</lbl_willcreate>
      <lbl_errors>Errors: {0}</lbl_errors>
      <btn_import>Start Import</btn_import>
      <!-- Progress -->
      <lbl_importing>Importing...</lbl_importing>
      <lbl_progress>{0} of {1} items processed</lbl_progress>
      <lbl_complete>Import complete: {0} created, {1} failed</lbl_complete>
    </contentimporter>
    <managechildren>
      <!-- Pick parent prompt -->
      <lbl_selectparent>Select a parent page to manage its children</lbl_selectparent>
      <btn_selectparent>Select Parent Page</btn_selectparent>
      <!-- Toolbar -->
      <btn_sortaz>Sort A-Z</btn_sortaz>
      <btn_sortza>Sort Z-A</btn_sortza>
      <btn_sortbydate>Sort by Date</btn_sortbydate>
      <btn_publishall>Publish All</btn_publishall>
      <btn_unpublishall>Unpublish All</btn_unpublishall>
      <btn_deleteselected>Delete Selected</btn_deleteselected>
      <btn_saveorder>Save Order</btn_saveorder>
      <!-- Table columns -->
      <col_name>Name</col_name>
      <col_status>Status</col_status>
      <col_changed>Changed</col_changed>
      <col_type>Type</col_type>
      <!-- Confirm dialogs -->
      <confirm_delete>Delete {0} selected item(s)? This cannot be undone.</confirm_delete>
      <confirm_publishall>Publish all {0} children?</confirm_publishall>
      <confirm_unpublishall>Unpublish all {0} children?</confirm_unpublishall>
    </managechildren>
    <recommendations>
      <!-- Info banner -->
      <banner_info>When editors create new content, Optimizely can suggest which content types to use. Define rules below to control these suggestions based on where content is being created.</banner_info>
      <!-- Toolbar -->
      <btn_addrule>Add Rule</btn_addrule>
      <!-- Table columns -->
      <col_parenttype>Parent Type</col_parenttype>
      <col_allowedtypes>Allowed Types</col_allowedtypes>
      <col_actions>Actions</col_actions>
      <!-- Rule dialog -->
      <dlg_addrule>Add Rule</dlg_addrule>
      <dlg_editrule>Edit Rule</dlg_editrule>
      <lbl_parenttype>Parent Content Type:</lbl_parenttype>
      <lbl_allowedtypes>Allowed Child Types:</lbl_allowedtypes>
      <btn_save>Save</btn_save>
      <btn_cancel>Cancel</btn_cancel>
      <btn_edit>Edit</btn_edit>
      <btn_delete>Delete</btn_delete>
      <!-- Empty state -->
      <empty_norules>No recommendation rules defined yet. Click "Add Rule" to create one.</empty_norules>
      <!-- Confirm -->
      <confirm_delete>Delete this rule?</confirm_delete>
    </recommendations>
    <gantt>
      <!-- Header -->
      <lbl_range>Range:</lbl_range>
      <opt_24h>24 hours</opt_24h>
      <opt_48h>48 hours</opt_48h>
      <opt_7d>7 days</opt_7d>
      <!-- Navigation -->
      <btn_today>Today</btn_today>
      <btn_back>Back</btn_back>
      <btn_forward>Forward</btn_forward>
      <!-- Job column header -->
      <col_job>Job</col_job>
      <!-- Tooltip labels -->
      <lbl_started>Started:</lbl_started>
      <lbl_ended>Ended:</lbl_ended>
      <lbl_duration>Duration:</lbl_duration>
      <lbl_status>Status:</lbl_status>
      <!-- Status values -->
      <status_succeeded>Succeeded</status_succeeded>
      <status_failed>Failed</status_failed>
      <status_running>Running</status_running>
      <!-- Empty state -->
      <empty_nojobs>No scheduled jobs found.</empty_nojobs>
      <empty_noexecutions>No executions in this time range.</empty_noexecutions>
    </gantt>
    <components>
      <!-- Content picker -->
      <picker_selectcontent>Select Content</picker_selectcontent>
      <picker_search>Search content by name...</picker_search>
      <picker_noresults>No results found</picker_noresults>
      <btn_cancel>Cancel</btn_cancel>
      <btn_select>Select</btn_select>
      <!-- Content type picker -->
      <typepicker_title>Select Content Type</typepicker_title>
      <typepicker_search>Search content types...</typepicker_search>
      <typepicker_noresults>No content types found</typepicker_noresults>
    </components>
    <editorpowertools>
      <!-- Shared utility strings in EPT namespace -->
      <loading>Loading...</loading>
      <noresults>No results found</noresults>
    </editorpowertools>
  </ui>
</editorpowertools>
```

### 2. C# `UiStringsProvider`

A simple service class in `src/EditorPowertools/Localization/` that loads all UI keys from `LocalizationService` and returns an anonymous object for JSON serialization.

```csharp
// src/EditorPowertools/Localization/UiStringsProvider.cs
public class UiStringsProvider
{
    private readonly LocalizationService _loc;

    public UiStringsProvider(LocalizationService loc) => _loc = loc;

    private string S(string path) => _loc.GetString(path);
    private string Base => "/editorpowertools/ui";

    public object GetAll() => new {
        shared = new { /* ... */ },
        contentaudit = new { /* ... */ },
        // etc.
    };
}
```

Register as scoped: `services.AddScoped<UiStringsProvider>();`

### 3. Layout Injection

In `_PowertoolsLayout.cshtml`, inject after the existing `window.EPT_*` block:

```html
<script>
    window.EPT_STRINGS = @Html.Raw(Json.Serialize(stringsProvider.GetAll()));
</script>
```

The layout already has a `@inject` for similar services — add `@inject UiStringsProvider StringsProvider`.

### 4. `EPT.s()` Safe Accessor

Add to `editorpowertools.js`:

```js
EPT.s = function(path, fallback) {
    try {
        var parts = path.split('.');
        var obj = window.EPT_STRINGS;
        for (var i = 0; i < parts.length; i++) {
            obj = obj[parts[i]];
            if (obj === undefined) return fallback || path;
        }
        return obj || fallback || path;
    } catch (e) {
        return fallback || path;
    }
};
```

Usage: `EPT.s('contentaudit.col_name', 'Name')`

### 5. JS File Updates

Each JS file replaces hardcoded strings with `EPT.s()` calls at the point of use.

**Pattern for column definitions (content-audit.js):**
```js
// Before:
{ key: 'name', label: 'Name', ... }

// After:
{ key: 'name', label: EPT.s('contentaudit.col_name', 'Name'), ... }
```

**Pattern for inline HTML strings:**
```js
// Before:
'<div class="ept-stat__label">Total items</div>'

// After:
'<div class="ept-stat__label">' + EPT.s('contentaudit.stat_totalitems', 'Total items') + '</div>'
```

**Pattern for `EPT.showLoading` / `EPT.showEmpty`:**
These accept a message string, so the caller provides the localized string:
```js
EPT.showLoading(el);  // EPT.showLoading already hardcodes "Loading..." — this is updated in editorpowertools.js
EPT.showEmpty(body, EPT.s('languageaudit.empty_nodata', 'No language data available.'));
```

---

## String Inventory Summary

| Tool JS file | Estimated string count |
|---|---|
| `content-audit.js` | ~55 |
| `language-audit.js` | ~40 |
| `cms-doctor.js` | ~25 |
| `content-type-audit.js` | ~15 |
| `link-checker.js` | ~20 |
| `security-audit.js` | ~20 |
| `bulk-property-editor.js` | ~20 |
| `content-statistics.js` | ~15 |
| `activity-timeline.js` | ~15 |
| `personalization-audit.js` | ~20 |
| `audience-manager.js` | ~15 |
| `content-importer.js` | ~25 |
| `manage-children.js` | ~15 |
| `content-type-recommendations.js` | ~10 |
| `scheduled-jobs-gantt.js` | ~15 |
| `editorpowertools.js` (shared) | ~5 |
| `components.js` | ~8 |
| **Total** | **~340** |

---

## Files Changed

1. **`src/EditorPowertools/lang/en.xml`** — add `<ui>` section (~340 strings)
2. **`src/EditorPowertools/lang/da.xml`** (and 9 other lang files) — add translated `<ui>` section
3. **`src/EditorPowertools/Localization/UiStringsProvider.cs`** — new service
4. **`src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml`** — inject `window.EPT_STRINGS`
5. **`src/EditorPowertools/modules/.../js/editorpowertools.js`** — add `EPT.s()` + update `showLoading`/`showEmpty`
6. **All other JS files** (~16 files) — replace hardcoded strings with `EPT.s()` calls

---

## Translation Note

`en.xml` is the authoritative base. All other 10 language files get the same `<ui>` section structure with translated values. For the initial implementation, non-English languages may use the English fallback text (since `EPT.s()` returns the fallback if a key is missing) — proper translations can follow.

---

## Out of Scope

- Server-side C# strings (already localized via `LocalizationService`)
- Strings that come from the API response (already localized server-side)
- Dynamic strings interpolated from data (e.g. content names, user names)
