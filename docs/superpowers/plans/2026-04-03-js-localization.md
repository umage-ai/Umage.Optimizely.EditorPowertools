# JS Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Localize all hardcoded English strings in the JavaScript UI layer (~340 strings across 17 JS files) by injecting server-side translated strings via `window.EPT_STRINGS`.

**Architecture:** A `UiStringsProvider` C# service reads all UI string keys from `LocalizationService` and is injected into `_PowertoolsLayout.cshtml`, which serializes the result to `window.EPT_STRINGS = {...}`. JS files call `EPT.s('tool.key', 'fallback')` instead of hardcoded strings.

**Tech Stack:** .NET 8 / C# / Optimizely `LocalizationService` / Vanilla JS / Razor / XML lang files

---

## File Map

**Create:**
- `src/EditorPowertools/Localization/UiStringsProvider.cs` — loads all UI strings from LocalizationService and serializes them

**Modify:**
- `src/EditorPowertools/lang/en.xml` — add `<ui>` section with all ~340 string keys
- `src/EditorPowertools/lang/da.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/sv.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/no.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/de.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/fi.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/fr.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/es.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/nl.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/ja.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/lang/zh-cn.xml` — add `<ui>` section (English text as initial fallback)
- `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs` — register UiStringsProvider
- `src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml` — inject window.EPT_STRINGS
- `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/editorpowertools.js` — add EPT.s(), update showLoading/createTable
- All 17 tool JS files — replace hardcoded strings with EPT.s() calls

---

### Task 1: Add `<ui>` section to en.xml

**Files:**
- Modify: `src/EditorPowertools/lang/en.xml`

- [ ] **Step 1: Open en.xml and insert the `<ui>` section**

Find the closing `</editorpowertools>` tag and insert the following block immediately before it:

```xml
      <!-- JavaScript UI strings (injected via window.EPT_STRINGS) -->
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
          <prev>Prev</prev>
          <next>Next</next>
          <yes>Yes</yes>
          <no>No</no>
          <open>Open</open>
          <all>All</all>
          <save>Save</save>
          <delete>Delete</delete>
          <edit>Edit</edit>
          <failed>Failed</failed>
        </shared>
        <contentaudit>
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
          <filter_all>All content</filter_all>
          <filter_pages>Pages only</filter_pages>
          <filter_blocks>Blocks only</filter_blocks>
          <filter_media>Media only</filter_media>
          <filter_unpublished>Unpublished</filter_unpublished>
          <filter_unused>Unused content</filter_unused>
          <op_contains>Contains</op_contains>
          <op_equals>Equals</op_equals>
          <op_startswith>Starts with</op_startswith>
          <op_isempty>Is empty</op_isempty>
          <op_isnotempty>Is not empty</op_isnotempty>
          <btn_filter>Filter</btn_filter>
          <btn_columns>Columns</btn_columns>
          <btn_selectall>Select all</btn_selectall>
          <btn_reset>Reset</btn_reset>
          <btn_addfilter>Add filter</btn_addfilter>
          <btn_clearall>Clear all</btn_clearall>
          <dlg_choosecolumns>Choose Columns</dlg_choosecolumns>
          <dlg_addfilter>Add Filter</dlg_addfilter>
          <lbl_column>Column</lbl_column>
          <lbl_operator>Operator</lbl_operator>
          <lbl_value>Value</lbl_value>
          <lbl_filtervalue>Filter value...</lbl_filtervalue>
          <lbl_search>Search by name...</lbl_search>
          <stat_totalitems>Total items</stat_totalitems>
          <stat_pages>Pages</stat_pages>
          <stat_currentpage>Current page</stat_currentpage>
          <empty_nodata>No data loaded.</empty_nodata>
          <empty_nofilter>No content matches the current filters.</empty_nofilter>
          <page_showing>Showing {0}-{1} of {2}</page_showing>
          <perpage>{0} per page</perpage>
          <fmt_excel>Excel (.xlsx)</fmt_excel>
          <fmt_csv>CSV (.csv)</fmt_csv>
          <fmt_json>JSON (.json)</fmt_json>
          <cell_openeditmode>Open in edit mode</cell_openeditmode>
        </contentaudit>
        <languageaudit>
          <tab_overview>Overview</tab_overview>
          <tab_missing>Missing Translations</tab_missing>
          <tab_stale>Stale Translations</tab_stale>
          <tab_queue>Translation Queue</tab_queue>
          <stat_totalcontent>Total Content</stat_totalcontent>
          <stat_languages>Languages</stat_languages>
          <stat_missing>Missing Translations</stat_missing>
          <stat_stale>Stale (30+ days)</stat_stale>
          <stat_itemstotranslate>Items to Translate</stat_itemstotranslate>
          <stat_page>Page</stat_page>
          <stat_missing_lang>Missing {0}</stat_missing_lang>
          <stat_stale_count>Stale Translations</stat_stale_count>
          <card_coverage>Language Coverage</card_coverage>
          <card_contentitems>{0} content items</card_contentitems>
          <card_published>{0} published</card_published>
          <lbl_language>Language:</lbl_language>
          <lbl_threshold>Threshold (days):</lbl_threshold>
          <lbl_targetlang>Target Language:</lbl_targetlang>
          <lbl_contenttype>Content Type:</lbl_contenttype>
          <lbl_alllanguages>All languages</lbl_alllanguages>
          <lbl_alltypes>All types</lbl_alltypes>
          <btn_coveragetree>Coverage Tree</btn_coveragetree>
          <btn_apply>Apply</btn_apply>
          <btn_exportcsv>Export CSV</btn_exportcsv>
          <btn_runnow>Run now</btn_runnow>
          <btn_started>Job started, please refresh in a few minutes.</btn_started>
          <btn_failed>Failed to start job</btn_failed>
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
          <empty_all_translated>All content has been translated to {0}</empty_all_translated>
          <empty_no_stale>No stale translations found (threshold: {0} days)</empty_no_stale>
          <empty_no_queue>No content needs translation to {0}</empty_no_queue>
          <empty_no_tree>No coverage tree data available</empty_no_tree>
          <empty_nodata>No language data available. Run the aggregation job to collect statistics.</empty_nodata>
          <banner_runjob>Run the [EditorPowertools] Content Analysis scheduled job to populate data.</banner_runjob>
          <page_info>Page {0} of {1}</page_info>
          <btn_previous>Previous</btn_previous>
          <btn_next>Next</btn_next>
        </languageaudit>
        <cmsdoctor>
          <header_title>CMS Doctor</header_title>
          <header_desc>Health checks for your Optimizely CMS. Extensible by third-party packages.</header_desc>
          <lbl_lastrun>Last run: {0}</lbl_lastrun>
          <btn_runall>Run All Checks</btn_runall>
          <btn_running>Running...</btn_running>
          <sum_healthy>Healthy</sum_healthy>
          <sum_warnings>Warnings</sum_warnings>
          <sum_faults>Faults</sum_faults>
          <sum_notchecked>Not Checked</sum_notchecked>
          <tag_all>All</tag_all>
          <status_ok>Healthy</status_ok>
          <status_warning>Warning</status_warning>
          <status_badpractice>Bad Practice</status_badpractice>
          <status_fault>Fault</status_fault>
          <status_performance>Performance</status_performance>
          <status_notchecked>Not Checked</status_notchecked>
          <btn_run>Run</btn_run>
          <btn_fix>Fix</btn_fix>
          <btn_details>Details</btn_details>
          <btn_dismiss>Dismiss</btn_dismiss>
          <btn_restore>Restore</btn_restore>
          <dlg_result>Result:</dlg_result>
          <dlg_details>Details:</dlg_details>
          <dlg_categories>Categories:</dlg_categories>
          <dlg_checked>Checked: {0}</dlg_checked>
          <btn_applyfix>Apply Fix</btn_applyfix>
          <btn_rerun>Re-run Check</btn_rerun>
          <btn_close>Close</btn_close>
          <confirm_applyfix>Apply fix for this check?</confirm_applyfix>
          <confirm_fix>Apply fix?</confirm_fix>
        </cmsdoctor>
        <contenttypeaudit>
          <alert_running>Aggregation job is currently running. Content counts will be updated when it completes.</alert_running>
          <alert_notrun>Content statistics have not been collected yet. The "Content" column will show data after the aggregation job has been run.</alert_notrun>
          <alert_old>Statistics were last updated {0}. Consider running the aggregation job for fresh data.</alert_old>
          <alert_started>Aggregation job has been started. Content counts will be updated when it completes.</alert_started>
          <btn_refresh>Refresh</btn_refresh>
          <btn_runnow>Run now</btn_runnow>
          <btn_starting>Starting...</btn_starting>
          <btn_failed>Failed</btn_failed>
          <stat_contenttypes>Content Types</stat_contenttypes>
          <stat_systemtypes>System Types</stat_systemtypes>
          <stat_orphaned>Orphaned</stat_orphaned>
          <stat_unused>Unused (0 content)</stat_unused>
          <stat_showing>Showing</stat_showing>
          <lbl_search>Search types... (or property:name)</lbl_search>
          <opt_allbases>All base types</opt_allbases>
          <chk_showsystem>Show system types</chk_showsystem>
          <btn_table>Table</btn_table>
          <btn_tree>Tree</btn_tree>
          <btn_export>Export</btn_export>
          <col_name>Name</col_name>
          <col_base>Base</col_base>
          <col_group>Group</col_group>
          <col_properties>Properties</col_properties>
          <col_content>Content</col_content>
          <opt_all>All</opt_all>
          <badge_system>System</badge_system>
          <badge_orphaned>Orphaned</badge_orphaned>
        </contenttypeaudit>
        <linkchecker>
          <alert_running>Link checker job is currently running. Results will be updated when it completes.</alert_running>
          <alert_notrun>Link checker has not been run yet. Run the scheduled job to scan content for links.</alert_notrun>
          <alert_lastran>Link check last ran {0}.</alert_lastran>
          <alert_started>Job started.</alert_started>
          <btn_refresh>Refresh</btn_refresh>
          <btn_runnow>Run now</btn_runnow>
          <btn_starting>Starting...</btn_starting>
          <btn_failed>Failed</btn_failed>
          <btn_export>Export</btn_export>
          <stat_total>Total Links</stat_total>
          <stat_broken>Broken</stat_broken>
          <stat_valid>Valid</stat_valid>
          <stat_internal>Internal</stat_internal>
          <stat_external>External</stat_external>
          <lbl_search>Search URLs, content, properties...</lbl_search>
          <opt_alltypes>All types</opt_alltypes>
          <opt_internal>Internal</opt_internal>
          <opt_external>External</opt_external>
          <opt_allstatus>All status</opt_allstatus>
          <opt_brokenonly>Broken only</opt_brokenonly>
          <opt_validonly>Valid only</opt_validonly>
          <col_status>Status</col_status>
          <col_url>URL</col_url>
          <col_type>Type</col_type>
          <col_foundin>Found In</col_foundin>
          <col_checked>Checked</col_checked>
          <cell_usedon>Used on: </cell_usedon>
        </linkchecker>
        <securityaudit>
          <tab_tree>Content Tree</tab_tree>
          <tab_roles>Role/User Explorer</tab_roles>
          <tab_issues>Issues</tab_issues>
          <banner_nodata>Security audit data not available.</banner_nodata>
          <banner_runjob>Run the [EditorPowertools] Content Analysis scheduled job to analyze content permissions.</banner_runjob>
          <banner_started>Aggregation job has been started. Data will update when it completes.</banner_started>
          <alert_old>Security data was last analyzed {0}. Consider running the aggregation job for fresh data.</alert_old>
          <btn_runnow>Run now</btn_runnow>
          <btn_starting>Starting...</btn_starting>
          <btn_refresh>Refresh</btn_refresh>
          <btn_failed>Failed</btn_failed>
          <stat_analyzed>Content Analyzed</stat_analyzed>
          <stat_roles>Roles/Users</stat_roles>
          <stat_issues>Issues Found</stat_issues>
          <stat_lastanalysis>Last Analysis</stat_lastanalysis>
          <pager_previous>Previous</pager_previous>
          <pager_info>Page {0} of {1}</pager_info>
          <pager_next>Next</pager_next>
        </securityaudit>
        <bulkeditor>
          <opt_selecttype>-- Select content type --</opt_selecttype>
          <btn_columns>Columns</btn_columns>
          <btn_addfilter>Add Filter</btn_addfilter>
          <chk_includereferences>Include references</chk_includereferences>
          <btn_applyfilters>Apply Filters</btn_applyfilters>
          <empty_selecttype>Select a content type above to start editing.</empty_selecttype>
          <lbl_perpage>Per page:</lbl_perpage>
          <pending_count>{0} changes pending</pending_count>
          <btn_discardall>Discard All</btn_discardall>
          <btn_saveall>Save All</btn_saveall>
          <btn_publishall>Publish All</btn_publishall>
          <confirm_discard>Discard all pending changes?</confirm_discard>
        </bulkeditor>
        <contentstatistics>
          <banner_runjob>Run the [EditorPowertools] Content Analysis scheduled job to populate data.</banner_runjob>
          <btn_runnow>Run now</btn_runnow>
          <btn_starting>Starting...</btn_starting>
          <error_load>Failed to load statistics from API.</error_load>
          <error_render>Error rendering statistics dashboard.</error_render>
          <chart_byctype>Content by Type</chart_byctype>
          <chart_bystatus>Content by Status</chart_bystatus>
          <chart_created>Content Created (last 12 months)</chart_created>
          <chart_staleness>Content Staleness</chart_staleness>
          <chart_editoractivity>Editor Activity (last 30 days)</chart_editoractivity>
          <stat_totalcontent>Total Content</stat_totalcontent>
          <stat_published>Published</stat_published>
          <stat_draft>Draft</stat_draft>
          <stat_editors>Active Editors</stat_editors>
          <stat_pages>Pages</stat_pages>
          <stat_blocks>Blocks</stat_blocks>
          <stat_media>Media</stat_media>
        </contentstatistics>
        <activitytimeline>
          <stat_today>Activities Today</stat_today>
          <stat_activeeditors>Active Editors</stat_activeeditors>
          <stat_publishes>Publishes Today</stat_publishes>
          <stat_drafts>Drafts Today</stat_drafts>
          <opt_allusers>All users</opt_allusers>
          <opt_published>Published</opt_published>
          <opt_draft>Draft saved</opt_draft>
          <opt_readytopublish>Ready to publish</opt_readytopublish>
          <opt_scheduled>Scheduled</opt_scheduled>
          <opt_rejected>Rejected</opt_rejected>
          <opt_previouslypublished>Previously published</opt_previouslypublished>
          <opt_comment>Comments</opt_comment>
          <opt_allcontenttypes>All content types</opt_allcontenttypes>
          <btn_filter>Filter</btn_filter>
          <btn_clear>Clear</btn_clear>
          <banner_showingfor>Showing timeline for {0}</banner_showingfor>
          <btn_showall>Show all activity</btn_showall>
        </activitytimeline>
        <personalizationaudit>
          <alert_running>Personalization analysis job is currently running. Results will be updated when it completes.</alert_running>
          <alert_notrun>Personalization usage has not been analyzed yet. Run the analysis job to scan content for audience usage.</alert_notrun>
          <alert_old>Analysis was last run {0}. Consider running the job again for fresh data.</alert_old>
          <btn_runnow>Run now</btn_runnow>
          <btn_refresh>Refresh</btn_refresh>
          <btn_starting>Starting...</btn_starting>
          <stat_total>Total Usages</stat_total>
          <stat_content>Content Items</stat_content>
          <stat_groups>Visitor Groups Used</stat_groups>
          <lbl_search>Search...</lbl_search>
          <lbl_type>Usage Type:</lbl_type>
          <lbl_group>Visitor Group:</lbl_group>
          <opt_alltypes>All types</opt_alltypes>
          <opt_allgroups>All groups</opt_allgroups>
          <col_content>Content</col_content>
          <col_type>Usage Type</col_type>
          <col_groups>Visitor Groups</col_groups>
          <col_location>Location</col_location>
        </personalizationaudit>
        <audiencemanager>
          <stat_audiences>Audiences</stat_audiences>
          <stat_withstats>With Statistics</stat_withstats>
          <stat_categories>Categories</stat_categories>
          <stat_totalcriteria>Total Criteria</stat_totalcriteria>
          <stat_showing>Showing</stat_showing>
          <lbl_search>Search audiences...</lbl_search>
          <opt_allcategories>All categories</opt_allcategories>
          <chk_hasstats>Has Statistics</chk_hasstats>
          <lnk_personalizationaudit>Personalization Audit</lnk_personalizationaudit>
          <btn_export>Export</btn_export>
          <col_name>Name</col_name>
          <col_criteria>Criteria</col_criteria>
          <col_operator>Operator</col_operator>
          <col_statistics>Statistics</col_statistics>
          <col_usage>Usage</col_usage>
          <opt_all>All</opt_all>
          <title_usagedetails>Show usage details</title_usagedetails>
          <title_notused>Not used in any personalized content</title_notused>
          <title_runjobfirst>Run the Personalization Analysis job first</title_runjobfirst>
        </audiencemanager>
        <contentimporter>
          <step_upload>1. Upload File</step_upload>
          <step_configure>2. Configure</step_configure>
          <step_dryrun>3. Dry Run</step_dryrun>
          <step_import>4. Import</step_import>
          <lbl_dropfile>Drop a CSV, JSON, or Excel file here, or click to browse</lbl_dropfile>
          <btn_browse>Browse...</btn_browse>
          <lbl_contenttype>Content Type:</lbl_contenttype>
          <lbl_language>Language:</lbl_language>
          <lbl_parent>Parent Location:</lbl_parent>
          <lbl_namecol>Name Column:</lbl_namecol>
          <lbl_publishafter>Publish after import</lbl_publishafter>
          <lbl_selectparent>Select parent...</lbl_selectparent>
          <btn_next>Next</btn_next>
          <btn_back>Back</btn_back>
          <lbl_mapping>Field Mapping</lbl_mapping>
          <lbl_sourcecol>Source Column</lbl_sourcecol>
          <lbl_targetfield>Target Field</lbl_targetfield>
          <opt_skip>-- Skip --</opt_skip>
          <btn_dryrun>Run Dry Run</btn_dryrun>
          <lbl_dryresult>Dry Run Results</lbl_dryresult>
          <btn_import>Start Import</btn_import>
          <btn_startover>Start Over</btn_startover>
          <lbl_importing>Importing...</lbl_importing>
        </contentimporter>
        <managechildren>
          <lbl_selectparent>Select a parent page to manage its children</lbl_selectparent>
          <btn_selectparent>Select Parent Page</btn_selectparent>
          <btn_sortaz>Sort A-Z</btn_sortaz>
          <btn_sortza>Sort Z-A</btn_sortza>
          <btn_sortbydate>Sort by Date</btn_sortbydate>
          <btn_publishall>Publish All</btn_publishall>
          <btn_unpublishall>Unpublish All</btn_unpublishall>
          <btn_deleteselected>Delete Selected</btn_deleteselected>
          <btn_saveorder>Save Order</btn_saveorder>
          <col_name>Name</col_name>
          <col_status>Status</col_status>
          <col_changed>Changed</col_changed>
          <col_type>Type</col_type>
          <confirm_delete>Delete {0} selected item(s)? This cannot be undone.</confirm_delete>
          <confirm_publishall>Publish all {0} children?</confirm_publishall>
          <confirm_unpublishall>Unpublish all {0} children?</confirm_unpublishall>
        </managechildren>
        <recommendations>
          <banner_info>When editors create new content, Optimizely can suggest which content types to use. Define rules below to control these suggestions based on where content is being created.</banner_info>
          <btn_addrule>Add Rule</btn_addrule>
          <col_parenttype>Parent Type</col_parenttype>
          <col_allowedtypes>Allowed Types</col_allowedtypes>
          <col_actions>Actions</col_actions>
          <dlg_addrule>Add Rule</dlg_addrule>
          <dlg_editrule>Edit Rule</dlg_editrule>
          <lbl_parenttype>Parent Content Type:</lbl_parenttype>
          <lbl_allowedtypes>Allowed Child Types:</lbl_allowedtypes>
          <btn_save>Save</btn_save>
          <btn_cancel>Cancel</btn_cancel>
          <btn_edit>Edit</btn_edit>
          <btn_delete>Delete</btn_delete>
          <empty_norules>No recommendation rules defined yet. Click "Add Rule" to create one.</empty_norules>
          <confirm_delete>Delete this rule?</confirm_delete>
        </recommendations>
        <gantt>
          <error_load>Failed to load Gantt data: {0}</error_load>
          <empty_nojobs>No scheduled jobs found.</empty_nojobs>
          <stat_totaljobs>Total Jobs</stat_totaljobs>
          <stat_enabled>Enabled</stat_enabled>
          <stat_running>Running Now</stat_running>
          <stat_executions>Executions in View</stat_executions>
          <btn_previous>Previous</btn_previous>
          <btn_today>Today</btn_today>
          <btn_next>Next</btn_next>
          <lbl_viewhours>{0}h view</lbl_viewhours>
          <col_job>Job</col_job>
        </gantt>
        <activeeditors>
          <error_nosignalr>SignalR client not available. Check browser console for errors.</error_nosignalr>
          <error_connect>Could not connect: {0}</error_connect>
          <stat_online>Online Now</stat_online>
          <stat_editing>Currently Editing</stat_editing>
          <stat_today>Active Today</stat_today>
          <section_online>Online Now</section_online>
          <section_today>Also Active Today</section_today>
          <lbl_connected>Connected {0}</lbl_connected>
          <btn_sendmessage>Send Message</btn_sendmessage>
          <badge_you>you</badge_you>
          <badge_offline>offline</badge_offline>
          <chat_title>Team Chat</chat_title>
          <chat_empty>No messages yet. Say hello!</chat_empty>
          <chat_placeholder>Type a message... (Enter to send)</chat_placeholder>
          <dlg_sendmessage>Send Message to {0}</dlg_sendmessage>
          <dlg_desc>This will send a CMS notification that {0} will see in their notification bell.</dlg_desc>
          <dlg_placeholder>Type your message...</dlg_placeholder>
          <btn_cancel>Cancel</btn_cancel>
          <btn_send>Send Notification</btn_send>
          <btn_sending>Sending...</btn_sending>
          <msg_sent>Message sent to {0}</msg_sent>
        </activeeditors>
        <components>
          <picker_title>Select Content</picker_title>
          <picker_search>Search content by name...</picker_search>
          <picker_noresults>No results found</picker_noresults>
          <btn_cancel>Cancel</btn_cancel>
          <btn_select>Select</btn_select>
          <typepicker_title>Select Content Type</typepicker_title>
          <typepicker_search>Search content types...</typepicker_search>
          <typepicker_noresults>No content types found</typepicker_noresults>
        </components>
        <editorpowertools>
          <loading>Loading...</loading>
          <noresults>No results found</noresults>
        </editorpowertools>
      </ui>
```

