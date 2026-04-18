/**
 * Bulk Property Editor - Edit property values across multiple content items at once.
 */
(function () {
    'use strict';

    var API = window.EPT_BASE_URL + 'BulkPropertyEditorApi';

    var state = {
        contentTypeId: null,
        language: 'en',
        page: 1,
        pageSize: 50,
        sortBy: 'Name',
        sortDirection: 'asc',
        filters: [],
        columns: [],
        availableColumns: [],
        pendingChanges: {},
        includeReferences: false,
        data: null,
        isLoading: false,
        selectAll: false,
        contentTypes: [],
        languages: [],
        // CMS 13: 'types' | 'contracts'. Only visible when any content type has isContract != null.
        activeTab: 'types'
    };

    var pendingEditAfterPageChange = null;

    // ---- DOM references ----
    var root = document.getElementById('bulk-editor-content');

    // ---- Utilities ----

    function escapeHtml(text) {
        if (!text && text !== 0) return '';
        var div = document.createElement('div');
        div.textContent = String(text);
        return div.innerHTML;
    }

    function formatDate(dateStr) {
        if (!dateStr) return '';
        var d = new Date(dateStr);
        if (isNaN(d.getTime())) return '';
        return d.toLocaleDateString('sv-SE') + ' ' + d.toLocaleTimeString('en-US', { hour12: false, hour: '2-digit', minute: '2-digit' });
    }

    function getStatusBadgeClass(status) {
        if (!status) return 'ept-badge--default';
        var s = status.toLowerCase().replace(/\s+/g, '');
        if (s === 'published') return 'ept-badge--success';
        if (s === 'draft' || s === 'notcreated') return 'ept-badge--warning';
        if (s === 'readytopublish' || s === 'checkedin') return 'ept-badge--primary';
        if (s === 'previouslypublished') return 'ept-badge--default';
        if (s === 'scheduled' || s === 'delayedpublish') return 'ept-badge--primary';
        if (s === 'rejected') return 'ept-badge--danger';
        return 'ept-badge--warning';
    }

    function getPendingChangeCount() {
        var count = 0;
        for (var cid in state.pendingChanges) {
            if (state.pendingChanges.hasOwnProperty(cid)) {
                var props = state.pendingChanges[cid];
                for (var p in props) {
                    if (props.hasOwnProperty(p)) count++;
                }
            }
        }
        return count;
    }

    function getChangedContentIds() {
        var ids = [];
        for (var cid in state.pendingChanges) {
            if (state.pendingChanges.hasOwnProperty(cid)) {
                var props = state.pendingChanges[cid];
                var hasProps = false;
                for (var p in props) {
                    if (props.hasOwnProperty(p)) { hasProps = true; break; }
                }
                if (hasProps) ids.push(parseInt(cid));
            }
        }
        return ids;
    }

    // ---- API calls ----

    function apiFetch(url, options) {
        options = options || {};
        options.headers = Object.assign({ 'X-Requested-With': 'XMLHttpRequest' }, options.headers);
        return fetch(url, options)
            .then(function (resp) {
                if (!resp.ok) {
                    return resp.json().then(function (data) {
                        throw new Error(data.message || 'Request failed with status ' + resp.status);
                    }).catch(function (e) {
                        if (e.message && !e.message.startsWith('Request failed')) throw e;
                        throw new Error('Request failed with status ' + resp.status);
                    });
                }
                return resp.json();
            });
    }

    // ---- Alert ----

    function showAlert(message, isSuccess) {
        var el = document.getElementById('bpeAlert');
        if (!el) return;
        el.className = 'bpe-alert bpe-alert--visible ' + (isSuccess ? 'bpe-alert--success' : 'bpe-alert--error');
        el.textContent = message;
        if (isSuccess) {
            setTimeout(function () { if (el) el.className = 'bpe-alert'; }, 5000);
        }
    }

    function clearAlert() {
        var el = document.getElementById('bpeAlert');
        if (el) el.className = 'bpe-alert';
    }

    // ---- Init ----

    var PREFS_KEY = 'BulkPropertyEditor';

    function init() {
        renderShell();
        loadInitialData();
    }

    function saveUserPrefs() {
        EPT.savePreferences(PREFS_KEY, {
            contentTypeId: state.contentTypeId,
            language: state.language,
            columns: state.columns,
            pageSize: state.pageSize,
            sortBy: state.sortBy,
            sortDirection: state.sortDirection,
            includeReferences: state.includeReferences
        });
    }

    // CMS 13 feature detection: any content type has an isContract flag (non-null) exposed by the backend.
    function hasCms13Data() {
        return state.contentTypes.some(function (t) { return t.isContract != null; });
    }

    function renderShell() {
        root.innerHTML =
            '<div class="bpe-alert" id="bpeAlert"></div>' +

            // CMS 13: Tabs for filtering content types vs contracts (hidden on CMS 12)
            '<div class="ept-tabs" id="bpeTabs" style="display: none;">' +
            '<button type="button" class="ept-tab" data-tab="types">' + EPT.s('bulkeditor.tab_types', 'Content types') + '</button>' +
            '<button type="button" class="ept-tab" data-tab="contracts">' + EPT.s('bulkeditor.tab_contracts', 'Contracts') + '</button>' +
            '</div>' +

            // Toolbar
            '<div class="ept-toolbar" id="bpeToolbar">' +
            '<select class="ept-select" id="bpeContentType" style="min-width: 240px;"><option value="">' + EPT.s('bulkeditor.opt_selecttype', '-- Select content type --') + '</option></select>' +
            '<select class="ept-select" id="bpeLanguage" style="min-width: 120px;"></select>' +
            '<div class="ept-toolbar__spacer"></div>' +
            '<div style="position: relative;">' +
            '<button type="button" class="ept-btn ept-btn--sm" id="bpeColumnsBtn">' + (EPT.icons.list || '') + ' ' + EPT.s('bulkeditor.btn_columns', 'Columns') + '</button>' +
            '<div class="bpe-col-picker" id="bpeColumnPicker"></div>' +
            '</div>' +
            '<button type="button" class="ept-btn ept-btn--sm" id="bpeAddFilterBtn">' + (EPT.icons.search || '') + ' ' + EPT.s('bulkeditor.btn_addfilter', 'Add Filter') + '</button>' +
            '<div class="bpe-toolbar-sep" id="bpeRefSep" style="display: none;"></div>' +
            '<label class="bpe-toolbar-check" id="bpeRefCheck" style="display: none;">' +
            '<input type="checkbox" id="bpeIncludeRefs" /> ' + EPT.s('bulkeditor.chk_includereferences', 'Include references') +
            '</label>' +
            '</div>' +

            // CMS 13: Composition badges for the currently selected type
            '<div id="bpeCompositionInfo" style="display:none; margin: 4px 0 8px 0;"></div>' +

            // Filter bar
            '<div class="bpe-filter-bar" id="bpeFilterBar">' +
            '<div id="bpeFilters"></div>' +
            '<button type="button" class="ept-btn ept-btn--primary ept-btn--sm" id="bpeApplyFilters" style="margin-top: 8px;">' + EPT.s('bulkeditor.btn_applyfilters', 'Apply Filters') + '</button>' +
            '</div>' +

            // CMS 13: Contract expansion preview note (populated when resolvedTypes is returned)
            '<div id="bpeExpansionNote" style="display:none;"></div>' +

            // Table area
            '<div class="ept-card" id="bpeTableCard">' +
            '<div class="ept-card__body" id="bpeTableBody">' +
            '<div class="ept-empty"><p>' + EPT.s('bulkeditor.empty_selecttype', 'Select a content type above to start editing.') + '</p></div>' +
            '</div>' +
            '</div>' +

            // Pagination
            '<div class="bpe-pagination" id="bpePagination" style="display: none;">' +
            '<div class="bpe-pagination-info" id="bpePaginationInfo"></div>' +
            '<div class="bpe-pagination-controls" id="bpePaginationControls"></div>' +
            '<div class="bpe-page-size">' +
            '<label>' + EPT.s('bulkeditor.lbl_perpage', 'Per page:') + '</label>' +
            '<select class="ept-select" id="bpePageSize">' +
            '<option value="25">25</option>' +
            '<option value="50" selected>50</option>' +
            '<option value="100">100</option>' +
            '</select>' +
            '</div>' +
            '</div>' +

            // Pending changes bar
            '<div class="bpe-pending-bar" id="bpePendingBar">' +
            '<div class="bpe-pending-info">' + EPT.s('bulkeditor.pending_count', '{0} changes pending').replace('{0}', '<span id="bpePendingCount">0</span>') + '</div>' +
            '<div class="bpe-pending-actions">' +
            '<button type="button" class="ept-btn bpe-btn-discard" id="bpeDiscardAll">' + EPT.s('bulkeditor.btn_discardall', 'Discard All') + '</button>' +
            '<button type="button" class="ept-btn ept-btn--primary" id="bpeSaveAll">' + EPT.s('bulkeditor.btn_saveall', 'Save All') + '</button>' +
            '<button type="button" class="ept-btn bpe-btn-success" id="bpePublishAll">' + EPT.s('bulkeditor.btn_publishall', 'Publish All') + '</button>' +
            '</div>' +
            '</div>';

        // CMS 13: Tab click handlers
        var tabsEl = document.getElementById('bpeTabs');
        if (tabsEl) {
            tabsEl.querySelectorAll('.ept-tab').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    var tab = btn.getAttribute('data-tab');
                    if (tab && tab !== state.activeTab) {
                        state.activeTab = tab;
                        // Clear current selection when switching tabs — the previously selected
                        // type may not belong to the new tab.
                        var sel = document.getElementById('bpeContentType');
                        if (sel) sel.value = '';
                        if (state.contentTypeId) changeContentType('');
                        renderContentTypeDropdown();
                        updateTabsActive();
                    }
                });
            });
        }

        // Bind events
        document.getElementById('bpeContentType').addEventListener('change', function () { changeContentType(this.value); });
        document.getElementById('bpeLanguage').addEventListener('change', function () { changeLanguage(this.value); });
        document.getElementById('bpeColumnsBtn').addEventListener('click', toggleColumnPicker);
        document.getElementById('bpeAddFilterBtn').addEventListener('click', addFilter);
        document.getElementById('bpeApplyFilters').addEventListener('click', applyFilters);
        document.getElementById('bpeIncludeRefs').addEventListener('change', function () { toggleReferences(this.checked); });
        document.getElementById('bpePageSize').addEventListener('change', function () { changePageSize(parseInt(this.value)); });
        document.getElementById('bpeDiscardAll').addEventListener('click', discardAll);
        document.getElementById('bpeSaveAll').addEventListener('click', saveAll);
        document.getElementById('bpePublishAll').addEventListener('click', publishAll);

        // Close column picker on outside click
        document.addEventListener('click', function (e) {
            var picker = document.getElementById('bpeColumnPicker');
            var btn = document.getElementById('bpeColumnsBtn');
            if (picker && picker.classList.contains('bpe-col-picker--open') && !picker.contains(e.target) && e.target !== btn && !btn.contains(e.target)) {
                picker.classList.remove('bpe-col-picker--open');
            }
        });
    }

    function loadInitialData() {
        Promise.all([
            apiFetch(API + '/GetContentTypes'),
            apiFetch(API + '/GetLanguages'),
            EPT.loadPreferences(PREFS_KEY)
        ]).then(function (results) {
            if (results[0].success) {
                state.contentTypes = results[0].contentTypes;
                // Start on 'types' tab by default. If the saved preference points to a contract,
                // the tab is switched later once prefs are applied.
                renderContentTypeDropdown();
            }
            if (results[1].success) {
                state.languages = results[1].languages;
                renderLanguageDropdown();
            }

            // Apply saved preferences
            var prefs = results[2] || {};
            if (prefs.pageSize) state.pageSize = prefs.pageSize;
            if (prefs.sortBy) state.sortBy = prefs.sortBy;
            if (prefs.sortDirection) state.sortDirection = prefs.sortDirection;
            if (prefs.includeReferences) state.includeReferences = prefs.includeReferences;

            if (prefs.language) {
                state.language = prefs.language;
                var langSel = document.getElementById('bpeLanguage');
                if (langSel) langSel.value = prefs.language;
            }

            if (prefs.contentTypeId) {
                var ctSel = document.getElementById('bpeContentType');
                if (ctSel) {
                    // CMS 13: switch to the correct tab if the saved type is a contract.
                    if (hasCms13Data()) {
                        var savedCt = state.contentTypes.find(function (t) { return t.id === prefs.contentTypeId; });
                        if (savedCt && savedCt.isContract) {
                            state.activeTab = 'contracts';
                            renderContentTypeDropdown();
                        }
                    }
                    ctSel.value = prefs.contentTypeId;
                    // Trigger content type change which loads columns and content
                    state.contentTypeId = prefs.contentTypeId;
                    state.columns = prefs.columns || [];
                    changeContentType(prefs.contentTypeId);
                }
            }
        }).catch(function (err) {
            showAlert('Failed to load initial data: ' + err.message, false);
        });
    }

    function updateTabsActive() {
        var tabsEl = document.getElementById('bpeTabs');
        if (!tabsEl) return;
        tabsEl.querySelectorAll('.ept-tab').forEach(function (btn) {
            if (btn.getAttribute('data-tab') === state.activeTab) {
                btn.classList.add('active');
            } else {
                btn.classList.remove('active');
            }
        });
    }

    function renderContentTypeDropdown() {
        var sel = document.getElementById('bpeContentType');
        var html = '<option value="">' + EPT.s('bulkeditor.opt_selecttype', '-- Select content type --') + '</option>';

        // CMS 13: show tabs and filter dropdown by active tab
        var cms13 = hasCms13Data();
        var tabsEl = document.getElementById('bpeTabs');
        if (tabsEl) {
            tabsEl.style.display = cms13 ? '' : 'none';
        }
        if (cms13) updateTabsActive();

        // Filter contentTypes by the active tab when CMS 13 data is available.
        var visibleTypes = cms13
            ? (state.activeTab === 'contracts'
                ? state.contentTypes.filter(function (t) { return t.isContract; })
                : state.contentTypes.filter(function (t) { return !t.isContract; }))
            : state.contentTypes;

        // Group by base type
        var groups = {};
        visibleTypes.forEach(function (ct) {
            var base = ct.baseType || 'Other';
            if (!groups[base]) groups[base] = [];
            groups[base].push(ct);
        });

        var order = ['Page', 'Block', 'Media', 'Other'];
        order.forEach(function (groupName) {
            var items = groups[groupName];
            if (!items || items.length === 0) return;
            html += '<optgroup label="' + escapeHtml(groupName) + 's">';
            items.sort(function (a, b) { return a.name.localeCompare(b.name); });
            items.forEach(function (ct) {
                // Options cannot contain HTML, so append composition markers as text.
                var suffix = '';
                if (ct.compositionBehaviors) {
                    var marks = [];
                    if (ct.compositionBehaviors.indexOf('SectionEnabled') >= 0) {
                        marks.push(EPT.s('bulkeditor.badge_section', 'Section'));
                    }
                    if (ct.compositionBehaviors.indexOf('ElementEnabled') >= 0) {
                        marks.push(EPT.s('bulkeditor.badge_element', 'Element'));
                    }
                    if (marks.length > 0) suffix = ' (' + marks.join(', ') + ')';
                }
                html += '<option value="' + ct.id + '">' + escapeHtml(ct.name + suffix) + '</option>';
            });
            html += '</optgroup>';
        });

        sel.innerHTML = html;

        // Preserve selected value if still present in filtered list
        if (state.contentTypeId) {
            sel.value = String(state.contentTypeId);
        }

        renderCompositionInfo();
    }

    // CMS 13: Show composition badges (Section / Element) for the currently selected content type
    // as real HTML pills rendered under the toolbar.
    function renderCompositionInfo() {
        var container = document.getElementById('bpeCompositionInfo');
        if (!container) return;
        container.innerHTML = '';
        if (!hasCms13Data() || !state.contentTypeId) {
            container.style.display = 'none';
            return;
        }
        var ct = null;
        for (var i = 0; i < state.contentTypes.length; i++) {
            if (state.contentTypes[i].id === state.contentTypeId) { ct = state.contentTypes[i]; break; }
        }
        if (!ct || !ct.compositionBehaviors || ct.compositionBehaviors.length === 0) {
            container.style.display = 'none';
            return;
        }
        var html = '';
        if (ct.compositionBehaviors.indexOf('SectionEnabled') >= 0) {
            html += '<span class="ept-badge ept-badge--success" style="margin-right:4px">' +
                escapeHtml(EPT.s('bulkeditor.badge_section', 'Section')) + '</span>';
        }
        if (ct.compositionBehaviors.indexOf('ElementEnabled') >= 0) {
            html += '<span class="ept-badge ept-badge--success" style="margin-right:4px">' +
                escapeHtml(EPT.s('bulkeditor.badge_element', 'Element')) + '</span>';
        }
        if (html) {
            container.innerHTML = html;
            container.style.display = '';
        } else {
            container.style.display = 'none';
        }
    }

    function renderLanguageDropdown() {
        var sel = document.getElementById('bpeLanguage');
        var html = '';
        state.languages.forEach(function (lang) {
            var selected = lang.isDefault ? ' selected' : '';
            html += '<option value="' + escapeHtml(lang.code) + '"' + selected + '>' + escapeHtml(lang.name) + ' (' + escapeHtml(lang.code) + ')</option>';
            if (lang.isDefault) state.language = lang.code;
        });
        sel.innerHTML = html;
    }

    // ---- Content type change ----

    function changeContentType(contentTypeId) {
        if (!contentTypeId) {
            state.contentTypeId = null;
            state.availableColumns = [];
            state.columns = [];
            state.filters = [];
            state.pendingChanges = {};
            document.getElementById('bpeTableBody').innerHTML = '<div class="ept-empty"><p>' + EPT.s('bulkeditor.empty_selecttype2', 'Select a content type above to start editing.') + '</p></div>';
            document.getElementById('bpePagination').style.display = 'none';
            renderFilterBar();
            renderColumnPicker();
            renderPendingBar();
            renderCompositionInfo();
            renderExpansionNote(null);
            return;
        }

        state.contentTypeId = parseInt(contentTypeId);
        state.page = 1;
        state.filters = [];
        state.columns = [];
        state.pendingChanges = {};
        renderFilterBar();
        renderPendingBar();
        renderCompositionInfo();

        // Detect base type for auto-enabling references
        var sel = document.getElementById('bpeContentType');
        var opt = sel.options[sel.selectedIndex];
        var optGroup = opt.parentElement;
        var baseType = optGroup && optGroup.tagName === 'OPTGROUP' ? optGroup.label : '';

        if (baseType === 'Blocks') {
            state.includeReferences = true;
            document.getElementById('bpeIncludeRefs').checked = true;
        } else {
            state.includeReferences = false;
            document.getElementById('bpeIncludeRefs').checked = false;
        }

        // Show/hide reference checkbox
        var refSep = document.getElementById('bpeRefSep');
        var refCheck = document.getElementById('bpeRefCheck');
        if (baseType === 'Blocks') {
            refSep.style.display = '';
            refCheck.style.display = '';
        } else {
            refSep.style.display = 'none';
            refCheck.style.display = 'none';
        }

        loadColumns(state.contentTypeId);
    }

    function loadColumns(contentTypeId) {
        apiFetch(API + '/GetProperties?id=' + contentTypeId)
            .then(function (result) {
                if (result.success) {
                    state.availableColumns = result.properties;
                    // If we have saved columns from prefs, filter to valid ones
                    if (state.columns.length > 0) {
                        var validNames = state.availableColumns.map(function (c) { return c.name; });
                        state.columns = state.columns.filter(function (c) { return validNames.indexOf(c) >= 0; });
                    }
                    renderColumnPicker();
                    saveUserPrefs();
                    loadContent();
                }
            })
            .catch(function (err) {
                showAlert('Failed to load properties: ' + err.message, false);
            });
    }

    // ---- Load content ----

    function loadContent() {
        if (!state.contentTypeId) return;

        clearAlert();
        state.isLoading = true;
        EPT.showLoading(document.getElementById('bpeTableBody'));
        document.getElementById('bpePagination').style.display = 'none';

        var params = [
            'contentTypeId=' + state.contentTypeId,
            'language=' + encodeURIComponent(state.language),
            'page=' + state.page,
            'pageSize=' + state.pageSize,
            'sortBy=' + encodeURIComponent(state.sortBy || 'Name'),
            'sortDirection=' + encodeURIComponent(state.sortDirection),
            'includeReferences=' + state.includeReferences
        ];

        if (state.filters.length > 0) {
            params.push('filters=' + encodeURIComponent(JSON.stringify(state.filters)));
        }

        if (state.columns.length > 0) {
            params.push('columns=' + encodeURIComponent(state.columns.join(',')));
        }

        apiFetch(API + '/GetContent?' + params.join('&'))
            .then(function (result) {
                state.isLoading = false;
                if (result.success) {
                    state.data = result.data;
                    renderExpansionNote(result.data);
                    renderTable(result.data);
                    renderPagination(result.data.totalCount, result.data.page, result.data.pageSize);
                } else {
                    showAlert(result.message || 'Failed to load content.', false);
                }
            })
            .catch(function (err) {
                state.isLoading = false;
                showAlert('Failed to load content: ' + err.message, false);
            });
    }

    // CMS 13: Render the contract expansion preview note. Cleared when no expansion.
    function renderExpansionNote(data) {
        var noteEl = document.getElementById('bpeExpansionNote');
        if (!noteEl) return;
        noteEl.innerHTML = '';
        noteEl.style.display = 'none';

        if (!data || !data.resolvedTypes || data.resolvedTypes.length === 0) return;

        var labels = data.resolvedTypes
            .map(function (id) {
                for (var i = 0; i < state.contentTypes.length; i++) {
                    if (state.contentTypes[i].id === id) return state.contentTypes[i].name;
                }
                return null;
            })
            .filter(function (n) { return !!n; });

        if (labels.length === 0) return;

        noteEl.textContent = EPT.s('bulkeditor.note_expansion', 'Contract expands to') + ': ' + labels.join(', ');
        noteEl.style.cssText = 'padding:8px 12px; margin:8px 0; background:var(--ept-primary-light,#e0f2fe); border-left:3px solid var(--ept-primary,#0284c7); border-radius:4px; font-size:13px;';
        noteEl.style.display = '';
    }

    // ---- Render table ----

    function renderTable(data) {
        var tableBody = document.getElementById('bpeTableBody');

        if (!data || !data.items || data.items.length === 0) {
            tableBody.innerHTML = '<div class="ept-empty"><p>' + EPT.s('bulkeditor.empty_noitems', 'No content items found matching your criteria.') + '</p></div>';
            return;
        }

        var selectedType = getSelectedContentType();
        var showParent = selectedType && (selectedType.baseType === 'Block' || selectedType.baseType === 'Media');

        // Build table HTML
        var html = '<div style="overflow-x: auto;"><table class="ept-table" id="bpeTable">';

        // Header
        html += '<thead><tr>';
        html += '<th style="width: 36px;"><input type="checkbox" id="bpeSelectAll" ' + (state.selectAll ? 'checked' : '') + ' /></th>';
        html += renderSortableHeader(EPT.s('bulkeditor.col_name', 'Name'), 'Name');
        html += renderSortableHeader(EPT.s('bulkeditor.col_status', 'Status'), 'Status');
        html += renderSortableHeader(EPT.s('bulkeditor.col_lastedited', 'Last Edited'), 'LastEdited');
        html += renderSortableHeader(EPT.s('bulkeditor.col_editedby', 'Edited By'), 'EditedBy');

        if (showParent) {
            html += '<th>' + EPT.s('bulkeditor.col_owner', 'Owner') + '</th>';
        }

        state.columns.forEach(function (colName) {
            var colInfo = findColumn(colName);
            var displayName = colInfo ? colInfo.displayName : colName;
            html += renderSortableHeader(displayName, colName);
        });

        if (state.includeReferences) {
            html += '<th>' + EPT.s('bulkeditor.col_refs', 'References') + '</th>';
        }

        html += '<th style="width: 140px;">' + EPT.s('bulkeditor.col_actions', 'Actions') + '</th>';
        html += '</tr></thead>';

        // Body
        html += '<tbody>';
        data.items.forEach(function (item) {
            var rowChanges = state.pendingChanges[item.contentId];
            html += '<tr data-content-id="' + item.contentId + '">';

            // Checkbox
            html += '<td><input type="checkbox" class="bpe-row-check" data-id="' + item.contentId + '" /></td>';

            // Name (editable if can edit)
            var nameValue = rowChanges && rowChanges['Name'] !== undefined ? rowChanges['Name'] : item.name;
            var nameCellClass = 'bpe-editable' + (rowChanges && rowChanges['Name'] !== undefined ? ' bpe-edited' : '');
            html += '<td class="' + nameCellClass + '" data-editable="true" data-content-id="' + item.contentId + '" data-prop="Name" data-type="String">' + escapeHtml(nameValue) + '</td>';

            // Status
            html += '<td><span class="ept-badge ' + getStatusBadgeClass(item.status) + '">' + escapeHtml(item.status) + '</span></td>';

            // Last Edited
            html += '<td>' + formatDate(item.lastEdited) + '</td>';

            // Edited By
            html += '<td>' + escapeHtml(item.editedBy) + '</td>';

            // Owner (for blocks/media)
            if (showParent) {
                if (item.parentName) {
                    html += '<td><a href="' + escapeHtml(item.parentEditUrl) + '" target="_blank" style="color: var(--ept-primary); text-decoration: none;">' + escapeHtml(item.parentName) + '</a></td>';
                } else {
                    html += '<td><span style="color: var(--ept-text-muted); font-size: 12px;">' + EPT.s('bulkeditor.lbl_global', 'global') + '</span></td>';
                }
            }

            // Custom columns
            state.columns.forEach(function (colName) {
                var prop = item.properties ? item.properties[colName] : null;
                if (prop) {
                    var displayVal = rowChanges && rowChanges[colName] !== undefined ? rowChanges[colName] : (prop.displayValue || '');
                    var cellClass = '';
                    if (prop.isEditable && item.canEdit) {
                        cellClass = 'bpe-editable';
                        if (rowChanges && rowChanges[colName] !== undefined) cellClass += ' bpe-edited';
                    } else if (rowChanges && rowChanges[colName] !== undefined) {
                        cellClass = 'bpe-edited';
                    }

                    if (prop.isEditable && item.canEdit) {
                        var rawAttr = prop.rawValue != null ? ' data-raw="' + escapeHtml(String(prop.rawValue)) + '"' : '';
                        html += '<td class="' + cellClass + '" data-editable="true" data-content-id="' + item.contentId + '" data-prop="' + escapeHtml(colName) + '" data-type="' + escapeHtml(prop.typeName) + '"' + rawAttr + '>' + escapeHtml(displayVal) + '</td>';
                    } else {
                        html += '<td>' + escapeHtml(displayVal) + '</td>';
                    }
                } else {
                    html += '<td></td>';
                }
            });

            // References
            if (state.includeReferences) {
                var refCount = item.references ? item.references.length : 0;
                if (refCount > 0) {
                    html += '<td><span class="bpe-ref-count" data-content-id="' + item.contentId + '">' + (refCount === 1 ? EPT.s('bulkeditor.lbl_ref', '{0} ref').replace('{0}', refCount) : EPT.s('bulkeditor.lbl_refs', '{0} refs').replace('{0}', refCount)) + '</span></td>';
                } else {
                    html += '<td><span style="color: var(--ept-text-muted); font-size: 12px;">' + EPT.s('bulkeditor.lbl_none', 'none') + '</span></td>';
                }
            }

            // Actions
            var hasChanges = rowChanges && Object.keys(rowChanges).length > 0;
            html += '<td><div class="bpe-row-actions">';
            html += '<a href="' + escapeHtml(item.editUrl) + '" target="_blank" class="ept-btn ept-btn--sm" style="text-decoration: none; font-size: 11px; padding: 2px 8px;">' + EPT.s('bulkeditor.btn_edit', 'Edit') + '</a>';
            if (hasChanges) {
                html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-save" data-id="' + item.contentId + '" style="font-size: 11px; padding: 2px 8px;">' + EPT.s('bulkeditor.btn_save', 'Save') + '</button>';
                if (item.canPublish) {
                    html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-publish" data-id="' + item.contentId + '" style="font-size: 11px; padding: 2px 8px;">' + EPT.s('bulkeditor.btn_publish', 'Publish') + '</button>';
                }
                html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-undo" data-id="' + item.contentId + '" style="font-size: 11px; padding: 2px 8px; color: var(--ept-text-secondary);" title="' + EPT.s('bulkeditor.btn_undo', 'Undo') + '">' + EPT.s('bulkeditor.btn_undo', 'Undo') + '</button>';
            }
            html += '</div></td>';

            html += '</tr>';
        });
        html += '</tbody></table></div>';

        tableBody.innerHTML = html;

        // Bind table events
        bindTableEvents();

        // Auto-focus cell after cross-page navigation
        if (pendingEditAfterPageChange) {
            var pending = pendingEditAfterPageChange;
            pendingEditAfterPageChange = null;
            var rows = document.querySelectorAll('#bpeTable tbody tr');
            if (rows.length > 0) {
                var targetRow = pending.position === 'first' ? rows[0] : rows[rows.length - 1];
                var targetCell = targetRow.children[pending.cellIndex];
                if (targetCell && targetCell.getAttribute('data-editable') === 'true') {
                    setTimeout(function () { startEditing(targetCell); }, 50);
                }
            }
        }
    }

    function bindTableEvents() {
        // Select all checkbox
        var selectAll = document.getElementById('bpeSelectAll');
        if (selectAll) {
            selectAll.addEventListener('change', function () { toggleSelectAll(this.checked); });
        }

        // Editable cells - click to edit
        var editableCells = document.querySelectorAll('td[data-editable="true"]');
        editableCells.forEach(function (cell) {
            cell.addEventListener('click', function () { startEditing(cell); });
        });

        // Row save buttons
        document.querySelectorAll('.bpe-btn-row-save').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                saveRow(parseInt(btn.getAttribute('data-id')));
            });
        });

        // Row publish buttons
        document.querySelectorAll('.bpe-btn-row-publish').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                publishRow(parseInt(btn.getAttribute('data-id')));
            });
        });

        // Row undo buttons
        document.querySelectorAll('.bpe-btn-row-undo').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                undoRow(parseInt(btn.getAttribute('data-id')));
            });
        });

        // Reference count clicks
        document.querySelectorAll('.bpe-ref-count').forEach(function (el) {
            el.addEventListener('click', function () {
                showReferences(parseInt(el.getAttribute('data-content-id')));
            });
        });
    }

    function renderSortableHeader(displayName, fieldName) {
        var cls = 'bpe-sortable';
        var arrow = '';
        if (state.sortBy === fieldName) {
            cls += state.sortDirection === 'asc' ? ' bpe-sorted-asc' : ' bpe-sorted-desc';
            arrow = state.sortDirection === 'asc' ? ' &#9650;' : ' &#9660;';
        }
        return '<th class="' + cls + '" data-sort-field="' + escapeHtml(fieldName) + '">' +
            escapeHtml(displayName) + '<span class="bpe-sort-arrow">' + arrow + '</span></th>';
    }

    function findColumn(name) {
        for (var i = 0; i < state.availableColumns.length; i++) {
            if (state.availableColumns[i].name === name) return state.availableColumns[i];
        }
        return null;
    }

    function getSelectedContentType() {
        var sel = document.getElementById('bpeContentType');
        if (!sel || !sel.value) return null;
        var opt = sel.options[sel.selectedIndex];
        var group = opt.parentElement;
        return {
            id: parseInt(sel.value),
            name: opt.text,
            baseType: group && group.tagName === 'OPTGROUP' ? group.label.replace(/s$/, '') : 'Other'
        };
    }

    // ---- Sorting ----

    // Delegate click on sortable headers
    document.addEventListener('click', function (e) {
        var th = e.target.closest('.bpe-sortable');
        if (th) {
            var field = th.getAttribute('data-sort-field');
            if (field) toggleSort(field);
        }
    });

    function toggleSort(column) {
        if (state.sortBy === column) {
            state.sortDirection = state.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
            state.sortBy = column;
            state.sortDirection = 'asc';
        }
        state.page = 1;
        saveUserPrefs();
        loadContent();
    }

    // ---- Inline editing ----

    function navigateVertical(cell, direction) {
        var input = cell.querySelector('input');
        if (input) {
            var contentId = cell.getAttribute('data-content-id');
            var propName = cell.getAttribute('data-prop');
            if (input.type === 'checkbox') {
                finishEditing(cell, contentId, propName, input.checked ? 'true' : 'false');
            } else {
                finishEditing(cell, contentId, propName, input.value);
            }
        }

        var row = cell.closest('tr');
        var targetRow = direction === 'down' ? row.nextElementSibling : row.previousElementSibling;
        var cellIndex = Array.from(row.children).indexOf(cell);

        if (targetRow) {
            var targetCell = targetRow.children[cellIndex];
            if (targetCell && targetCell.getAttribute('data-editable') === 'true') {
                startEditing(targetCell);
            }
            return;
        }

        var totalPages = state.data ? state.data.totalPages : 1;
        if (direction === 'down' && state.page < totalPages) {
            pendingEditAfterPageChange = { cellIndex: cellIndex, position: 'first' };
            changePage(state.page + 1);
        } else if (direction === 'up' && state.page > 1) {
            pendingEditAfterPageChange = { cellIndex: cellIndex, position: 'last' };
            changePage(state.page - 1);
        }
    }

    function startEditing(cell) {
        if (cell.classList.contains('bpe-editing')) return;
        if (cell.getAttribute('data-editable') !== 'true') return;

        var contentId = cell.getAttribute('data-content-id');
        var propName = cell.getAttribute('data-prop');
        var typeName = (cell.getAttribute('data-type') || 'String').toLowerCase();
        var currentValue = cell.textContent.trim();

        if (state.pendingChanges[contentId] && state.pendingChanges[contentId][propName] !== undefined) {
            currentValue = state.pendingChanges[contentId][propName];
            if (currentValue === null) currentValue = '';
        }

        cell.classList.add('bpe-editing');

        if (typeName === 'boolean' || typeName === 'bool') {
            var checked = currentValue === 'true' || currentValue === 'True' || currentValue === true;
            cell.innerHTML = '<input type="checkbox" ' + (checked ? 'checked' : '') + ' />';
            var cb = cell.querySelector('input');
            cb.focus();
            cb.addEventListener('change', function () {
                finishEditing(cell, contentId, propName, cb.checked ? 'true' : 'false');
            });
            cb.addEventListener('blur', function () {
                finishEditing(cell, contentId, propName, cb.checked ? 'true' : 'false');
            });
        } else if (typeName === 'int32' || typeName === 'int64' || typeName === 'double' || typeName === 'decimal' || typeName === 'number') {
            cell.innerHTML = '<input type="number" value="' + escapeHtml(currentValue) + '" />';
            var numInput = cell.querySelector('input');
            numInput.focus();
            numInput.select();
            numInput.addEventListener('blur', function () {
                finishEditing(cell, contentId, propName, numInput.value);
            });
            numInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { numInput.blur(); }
                else if (e.key === 'Escape') { cancelEditing(cell, currentValue); }
                else if (e.key === 'ArrowDown') { e.preventDefault(); navigateVertical(cell, 'down'); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); navigateVertical(cell, 'up'); }
            });
        } else if (typeName === 'datetime' || typeName === 'datetimeoffset' || typeName === 'date') {
            var dateVal = currentValue;
            if (dateVal) {
                var d = new Date(dateVal);
                if (!isNaN(d.getTime())) {
                    dateVal = d.toISOString().slice(0, 16);
                }
            }
            cell.innerHTML = '<input type="datetime-local" value="' + escapeHtml(dateVal) + '" />';
            var dateInput = cell.querySelector('input');
            dateInput.focus();
            dateInput.addEventListener('blur', function () {
                finishEditing(cell, contentId, propName, dateInput.value);
            });
            dateInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { dateInput.blur(); }
                else if (e.key === 'Escape') { cancelEditing(cell, currentValue); }
                else if (e.key === 'ArrowDown') { e.preventDefault(); navigateVertical(cell, 'down'); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); navigateVertical(cell, 'up'); }
            });
        } else if (typeName === 'url') {
            cell.innerHTML = '<input type="url" value="' + escapeHtml(currentValue) + '" placeholder="https://..." style="width:100%" />';
            var urlInput = cell.querySelector('input');
            urlInput.focus();
            urlInput.select();
            urlInput.addEventListener('blur', function () {
                finishEditing(cell, contentId, propName, urlInput.value);
            });
            urlInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { urlInput.blur(); }
                else if (e.key === 'Escape') { cancelEditing(cell, currentValue); }
            });
        } else if (typeName === 'pagereference' || typeName === 'contentreference') {
            var rawVal = cell.getAttribute('data-raw') || currentValue;
            // Extract just the ID number from display like "Name (ID: 5)"
            var idMatch = rawVal ? String(rawVal).match(/\d+/) : null;
            var currentId = idMatch ? idMatch[0] : '';
            cell.innerHTML = '<div style="display:flex;gap:4px;align-items:center">' +
                '<input type="text" value="' + escapeHtml(currentId) + '" style="width:60px" placeholder="ID" readonly />' +
                '<button type="button" class="ept-btn ept-btn--sm">' + EPT.s('bulkeditor.btn_browse', 'Browse...') + '</button>' +
                '<button type="button" class="ept-btn ept-btn--sm" title="Clear">&times;</button>' +
                '</div>';
            var refInput = cell.querySelector('input');
            var browseBtn = cell.querySelectorAll('button')[0];
            var clearBtn = cell.querySelectorAll('button')[1];
            browseBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                EPT.contentPicker({ title: EPT.s('bulkeditor.dlg_selectcontent', 'Select Content') }).then(function (selected) {
                    if (selected) {
                        refInput.value = selected.id;
                        finishEditing(cell, contentId, propName, String(selected.id));
                    }
                });
            });
            clearBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                finishEditing(cell, contentId, propName, '');
            });
            browseBtn.focus();
        } else {
            cell.innerHTML = '<input type="text" value="' + escapeHtml(currentValue) + '" />';
            var textInput = cell.querySelector('input');
            textInput.focus();
            textInput.select();
            textInput.addEventListener('blur', function () {
                finishEditing(cell, contentId, propName, textInput.value);
            });
            textInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') { textInput.blur(); }
                else if (e.key === 'Escape') { cancelEditing(cell, currentValue); }
                else if (e.key === 'ArrowDown') { e.preventDefault(); navigateVertical(cell, 'down'); }
                else if (e.key === 'ArrowUp') { e.preventDefault(); navigateVertical(cell, 'up'); }
            });
        }
    }

    function finishEditing(cell, contentId, propName, newValue) {
        cell.classList.remove('bpe-editing');

        var originalValue = getOriginalValue(contentId, propName);

        if (String(newValue) !== String(originalValue)) {
            trackChange(contentId, propName, newValue);
            cell.classList.add('bpe-edited');
        } else {
            if (state.pendingChanges[contentId]) {
                delete state.pendingChanges[contentId][propName];
                if (Object.keys(state.pendingChanges[contentId]).length === 0) {
                    delete state.pendingChanges[contentId];
                }
            }
            cell.classList.remove('bpe-edited');
        }

        // Display the value
        var displayValue = newValue;
        var cellType = (cell.getAttribute('data-type') || '').toLowerCase();
        if (cellType === 'boolean' || cellType === 'bool') {
            displayValue = newValue === 'true' ? 'True' : 'False';
        } else if (cellType === 'datetime' || cellType === 'datetimeoffset' || cellType === 'date') {
            if (newValue) displayValue = formatDate(newValue);
        }
        cell.textContent = displayValue || '';

        renderPendingBar();
        updateRowActions(contentId);
    }

    function cancelEditing(cell, originalDisplayValue) {
        cell.classList.remove('bpe-editing');
        cell.textContent = originalDisplayValue || '';
    }

    function getOriginalValue(contentId, propName) {
        if (!state.data || !state.data.items) return '';
        for (var i = 0; i < state.data.items.length; i++) {
            var item = state.data.items[i];
            if (item.contentId === parseInt(contentId)) {
                if (propName === 'Name') return item.name || '';
                if (item.properties && item.properties[propName]) {
                    return item.properties[propName].displayValue || '';
                }
                return '';
            }
        }
        return '';
    }

    function trackChange(contentId, propName, newValue) {
        if (!state.pendingChanges[contentId]) {
            state.pendingChanges[contentId] = {};
        }
        state.pendingChanges[contentId][propName] = newValue;
    }

    function updateRowActions(contentId) {
        var row = document.querySelector('tr[data-content-id="' + contentId + '"]');
        if (!row) return;

        var actionsTd = row.querySelector('td:last-child');
        var hasChanges = state.pendingChanges[contentId] && Object.keys(state.pendingChanges[contentId]).length > 0;

        var item = findItemById(contentId);
        var editUrl = item ? item.editUrl : '';

        var html = '<div class="bpe-row-actions">';
        html += '<a href="' + escapeHtml(editUrl) + '" target="_blank" class="ept-btn ept-btn--sm" style="text-decoration: none; font-size: 11px; padding: 2px 8px;">Edit</a>';
        if (hasChanges) {
            html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-save" data-id="' + contentId + '" style="font-size: 11px; padding: 2px 8px;">Save</button>';
            if (item && item.canPublish) {
                html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-publish" data-id="' + contentId + '" style="font-size: 11px; padding: 2px 8px;">Publish</button>';
            }
            html += '<button type="button" class="ept-btn ept-btn--sm bpe-btn-row-undo" data-id="' + contentId + '" style="font-size: 11px; padding: 2px 8px; color: var(--ept-text-secondary);" title="Undo changes on this row">Undo</button>';
        }
        html += '</div>';
        actionsTd.innerHTML = html;

        // Re-bind events for this row
        actionsTd.querySelectorAll('.bpe-btn-row-save').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                saveRow(parseInt(btn.getAttribute('data-id')));
            });
        });
        actionsTd.querySelectorAll('.bpe-btn-row-publish').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                publishRow(parseInt(btn.getAttribute('data-id')));
            });
        });
        actionsTd.querySelectorAll('.bpe-btn-row-undo').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                undoRow(parseInt(btn.getAttribute('data-id')));
            });
        });
    }

    function findItemById(contentId) {
        if (!state.data || !state.data.items) return null;
        var id = parseInt(contentId);
        for (var i = 0; i < state.data.items.length; i++) {
            if (state.data.items[i].contentId === id) return state.data.items[i];
        }
        return null;
    }

    // ---- Save / Publish ----

    function saveRow(contentId) {
        var changes = state.pendingChanges[contentId];
        if (!changes) return;

        var items = [{
            contentId: parseInt(contentId),
            language: state.language,
            propertyChanges: changes
        }];

        setBusy(true);

        apiFetch(API + '/BulkSave', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'save', items: items })
        })
        .then(function (result) {
            setBusy(false);
            if (result.success) {
                delete state.pendingChanges[contentId];
                showAlert('Content saved successfully.', true);
                loadContent();
                renderPendingBar();
            } else {
                showAlert(result.message || 'Save failed.', false);
            }
        })
        .catch(function (err) {
            setBusy(false);
            showAlert('Save failed: ' + err.message, false);
        });
    }

    function publishRow(contentId) {
        var changes = state.pendingChanges[contentId];

        setBusy(true);

        var savePromise;
        if (changes && Object.keys(changes).length > 0) {
            var items = [{
                contentId: parseInt(contentId),
                language: state.language,
                propertyChanges: changes
            }];
            savePromise = apiFetch(API + '/BulkSave', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ action: 'save', items: items })
            });
        } else {
            savePromise = Promise.resolve({ success: true });
        }

        savePromise
            .then(function () {
                return apiFetch(API + '/Publish?id=' + contentId + '&language=' + encodeURIComponent(state.language), {
                    method: 'POST'
                });
            })
            .then(function (result) {
                setBusy(false);
                if (result.success) {
                    delete state.pendingChanges[contentId];
                    showAlert('Content published successfully.', true);
                    loadContent();
                    renderPendingBar();
                } else {
                    showAlert(result.message || 'Publish failed.', false);
                }
            })
            .catch(function (err) {
                setBusy(false);
                showAlert('Publish failed: ' + err.message, false);
            });
    }

    function saveAll() {
        var changedIds = getChangedContentIds();
        if (changedIds.length === 0) return;

        var items = changedIds.map(function (cid) {
            return {
                contentId: cid,
                language: state.language,
                propertyChanges: state.pendingChanges[cid]
            };
        });

        setBusy(true);

        apiFetch(API + '/BulkSave', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'save', items: items })
        })
        .then(function (result) {
            setBusy(false);
            if (result.success) {
                state.pendingChanges = {};
                showAlert('All changes saved successfully.', true);
                loadContent();
                renderPendingBar();
            } else {
                showAlert(result.message || 'Bulk save failed.', false);
            }
        })
        .catch(function (err) {
            setBusy(false);
            showAlert('Bulk save failed: ' + err.message, false);
        });
    }

    function publishAll() {
        var changedIds = getChangedContentIds();
        if (changedIds.length === 0) return;

        if (!confirm('Publish all ' + changedIds.length + ' changed items?')) return;

        var items = changedIds.map(function (cid) {
            return {
                contentId: cid,
                language: state.language,
                propertyChanges: state.pendingChanges[cid]
            };
        });

        setBusy(true);

        apiFetch(API + '/BulkSave', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ action: 'publish', items: items })
        })
        .then(function (result) {
            setBusy(false);
            if (result.success) {
                state.pendingChanges = {};
                showAlert('All changes published successfully.', true);
                loadContent();
                renderPendingBar();
            } else {
                showAlert(result.message || 'Bulk publish failed.', false);
            }
        })
        .catch(function (err) {
            setBusy(false);
            showAlert('Bulk publish failed: ' + err.message, false);
        });
    }

    function undoRow(contentId) {
        delete state.pendingChanges[contentId];
        renderPendingBar();
        if (state.data) renderTable(state.data);
    }

    function discardAll() {
        if (!confirm(EPT.s('bulkeditor.confirm_discard', 'Discard all pending changes?'))) return;
        state.pendingChanges = {};
        renderPendingBar();
        if (state.data) renderTable(state.data);
    }

    function setBusy(busy) {
        var saveBtn = document.getElementById('bpeSaveAll');
        var pubBtn = document.getElementById('bpePublishAll');
        if (saveBtn) saveBtn.disabled = busy;
        if (pubBtn) pubBtn.disabled = busy;
    }

    // ---- Filters ----

    function addFilter() {
        if (state.availableColumns.length === 0 && !state.contentTypeId) {
            showAlert('Please select a content type first.', false);
            return;
        }
        state.filters.push({ propertyName: 'Name', operator: 'contains', value: '' });
        renderFilterBar();
    }

    function removeFilter(index) {
        state.filters.splice(index, 1);
        renderFilterBar();
    }

    function renderFilterBar() {
        var bar = document.getElementById('bpeFilterBar');
        var container = document.getElementById('bpeFilters');

        if (state.filters.length === 0) {
            bar.style.display = 'none';
            container.innerHTML = '';
            return;
        }

        bar.style.display = '';

        var html = '';
        state.filters.forEach(function (filter, index) {
            html += '<div class="bpe-filter-row">';

            // Property dropdown
            html += '<select class="ept-select bpe-filter-prop" data-index="' + index + '" data-field="propertyName">';
            html += '<option value="Name"' + (filter.propertyName === 'Name' ? ' selected' : '') + '>Name</option>';
            html += '<option value="Status"' + (filter.propertyName === 'Status' ? ' selected' : '') + '>Status</option>';
            html += '<option value="EditedBy"' + (filter.propertyName === 'EditedBy' ? ' selected' : '') + '>Edited By</option>';
            state.availableColumns.forEach(function (col) {
                html += '<option value="' + escapeHtml(col.name) + '"' + (filter.propertyName === col.name ? ' selected' : '') + '>' + escapeHtml(col.displayName) + '</option>';
            });
            html += '</select>';

            // Operator dropdown
            html += '<select class="ept-select bpe-filter-op" data-index="' + index + '" data-field="operator">';
            html += '<option value="contains"' + (filter.operator === 'contains' ? ' selected' : '') + '>contains</option>';
            html += '<option value="startsWith"' + (filter.operator === 'startsWith' ? ' selected' : '') + '>starts with</option>';
            html += '<option value="equals"' + (filter.operator === 'equals' ? ' selected' : '') + '>equals</option>';
            html += '<option value="notEmpty"' + (filter.operator === 'notEmpty' ? ' selected' : '') + '>is not empty</option>';
            html += '</select>';

            // Value input
            var hideVal = filter.operator === 'notEmpty' ? ' style="display:none;"' : '';
            html += '<input type="text" class="ept-search bpe-filter-val" value="' + escapeHtml(filter.value) + '"' + hideVal + ' data-index="' + index + '" data-field="value" />';

            // Remove button
            html += '<button type="button" class="ept-btn ept-btn--sm bpe-filter-remove" data-index="' + index + '" title="Remove filter">&times;</button>';

            html += '</div>';
        });

        container.innerHTML = html;

        // Bind filter events
        container.querySelectorAll('.bpe-filter-prop, .bpe-filter-op').forEach(function (sel) {
            sel.addEventListener('change', function () {
                var idx = parseInt(sel.getAttribute('data-index'));
                var field = sel.getAttribute('data-field');
                updateFilter(idx, field, sel.value);
            });
        });

        container.querySelectorAll('.bpe-filter-val').forEach(function (input) {
            input.addEventListener('change', function () {
                var idx = parseInt(input.getAttribute('data-index'));
                updateFilter(idx, 'value', input.value);
            });
            input.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') applyFilters();
            });
        });

        container.querySelectorAll('.bpe-filter-remove').forEach(function (btn) {
            btn.addEventListener('click', function () {
                removeFilter(parseInt(btn.getAttribute('data-index')));
            });
        });
    }

    function updateFilter(index, field, value) {
        if (state.filters[index]) {
            state.filters[index][field] = value;
            if (field === 'operator') {
                renderFilterBar();
            }
        }
    }

    function applyFilters() {
        state.page = 1;
        loadContent();
    }

    // ---- Column picker ----

    function toggleColumnPicker() {
        var picker = document.getElementById('bpeColumnPicker');
        picker.classList.toggle('bpe-col-picker--open');
    }

    function renderColumnPicker() {
        var picker = document.getElementById('bpeColumnPicker');

        if (state.availableColumns.length === 0) {
            picker.innerHTML = '<div style="padding: 12px; color: var(--ept-text-muted); font-size: 13px;">Select a content type first.</div>';
            return;
        }

        var html = '';
        state.availableColumns.forEach(function (col) {
            var checked = state.columns.indexOf(col.name) >= 0;
            html += '<label class="bpe-col-picker-item">';
            html += '<input type="checkbox" data-col="' + escapeHtml(col.name) + '" ' + (checked ? 'checked' : '') + ' />';
            html += escapeHtml(col.displayName);
            if (col.isEditable) {
                html += ' <span style="color: var(--ept-text-muted); font-size: 11px;">(editable)</span>';
            }
            html += '</label>';
        });

        picker.innerHTML = html;

        // Bind events
        picker.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
            cb.addEventListener('change', function (e) {
                e.stopPropagation();
                toggleColumn(cb.getAttribute('data-col'), cb.checked);
            });
        });
    }

    function toggleColumn(name, checked) {
        if (checked) {
            if (state.columns.indexOf(name) < 0) {
                state.columns.push(name);
            }
        } else {
            state.columns = state.columns.filter(function (c) { return c !== name; });
        }
        saveUserPrefs();
        loadContent();
    }

    // ---- Pagination ----

    function renderPagination(totalCount, page, pageSize) {
        var paginationEl = document.getElementById('bpePagination');
        if (!totalCount || totalCount === 0) {
            paginationEl.style.display = 'none';
            return;
        }

        paginationEl.style.display = '';

        var totalPages = Math.ceil(totalCount / pageSize);
        var startItem = (page - 1) * pageSize + 1;
        var endItem = Math.min(page * pageSize, totalCount);

        document.getElementById('bpePaginationInfo').textContent = 'Showing ' + startItem + '-' + endItem + ' of ' + totalCount;

        var controlsHtml = '';

        // Previous
        controlsHtml += '<button type="button" class="ept-btn ept-btn--sm bpe-page-btn" data-page="' + (page - 1) + '" ' + (page <= 1 ? 'disabled' : '') + '>&laquo;</button>';

        // Page numbers
        var startPage = Math.max(1, page - 2);
        var endPage = Math.min(totalPages, page + 2);

        if (startPage > 1) {
            controlsHtml += '<button type="button" class="ept-btn ept-btn--sm bpe-page-btn" data-page="1">1</button>';
            if (startPage > 2) {
                controlsHtml += '<span style="padding: 0 4px; color: var(--ept-text-muted);">...</span>';
            }
        }

        for (var i = startPage; i <= endPage; i++) {
            var activeClass = i === page ? ' ept-btn--primary' : '';
            controlsHtml += '<button type="button" class="ept-btn ept-btn--sm bpe-page-btn' + activeClass + '" data-page="' + i + '"' + (i === page ? ' disabled' : '') + '>' + i + '</button>';
        }

        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                controlsHtml += '<span style="padding: 0 4px; color: var(--ept-text-muted);">...</span>';
            }
            controlsHtml += '<button type="button" class="ept-btn ept-btn--sm bpe-page-btn" data-page="' + totalPages + '">' + totalPages + '</button>';
        }

        // Next
        controlsHtml += '<button type="button" class="ept-btn ept-btn--sm bpe-page-btn" data-page="' + (page + 1) + '" ' + (page >= totalPages ? 'disabled' : '') + '>&raquo;</button>';

        var controlsEl = document.getElementById('bpePaginationControls');
        controlsEl.innerHTML = controlsHtml;

        // Bind page button events
        controlsEl.querySelectorAll('.bpe-page-btn').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var p = parseInt(btn.getAttribute('data-page'));
                if (p >= 1 && p <= totalPages) changePage(p);
            });
        });
    }

    function changePage(page) {
        state.page = page;
        loadContent();
    }

    function changePageSize(size) {
        state.pageSize = size;
        state.page = 1;
        saveUserPrefs();
        loadContent();
    }

    // ---- Pending bar ----

    function renderPendingBar() {
        var bar = document.getElementById('bpePendingBar');
        var count = getPendingChangeCount();
        var countEl = document.getElementById('bpePendingCount');
        if (countEl) countEl.textContent = count;
        if (bar) {
            if (count > 0) {
                bar.classList.add('bpe-pending-bar--visible');
            } else {
                bar.classList.remove('bpe-pending-bar--visible');
            }
        }
    }

    // ---- Language ----

    function changeLanguage(code) {
        state.language = code;
        state.page = 1;
        state.pendingChanges = {};
        renderPendingBar();
        if (state.contentTypeId) {
            loadContent();
        }
    }

    // ---- References ----

    function toggleReferences(checked) {
        state.includeReferences = checked;
        if (state.contentTypeId) {
            loadContent();
        }
    }

    function showReferences(contentId) {
        if (!state.data || !state.data.items) return;
        var item = null;
        for (var i = 0; i < state.data.items.length; i++) {
            if (state.data.items[i].contentId === contentId) {
                item = state.data.items[i];
                break;
            }
        }
        if (!item || !item.references || item.references.length === 0) {
            showAlert('No references found.', false);
            return;
        }

        var dialog = EPT.openDialog('References for "' + item.name + '"', { wide: false });
        var html = '<table class="ept-table"><thead><tr><th>Name</th><th>Type</th><th></th></tr></thead><tbody>';
        item.references.forEach(function (ref) {
            html += '<tr>';
            html += '<td>' + escapeHtml(ref.name) + '</td>';
            html += '<td>' + escapeHtml(ref.contentTypeName) + '</td>';
            html += '<td>';
            if (ref.editUrl) {
                html += '<a href="' + escapeHtml(ref.editUrl) + '" target="_blank" class="ept-btn ept-btn--sm" style="text-decoration: none; font-size: 11px; padding: 2px 8px;">Edit</a>';
            }
            html += '</td>';
            html += '</tr>';
        });
        html += '</tbody></table>';
        dialog.body.innerHTML = html;
    }

    // ---- Select all ----

    function toggleSelectAll(checked) {
        state.selectAll = checked;
        var checkboxes = document.querySelectorAll('.bpe-row-check');
        checkboxes.forEach(function (cb) { cb.checked = checked; });
    }

    // ---- Init on DOM ready ----

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