- [ ] **Step 2: Verify XML is well-formed**

```bash
cd "C:/Github/EditorPowertools"
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | head -20
```

Expected: build succeeds (or fails only on unrelated issues, not XML parse errors).

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/lang/en.xml
git commit -m "feat: add <ui> section to en.xml with all JS localization keys"
```

---

### Task 2: Add `<ui>` section to all other 10 lang files

**Files:**
- Modify: `src/EditorPowertools/lang/da.xml`, `sv.xml`, `no.xml`, `de.xml`, `fi.xml`, `fr.xml`, `es.xml`, `nl.xml`, `ja.xml`, `zh-cn.xml`

- [ ] **Step 1: For each of the 10 language files, add the same `<ui>` section**

In each file, find the closing `</editorpowertools>` tag and insert the identical `<ui>` block from Task 1 (English text). This uses English as a placeholder since `EPT.s()` already has an English fallback — these files enable real translations to be added later without code changes.

For example, in `da.xml`, find `</editorpowertools>` and insert the complete `<ui>...</ui>` block from Task 1 immediately before it.

Apply to all 10 files: `da.xml`, `sv.xml`, `no.xml`, `de.xml`, `fi.xml`, `fr.xml`, `es.xml`, `nl.xml`, `ja.xml`, `zh-cn.xml`.

- [ ] **Step 2: Build to verify no XML errors**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | head -20
```

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/lang/
git commit -m "feat: add <ui> section to all 10 language files (English placeholder)"
```

---

### Task 3: Create UiStringsProvider.cs

**Files:**
- Create: `src/EditorPowertools/Localization/UiStringsProvider.cs`

- [ ] **Step 1: Create the file**

```csharp
using EPiServer.Framework.Localization;

namespace EditorPowertools.Localization;

/// <summary>
/// Provides all JavaScript UI strings from the localization service,
/// serialized to window.EPT_STRINGS in the layout.
/// </summary>
public class UiStringsProvider(LocalizationService loc)
{
    private string S(string key) => loc.GetString($"/editorpowertools/ui/{key}");

    public object GetAll() => new
    {
        shared = new
        {
            loading = S("shared/loading"),
            noresults = S("shared/noresults"),
            cancel = S("shared/cancel"),
            apply = S("shared/apply"),
            close = S("shared/close"),
            runnow = S("shared/runnow"),
            refresh = S("shared/refresh"),
            starting = S("shared/starting"),
            export = S("shared/export"),
            prev = S("shared/prev"),
            next = S("shared/next"),
            yes = S("shared/yes"),
            no = S("shared/no"),
            open = S("shared/open"),
            all = S("shared/all"),
            save = S("shared/save"),
            delete = S("shared/delete"),
            edit = S("shared/edit"),
            failed = S("shared/failed")
        },
        contentaudit = new
        {
            col_contentid = S("contentaudit/col_contentid"),
            col_name = S("contentaudit/col_name"),
            col_language = S("contentaudit/col_language"),
            col_contenttype = S("contentaudit/col_contenttype"),
            col_maintype = S("contentaudit/col_maintype"),
            col_url = S("contentaudit/col_url"),
            col_editurl = S("contentaudit/col_editurl"),
            col_breadcrumb = S("contentaudit/col_breadcrumb"),
            col_status = S("contentaudit/col_status"),
            col_createdby = S("contentaudit/col_createdby"),
            col_created = S("contentaudit/col_created"),
            col_changedby = S("contentaudit/col_changedby"),
            col_changed = S("contentaudit/col_changed"),
            col_published = S("contentaudit/col_published"),
            col_publisheduntil = S("contentaudit/col_publisheduntil"),
            col_masterlanguage = S("contentaudit/col_masterlanguage"),
            col_alllanguages = S("contentaudit/col_alllanguages"),
            col_referencecount = S("contentaudit/col_referencecount"),
            col_versioncount = S("contentaudit/col_versioncount"),
            col_haspersonalizations = S("contentaudit/col_haspersonalizations"),
            filter_all = S("contentaudit/filter_all"),
            filter_pages = S("contentaudit/filter_pages"),
            filter_blocks = S("contentaudit/filter_blocks"),
            filter_media = S("contentaudit/filter_media"),
            filter_unpublished = S("contentaudit/filter_unpublished"),
            filter_unused = S("contentaudit/filter_unused"),
            op_contains = S("contentaudit/op_contains"),
            op_equals = S("contentaudit/op_equals"),
            op_startswith = S("contentaudit/op_startswith"),
            op_isempty = S("contentaudit/op_isempty"),
            op_isnotempty = S("contentaudit/op_isnotempty"),
            btn_filter = S("contentaudit/btn_filter"),
            btn_columns = S("contentaudit/btn_columns"),
            btn_selectall = S("contentaudit/btn_selectall"),
            btn_reset = S("contentaudit/btn_reset"),
            btn_addfilter = S("contentaudit/btn_addfilter"),
            btn_clearall = S("contentaudit/btn_clearall"),
            dlg_choosecolumns = S("contentaudit/dlg_choosecolumns"),
            dlg_addfilter = S("contentaudit/dlg_addfilter"),
            lbl_column = S("contentaudit/lbl_column"),
            lbl_operator = S("contentaudit/lbl_operator"),
            lbl_value = S("contentaudit/lbl_value"),
            lbl_filtervalue = S("contentaudit/lbl_filtervalue"),
            lbl_search = S("contentaudit/lbl_search"),
            stat_totalitems = S("contentaudit/stat_totalitems"),
            stat_pages = S("contentaudit/stat_pages"),
            stat_currentpage = S("contentaudit/stat_currentpage"),
            empty_nodata = S("contentaudit/empty_nodata"),
            empty_nofilter = S("contentaudit/empty_nofilter"),
            page_showing = S("contentaudit/page_showing"),
            perpage = S("contentaudit/perpage"),
            fmt_excel = S("contentaudit/fmt_excel"),
            fmt_csv = S("contentaudit/fmt_csv"),
            fmt_json = S("contentaudit/fmt_json"),
            cell_openeditmode = S("contentaudit/cell_openeditmode")
        },
        languageaudit = new
        {
            tab_overview = S("languageaudit/tab_overview"),
            tab_missing = S("languageaudit/tab_missing"),
            tab_stale = S("languageaudit/tab_stale"),
            tab_queue = S("languageaudit/tab_queue"),
            stat_totalcontent = S("languageaudit/stat_totalcontent"),
            stat_languages = S("languageaudit/stat_languages"),
            stat_missing = S("languageaudit/stat_missing"),
            stat_stale = S("languageaudit/stat_stale"),
            stat_itemstotranslate = S("languageaudit/stat_itemstotranslate"),
            stat_page = S("languageaudit/stat_page"),
            stat_missing_lang = S("languageaudit/stat_missing_lang"),
            stat_stale_count = S("languageaudit/stat_stale_count"),
            card_coverage = S("languageaudit/card_coverage"),
            card_contentitems = S("languageaudit/card_contentitems"),
            card_published = S("languageaudit/card_published"),
            lbl_language = S("languageaudit/lbl_language"),
            lbl_threshold = S("languageaudit/lbl_threshold"),
            lbl_targetlang = S("languageaudit/lbl_targetlang"),
            lbl_contenttype = S("languageaudit/lbl_contenttype"),
            lbl_alllanguages = S("languageaudit/lbl_alllanguages"),
            lbl_alltypes = S("languageaudit/lbl_alltypes"),
            btn_coveragetree = S("languageaudit/btn_coveragetree"),
            btn_apply = S("languageaudit/btn_apply"),
            btn_exportcsv = S("languageaudit/btn_exportcsv"),
            btn_runnow = S("languageaudit/btn_runnow"),
            btn_started = S("languageaudit/btn_started"),
            btn_failed = S("languageaudit/btn_failed"),
            col_id = S("languageaudit/col_id"),
            col_name = S("languageaudit/col_name"),
            col_type = S("languageaudit/col_type"),
            col_master = S("languageaudit/col_master"),
            col_available = S("languageaudit/col_available"),
            col_location = S("languageaudit/col_location"),
            col_masterlanguage = S("languageaudit/col_masterlanguage"),
            col_stalelanguage = S("languageaudit/col_stalelanguage"),
            col_daysbehind = S("languageaudit/col_daysbehind"),
            col_lastupdated = S("languageaudit/col_lastupdated"),
            col_available_short = S("languageaudit/col_available_short"),
            empty_all_translated = S("languageaudit/empty_all_translated"),
            empty_no_stale = S("languageaudit/empty_no_stale"),
            empty_no_queue = S("languageaudit/empty_no_queue"),
            empty_no_tree = S("languageaudit/empty_no_tree"),
            empty_nodata = S("languageaudit/empty_nodata"),
            banner_runjob = S("languageaudit/banner_runjob"),
            page_info = S("languageaudit/page_info"),
            btn_previous = S("languageaudit/btn_previous"),
            btn_next = S("languageaudit/btn_next")
        },
        cmsdoctor = new
        {
            header_title = S("cmsdoctor/header_title"),
            header_desc = S("cmsdoctor/header_desc"),
            lbl_lastrun = S("cmsdoctor/lbl_lastrun"),
            btn_runall = S("cmsdoctor/btn_runall"),
            btn_running = S("cmsdoctor/btn_running"),
            sum_healthy = S("cmsdoctor/sum_healthy"),
            sum_warnings = S("cmsdoctor/sum_warnings"),
            sum_faults = S("cmsdoctor/sum_faults"),
            sum_notchecked = S("cmsdoctor/sum_notchecked"),
            tag_all = S("cmsdoctor/tag_all"),
            status_ok = S("cmsdoctor/status_ok"),
            status_warning = S("cmsdoctor/status_warning"),
            status_badpractice = S("cmsdoctor/status_badpractice"),
            status_fault = S("cmsdoctor/status_fault"),
            status_performance = S("cmsdoctor/status_performance"),
            status_notchecked = S("cmsdoctor/status_notchecked"),
            btn_run = S("cmsdoctor/btn_run"),
            btn_fix = S("cmsdoctor/btn_fix"),
            btn_details = S("cmsdoctor/btn_details"),
            btn_dismiss = S("cmsdoctor/btn_dismiss"),
            btn_restore = S("cmsdoctor/btn_restore"),
            dlg_result = S("cmsdoctor/dlg_result"),
            dlg_details = S("cmsdoctor/dlg_details"),
            dlg_categories = S("cmsdoctor/dlg_categories"),
            dlg_checked = S("cmsdoctor/dlg_checked"),
            btn_applyfix = S("cmsdoctor/btn_applyfix"),
            btn_rerun = S("cmsdoctor/btn_rerun"),
            btn_close = S("cmsdoctor/btn_close"),
            confirm_applyfix = S("cmsdoctor/confirm_applyfix"),
            confirm_fix = S("cmsdoctor/confirm_fix")
        },
        contenttypeaudit = new
        {
            alert_running = S("contenttypeaudit/alert_running"),
            alert_notrun = S("contenttypeaudit/alert_notrun"),
            alert_old = S("contenttypeaudit/alert_old"),
            alert_started = S("contenttypeaudit/alert_started"),
            btn_refresh = S("contenttypeaudit/btn_refresh"),
            btn_runnow = S("contenttypeaudit/btn_runnow"),
            btn_starting = S("contenttypeaudit/btn_starting"),
            btn_failed = S("contenttypeaudit/btn_failed"),
            stat_contenttypes = S("contenttypeaudit/stat_contenttypes"),
            stat_systemtypes = S("contenttypeaudit/stat_systemtypes"),
            stat_orphaned = S("contenttypeaudit/stat_orphaned"),
            stat_unused = S("contenttypeaudit/stat_unused"),
            stat_showing = S("contenttypeaudit/stat_showing"),
            lbl_search = S("contenttypeaudit/lbl_search"),
            opt_allbases = S("contenttypeaudit/opt_allbases"),
            chk_showsystem = S("contenttypeaudit/chk_showsystem"),
            btn_table = S("contenttypeaudit/btn_table"),
            btn_tree = S("contenttypeaudit/btn_tree"),
            btn_export = S("contenttypeaudit/btn_export"),
            col_name = S("contenttypeaudit/col_name"),
            col_base = S("contenttypeaudit/col_base"),
            col_group = S("contenttypeaudit/col_group"),
            col_properties = S("contenttypeaudit/col_properties"),
            col_content = S("contenttypeaudit/col_content"),
            opt_all = S("contenttypeaudit/opt_all"),
            badge_system = S("contenttypeaudit/badge_system"),
            badge_orphaned = S("contenttypeaudit/badge_orphaned")
        },
        linkchecker = new
        {
            alert_running = S("linkchecker/alert_running"),
            alert_notrun = S("linkchecker/alert_notrun"),
            alert_lastran = S("linkchecker/alert_lastran"),
            alert_started = S("linkchecker/alert_started"),
            btn_refresh = S("linkchecker/btn_refresh"),
            btn_runnow = S("linkchecker/btn_runnow"),
            btn_starting = S("linkchecker/btn_starting"),
            btn_failed = S("linkchecker/btn_failed"),
            btn_export = S("linkchecker/btn_export"),
            stat_total = S("linkchecker/stat_total"),
            stat_broken = S("linkchecker/stat_broken"),
            stat_valid = S("linkchecker/stat_valid"),
            stat_internal = S("linkchecker/stat_internal"),
            stat_external = S("linkchecker/stat_external"),
            lbl_search = S("linkchecker/lbl_search"),
            opt_alltypes = S("linkchecker/opt_alltypes"),
            opt_internal = S("linkchecker/opt_internal"),
            opt_external = S("linkchecker/opt_external"),
            opt_allstatus = S("linkchecker/opt_allstatus"),
            opt_brokenonly = S("linkchecker/opt_brokenonly"),
            opt_validonly = S("linkchecker/opt_validonly"),
            col_status = S("linkchecker/col_status"),
            col_url = S("linkchecker/col_url"),
            col_type = S("linkchecker/col_type"),
            col_foundin = S("linkchecker/col_foundin"),
            col_checked = S("linkchecker/col_checked"),
            cell_usedon = S("linkchecker/cell_usedon")
        },
        securityaudit = new
        {
            tab_tree = S("securityaudit/tab_tree"),
            tab_roles = S("securityaudit/tab_roles"),
            tab_issues = S("securityaudit/tab_issues"),
            banner_nodata = S("securityaudit/banner_nodata"),
            banner_runjob = S("securityaudit/banner_runjob"),
            banner_started = S("securityaudit/banner_started"),
            alert_old = S("securityaudit/alert_old"),
            btn_runnow = S("securityaudit/btn_runnow"),
            btn_starting = S("securityaudit/btn_starting"),
            btn_refresh = S("securityaudit/btn_refresh"),
            btn_failed = S("securityaudit/btn_failed"),
            stat_analyzed = S("securityaudit/stat_analyzed"),
            stat_roles = S("securityaudit/stat_roles"),
            stat_issues = S("securityaudit/stat_issues"),
            stat_lastanalysis = S("securityaudit/stat_lastanalysis"),
            pager_previous = S("securityaudit/pager_previous"),
            pager_info = S("securityaudit/pager_info"),
            pager_next = S("securityaudit/pager_next")
        },
        bulkeditor = new
        {
            opt_selecttype = S("bulkeditor/opt_selecttype"),
            btn_columns = S("bulkeditor/btn_columns"),
            btn_addfilter = S("bulkeditor/btn_addfilter"),
            chk_includereferences = S("bulkeditor/chk_includereferences"),
            btn_applyfilters = S("bulkeditor/btn_applyfilters"),
            empty_selecttype = S("bulkeditor/empty_selecttype"),
            lbl_perpage = S("bulkeditor/lbl_perpage"),
            pending_count = S("bulkeditor/pending_count"),
            btn_discardall = S("bulkeditor/btn_discardall"),
            btn_saveall = S("bulkeditor/btn_saveall"),
            btn_publishall = S("bulkeditor/btn_publishall"),
            confirm_discard = S("bulkeditor/confirm_discard")
        },
        contentstatistics = new
        {
            banner_runjob = S("contentstatistics/banner_runjob"),
            btn_runnow = S("contentstatistics/btn_runnow"),
            btn_starting = S("contentstatistics/btn_starting"),
            error_load = S("contentstatistics/error_load"),
            error_render = S("contentstatistics/error_render"),
            chart_byctype = S("contentstatistics/chart_byctype"),
            chart_bystatus = S("contentstatistics/chart_bystatus"),
            chart_created = S("contentstatistics/chart_created"),
            chart_staleness = S("contentstatistics/chart_staleness"),
            chart_editoractivity = S("contentstatistics/chart_editoractivity"),
            stat_totalcontent = S("contentstatistics/stat_totalcontent"),
            stat_published = S("contentstatistics/stat_published"),
            stat_draft = S("contentstatistics/stat_draft"),
            stat_editors = S("contentstatistics/stat_editors"),
            stat_pages = S("contentstatistics/stat_pages"),
            stat_blocks = S("contentstatistics/stat_blocks"),
            stat_media = S("contentstatistics/stat_media")
        },
        activitytimeline = new
        {
            stat_today = S("activitytimeline/stat_today"),
            stat_activeeditors = S("activitytimeline/stat_activeeditors"),
            stat_publishes = S("activitytimeline/stat_publishes"),
            stat_drafts = S("activitytimeline/stat_drafts"),
            opt_allusers = S("activitytimeline/opt_allusers"),
            opt_published = S("activitytimeline/opt_published"),
            opt_draft = S("activitytimeline/opt_draft"),
            opt_readytopublish = S("activitytimeline/opt_readytopublish"),
            opt_scheduled = S("activitytimeline/opt_scheduled"),
            opt_rejected = S("activitytimeline/opt_rejected"),
            opt_previouslypublished = S("activitytimeline/opt_previouslypublished"),
            opt_comment = S("activitytimeline/opt_comment"),
            opt_allcontenttypes = S("activitytimeline/opt_allcontenttypes"),
            btn_filter = S("activitytimeline/btn_filter"),
            btn_clear = S("activitytimeline/btn_clear"),
            banner_showingfor = S("activitytimeline/banner_showingfor"),
            btn_showall = S("activitytimeline/btn_showall")
        },
        personalizationaudit = new
        {
            alert_running = S("personalizationaudit/alert_running"),
            alert_notrun = S("personalizationaudit/alert_notrun"),
            alert_old = S("personalizationaudit/alert_old"),
            btn_runnow = S("personalizationaudit/btn_runnow"),
            btn_refresh = S("personalizationaudit/btn_refresh"),
            btn_starting = S("personalizationaudit/btn_starting"),
            stat_total = S("personalizationaudit/stat_total"),
            stat_content = S("personalizationaudit/stat_content"),
            stat_groups = S("personalizationaudit/stat_groups"),
            lbl_search = S("personalizationaudit/lbl_search"),
            lbl_type = S("personalizationaudit/lbl_type"),
            lbl_group = S("personalizationaudit/lbl_group"),
            opt_alltypes = S("personalizationaudit/opt_alltypes"),
            opt_allgroups = S("personalizationaudit/opt_allgroups"),
            col_content = S("personalizationaudit/col_content"),
            col_type = S("personalizationaudit/col_type"),
            col_groups = S("personalizationaudit/col_groups"),
            col_location = S("personalizationaudit/col_location")
        },
        audiencemanager = new
        {
            stat_audiences = S("audiencemanager/stat_audiences"),
            stat_withstats = S("audiencemanager/stat_withstats"),
            stat_categories = S("audiencemanager/stat_categories"),
            stat_totalcriteria = S("audiencemanager/stat_totalcriteria"),
            stat_showing = S("audiencemanager/stat_showing"),
            lbl_search = S("audiencemanager/lbl_search"),
            opt_allcategories = S("audiencemanager/opt_allcategories"),
            chk_hasstats = S("audiencemanager/chk_hasstats"),
            lnk_personalizationaudit = S("audiencemanager/lnk_personalizationaudit"),
            btn_export = S("audiencemanager/btn_export"),
            col_name = S("audiencemanager/col_name"),
            col_criteria = S("audiencemanager/col_criteria"),
            col_operator = S("audiencemanager/col_operator"),
            col_statistics = S("audiencemanager/col_statistics"),
            col_usage = S("audiencemanager/col_usage"),
            opt_all = S("audiencemanager/opt_all"),
            title_usagedetails = S("audiencemanager/title_usagedetails"),
            title_notused = S("audiencemanager/title_notused"),
            title_runjobfirst = S("audiencemanager/title_runjobfirst")
        },
        contentimporter = new
        {
            step_upload = S("contentimporter/step_upload"),
            step_configure = S("contentimporter/step_configure"),
            step_dryrun = S("contentimporter/step_dryrun"),
            step_import = S("contentimporter/step_import"),
            lbl_dropfile = S("contentimporter/lbl_dropfile"),
            btn_browse = S("contentimporter/btn_browse"),
            lbl_contenttype = S("contentimporter/lbl_contenttype"),
            lbl_language = S("contentimporter/lbl_language"),
            lbl_parent = S("contentimporter/lbl_parent"),
            lbl_namecol = S("contentimporter/lbl_namecol"),
            lbl_publishafter = S("contentimporter/lbl_publishafter"),
            lbl_selectparent = S("contentimporter/lbl_selectparent"),
            btn_next = S("contentimporter/btn_next"),
            btn_back = S("contentimporter/btn_back"),
            lbl_mapping = S("contentimporter/lbl_mapping"),
            lbl_sourcecol = S("contentimporter/lbl_sourcecol"),
            lbl_targetfield = S("contentimporter/lbl_targetfield"),
            opt_skip = S("contentimporter/opt_skip"),
            btn_dryrun = S("contentimporter/btn_dryrun"),
            lbl_dryresult = S("contentimporter/lbl_dryresult"),
            btn_import = S("contentimporter/btn_import"),
            btn_startover = S("contentimporter/btn_startover"),
            lbl_importing = S("contentimporter/lbl_importing")
        },
        managechildren = new
        {
            lbl_selectparent = S("managechildren/lbl_selectparent"),
            btn_selectparent = S("managechildren/btn_selectparent"),
            btn_sortaz = S("managechildren/btn_sortaz"),
            btn_sortza = S("managechildren/btn_sortza"),
            btn_sortbydate = S("managechildren/btn_sortbydate"),
            btn_publishall = S("managechildren/btn_publishall"),
            btn_unpublishall = S("managechildren/btn_unpublishall"),
            btn_deleteselected = S("managechildren/btn_deleteselected"),
            btn_saveorder = S("managechildren/btn_saveorder"),
            col_name = S("managechildren/col_name"),
            col_status = S("managechildren/col_status"),
            col_changed = S("managechildren/col_changed"),
            col_type = S("managechildren/col_type"),
            confirm_delete = S("managechildren/confirm_delete"),
            confirm_publishall = S("managechildren/confirm_publishall"),
            confirm_unpublishall = S("managechildren/confirm_unpublishall")
        },
        recommendations = new
        {
            banner_info = S("recommendations/banner_info"),
            btn_addrule = S("recommendations/btn_addrule"),
            col_parenttype = S("recommendations/col_parenttype"),
            col_allowedtypes = S("recommendations/col_allowedtypes"),
            col_actions = S("recommendations/col_actions"),
            dlg_addrule = S("recommendations/dlg_addrule"),
            dlg_editrule = S("recommendations/dlg_editrule"),
            lbl_parenttype = S("recommendations/lbl_parenttype"),
            lbl_allowedtypes = S("recommendations/lbl_allowedtypes"),
            btn_save = S("recommendations/btn_save"),
            btn_cancel = S("recommendations/btn_cancel"),
            btn_edit = S("recommendations/btn_edit"),
            btn_delete = S("recommendations/btn_delete"),
            empty_norules = S("recommendations/empty_norules"),
            confirm_delete = S("recommendations/confirm_delete")
        },
        gantt = new
        {
            error_load = S("gantt/error_load"),
            empty_nojobs = S("gantt/empty_nojobs"),
            stat_totaljobs = S("gantt/stat_totaljobs"),
            stat_enabled = S("gantt/stat_enabled"),
            stat_running = S("gantt/stat_running"),
            stat_executions = S("gantt/stat_executions"),
            btn_previous = S("gantt/btn_previous"),
            btn_today = S("gantt/btn_today"),
            btn_next = S("gantt/btn_next"),
            lbl_viewhours = S("gantt/lbl_viewhours"),
            col_job = S("gantt/col_job")
        },
        activeeditors = new
        {
            error_nosignalr = S("activeeditors/error_nosignalr"),
            error_connect = S("activeeditors/error_connect"),
            stat_online = S("activeeditors/stat_online"),
            stat_editing = S("activeeditors/stat_editing"),
            stat_today = S("activeeditors/stat_today"),
            section_online = S("activeeditors/section_online"),
            section_today = S("activeeditors/section_today"),
            lbl_connected = S("activeeditors/lbl_connected"),
            btn_sendmessage = S("activeeditors/btn_sendmessage"),
            badge_you = S("activeeditors/badge_you"),
            badge_offline = S("activeeditors/badge_offline"),
            chat_title = S("activeeditors/chat_title"),
            chat_empty = S("activeeditors/chat_empty"),
            chat_placeholder = S("activeeditors/chat_placeholder"),
            dlg_sendmessage = S("activeeditors/dlg_sendmessage"),
            dlg_desc = S("activeeditors/dlg_desc"),
            dlg_placeholder = S("activeeditors/dlg_placeholder"),
            btn_cancel = S("activeeditors/btn_cancel"),
            btn_send = S("activeeditors/btn_send"),
            btn_sending = S("activeeditors/btn_sending"),
            msg_sent = S("activeeditors/msg_sent")
        },
        components = new
        {
            picker_title = S("components/picker_title"),
            picker_search = S("components/picker_search"),
            picker_noresults = S("components/picker_noresults"),
            btn_cancel = S("components/btn_cancel"),
            btn_select = S("components/btn_select"),
            typepicker_title = S("components/typepicker_title"),
            typepicker_search = S("components/typepicker_search"),
            typepicker_noresults = S("components/typepicker_noresults")
        },
        editorpowertools = new
        {
            loading = S("editorpowertools/loading"),
            noresults = S("editorpowertools/noresults")
        }
    };
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Localization/UiStringsProvider.cs
git commit -m "feat: add UiStringsProvider for JS localization injection"
```

---

### Task 4: Register UiStringsProvider

**Files:**
- Modify: `src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Add using and registration**

Add this using at the top of the file:
```csharp
using EditorPowertools.Localization;
```

Inside `AddEditorPowertools(this IServiceCollection services, Action<EditorPowertoolsOptions> configureOptions)`, add after the existing registrations:
```csharp
services.AddScoped<UiStringsProvider>();
```

- [ ] **Step 2: Build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/EditorPowertools/Infrastructure/ServiceCollectionExtensions.cs
git commit -m "feat: register UiStringsProvider as scoped service"
```

---

### Task 5: Inject `window.EPT_STRINGS` in the layout

**Files:**
- Modify: `src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml`

- [ ] **Step 1: Add @inject and the script block**

At the top of `_PowertoolsLayout.cshtml`, after the existing `@using` lines, add:
```csharp
@inject EditorPowertools.Localization.UiStringsProvider UiStrings
@using System.Text.Json
```

In the existing `<script>` block that defines `window.EPT_BASE_URL` etc., add a new line at the end:
```javascript
window.EPT_STRINGS = @Html.Raw(JsonSerializer.Serialize(UiStrings.GetAll()));
```

The complete script block should look like:
```html
<script>
    window.EPT_BASE_URL = '@Html.Raw(Paths.ToResource(typeof(EditorPowertools.Menu.EditorPowertoolsMenuProvider), ""))';
    window.EPT_API_URL = '/editorpowertools/api';
    window.EPT_HUB_URL = '/editorpowertools/hubs';
    window.EPT_CMS_URL = '@Html.Raw(Paths.ToResource("CMS", ""))';
    window.EPT_ADMIN_URL = '@Html.Raw(Paths.ToResource("EPiServer.Cms.UI.Admin", "default"))';
    window.EPT_VG_URL = '@Html.Raw(Paths.ToResource("EPiServer.Cms.UI.VisitorGroups", "ManageVisitorGroups"))';
    window.EPT_STRINGS = @Html.Raw(JsonSerializer.Serialize(UiStrings.GetAll()));
</script>
```

- [ ] **Step 2: Build and verify layout compiles**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Start the sample site and verify EPT_STRINGS is populated**

```bash
dotnet run --project src/EditorPowertools.SampleSite 2>&1 &
```

Open a browser, navigate to any Editor Powertools page, open DevTools console, type `window.EPT_STRINGS` — should show a large object with all tool namespaces.

- [ ] **Step 4: Commit**

```bash
git add src/EditorPowertools/Views/Shared/_PowertoolsLayout.cshtml
git commit -m "feat: inject window.EPT_STRINGS from UiStringsProvider into layout"
```

---

### Task 6: Add `EPT.s()` to editorpowertools.js, update `showLoading` and `createTable`

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/editorpowertools.js`

- [ ] **Step 1: Add `EPT.s()` function**

After the existing `EPT` object properties (after the `downloadCsv` function or wherever the object ends, before the closing `};`), add:

```js
/**
 * Safe accessor for window.EPT_STRINGS.
 * path: dot-separated key e.g. 'contentaudit.col_name'
 * fallback: English string returned if key is missing
 */
s: function(path, fallback) {
    try {
        var parts = path.split('.');
        var obj = window.EPT_STRINGS;
        for (var i = 0; i < parts.length; i++) {
            if (obj === undefined || obj === null) return fallback || path;
            obj = obj[parts[i]];
        }
        return (obj && typeof obj === 'string') ? obj : (fallback || path);
    } catch (e) {
        return fallback || path;
    }
},
```

- [ ] **Step 2: Update `showLoading` to use the localized string**

Find:
```js
showLoading(el) {
    el.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>Loading...</p></div>';
},
```

Replace with:
```js
showLoading(el) {
    var msg = (window.EPT_STRINGS && window.EPT_STRINGS.editorpowertools && window.EPT_STRINGS.editorpowertools.loading)
        ? window.EPT_STRINGS.editorpowertools.loading : 'Loading...';
    el.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>' + msg + '</p></div>';
},
```

- [ ] **Step 3: Update `createTable` empty state**

In the `createTable` function, find:
```js
tr.innerHTML = `<td colspan="${columns.length}" class="ept-empty"><p>No results found</p></td>`;
```

Replace with:
```js
var noResultsMsg = (window.EPT_STRINGS && window.EPT_STRINGS.editorpowertools && window.EPT_STRINGS.editorpowertools.noresults)
    ? window.EPT_STRINGS.editorpowertools.noresults : 'No results found';
tr.innerHTML = '<td colspan="' + columns.length + '" class="ept-empty"><p>' + noResultsMsg + '</p></td>';
```

Note: this is inside a template literal context in the original — convert to string concatenation as shown.

- [ ] **Step 4: Build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
```

- [ ] **Step 5: Commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/editorpowertools.js
git commit -m "feat: add EPT.s() accessor and localize showLoading/createTable"
```

---

### Task 7: Localize content-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js`

**Convention for this and all following tasks:** Replace `'Hardcoded string'` with `EPT.s('tool.key', 'Hardcoded string')`. The second argument is the English fallback, ensuring the UI never shows a raw key path.

- [ ] **Step 1: Replace ALL_COLUMNS labels**

Find the `ALL_COLUMNS` array and replace each `label:` value:

```js
var ALL_COLUMNS = [
    { key: 'contentId', label: EPT.s('contentaudit.col_contentid', 'Content ID'), sortable: true, defaultVisible: true, align: 'right' },
    { key: 'name', label: EPT.s('contentaudit.col_name', 'Name'), sortable: true, defaultVisible: true },
    { key: 'language', label: EPT.s('contentaudit.col_language', 'Language'), sortable: true, defaultVisible: true },
    { key: 'contentType', label: EPT.s('contentaudit.col_contenttype', 'Content Type'), sortable: true, defaultVisible: true },
    { key: 'mainType', label: EPT.s('contentaudit.col_maintype', 'Main Type'), sortable: true, defaultVisible: true },
    { key: 'url', label: EPT.s('contentaudit.col_url', 'URL'), sortable: true, defaultVisible: false },
    { key: 'editUrl', label: EPT.s('contentaudit.col_editurl', 'Edit URL'), sortable: false, defaultVisible: false },
    { key: 'breadcrumb', label: EPT.s('contentaudit.col_breadcrumb', 'Breadcrumb'), sortable: true, defaultVisible: false },
    { key: 'status', label: EPT.s('contentaudit.col_status', 'Status'), sortable: true, defaultVisible: true },
    { key: 'createdBy', label: EPT.s('contentaudit.col_createdby', 'Created By'), sortable: true, defaultVisible: false },
    { key: 'created', label: EPT.s('contentaudit.col_created', 'Created'), sortable: true, defaultVisible: false, type: 'date' },
    { key: 'changedBy', label: EPT.s('contentaudit.col_changedby', 'Changed By'), sortable: true, defaultVisible: true },
    { key: 'changed', label: EPT.s('contentaudit.col_changed', 'Changed'), sortable: true, defaultVisible: true, type: 'date' },
    { key: 'published', label: EPT.s('contentaudit.col_published', 'Published'), sortable: true, defaultVisible: true, type: 'date' },
    { key: 'publishedUntil', label: EPT.s('contentaudit.col_publisheduntil', 'Published Until'), sortable: true, defaultVisible: false, type: 'date' },
    { key: 'masterLanguage', label: EPT.s('contentaudit.col_masterlanguage', 'Master Language'), sortable: true, defaultVisible: false },
    { key: 'allLanguages', label: EPT.s('contentaudit.col_alllanguages', 'All Languages'), sortable: true, defaultVisible: false },
    { key: 'referenceCount', label: EPT.s('contentaudit.col_referencecount', 'Reference Count'), sortable: true, defaultVisible: false, align: 'right' },
    { key: 'versionCount', label: EPT.s('contentaudit.col_versioncount', 'Version Count'), sortable: true, defaultVisible: false, align: 'right' },
    { key: 'hasPersonalizations', label: EPT.s('contentaudit.col_haspersonalizations', 'Has Personalizations'), sortable: true, defaultVisible: false }
];
```

- [ ] **Step 2: Replace QUICK_FILTERS labels**

```js
var QUICK_FILTERS = [
    { key: '', label: EPT.s('contentaudit.filter_all', 'All content') },
    { key: 'pages', label: EPT.s('contentaudit.filter_pages', 'Pages only') },
    { key: 'blocks', label: EPT.s('contentaudit.filter_blocks', 'Blocks only') },
    { key: 'media', label: EPT.s('contentaudit.filter_media', 'Media only') },
    { key: 'unpublished', label: EPT.s('contentaudit.filter_unpublished', 'Unpublished') },
    { key: 'unused', label: EPT.s('contentaudit.filter_unused', 'Unused content') }
];
```

- [ ] **Step 3: Replace FILTER_OPERATORS labels**

```js
var FILTER_OPERATORS = [
    { key: 'contains', label: EPT.s('contentaudit.op_contains', 'Contains') },
    { key: 'equals', label: EPT.s('contentaudit.op_equals', 'Equals') },
    { key: 'startsWith', label: EPT.s('contentaudit.op_startswith', 'Starts with') },
    { key: 'isEmpty', label: EPT.s('contentaudit.op_isempty', 'Is empty') },
    { key: 'isNotEmpty', label: EPT.s('contentaudit.op_isnotempty', 'Is not empty') }
];
```

- [ ] **Step 4: Replace hardcoded strings in renderStats()**

```js
// Find:
'<div class="ept-stat__label">Total items</div>'
// Replace with:
'<div class="ept-stat__label">' + EPT.s('contentaudit.stat_totalitems', 'Total items') + '</div>'

// Find:
'<div class="ept-stat__label">Pages</div>'
// Replace with:
'<div class="ept-stat__label">' + EPT.s('contentaudit.stat_pages', 'Pages') + '</div>'

// Find:
'<div class="ept-stat__label">Current page</div>'
// Replace with:
'<div class="ept-stat__label">' + EPT.s('contentaudit.stat_currentpage', 'Current page') + '</div>'
```

- [ ] **Step 5: Replace hardcoded strings in renderToolbar()**

```js
// Search placeholder
// Find: placeholder="Search by name..."
// Replace: placeholder="' + EPT.s('contentaudit.lbl_search', 'Search by name...') + '"

// Find: '>Filter'
// Replace: '>' + EPT.s('contentaudit.btn_filter', 'Filter')
// (also update the filter count display: it should stay as: EPT.s('contentaudit.btn_filter','Filter') + (state.filters.length > 0 ? ' (' + state.filters.length + ')' : ''))

// Find: '>Columns</button>'
// Replace: '>' + EPT.s('contentaudit.btn_columns', 'Columns') + '</button>'

// Find: '>Export</button>'
// Replace: '>' + EPT.s('contentaudit.btn_export', 'Export') + '</button>' (use shared.export)

// Find: '>Excel (.xlsx)</a>'
// Replace: '>' + EPT.s('contentaudit.fmt_excel', 'Excel (.xlsx)') + '</a>'

// Find: '>CSV (.csv)</a>'
// Replace: '>' + EPT.s('contentaudit.fmt_csv', 'CSV (.csv)') + '</a>'

// Find: '>JSON (.json)</a>'
// Replace: '>' + EPT.s('contentaudit.fmt_json', 'JSON (.json)') + '</a>'

// Find: '>Clear all</button>'
// Replace: '>' + EPT.s('contentaudit.btn_clearall', 'Clear all') + '</button>'
```

- [ ] **Step 6: Replace hardcoded strings in renderTable()**

```js
// Find: '<div class="ept-empty"><p>No data loaded.</p></div>'
// Replace: '<div class="ept-empty"><p>' + EPT.s('contentaudit.empty_nodata', 'No data loaded.') + '</p></div>'

// Find: '<div class="ept-empty"><p>No content matches the current filters.</p></div>'
// Replace: '<div class="ept-empty"><p>' + EPT.s('contentaudit.empty_nofilter', 'No content matches the current filters.') + '</p></div>'
```

- [ ] **Step 7: Replace in renderCell()**

```js
// Find: title="Open in edit mode"
// Replace: title="' + EPT.s('contentaudit.cell_openeditmode', 'Open in edit mode') + '"

// Find: '>Open</a>'
// Replace: '>' + EPT.s('shared.open', 'Open') + '</a>'

// Find: '>Yes</span>' (hasPersonalizations true)
// Replace: '>' + EPT.s('shared.yes', 'Yes') + '</span>'

// Find: return '<td>No</td>' (hasPersonalizations false)
// Replace: return '<td>' + EPT.s('shared.no', 'No') + '</td>'
```

- [ ] **Step 8: Replace in renderPagination()**

```js
// Find: 'Showing ' + (((d.page-1)*d.pageSize)+1) + '-' + Math.min(...) + ' of ' + d.totalCount.toLocaleString()
// Replace the Showing string using format substitution:
var showingStr = EPT.s('contentaudit.page_showing', 'Showing {0}-{1} of {2}')
    .replace('{0}', ((d.page - 1) * d.pageSize) + 1)
    .replace('{1}', Math.min(d.page * d.pageSize, d.totalCount))
    .replace('{2}', d.totalCount.toLocaleString());

// Find: '>&laquo; Prev</button>'
// Replace: '>&laquo; ' + EPT.s('shared.prev', 'Prev') + '</button>'

// Find: '>Next &raquo;</button>'
// Replace: '>' + EPT.s('shared.next', 'Next') + ' &raquo;</button>'

// Find: sizes[j] + ' per page'
// Replace: EPT.s('contentaudit.perpage', '{0} per page').replace('{0}', sizes[j])
```

- [ ] **Step 9: Replace dialog strings in openColumnPicker()**

```js
// Find: EPT.openDialog('Choose Columns')
// Replace: EPT.openDialog(EPT.s('contentaudit.dlg_choosecolumns', 'Choose Columns'))

// Find: '>Select all</button>'
// Replace: '>' + EPT.s('contentaudit.btn_selectall', 'Select all') + '</button>'

// Find: '>Reset</button>'
// Replace: '>' + EPT.s('contentaudit.btn_reset', 'Reset') + '</button>'

// Find: '>Apply</button>'
// Replace: '>' + EPT.s('contentaudit.btn_apply', 'Apply') + '</button>' (use shared.apply)
```

Wait — `btn_apply` isn't in the contentaudit section but `shared.apply` is. Use `EPT.s('shared.apply', 'Apply')`.

- [ ] **Step 10: Replace dialog strings in openFilterDialog()**

```js
// Find: EPT.openDialog('Add Filter')
// Replace: EPT.openDialog(EPT.s('contentaudit.dlg_addfilter', 'Add Filter'))

// Find: '>Column</label>'
// Replace: '>' + EPT.s('contentaudit.lbl_column', 'Column') + '</label>'

// Find: '>Operator</label>'
// Replace: '>' + EPT.s('contentaudit.lbl_operator', 'Operator') + '</label>'

// Find: '>Value</label>'
// Replace: '>' + EPT.s('contentaudit.lbl_value', 'Value') + '</label>'

// Find: placeholder="Filter value..."
// Replace: placeholder="' + EPT.s('contentaudit.lbl_filtervalue', 'Filter value...') + '"

// Find: '>Cancel</button>'
// Replace: '>' + EPT.s('shared.cancel', 'Cancel') + '</button>'

// Find: '>Add filter</button>'
// Replace: '>' + EPT.s('contentaudit.btn_addfilter', 'Add filter') + '</button>'
```

- [ ] **Step 11: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-audit.js
git commit -m "feat: localize all strings in content-audit.js"
```

---

### Task 8: Localize language-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/language-audit.js`

- [ ] **Step 1: Replace tab labels in `renderTabs()`**

```js
const tabDefs = [
    { id: 'overview', label: EPT.s('languageaudit.tab_overview', 'Overview') },
    { id: 'missing', label: EPT.s('languageaudit.tab_missing', 'Missing Translations') },
    { id: 'stale', label: EPT.s('languageaudit.tab_stale', 'Stale Translations') },
    { id: 'queue', label: EPT.s('languageaudit.tab_queue', 'Translation Queue') }
];
```

- [ ] **Step 2: Replace stat labels in `renderOverview()`**

```js
// Find: '<div class="ept-stat__label">Total Content</div>'
// Replace each label:
`<div class="ept-stat__label">${EPT.s('languageaudit.stat_totalcontent', 'Total Content')}</div>`
`<div class="ept-stat__label">${EPT.s('languageaudit.stat_languages', 'Languages')}</div>`
`<div class="ept-stat__label">${EPT.s('languageaudit.stat_missing', 'Missing Translations')}</div>`
`<div class="ept-stat__label">${EPT.s('languageaudit.stat_stale', 'Stale (30+ days)')}</div>`

// Card header:
// Find: '<h3>Language Coverage</h3>'
// Replace: `<h3>${EPT.s('languageaudit.card_coverage', 'Language Coverage')}</h3>`

// Empty:
// Find: EPT.showEmpty(body, 'No language data available. Run the aggregation job to collect statistics.')
// Replace: EPT.showEmpty(body, EPT.s('languageaudit.empty_nodata', 'No language data available. Run the aggregation job to collect statistics.'))

// Language card content items / published:
// Find: `<span>${stat.totalContent} content items</span>`
// Replace: `<span>${EPT.s('languageaudit.card_contentitems', '{0} content items').replace('{0}', stat.totalContent)}</span>`
// Find: `<span>${stat.publishedCount} published</span>`
// Replace: `<span>${EPT.s('languageaudit.card_published', '{0} published').replace('{0}', stat.publishedCount)}</span>`
```

- [ ] **Step 3: Replace toolbar and button labels in `renderMissing()`**

```js
// Find: 'Language:'
// Replace: EPT.s('languageaudit.lbl_language', 'Language:')

// Find: `${EPT.icons.tree} Coverage Tree`
// Replace: `${EPT.icons.tree} ${EPT.s('languageaudit.btn_coveragetree', 'Coverage Tree')}`
```

- [ ] **Step 4: Replace in `loadMissing()`**

```js
// Missing lang stat:
// Find: `Missing ${escHtml(selectedLanguage)}`
// Replace: EPT.s('languageaudit.stat_missing_lang', 'Missing {0}').replace('{0}', escHtml(selectedLanguage))

// Empty state:
// Find: `All content has been translated to ${selectedLanguage}`
// Replace: EPT.s('languageaudit.empty_all_translated', 'All content has been translated to {0}').replace('{0}', selectedLanguage)

// Column labels (columns array):
{ key: 'contentId', label: EPT.s('languageaudit.col_id', 'ID'), align: 'right' },
{ key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), ... },
{ key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
{ key: 'masterLanguage', label: EPT.s('languageaudit.col_master', 'Master') },
{ key: 'availableLanguages', label: EPT.s('languageaudit.col_available', 'Available Languages'), ... },
{ key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), ... }
```

- [ ] **Step 5: Replace in `renderStale()` toolbar**

```js
// Find: 'Threshold (days):'
// Replace: EPT.s('languageaudit.lbl_threshold', 'Threshold (days):')

// Find: 'All languages'
// Replace: EPT.s('languageaudit.lbl_alllanguages', 'All languages')

// Find: `${EPT.icons.search} Apply`
// Replace: `${EPT.icons.search} ${EPT.s('languageaudit.btn_apply', 'Apply')}`
```

- [ ] **Step 6: Replace in `loadStale()`**

```js
// Stat label:
// Find: 'Stale Translations'
// Replace: EPT.s('languageaudit.stat_stale_count', 'Stale Translations')

// Empty:
// Find: `No stale translations found (threshold: ${threshold} days)`
// Replace: EPT.s('languageaudit.empty_no_stale', 'No stale translations found (threshold: {0} days)').replace('{0}', threshold)

// Column labels in stale columns array:
{ key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), ... },
{ key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
{ key: 'masterLanguage', label: EPT.s('languageaudit.col_masterlanguage', 'Master Language'), ... },
{ key: 'otherLanguage', label: EPT.s('languageaudit.col_stalelanguage', 'Stale Language'), ... },
{ key: 'daysBehind', label: EPT.s('languageaudit.col_daysbehind', 'Days Behind'), ... },
{ key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), ... }
```

- [ ] **Step 7: Replace in `renderQueue()` toolbar**

```js
// Find: 'Target Language:'
// Replace: EPT.s('languageaudit.lbl_targetlang', 'Target Language:')

// Find: 'Content Type:'
// Replace: EPT.s('languageaudit.lbl_contenttype', 'Content Type:')

// Find: placeholder="All types"
// Replace: placeholder="${EPT.s('languageaudit.lbl_alltypes', 'All types')}"

// Find: `${EPT.icons.download} Export CSV`
// Replace: `${EPT.icons.download} ${EPT.s('languageaudit.btn_exportcsv', 'Export CSV')}`
```

- [ ] **Step 8: Replace in `loadQueue()`**

```js
// Stat labels:
// Find: 'Items to Translate'
// Replace: EPT.s('languageaudit.stat_itemstotranslate', 'Items to Translate')
// Find: 'Page'
// Replace: EPT.s('languageaudit.stat_page', 'Page')

// Empty:
// Find: `No content needs translation to ${targetLang}`
// Replace: EPT.s('languageaudit.empty_no_queue', 'No content needs translation to {0}').replace('{0}', targetLang)

// Column labels:
{ key: 'contentId', label: EPT.s('languageaudit.col_id', 'ID'), align: 'right' },
{ key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), ... },
{ key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
{ key: 'masterLanguage', label: EPT.s('languageaudit.col_master', 'Master') },
{ key: 'masterLastModified', label: EPT.s('languageaudit.col_lastupdated', 'Last Updated'), ... },
{ key: 'availableLanguages', label: EPT.s('languageaudit.col_available_short', 'Available'), ... },
{ key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), ... }

// Pagination buttons:
// Find: prev.textContent = 'Previous'
// Replace: prev.textContent = EPT.s('languageaudit.btn_previous', 'Previous')
// Find: info.textContent = `Page ${result.page} of ${result.totalPages}`
// Replace: info.textContent = EPT.s('languageaudit.page_info','Page {0} of {1}').replace('{0}',result.page).replace('{1}',result.totalPages)
// Find: next.textContent = 'Next'
// Replace: next.textContent = EPT.s('languageaudit.btn_next', 'Next')
```

- [ ] **Step 9: Replace in `renderNoDataBanner()`**

```js
// Find: 'Run the <strong>[EditorPowertools] Content Analysis</strong> scheduled job to populate data.'
// Replace with separate text + strong:
// The banner text should use EPT.s('languageaudit.banner_runjob', 'Run the [EditorPowertools] Content Analysis scheduled job to populate data.')
// Keep the <strong> formatting — the key contains the full sentence without HTML, so render as text:
`<p style="...">${EPT.s('languageaudit.banner_runjob', 'Run the [EditorPowertools] Content Analysis scheduled job to populate data.')}</p>`

// Button:
// Find: btn.textContent = 'Run now'
// Replace: btn.textContent = EPT.s('languageaudit.btn_runnow', 'Run now')
// Find: btn.textContent = 'Starting...'
// Replace: btn.textContent = EPT.s('shared.starting', 'Starting...')
// Find: btn.textContent = 'Job started, please refresh in a few minutes.'
// Replace: btn.textContent = EPT.s('languageaudit.btn_started', 'Job started, please refresh in a few minutes.')
// Find: btn.textContent = 'Failed to start job'
// Replace: btn.textContent = EPT.s('languageaudit.btn_failed', 'Failed to start job')
```

- [ ] **Step 10: Replace in `loadCoverageTree()`**

```js
// Find: EPT.showEmpty(body, 'No coverage tree data available')
// Replace: EPT.showEmpty(body, EPT.s('languageaudit.empty_no_tree', 'No coverage tree data available'))
```

- [ ] **Step 11: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/language-audit.js
git commit -m "feat: localize all strings in language-audit.js"
```

---

### Task 9: Localize cms-doctor.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/cms-doctor.js`

- [ ] **Step 1: Replace statusConfig labels**

```js
var statusConfig = {
    'OK':          { ..., label: EPT.s('cmsdoctor.status_ok', 'Healthy') },
    'Warning':     { ..., label: EPT.s('cmsdoctor.status_warning', 'Warning') },
    'BadPractice': { ..., label: EPT.s('cmsdoctor.status_badpractice', 'Bad Practice') },
    'Fault':       { ..., label: EPT.s('cmsdoctor.status_fault', 'Fault') },
    'Performance': { ..., label: EPT.s('cmsdoctor.status_performance', 'Performance') },
    'NotChecked':  { ..., label: EPT.s('cmsdoctor.status_notchecked', 'Not Checked') }
};
```

Keep all other properties (`color`, `bg`, `border`, `icon`) as they are. Only `label` changes.

- [ ] **Step 2: Replace header and run-all in `render()`**

```js
// Find: '<h1>CMS Doctor</h1>'
// Replace: '<h1>' + EPT.s('cmsdoctor.header_title', 'CMS Doctor') + '</h1>'

// Find: '<p>Health checks for your Optimizely CMS. Extensible by third-party packages.</p>'
// Replace: '<p>' + EPT.s('cmsdoctor.header_desc', 'Health checks for your Optimizely CMS. Extensible by third-party packages.') + '</p>'

// Find: 'Last run: ' + new Date(d.lastFullCheck).toLocaleString()
// Replace: EPT.s('cmsdoctor.lbl_lastrun', 'Last run: {0}').replace('{0}', new Date(d.lastFullCheck).toLocaleString())

// Find: state.loading ? 'Running...' : 'Run All Checks'
// Replace: state.loading ? EPT.s('cmsdoctor.btn_running', 'Running...') : EPT.s('cmsdoctor.btn_runall', 'Run All Checks')
```

- [ ] **Step 3: Replace summary pill labels in `summaryPill()` calls**

```js
// Find: summaryPill(d.okCount, 'Healthy', 'OK')
// Replace: summaryPill(d.okCount, EPT.s('cmsdoctor.sum_healthy', 'Healthy'), 'OK')

// Find: summaryPill(d.warningCount, 'Warnings', 'Warning')
// Replace: summaryPill(d.warningCount, EPT.s('cmsdoctor.sum_warnings', 'Warnings'), 'Warning')

// Find: summaryPill(d.faultCount, 'Faults', 'Fault')
// Replace: summaryPill(d.faultCount, EPT.s('cmsdoctor.sum_faults', 'Faults'), 'Fault')

// Find: summaryPill(d.notCheckedCount, 'Not Checked', 'NotChecked')
// Replace: summaryPill(d.notCheckedCount, EPT.s('cmsdoctor.sum_notchecked', 'Not Checked'), 'NotChecked')
```

- [ ] **Step 4: Replace tag filter "All" button**

```js
// Find: '" data-tag="">All</button>'
// Replace: '" data-tag="">' + EPT.s('cmsdoctor.tag_all', 'All') + '</button>'
```

- [ ] **Step 5: Replace card action button labels in `renderCard()`**

```js
// Find: '>Run</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_run', 'Run') + '</button>'

// Find: '>Fix</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_fix', 'Fix') + '</button>'

// Find: '>Details</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_details', 'Details') + '</button>'

// Find: '>Restore</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_restore', 'Restore') + '</button>'

// Find: '>Dismiss</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_dismiss', 'Dismiss') + '</button>'
```

- [ ] **Step 6: Replace detail dialog labels in `showDetailDialog()`**

```js
// Find: '<strong>Result:</strong>'
// Replace: '<strong>' + EPT.s('cmsdoctor.dlg_result', 'Result:') + '</strong>'

// Find: '<strong>Details:</strong>'
// Replace: '<strong>' + EPT.s('cmsdoctor.dlg_details', 'Details:') + '</strong>'

// Find: '<strong>Categories:</strong>'
// Replace: '<strong>' + EPT.s('cmsdoctor.dlg_categories', 'Categories:') + '</strong>'

// Find: 'Checked: ' + new Date(check.checkTime).toLocaleString()
// Replace: EPT.s('cmsdoctor.dlg_checked', 'Checked: {0}').replace('{0}', new Date(check.checkTime).toLocaleString())

// Find: '>Apply Fix</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_applyfix', 'Apply Fix') + '</button>'

// Find: '>Re-run Check</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_rerun', 'Re-run Check') + '</button>'

// Find: '>Close</button>'
// Replace: '>' + EPT.s('cmsdoctor.btn_close', 'Close') + '</button>'
```

- [ ] **Step 7: Replace confirm() dialog strings**

```js
// Find: if (!confirm('Apply fix for this check?')) return;
// Replace: if (!confirm(EPT.s('cmsdoctor.confirm_applyfix', 'Apply fix for this check?'))) return;

// Find: if (!confirm('Apply fix?')) return;
// Replace: if (!confirm(EPT.s('cmsdoctor.confirm_fix', 'Apply fix?'))) return;
```

- [ ] **Step 8: Replace loading string in `render()` initial state**

```js
// Find: '<div class="ept-loading"><div class="ept-spinner"></div><p>Loading...</p></div>'
// Replace: '<div class="ept-loading"><div class="ept-spinner"></div><p>' + EPT.s('shared.loading', 'Loading...') + '</p></div>'
```

- [ ] **Step 9: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/cms-doctor.js
git commit -m "feat: localize all strings in cms-doctor.js"
```

---

### Task 10: Localize content-type-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js`

**All replacements follow the same pattern: `'English string'` → `EPT.s('contenttypeaudit.key', 'English string')`**

- [ ] **Step 1: Replace job alert strings in `renderJobAlert()`**

```js
// Alert messages (innerHTML assignments):
// Running: '⏳ <strong>Aggregation job is currently running.</strong> Content counts will be updated when it completes. <button...>Refresh</button>'
// Replace Refresh button text: EPT.s('contenttypeaudit.btn_refresh', 'Refresh')
// Replace running message: EPT.s('contenttypeaudit.alert_running', '...')

// Not run: '<strong>Content statistics have not been collected yet.</strong> ...'
// Replace: EPT.s('contenttypeaudit.alert_notrun', '...')

// Old: 'Statistics were last updated <strong>' + ago + '</strong>. Consider running...'
// Replace the text part: EPT.s('contenttypeaudit.alert_old', 'Statistics were last updated {0}. Consider running the aggregation job for fresh data.').replace('{0}', '<strong>' + ago + '</strong>')

// Run now button: btn.textContent = 'Run now' → EPT.s('contenttypeaudit.btn_runnow', 'Run now')
// Starting: btn.textContent = 'Starting...' → EPT.s('contenttypeaudit.btn_starting', 'Starting...')
// Started: el.innerHTML includes 'Aggregation job has been started...' → use EPT.s('contenttypeaudit.alert_started', '...')
// Refresh: 'Refresh' → EPT.s('contenttypeaudit.btn_refresh', 'Refresh')
// Failed: btn.textContent = 'Failed' → EPT.s('contenttypeaudit.btn_failed', 'Failed')
```

- [ ] **Step 2: Replace stat labels in `renderStats()`**

```js
// Find each ept-stat__label:
`<div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_contenttypes', 'Content Types')}</div>`
`<div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_systemtypes', 'System Types')}</div>`
`<div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_orphaned', 'Orphaned')}</div>`
`<div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_unused', 'Unused (0 content)')}</div>`
`<div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_showing', 'Showing')}</div>`
```

- [ ] **Step 3: Replace toolbar strings in `renderToolbar()`**

```js
// placeholder="Search types... (or property:name)"
// Replace: `placeholder="${EPT.s('contenttypeaudit.lbl_search', 'Search types... (or property:name)')}"`

// '<option value="">All base types</option>'
// Replace: `<option value="">${EPT.s('contenttypeaudit.opt_allbases', 'All base types')}</option>`

// 'Show system types' (checkbox label)
// Replace: EPT.s('contenttypeaudit.chk_showsystem', 'Show system types')

// `${EPT.icons.list} Table`
// Replace: `${EPT.icons.list} ${EPT.s('contenttypeaudit.btn_table', 'Table')}`

// `${EPT.icons.tree} Tree`
// Replace: `${EPT.icons.tree} ${EPT.s('contenttypeaudit.btn_tree', 'Tree')}`

// `${EPT.icons.download} Export`
// Replace: `${EPT.icons.download} ${EPT.s('contenttypeaudit.btn_export', 'Export')}`
```

- [ ] **Step 4: Replace column labels in `renderTable()` columns array**

```js
{ key: 'name', label: EPT.s('contenttypeaudit.col_name', 'Name'), ... },
{ key: 'base', label: EPT.s('contenttypeaudit.col_base', 'Base'), ... },
{ key: 'groupName', label: EPT.s('contenttypeaudit.col_group', 'Group'), ... },
{ key: 'propertyCount', label: EPT.s('contenttypeaudit.col_properties', 'Properties'), ... },
{ key: 'contentCount', label: EPT.s('contenttypeaudit.col_content', 'Content'), ... },

// Column filter 'All' option:
// '<option value="">All</option>'
// Replace: `<option value="">${EPT.s('contenttypeaudit.opt_all', 'All')}</option>`
```

- [ ] **Step 5: Replace badge strings in `renderTypeName()`**

```js
// Find: '<span class="ept-badge ept-badge--default">System</span>'
// Replace: `<span class="ept-badge ept-badge--default">${EPT.s('contenttypeaudit.badge_system', 'System')}</span>`

// Find: '<span class="ept-badge ept-badge--danger">Orphaned</span>'
// Replace: `<span class="ept-badge ept-badge--danger">${EPT.s('contenttypeaudit.badge_orphaned', 'Orphaned')}</span>`
```

- [ ] **Step 6: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-audit.js
git commit -m "feat: localize all strings in content-type-audit.js"
```

---

### Task 11: Localize link-checker.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/link-checker.js`

- [ ] **Step 1: Replace all hardcoded strings**

Apply `EPT.s()` to every hardcoded string. Complete replacement table:

| Find | Replace with |
|---|---|
| `'Link checker job is currently running. Results will be updated when it completes.'` | `EPT.s('linkchecker.alert_running', '...')` |
| `'Link checker has not been run yet. Run the scheduled job to scan content for links.'` | `EPT.s('linkchecker.alert_notrun', '...')` |
| `'Link check last ran '` + ago + `'.'` | `EPT.s('linkchecker.alert_lastran','{0}').replace('{0}', ago)` |
| `'Refresh'` (button) | `EPT.s('linkchecker.btn_refresh', 'Refresh')` |
| `'Run now'` | `EPT.s('linkchecker.btn_runnow', 'Run now')` |
| `'Starting...'` | `EPT.s('linkchecker.btn_starting', 'Starting...')` |
| `'Job started.'` | `EPT.s('linkchecker.alert_started', 'Job started.')` |
| `'Failed'` | `EPT.s('linkchecker.btn_failed', 'Failed')` |
| `'Total Links'` (stat) | `EPT.s('linkchecker.stat_total', 'Total Links')` |
| `'Broken'` (stat) | `EPT.s('linkchecker.stat_broken', 'Broken')` |
| `'Valid'` (stat) | `EPT.s('linkchecker.stat_valid', 'Valid')` |
| `'Internal'` (stat) | `EPT.s('linkchecker.stat_internal', 'Internal')` |
| `'External'` (stat) | `EPT.s('linkchecker.stat_external', 'External')` |
| `placeholder="Search URLs, content, properties..."` | `placeholder="${EPT.s('linkchecker.lbl_search','...')}"` |
| `'All types'` (option) | `EPT.s('linkchecker.opt_alltypes', 'All types')` |
| `'Internal'` (option) | `EPT.s('linkchecker.opt_internal', 'Internal')` |
| `'External'` (option) | `EPT.s('linkchecker.opt_external', 'External')` |
| `'All status'` (option) | `EPT.s('linkchecker.opt_allstatus', 'All status')` |
| `'Broken only'` | `EPT.s('linkchecker.opt_brokenonly', 'Broken only')` |
| `'Valid only'` | `EPT.s('linkchecker.opt_validonly', 'Valid only')` |
| `${EPT.icons.download} Export` | `${EPT.icons.download} ${EPT.s('linkchecker.btn_export','Export')}` |
| `'Status'` (col) | `EPT.s('linkchecker.col_status', 'Status')` |
| `'URL'` (col) | `EPT.s('linkchecker.col_url', 'URL')` |
| `'Type'` (col) | `EPT.s('linkchecker.col_type', 'Type')` |
| `'Found In'` (col) | `EPT.s('linkchecker.col_foundin', 'Found In')` |
| `'Checked'` (col) | `EPT.s('linkchecker.col_checked', 'Checked')` |
| `'Used on: '` | `EPT.s('linkchecker.cell_usedon', 'Used on: ')` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/link-checker.js
git commit -m "feat: localize all strings in link-checker.js"
```

---

### Task 12: Localize security-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/security-audit.js`

- [ ] **Step 1: Replace tab labels in `renderShell()`**

```js
// Find: 'Content Tree' / 'Role/User Explorer' / 'Issues'
html += '<button class="sa-tab" data-tab="tree">' + EPT.s('securityaudit.tab_tree', 'Content Tree') + '</button>';
html += '<button class="sa-tab" data-tab="roles">' + EPT.s('securityaudit.tab_roles', 'Role/User Explorer') + '</button>';
html += '<button class="sa-tab" data-tab="issues">' + EPT.s('securityaudit.tab_issues', 'Issues') + ' <span id="sa-issues-count-badge"...></span></button>';
```

- [ ] **Step 2: Replace banner strings in `renderStatusAlert()`**

```js
// No data banner:
// Find: 'Security audit data not available.'
// Replace: EPT.s('securityaudit.banner_nodata', 'Security audit data not available.')
// Find: 'Run the <strong>[EditorPowertools] Content Analysis</strong> scheduled job to analyze content permissions.'
// Replace: EPT.s('securityaudit.banner_runjob', 'Run the [EditorPowertools] Content Analysis scheduled job to analyze content permissions.')
// Find: 'Run now' (button)
// Replace: EPT.s('securityaudit.btn_runnow', 'Run now')

// Old data alert:
// Find: 'Security data was last analyzed <strong>' + timeAgo(lastDate) + '</strong>. Consider running the aggregation job for fresh data.'
// Replace: EPT.s('securityaudit.alert_old','Security data was last analyzed {0}. Consider running the aggregation job for fresh data.').replace('{0}', '<strong>'+timeAgo(lastDate)+'</strong>')
// Find: 'Run now' (button text)
// Replace: EPT.s('securityaudit.btn_runnow', 'Run now')
```

- [ ] **Step 3: Replace in `wireRunJobButton()`**

```js
// Find: btn.textContent = 'Starting...'
// Replace: btn.textContent = EPT.s('securityaudit.btn_starting', 'Starting...')
// Find: 'Aggregation job has been started...'
// Replace: EPT.s('securityaudit.banner_started', 'Aggregation job has been started. Data will update when it completes.')
// Find: 'Refresh'
// Replace: EPT.s('securityaudit.btn_refresh', 'Refresh')
// Find: btn.textContent = 'Failed'
// Replace: btn.textContent = EPT.s('securityaudit.btn_failed', 'Failed')
```

- [ ] **Step 4: Replace stat labels in `renderStats()`**

```js
'<div class="ept-stat__label">' + EPT.s('securityaudit.stat_analyzed', 'Content Analyzed') + '</div>'
'<div class="ept-stat__label">' + EPT.s('securityaudit.stat_roles', 'Roles/Users') + '</div>'
'<div class="ept-stat__label">' + EPT.s('securityaudit.stat_issues', 'Issues Found') + '</div>'
'<div class="ept-stat__label">' + EPT.s('securityaudit.stat_lastanalysis', 'Last Analysis') + '</div>'
```

- [ ] **Step 5: Replace pagination strings in `renderPagination()`**

```js
prevBtn.textContent = EPT.s('securityaudit.pager_previous', 'Previous');
info.textContent = EPT.s('securityaudit.pager_info', 'Page {0} of {1}').replace('{0}', page).replace('{1}', totalPages);
nextBtn.textContent = EPT.s('securityaudit.pager_next', 'Next');
```

- [ ] **Step 6: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/security-audit.js
git commit -m "feat: localize all strings in security-audit.js"
```

---

### Task 13: Localize bulk-property-editor.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js`

- [ ] **Step 1: Replace all hardcoded strings in `renderShell()`**

```js
// Find: '<option value="">-- Select content type --</option>'
// Replace: '<option value="">' + EPT.s('bulkeditor.opt_selecttype', '-- Select content type --') + '</option>'

// Find: ' Columns' (button text with icon)
// Replace: ' ' + EPT.s('bulkeditor.btn_columns', 'Columns')

// Find: ' Add Filter' (button text with icon)
// Replace: ' ' + EPT.s('bulkeditor.btn_addfilter', 'Add Filter')

// Find: 'Include references' (checkbox label)
// Replace: EPT.s('bulkeditor.chk_includereferences', 'Include references')

// Find: 'Apply Filters' (button)
// Replace: EPT.s('bulkeditor.btn_applyfilters', 'Apply Filters')

// Find: '<div class="ept-empty"><p>Select a content type above to start editing.</p></div>'
// Replace: '<div class="ept-empty"><p>' + EPT.s('bulkeditor.empty_selecttype', 'Select a content type above to start editing.') + '</p></div>'

// Find: '<label>Per page:</label>'
// Replace: '<label>' + EPT.s('bulkeditor.lbl_perpage', 'Per page:') + '</label>'

// Find: 'Discard All' (button)
// Replace: EPT.s('bulkeditor.btn_discardall', 'Discard All')

// Find: 'Save All' (button)
// Replace: EPT.s('bulkeditor.btn_saveall', 'Save All')

// Find: 'Publish All' (button)
// Replace: EPT.s('bulkeditor.btn_publishall', 'Publish All')
```

- [ ] **Step 2: Replace pending count display**

Find the place where pending count is displayed (look for `'changes pending'`):
```js
// Find: span_element.textContent = count + ' changes pending'
// or: '0 changes pending' / 'X changes pending'
// Replace: EPT.s('bulkeditor.pending_count', '{0} changes pending').replace('{0}', count)
```

- [ ] **Step 3: Replace confirm dialog**

```js
// Find: if (!confirm('Discard all pending changes?'))
// Replace: if (!confirm(EPT.s('bulkeditor.confirm_discard', 'Discard all pending changes?')))
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/bulk-property-editor.js
git commit -m "feat: localize all strings in bulk-property-editor.js"
```

---

### Task 14: Localize content-statistics.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js`

- [ ] **Step 1: Replace banner and error strings**

```js
// Banner: 'Run the [EditorPowertools] Content Analysis scheduled job to populate data.'
// Replace: EPT.s('contentstatistics.banner_runjob', 'Run the [EditorPowertools] Content Analysis scheduled job to populate data.')

// Run now button: 'Run now'
// Replace: EPT.s('contentstatistics.btn_runnow', 'Run now')

// Starting: btn.textContent = 'Starting...'
// Replace: EPT.s('contentstatistics.btn_starting', 'Starting...')

// Error messages:
// 'Failed to load statistics from API.'
// Replace: EPT.s('contentstatistics.error_load', 'Failed to load statistics from API.')

// 'Error rendering statistics dashboard.'
// Replace: EPT.s('contentstatistics.error_render', 'Error rendering statistics dashboard.')
```

- [ ] **Step 2: Replace chart titles and stat labels**

Read the full content of `content-statistics.js` and apply `EPT.s()` to all remaining hardcoded strings (chart titles, stat card labels) following the keys in the `contentstatistics` section of `en.xml`. The pattern is identical to all previous tasks.

- [ ] **Step 3: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-statistics.js
git commit -m "feat: localize all strings in content-statistics.js"
```

---

### Task 15: Localize activity-timeline.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/activity-timeline.js`

- [ ] **Step 1: Replace stat labels in `renderStats()`**

```js
'<div class="ept-stat__label">Activities Today</div>'  → EPT.s('activitytimeline.stat_today', 'Activities Today')
'<div class="ept-stat__label">Active Editors</div>'    → EPT.s('activitytimeline.stat_activeeditors', 'Active Editors')
'<div class="ept-stat__label">Publishes Today</div>'   → EPT.s('activitytimeline.stat_publishes', 'Publishes Today')
'<div class="ept-stat__label">Drafts Today</div>'      → EPT.s('activitytimeline.stat_drafts', 'Drafts Today')
```

- [ ] **Step 2: Replace toolbar options and buttons in `renderToolbar()`**

```js
'<option value="">All users</option>'          → EPT.s('activitytimeline.opt_allusers', 'All users')
'<option value="Published">Published</option>' → EPT.s('activitytimeline.opt_published', 'Published')
'<option value="Draft">Draft saved</option>'   → EPT.s('activitytimeline.opt_draft', 'Draft saved')
'Ready to publish'                             → EPT.s('activitytimeline.opt_readytopublish', 'Ready to publish')
'Scheduled'                                    → EPT.s('activitytimeline.opt_scheduled', 'Scheduled')
'Rejected'                                     → EPT.s('activitytimeline.opt_rejected', 'Rejected')
'Previously published'                         → EPT.s('activitytimeline.opt_previouslypublished', 'Previously published')
'Comments'                                     → EPT.s('activitytimeline.opt_comment', 'Comments')
'All content types'                            → EPT.s('activitytimeline.opt_allcontenttypes', 'All content types')
'Filter' (button)                              → EPT.s('activitytimeline.btn_filter', 'Filter')
'Clear' (button)                               → EPT.s('activitytimeline.btn_clear', 'Clear')
```

- [ ] **Step 3: Replace content banner strings in `renderContentBanner()`**

```js
// Find: 'Showing timeline for <strong>' + escHtml(...) + '</strong>'
// Replace: EPT.s('activitytimeline.banner_showingfor', 'Showing timeline for {0}').replace('{0}', '<strong>' + escHtml(state.contentName || 'Content #' + state.contentId) + '</strong>')

// Find: 'Show all activity' (button text)
// Replace: EPT.s('activitytimeline.btn_showall', 'Show all activity')
```

- [ ] **Step 4: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/activity-timeline.js
git commit -m "feat: localize all strings in activity-timeline.js"
```

---

### Task 16: Localize personalization-audit.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/personalization-audit.js`

- [ ] **Step 1: Replace all hardcoded strings**

Follow the same pattern as link-checker.js Task 11. Complete replacement table:

| Find | Replace |
|---|---|
| `'Personalization analysis job is currently running...'` | `EPT.s('personalizationaudit.alert_running', '...')` |
| `'Personalization usage has not been analyzed yet...'` | `EPT.s('personalizationaudit.alert_notrun', '...')` |
| `'Analysis was last run {ago}. Consider running...'` | `EPT.s('personalizationaudit.alert_old', '...').replace('{0}', ago)` |
| `'Run now'` | `EPT.s('personalizationaudit.btn_runnow', 'Run now')` |
| `'Refresh'` | `EPT.s('personalizationaudit.btn_refresh', 'Refresh')` |
| `'Starting...'` | `EPT.s('personalizationaudit.btn_starting', 'Starting...')` |
| `'Total Usages'` (stat) | `EPT.s('personalizationaudit.stat_total', 'Total Usages')` |
| `'Content Items'` (stat) | `EPT.s('personalizationaudit.stat_content', 'Content Items')` |
| `'Visitor Groups Used'` (stat) | `EPT.s('personalizationaudit.stat_groups', 'Visitor Groups Used')` |
| `placeholder="Search..."` | `placeholder="${EPT.s('personalizationaudit.lbl_search','Search...')}"` |
| `'Usage Type:'` | `EPT.s('personalizationaudit.lbl_type', 'Usage Type:')` |
| `'Visitor Group:'` | `EPT.s('personalizationaudit.lbl_group', 'Visitor Group:')` |
| `'All types'` (option) | `EPT.s('personalizationaudit.opt_alltypes', 'All types')` |
| `'All groups'` (option) | `EPT.s('personalizationaudit.opt_allgroups', 'All groups')` |
| `'Content'` (col) | `EPT.s('personalizationaudit.col_content', 'Content')` |
| `'Usage Type'` (col) | `EPT.s('personalizationaudit.col_type', 'Usage Type')` |
| `'Visitor Groups'` (col) | `EPT.s('personalizationaudit.col_groups', 'Visitor Groups')` |
| `'Location'` (col) | `EPT.s('personalizationaudit.col_location', 'Location')` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/personalization-audit.js
git commit -m "feat: localize all strings in personalization-audit.js"
```

---

### Task 17: Localize audience-manager.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/audience-manager.js`

- [ ] **Step 1: Replace stat labels in `renderStats()`**

```js
`<div class="ept-stat__label">${EPT.s('audiencemanager.stat_audiences', 'Audiences')}</div>`
`<div class="ept-stat__label">${EPT.s('audiencemanager.stat_withstats', 'With Statistics')}</div>`
`<div class="ept-stat__label">${EPT.s('audiencemanager.stat_categories', 'Categories')}</div>`
`<div class="ept-stat__label">${EPT.s('audiencemanager.stat_totalcriteria', 'Total Criteria')}</div>`
`<div class="ept-stat__label">${EPT.s('audiencemanager.stat_showing', 'Showing')}</div>`
```

- [ ] **Step 2: Replace toolbar strings in `renderToolbar()`**

```js
placeholder="Search audiences..."  → `placeholder="${EPT.s('audiencemanager.lbl_search', 'Search audiences...')}"`
'All categories'                    → EPT.s('audiencemanager.opt_allcategories', 'All categories')
'Has Statistics'                    → EPT.s('audiencemanager.chk_hasstats', 'Has Statistics')
'Personalization Audit'             → EPT.s('audiencemanager.lnk_personalizationaudit', 'Personalization Audit')
'Export'                            → EPT.s('audiencemanager.btn_export', 'Export')
```

- [ ] **Step 3: Replace column labels in `renderTable()`**

```js
{ key: 'cleanName', label: EPT.s('audiencemanager.col_name', 'Name'), ... },
{ key: 'criteriaCount', label: EPT.s('audiencemanager.col_criteria', 'Criteria'), ... },
{ key: 'criteriaOperator', label: EPT.s('audiencemanager.col_operator', 'Operator'), ... },
{ key: 'statisticsEnabled', label: EPT.s('audiencemanager.col_statistics', 'Statistics'), ... },
{ key: 'usageCount', label: EPT.s('audiencemanager.col_usage', 'Usage'), ... },

// Column filter 'All' option:
`<option value="">${EPT.s('audiencemanager.opt_all', 'All')}</option>`
```

- [ ] **Step 4: Replace title attributes in `renderUsageCount()`**

```js
// Find: title="Show usage details"
// Replace: title="${EPT.s('audiencemanager.title_usagedetails', 'Show usage details')}"

// Find: title="Not used in any personalized content"
// Replace: title="${EPT.s('audiencemanager.title_notused', 'Not used in any personalized content')}"

// Find: title="Run the Personalization Analysis job first"
// Replace: title="${EPT.s('audiencemanager.title_runjobfirst', 'Run the Personalization Analysis job first')}"
```

- [ ] **Step 5: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/audience-manager.js
git commit -m "feat: localize all strings in audience-manager.js"
```

---

### Task 18: Localize content-importer.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-importer.js`

- [ ] **Step 1: Read the full file and replace all hardcoded UI strings**

The content-importer.js has a multi-step wizard. Read the full file, then apply `EPT.s()` to every hardcoded string using the `contentimporter.*` keys defined in `en.xml`. Key strings to find and replace:

| Context | Find | Key |
|---|---|---|
| Step labels | `'1. Upload File'`, `'2. Configure'`, etc. | `contentimporter.step_upload`, etc. |
| Upload step | `'Drop a CSV, JSON, or Excel file here...'` | `contentimporter.lbl_dropfile` |
| Upload step | `'Browse...'` | `contentimporter.btn_browse` |
| Configure step | `'Content Type:'`, `'Language:'`, `'Parent Location:'` etc. | `contentimporter.lbl_*` |
| Configure step | `'Select parent...'` | `contentimporter.lbl_selectparent` |
| Configure step | `'Publish after import'` | `contentimporter.lbl_publishafter` |
| Mapping step | `'Field Mapping'`, `'Source Column'`, `'Target Field'` | `contentimporter.lbl_*` |
| Mapping step | `'-- Skip --'` | `contentimporter.opt_skip` |
| Mapping step | `'Run Dry Run'` | `contentimporter.btn_dryrun` |
| Preview step | `'Dry Run Results'` | `contentimporter.lbl_dryresult` |
| Import buttons | `'Start Import'`, `'Start Over'` | `contentimporter.btn_import`, etc. |
| Import step | `'Importing...'` | `contentimporter.lbl_importing` |
| Navigation | `'Next'`, `'Back'` | `contentimporter.btn_next`, `contentimporter.btn_back` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-importer.js
git commit -m "feat: localize all strings in content-importer.js"
```

---

### Task 19: Localize manage-children.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/manage-children.js`

- [ ] **Step 1: Read the full file and replace all hardcoded strings**

| Find | Key |
|---|---|
| `'Select a parent page to manage its children'` | `managechildren.lbl_selectparent` |
| `'Select Parent Page'` | `managechildren.btn_selectparent` |
| `'Sort A-Z'` | `managechildren.btn_sortaz` |
| `'Sort Z-A'` | `managechildren.btn_sortza` |
| `'Sort by Date'` | `managechildren.btn_sortbydate` |
| `'Publish All'` | `managechildren.btn_publishall` |
| `'Unpublish All'` | `managechildren.btn_unpublishall` |
| `'Delete Selected'` | `managechildren.btn_deleteselected` |
| `'Save Order'` | `managechildren.btn_saveorder` |
| `'Name'` (col) | `managechildren.col_name` |
| `'Status'` (col) | `managechildren.col_status` |
| `'Changed'` (col) | `managechildren.col_changed` |
| `'Type'` (col) | `managechildren.col_type` |
| `'Delete {n} selected item(s)? This cannot be undone.'` | `managechildren.confirm_delete` with `.replace('{0}', count)` |
| `'Publish all {n} children?'` | `managechildren.confirm_publishall` with `.replace('{0}', count)` |
| `'Unpublish all {n} children?'` | `managechildren.confirm_unpublishall` with `.replace('{0}', count)` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/manage-children.js
git commit -m "feat: localize all strings in manage-children.js"
```

---

### Task 20: Localize content-type-recommendations.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-recommendations.js`

- [ ] **Step 1: Replace all hardcoded strings**

| Find | Key |
|---|---|
| `'When editors create new content, Optimizely can suggest...'` (info banner) | `recommendations.banner_info` |
| `'Add Rule'` (button) | `recommendations.btn_addrule` |
| `'Parent Type'` (col) | `recommendations.col_parenttype` |
| `'Allowed Types'` (col) | `recommendations.col_allowedtypes` |
| `''` actions col label | (empty, no change) |
| `'Add Rule'` (dialog title) | `recommendations.dlg_addrule` |
| `'Edit Rule'` (dialog title) | `recommendations.dlg_editrule` |
| `'Parent Content Type:'` | `recommendations.lbl_parenttype` |
| `'Allowed Child Types:'` | `recommendations.lbl_allowedtypes` |
| `'Save'` | `recommendations.btn_save` |
| `'Cancel'` | `recommendations.btn_cancel` |
| `'Edit'` | `recommendations.btn_edit` |
| `'Delete'` | `recommendations.btn_delete` |
| `'No recommendation rules defined yet. Click "Add Rule" to create one.'` | `recommendations.empty_norules` |
| `'Delete this rule?'` (confirm) | `recommendations.confirm_delete` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/content-type-recommendations.js
git commit -m "feat: localize all strings in content-type-recommendations.js"
```

---

### Task 21: Localize scheduled-jobs-gantt.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/scheduled-jobs-gantt.js`

- [ ] **Step 1: Replace all hardcoded strings**

| Find | Key |
|---|---|
| `'Failed to load Gantt data: ' + err.message` | `EPT.s('gantt.error_load', 'Failed to load Gantt data: {0}').replace('{0}', err.message \|\| err)` |
| `'No scheduled jobs found.'` (EPT.showEmpty call) | `EPT.s('gantt.empty_nojobs', 'No scheduled jobs found.')` |
| `'Total Jobs'` (stat) | `EPT.s('gantt.stat_totaljobs', 'Total Jobs')` |
| `'Enabled'` (stat) | `EPT.s('gantt.stat_enabled', 'Enabled')` |
| `'Running Now'` (stat) | `EPT.s('gantt.stat_running', 'Running Now')` |
| `'Executions in View'` (stat) | `EPT.s('gantt.stat_executions', 'Executions in View')` |
| `'Previous'` (nav button) | `EPT.s('gantt.btn_previous', 'Previous')` |
| `'Today'` (nav button) | `EPT.s('gantt.btn_today', 'Today')` |
| `'Next'` (nav button) | `EPT.s('gantt.btn_next', 'Next')` |
| `state.viewRangeHours + 'h view'` | `EPT.s('gantt.lbl_viewhours', '{0}h view').replace('{0}', state.viewRangeHours)` |
| `'Job'` (job column header textContent) | `EPT.s('gantt.col_job', 'Job')` |

- [ ] **Step 2: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/scheduled-jobs-gantt.js
git commit -m "feat: localize all strings in scheduled-jobs-gantt.js"
```

---

### Task 22: Localize active-editors-overview.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js`

- [ ] **Step 1: Replace error strings**

```js
// Find: 'SignalR client not available. Check browser console for errors.'
// Replace: EPT.s('activeeditors.error_nosignalr', 'SignalR client not available. Check browser console for errors.')

// Find: 'Could not connect: ' + escHtml(err.message)
// Replace: EPT.s('activeeditors.error_connect', 'Could not connect: {0}').replace('{0}', escHtml(err.message))
```

- [ ] **Step 2: Replace stat labels in `renderStats()`**

```js
'Online Now'         → EPT.s('activeeditors.stat_online', 'Online Now')
'Currently Editing'  → EPT.s('activeeditors.stat_editing', 'Currently Editing')
'Active Today'       → EPT.s('activeeditors.stat_today', 'Active Today')
```

- [ ] **Step 3: Replace section headers and card strings in `renderEditors()`**

```js
'<h3>Online Now</h3>'       → '<h3>' + EPT.s('activeeditors.section_online', 'Online Now') + '</h3>'
'<h3>Also Active Today</h3>' → '<h3>' + EPT.s('activeeditors.section_today', 'Also Active Today') + '</h3>'
'Connected ' + connTime      → EPT.s('activeeditors.lbl_connected', 'Connected {0}').replace('{0}', connTime)
'Send Message' (button)     → EPT.s('activeeditors.btn_sendmessage', 'Send Message')
'you' (badge)               → EPT.s('activeeditors.badge_you', 'you')
'offline' (badge)           → EPT.s('activeeditors.badge_offline', 'offline')
```

- [ ] **Step 4: Replace dialog strings in `showNotifyDialog()`**

```js
// Dialog title: 'Send Message to ' + displayName
// Replace: EPT.s('activeeditors.dlg_sendmessage', 'Send Message to {0}').replace('{0}', displayName)

// Description text: 'This will send a CMS notification that ' + displayName + ' will see...'
// Replace: EPT.s('activeeditors.dlg_desc', 'This will send a CMS notification that {0} will see in their notification bell.').replace('{0}', escHtml(displayName))

// placeholder="Type your message..."
// Replace: 'placeholder="' + EPT.s('activeeditors.dlg_placeholder', 'Type your message...') + '"'

// 'Cancel' button
// Replace: EPT.s('activeeditors.btn_cancel', 'Cancel')

// 'Send Notification' button
// Replace: EPT.s('activeeditors.btn_send', 'Send Notification')

// sendBtn.textContent = 'Sending...'
// Replace: sendBtn.textContent = EPT.s('activeeditors.btn_sending', 'Sending...')

// Success: 'Message sent to ' + escHtml(displayName)
// Replace: EPT.s('activeeditors.msg_sent', 'Message sent to {0}').replace('{0}', escHtml(displayName))
```

- [ ] **Step 5: Replace chat strings in `renderChatPanel()`**

```js
'<h3>Team Chat</h3>'                          → '<h3>' + EPT.s('activeeditors.chat_title', 'Team Chat') + '</h3>'
'No messages yet. Say hello!'                 → EPT.s('activeeditors.chat_empty', 'No messages yet. Say hello!')
placeholder="Type a message... (Enter to send)" → EPT.s('activeeditors.chat_placeholder', '...')
```

- [ ] **Step 6: Update `renderChat()` empty state**

```js
// Find: '<div class="ae-chat-empty">No messages yet. Say hello!</div>'
// Replace: '<div class="ae-chat-empty">' + EPT.s('activeeditors.chat_empty', 'No messages yet. Say hello!') + '</div>'
```

- [ ] **Step 7: Build and commit**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/active-editors-overview.js
git commit -m "feat: localize all strings in active-editors-overview.js"
```

---

### Task 23: Localize components.js

**Files:**
- Modify: `src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/components.js`

- [ ] **Step 1: Replace content picker strings**

```js
// In EPT.contentPicker:
// Find: opts.title || 'Select Content'
// Replace: opts.title || EPT.s('components.picker_title', 'Select Content')

// Find: placeholder="Search content by name..."
// Replace: `placeholder="${EPT.s('components.picker_search', 'Search content by name...')}"`

// Find: EPT.showEmpty(resultsContainer, 'No results found')
// Replace: EPT.showEmpty(resultsContainer, EPT.s('components.picker_noresults', 'No results found'))

// Find: class="ept-btn ept-picker-cancel">Cancel</button>
// Replace: ... + EPT.s('components.btn_cancel', 'Cancel') + </button>

// Find: class="ept-btn ept-btn--primary ept-picker-select" ...>Select</button>
// Replace: ... + EPT.s('components.btn_select', 'Select') + </button>
```

- [ ] **Step 2: Replace content type picker strings** (if EPT.contentTypePicker exists)

```js
// Find: 'Select Content Type' (dialog title)
// Replace: EPT.s('components.typepicker_title', 'Select Content Type')

// Find: placeholder="Search content types..."
// Replace: EPT.s('components.typepicker_search', 'Search content types...')

// Find: 'No content types found'
// Replace: EPT.s('components.typepicker_noresults', 'No content types found')
```

- [ ] **Step 3: Build**

```bash
dotnet build src/EditorPowertools/EditorPowertools.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 4: Final smoke test**

Start the sample site and manually visit each tool page. Open browser DevTools and verify:
1. `window.EPT_STRINGS` is populated (type it in console)
2. All visible UI labels are rendered (no raw key paths like `contentaudit.col_name` visible)
3. No JS errors in console

```bash
dotnet run --project src/EditorPowertools.SampleSite
```

- [ ] **Step 5: Final commit**

```bash
git add src/EditorPowertools/modules/_protected/EditorPowertools/ClientResources/js/components.js
git commit -m "feat: localize all strings in components.js"
```

---

## Self-Review

**Spec coverage check:**
- ✅ XML `<ui>` section in en.xml and all 10 other lang files — Tasks 1-2
- ✅ `UiStringsProvider` C# service — Task 3
- ✅ Service registration — Task 4
- ✅ Layout injection of `window.EPT_STRINGS` — Task 5
- ✅ `EPT.s()` safe accessor + `showLoading`/`createTable` updates — Task 6
- ✅ All 17 JS files localized — Tasks 7-23
- ✅ English fallback in every `EPT.s()` call — shown throughout
- ✅ Non-English lang files use English as placeholder — Task 2

**Placeholder scan:** No TBDs. Task 14 (content-statistics) and Task 18 (content-importer) reference reading the full file — both are complete files in the repo that the executor will read; all key mappings are defined in en.xml.

**Type consistency:** `EPT.s(path, fallback)` — `path` is always `'tool.key'` (dot-separated), `fallback` is always the English string. `UiStringsProvider.S(key)` — `key` is always `'tool/key'` (slash-separated for the XML path). These are consistent throughout.
