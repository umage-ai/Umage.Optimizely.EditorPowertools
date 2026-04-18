/**
 * Content Audit - Comprehensive content inventory with configurable columns, filters, and export.
 */
(function () {
    'use strict';

    var API = window.EPT_BASE_URL + 'ContentAuditApi';
    var PREFS_KEY = 'ContentAudit';

    // ---- Column definitions ----
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

    var QUICK_FILTERS = [
        { key: '', label: EPT.s('contentaudit.filter_all', 'All content') },
        { key: 'pages', label: EPT.s('contentaudit.filter_pages', 'Pages only') },
        { key: 'blocks', label: EPT.s('contentaudit.filter_blocks', 'Blocks only') },
        { key: 'media', label: EPT.s('contentaudit.filter_media', 'Media only') },
        { key: 'unpublished', label: EPT.s('contentaudit.filter_unpublished', 'Unpublished') },
        { key: 'unused', label: EPT.s('contentaudit.filter_unused', 'Unused content') }
    ];

    var FILTER_OPERATORS = [
        { key: 'contains', label: EPT.s('contentaudit.op_contains', 'Contains') },
        { key: 'equals', label: EPT.s('contentaudit.op_equals', 'Equals') },
        { key: 'startsWith', label: EPT.s('contentaudit.op_startswith', 'Starts with') },
        { key: 'isEmpty', label: EPT.s('contentaudit.op_isempty', 'Is empty') },
        { key: 'isNotEmpty', label: EPT.s('contentaudit.op_isnotempty', 'Is not empty') }
    ];

    // ---- State ----
    var state = {
        page: 1,
        pageSize: 50,
        sortBy: 'name',
        sortDirection: 'asc',
        search: '',
        quickFilter: '',
        contractFilter: '',
        compositionFilter: '',
        filters: [],
        visibleColumns: [],
        data: null,
        isLoading: false
    };

    // ---- DOM ----
    var root;

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

    function debounce(fn, delay) {
        var timer;
        return function () {
            var args = arguments;
            var ctx = this;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, delay);
        };
    }

    // ---- Preferences ----

    var savePrefsDebounced = debounce(function () {
        var prefs = {
            visibleColumns: state.visibleColumns,
            pageSize: state.pageSize,
            sortBy: state.sortBy,
            sortDirection: state.sortDirection
        };
        try {
            fetch(window.EPT_BASE_URL + 'PreferencesApi/Save?id=' + PREFS_KEY, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify(prefs)
            });
        } catch (e) { /* ignore */ }
    }, 1000);

    function loadPreferences() {
        return fetch(window.EPT_BASE_URL + 'PreferencesApi/Get?id=' + PREFS_KEY)
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (prefs) {
                if (prefs && prefs.visibleColumns && prefs.visibleColumns.length > 0) {
                    state.visibleColumns = prefs.visibleColumns;
                }
                if (prefs && prefs.pageSize) state.pageSize = prefs.pageSize;
                if (prefs && prefs.sortBy) state.sortBy = prefs.sortBy;
                if (prefs && prefs.sortDirection) state.sortDirection = prefs.sortDirection;
            })
            .catch(function () { /* use defaults */ });
    }

    // ---- API ----

    function buildQueryString() {
        var params = [
            'page=' + state.page,
            'pageSize=' + state.pageSize,
            'columns=' + encodeURIComponent(state.visibleColumns.join(','))
        ];
        if (state.sortBy) params.push('sortBy=' + encodeURIComponent(state.sortBy));
        if (state.sortDirection) params.push('sortDirection=' + encodeURIComponent(state.sortDirection));
        if (state.search) params.push('search=' + encodeURIComponent(state.search));
        if (state.quickFilter) params.push('quickFilter=' + encodeURIComponent(state.quickFilter));
        if (state.contractFilter) params.push('contractFilter=' + encodeURIComponent(state.contractFilter));
        if (state.compositionFilter) params.push('compositionFilter=' + encodeURIComponent(state.compositionFilter));
        if (state.filters.length > 0) {
            params.push('filters=' + encodeURIComponent(JSON.stringify(state.filters)));
        }
        return params.join('&');
    }

    function fetchData() {
        state.isLoading = true;
        renderLoadingState();

        var url = API + '/GetContent?' + buildQueryString();
        return fetch(url)
            .then(function (r) {
                if (!r.ok) throw new Error('HTTP ' + r.status);
                return r.json();
            })
            .then(function (result) {
                state.isLoading = false;
                if (result.success) {
                    state.data = result.data;
                } else {
                    state.data = null;
                }
                render();
            })
            .catch(function (err) {
                state.isLoading = false;
                state.data = null;
                renderError(err.message);
            });
    }

    // ---- Rendering ----

    function renderLoadingState() {
        var tableArea = document.getElementById('ca-table-area');
        if (tableArea) {
            EPT.showLoading(tableArea);
        }
    }

    function renderError(msg) {
        var tableArea = document.getElementById('ca-table-area');
        var target = tableArea || root;
        if (target) {
            target.innerHTML = '<div class="ept-empty"><p>Error: ' + escapeHtml(msg) + '</p></div>';
        }
    }

    function render() {
        var html = '';
        html += renderStats();
        html += '<div class="ept-card"><div class="ept-card__body" style="padding:0">';
        html += renderToolbar();
        html += '<div id="ca-table-area">';
        html += renderTable();
        html += '</div>';
        html += renderPagination();
        html += '</div></div>';
        root.innerHTML = html;
        bindEvents();
    }

    function renderStats() {
        var d = state.data;
        if (!d) return '';
        var totalDisplay = d.totalCount != null ? d.totalCount.toLocaleString() : EPT.s('shared.unknown', '—');
        var totalPagesDisplay = d.totalPages != null ? d.totalPages : EPT.s('shared.unknown', '—');
        return '<div class="ept-stats">' +
            '<div class="ept-stat"><div class="ept-stat__value">' + totalDisplay + '</div><div class="ept-stat__label">' + EPT.s('contentaudit.stat_totalitems', 'Total items') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + totalPagesDisplay + '</div><div class="ept-stat__label">' + EPT.s('contentaudit.stat_pages', 'Pages') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + d.page + '</div><div class="ept-stat__label">' + EPT.s('contentaudit.stat_currentpage', 'Current page') + '</div></div>' +
            '</div>';
    }

    function renderToolbar() {
        var html = '<div class="ept-toolbar" style="flex-wrap:wrap;gap:8px;">';

        // Search
        html += '<div class="ept-search" style="min-width:200px;">';
        html += '<span class="ept-search__icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg></span>';
        html += '<input type="text" id="ca-search" placeholder="' + EPT.s('contentaudit.lbl_search', 'Search by name...') + '" value="' + escapeHtml(state.search) + '">';
        html += '</div>';

        // Quick filter
        html += '<select id="ca-quick-filter" class="ept-select">';
        for (var i = 0; i < QUICK_FILTERS.length; i++) {
            var qf = QUICK_FILTERS[i];
            html += '<option value="' + qf.key + '"' + (state.quickFilter === qf.key ? ' selected' : '') + '>' + escapeHtml(qf.label) + '</option>';
        }
        html += '</select>';

        // Page size
        html += '<select id="ca-page-size" class="ept-select" style="width:auto">';
        var sizes = [25, 50, 100, 200];
        for (var j = 0; j < sizes.length; j++) {
            html += '<option value="' + sizes[j] + '"' + (state.pageSize === sizes[j] ? ' selected' : '') + '>' + EPT.s('contentaudit.perpage', '{0} per page').replace('{0}', sizes[j]) + '</option>';
        }
        html += '</select>';

        html += '<div class="ept-toolbar__spacer"></div>';

        // Filter button
        html += '<button class="ept-btn ept-btn--sm" id="ca-filter-btn" title="Add filter">' + EPT.s('contentaudit.btn_filter', 'Filter') +
            (state.filters.length > 0 ? ' (' + state.filters.length + ')' : '') + '</button>';

        // Column picker
        html += '<button class="ept-btn ept-btn--sm" id="ca-columns-btn" title="Choose columns">' + EPT.s('contentaudit.btn_columns', 'Columns') + '</button>';

        // Export dropdown
        html += '<div class="ept-dropdown" style="position:relative;display:inline-block">';
        html += '<button class="ept-btn ept-btn--sm ept-btn--primary" id="ca-export-btn">' + EPT.s('shared.export', 'Export') + '</button>';
        html += '<div id="ca-export-menu" class="ept-dropdown-menu" style="display:none;position:absolute;right:0;top:100%;z-index:10;background:var(--ept-bg-card,#fff);border:1px solid var(--ept-border,#ddd);border-radius:4px;box-shadow:0 2px 8px rgba(0,0,0,.15);min-width:140px">';
        html += '<a href="#" class="ept-dropdown-item" data-format="xlsx" style="display:block;padding:8px 12px;text-decoration:none;color:inherit">' + EPT.s('contentaudit.fmt_excel', 'Excel (.xlsx)') + '</a>';
        html += '<a href="#" class="ept-dropdown-item" data-format="csv" style="display:block;padding:8px 12px;text-decoration:none;color:inherit">' + EPT.s('contentaudit.fmt_csv', 'CSV (.csv)') + '</a>';
        html += '<a href="#" class="ept-dropdown-item" data-format="json" style="display:block;padding:8px 12px;text-decoration:none;color:inherit">' + EPT.s('contentaudit.fmt_json', 'JSON (.json)') + '</a>';
        html += '</div></div>';

        html += '</div>';

        // CMS 13 filter row (cosmetic on CMS 12 — backend short-circuits via CmsFeatureFlags.ContractsAvailable)
        html += '<div class="ept-toolbar" style="flex-wrap:wrap;gap:8px;padding-top:0;">';
        html += '<fieldset class="ept-filter-group" style="margin:0;padding:4px 10px;border:1px solid var(--ept-border,#e5e7eb);border-radius:4px;display:flex;align-items:center;gap:10px;flex-wrap:wrap">';
        html += '<legend style="font-weight:600;padding:0 4px;font-size:0.8em">' + EPT.s('contentaudit.filter_cms13', 'CMS 13') + '</legend>';
        html += '<label style="margin:0;display:inline-flex;align-items:center;gap:4px;font-size:0.9em">';
        html += EPT.s('contentaudit.filter_contract', 'Contracts') + ':';
        html += '<select id="ca-contract-filter" class="ept-select" style="width:auto">';
        html += '<option value=""' + (state.contractFilter === '' ? ' selected' : '') + '>' + EPT.s('contentaudit.any', 'Any') + '</option>';
        html += '<option value="include"' + (state.contractFilter === 'include' ? ' selected' : '') + '>' + EPT.s('contentaudit.include', 'Include') + '</option>';
        html += '<option value="exclude"' + (state.contractFilter === 'exclude' ? ' selected' : '') + '>' + EPT.s('contentaudit.exclude', 'Exclude') + '</option>';
        html += '<option value="only"' + (state.contractFilter === 'only' ? ' selected' : '') + '>' + EPT.s('contentaudit.only', 'Contracts only') + '</option>';
        html += '</select></label>';
        html += '<label style="margin:0;display:inline-flex;align-items:center;gap:4px;font-size:0.9em">';
        html += EPT.s('contentaudit.filter_composition', 'Composition') + ':';
        html += '<select id="ca-composition-filter" class="ept-select" style="width:auto">';
        html += '<option value=""' + (state.compositionFilter === '' ? ' selected' : '') + '>' + EPT.s('contentaudit.any', 'Any') + '</option>';
        html += '<option value="section"' + (state.compositionFilter === 'section' ? ' selected' : '') + '>' + EPT.s('contentaudit.section', 'Section') + '</option>';
        html += '<option value="element"' + (state.compositionFilter === 'element' ? ' selected' : '') + '>' + EPT.s('contentaudit.element', 'Element') + '</option>';
        html += '</select></label>';
        html += '</fieldset>';
        html += '</div>';

        // Active filters display
        if (state.filters.length > 0) {
            html += '<div class="ept-toolbar" style="padding-top:0;gap:6px;flex-wrap:wrap">';
            for (var f = 0; f < state.filters.length; f++) {
                var filter = state.filters[f];
                var colLabel = getColumnLabel(filter.column);
                var opLabel = filter.operator;
                var showVal = filter.operator !== 'isEmpty' && filter.operator !== 'isNotEmpty';
                html += '<span class="ept-badge ept-badge--primary" style="cursor:pointer" data-remove-filter="' + f + '">';
                html += escapeHtml(colLabel) + ' ' + escapeHtml(opLabel);
                if (showVal) html += ' "' + escapeHtml(filter.value) + '"';
                html += ' &times;</span>';
            }
            html += '<button class="ept-btn ept-btn--sm" id="ca-clear-filters">' + EPT.s('contentaudit.btn_clearall', 'Clear all') + '</button>';
            html += '</div>';
        }

        return html;
    }

    function renderTable() {
        var d = state.data;
        if (!d || !d.items) return '<div class="ept-empty"><p>' + EPT.s('contentaudit.empty_nodata', 'No data loaded.') + '</p></div>';
        if (d.items.length === 0) return '<div class="ept-empty"><p>' + EPT.s('contentaudit.empty_nofilter', 'No content matches the current filters.') + '</p></div>';

        var cols = getVisibleColumnDefs();
        var html = '<div style="overflow-x:auto"><table class="ept-table"><thead><tr>';

        for (var c = 0; c < cols.length; c++) {
            var col = cols[c];
            var sortClass = '';
            var sortIndicator = '';
            if (col.sortable) {
                sortClass = ' style="cursor:pointer;user-select:none"';
                if (state.sortBy === col.key) {
                    sortIndicator = state.sortDirection === 'asc' ? ' &#9650;' : ' &#9660;';
                }
            }
            var alignClass = col.align === 'right' ? ' class="num"' : '';
            html += '<th' + alignClass + sortClass + ' data-sort-col="' + col.key + '">' + escapeHtml(col.label) + sortIndicator + '</th>';
        }
        html += '</tr></thead><tbody>';

        for (var r = 0; r < d.items.length; r++) {
            var row = d.items[r];
            html += '<tr>';
            for (var c2 = 0; c2 < cols.length; c2++) {
                var col2 = cols[c2];
                html += renderCell(row, col2);
            }
            html += '</tr>';
        }

        html += '</tbody></table></div>';
        return html;
    }

    function renderCell(row, col) {
        var val = row[col.key];
        var alignClass = col.align === 'right' ? ' class="num"' : '';

        if (col.key === 'name') {
            var editLink = row.editUrl ? ' <a href="' + escapeHtml(row.editUrl) + '" target="_blank" title="' + EPT.s('contentaudit.cell_openeditmode', 'Open in edit mode') + '" style="opacity:0.5;font-size:0.85em">&#9998;</a>' : '';
            return '<td>' + escapeHtml(val) + editLink + '</td>';
        }

        if (col.key === 'status') {
            if (!val) return '<td></td>';
            return '<td><span class="ept-badge ' + getStatusBadgeClass(val) + '">' + escapeHtml(val) + '</span></td>';
        }

        if (col.key === 'url') {
            if (!val) return '<td></td>';
            return '<td><a href="' + escapeHtml(val) + '" target="_blank" title="' + escapeHtml(val) + '" style="max-width:250px;display:inline-block;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">' + escapeHtml(val) + '</a></td>';
        }

        if (col.key === 'editUrl') {
            if (!val) return '<td></td>';
            return '<td><a href="' + escapeHtml(val) + '" target="_blank">' + EPT.s('shared.open', 'Open') + '</a></td>';
        }

        if (col.key === 'hasPersonalizations') {
            if (val === true) return '<td><span class="ept-badge ept-badge--primary">' + EPT.s('shared.yes', 'Yes') + '</span></td>';
            if (val === false) return '<td>' + EPT.s('shared.no', 'No') + '</td>';
            return '<td></td>';
        }

        if (col.type === 'date') {
            return '<td>' + formatDate(val) + '</td>';
        }

        if (val === null || val === undefined) return '<td' + alignClass + '></td>';
        return '<td' + alignClass + '>' + escapeHtml(String(val)) + '</td>';
    }

    function renderPagination() {
        var d = state.data;
        if (!d || d.totalPages <= 1) return '';

        var html = '<div style="display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-top:1px solid var(--ept-border,#e2e8f0)">';
        var from = ((d.page - 1) * d.pageSize) + 1;
        var to = d.totalCount != null ? Math.min(d.page * d.pageSize, d.totalCount) : d.page * d.pageSize;
        var ofTotal = d.totalCount != null ? d.totalCount.toLocaleString() : EPT.s('shared.unknown', '—');
        html += '<span style="font-size:0.875em;color:var(--ept-text-muted,#64748b)">' +
            EPT.s('contentaudit.page_showing', 'Showing {0}-{1} of {2}')
                .replace('{0}', from)
                .replace('{1}', to)
                .replace('{2}', ofTotal) + '</span>';

        html += '<div style="display:flex;gap:4px">';

        // Previous
        html += '<button class="ept-btn ept-btn--sm" data-page="' + (d.page - 1) + '"' + (d.page <= 1 ? ' disabled' : '') + '>&laquo; ' + EPT.s('shared.prev', 'Prev') + '</button>';

        // Page numbers (show max 7)
        var startPage = Math.max(1, d.page - 3);
        var endPage = Math.min(d.totalPages, startPage + 6);
        if (endPage - startPage < 6) startPage = Math.max(1, endPage - 6);

        if (startPage > 1) {
            html += '<button class="ept-btn ept-btn--sm" data-page="1">1</button>';
            if (startPage > 2) html += '<span style="padding:0 4px;line-height:32px">...</span>';
        }

        for (var p = startPage; p <= endPage; p++) {
            var active = p === d.page ? ' ept-btn--primary' : '';
            html += '<button class="ept-btn ept-btn--sm' + active + '" data-page="' + p + '">' + p + '</button>';
        }

        if (endPage < d.totalPages) {
            if (endPage < d.totalPages - 1) html += '<span style="padding:0 4px;line-height:32px">...</span>';
            html += '<button class="ept-btn ept-btn--sm" data-page="' + d.totalPages + '">' + d.totalPages + '</button>';
        }

        // Next
        html += '<button class="ept-btn ept-btn--sm" data-page="' + (d.page + 1) + '"' + (d.page >= d.totalPages ? ' disabled' : '') + '>' + EPT.s('shared.next', 'Next') + ' &raquo;</button>';

        html += '</div></div>';
        return html;
    }

    // ---- Event Binding ----

    function bindEvents() {
        // Search
        var searchInput = document.getElementById('ca-search');
        if (searchInput) {
            searchInput.addEventListener('input', debounce(function () {
                state.search = searchInput.value;
                state.page = 1;
                fetchData();
            }, 400));
        }

        // Quick filter
        var quickFilterSelect = document.getElementById('ca-quick-filter');
        if (quickFilterSelect) {
            quickFilterSelect.addEventListener('change', function () {
                state.quickFilter = quickFilterSelect.value;
                state.page = 1;
                fetchData();
            });
        }

        // CMS 13 contract filter
        var contractFilterSelect = document.getElementById('ca-contract-filter');
        if (contractFilterSelect) {
            contractFilterSelect.addEventListener('change', function () {
                state.contractFilter = contractFilterSelect.value;
                state.page = 1;
                fetchData();
            });
        }

        // CMS 13 composition filter
        var compositionFilterSelect = document.getElementById('ca-composition-filter');
        if (compositionFilterSelect) {
            compositionFilterSelect.addEventListener('change', function () {
                state.compositionFilter = compositionFilterSelect.value;
                state.page = 1;
                fetchData();
            });
        }

        // Page size
        var pageSizeSelect = document.getElementById('ca-page-size');
        if (pageSizeSelect) {
            pageSizeSelect.addEventListener('change', function () {
                state.pageSize = parseInt(pageSizeSelect.value, 10);
                state.page = 1;
                savePrefsDebounced();
                fetchData();
            });
        }

        // Sort by clicking headers
        var sortHeaders = root.querySelectorAll('[data-sort-col]');
        for (var i = 0; i < sortHeaders.length; i++) {
            (function (th) {
                var colDef = ALL_COLUMNS.find(function (c) { return c.key === th.getAttribute('data-sort-col'); });
                if (!colDef || !colDef.sortable) return;
                th.addEventListener('click', function () {
                    var col = th.getAttribute('data-sort-col');
                    if (state.sortBy === col) {
                        state.sortDirection = state.sortDirection === 'asc' ? 'desc' : 'asc';
                    } else {
                        state.sortBy = col;
                        state.sortDirection = 'asc';
                    }
                    savePrefsDebounced();
                    fetchData();
                });
            })(sortHeaders[i]);
        }

        // Pagination
        var pageButtons = root.querySelectorAll('[data-page]');
        for (var j = 0; j < pageButtons.length; j++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    if (btn.disabled) return;
                    var pg = parseInt(btn.getAttribute('data-page'), 10);
                    if (pg >= 1 && pg <= state.data.totalPages) {
                        state.page = pg;
                        fetchData();
                    }
                });
            })(pageButtons[j]);
        }

        // Filter button
        var filterBtn = document.getElementById('ca-filter-btn');
        if (filterBtn) {
            filterBtn.addEventListener('click', openFilterDialog);
        }

        // Clear filters
        var clearFilters = document.getElementById('ca-clear-filters');
        if (clearFilters) {
            clearFilters.addEventListener('click', function () {
                state.filters = [];
                state.page = 1;
                fetchData();
            });
        }

        // Remove individual filter
        var removeBtns = root.querySelectorAll('[data-remove-filter]');
        for (var k = 0; k < removeBtns.length; k++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    var idx = parseInt(btn.getAttribute('data-remove-filter'), 10);
                    state.filters.splice(idx, 1);
                    state.page = 1;
                    fetchData();
                });
            })(removeBtns[k]);
        }

        // Column picker
        var columnsBtn = document.getElementById('ca-columns-btn');
        if (columnsBtn) {
            columnsBtn.addEventListener('click', openColumnPicker);
        }

        // Export
        var exportBtn = document.getElementById('ca-export-btn');
        var exportMenu = document.getElementById('ca-export-menu');
        if (exportBtn && exportMenu) {
            exportBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                exportMenu.style.display = exportMenu.style.display === 'none' ? 'block' : 'none';
            });
            document.addEventListener('click', function () {
                exportMenu.style.display = 'none';
            });
            var exportItems = exportMenu.querySelectorAll('[data-format]');
            for (var m = 0; m < exportItems.length; m++) {
                (function (item) {
                    item.addEventListener('click', function (e) {
                        e.preventDefault();
                        e.stopPropagation();
                        exportMenu.style.display = 'none';
                        doExport(item.getAttribute('data-format'));
                    });
                })(exportItems[m]);
            }
        }
    }

    // ---- Column Picker Dialog ----

    function openColumnPicker() {
        var dlg = EPT.openDialog(EPT.s('contentaudit.dlg_choosecolumns', 'Choose Columns'));
        var body = dlg.body;

        var html = '<div style="max-height:400px;overflow-y:auto;padding:8px 0">';
        for (var i = 0; i < ALL_COLUMNS.length; i++) {
            var col = ALL_COLUMNS[i];
            var checked = state.visibleColumns.indexOf(col.key) >= 0 ? ' checked' : '';
            html += '<label style="display:block;padding:4px 8px;cursor:pointer"><input type="checkbox" value="' +
                col.key + '"' + checked + ' style="margin-right:8px"> ' + escapeHtml(col.label) + '</label>';
        }
        html += '</div>';
        html += '<div style="display:flex;gap:8px;justify-content:flex-end;padding-top:12px;border-top:1px solid var(--ept-border,#e2e8f0)">';
        html += '<button class="ept-btn ept-btn--sm" id="ca-col-select-all">' + EPT.s('contentaudit.btn_selectall', 'Select all') + '</button>';
        html += '<button class="ept-btn ept-btn--sm" id="ca-col-reset">' + EPT.s('contentaudit.btn_reset', 'Reset') + '</button>';
        html += '<button class="ept-btn ept-btn--sm ept-btn--primary" id="ca-col-apply">' + EPT.s('shared.apply', 'Apply') + '</button>';
        html += '</div>';
        body.innerHTML = html;

        body.querySelector('#ca-col-select-all').addEventListener('click', function () {
            var cbs = body.querySelectorAll('input[type=checkbox]');
            for (var j = 0; j < cbs.length; j++) cbs[j].checked = true;
        });

        body.querySelector('#ca-col-reset').addEventListener('click', function () {
            var cbs = body.querySelectorAll('input[type=checkbox]');
            for (var j = 0; j < cbs.length; j++) {
                var col = ALL_COLUMNS.find(function (c) { return c.key === cbs[j].value; });
                cbs[j].checked = col ? col.defaultVisible : false;
            }
        });

        body.querySelector('#ca-col-apply').addEventListener('click', function () {
            var cbs = body.querySelectorAll('input[type=checkbox]:checked');
            state.visibleColumns = [];
            for (var j = 0; j < cbs.length; j++) {
                state.visibleColumns.push(cbs[j].value);
            }
            if (state.visibleColumns.length === 0) {
                state.visibleColumns = ['contentId', 'name'];
            }
            savePrefsDebounced();
            dlg.close();
            fetchData();
        });
    }

    // ---- Filter Dialog ----

    function openFilterDialog() {
        var dlg = EPT.openDialog(EPT.s('contentaudit.dlg_addfilter', 'Add Filter'));
        var body = dlg.body;

        var html = '<div style="display:flex;flex-direction:column;gap:12px">';

        // Column select
        html += '<div><label style="display:block;margin-bottom:4px;font-weight:600">' + EPT.s('contentaudit.lbl_column', 'Column') + '</label>';
        html += '<select id="ca-filter-col" class="ept-select" style="width:100%">';
        for (var i = 0; i < ALL_COLUMNS.length; i++) {
            html += '<option value="' + ALL_COLUMNS[i].key + '">' + escapeHtml(ALL_COLUMNS[i].label) + '</option>';
        }
        html += '</select></div>';

        // Operator select
        html += '<div><label style="display:block;margin-bottom:4px;font-weight:600">' + EPT.s('contentaudit.lbl_operator', 'Operator') + '</label>';
        html += '<select id="ca-filter-op" class="ept-select" style="width:100%">';
        for (var j = 0; j < FILTER_OPERATORS.length; j++) {
            html += '<option value="' + FILTER_OPERATORS[j].key + '">' + escapeHtml(FILTER_OPERATORS[j].label) + '</option>';
        }
        html += '</select></div>';

        // Value
        html += '<div id="ca-filter-value-wrap"><label style="display:block;margin-bottom:4px;font-weight:600">' + EPT.s('contentaudit.lbl_value', 'Value') + '</label>';
        html += '<input type="text" id="ca-filter-value" class="ept-input" style="width:100%" placeholder="' + EPT.s('contentaudit.lbl_filtervalue', 'Filter value...') + '">';
        html += '</div>';

        html += '<div style="display:flex;gap:8px;justify-content:flex-end">';
        html += '<button class="ept-btn ept-btn--sm" id="ca-filter-cancel">' + EPT.s('shared.cancel', 'Cancel') + '</button>';
        html += '<button class="ept-btn ept-btn--sm ept-btn--primary" id="ca-filter-add">' + EPT.s('contentaudit.btn_addfilter', 'Add filter') + '</button>';
        html += '</div></div>';

        body.innerHTML = html;

        // Toggle value field based on operator
        var opSelect = body.querySelector('#ca-filter-op');
        var valueWrap = body.querySelector('#ca-filter-value-wrap');
        opSelect.addEventListener('change', function () {
            valueWrap.style.display = (opSelect.value === 'isEmpty' || opSelect.value === 'isNotEmpty') ? 'none' : '';
        });

        body.querySelector('#ca-filter-cancel').addEventListener('click', function () { dlg.close(); });
        body.querySelector('#ca-filter-add').addEventListener('click', function () {
            var col = body.querySelector('#ca-filter-col').value;
            var op = body.querySelector('#ca-filter-op').value;
            var val = body.querySelector('#ca-filter-value').value;

            state.filters.push({ column: col, operator: op, value: val });
            state.page = 1;
            dlg.close();
            fetchData();
        });
    }

    // ---- Export ----

    function doExport(format) {
        var params = [
            'format=' + encodeURIComponent(format),
            'columns=' + encodeURIComponent(state.visibleColumns.join(','))
        ];
        if (state.search) params.push('search=' + encodeURIComponent(state.search));
        if (state.quickFilter) params.push('quickFilter=' + encodeURIComponent(state.quickFilter));
        if (state.contractFilter) params.push('contractFilter=' + encodeURIComponent(state.contractFilter));
        if (state.compositionFilter) params.push('compositionFilter=' + encodeURIComponent(state.compositionFilter));
        if (state.sortBy) params.push('sortBy=' + encodeURIComponent(state.sortBy));
        if (state.sortDirection) params.push('sortDirection=' + encodeURIComponent(state.sortDirection));
        if (state.filters.length > 0) {
            params.push('filters=' + encodeURIComponent(JSON.stringify(state.filters)));
        }

        window.location.href = API + '/Export?' + params.join('&');
    }

    // ---- Helpers ----

    function getVisibleColumnDefs() {
        var result = [];
        for (var i = 0; i < state.visibleColumns.length; i++) {
            var col = ALL_COLUMNS.find(function (c) { return c.key === state.visibleColumns[i]; });
            if (col) result.push(col);
        }
        return result;
    }

    function getColumnLabel(key) {
        var col = ALL_COLUMNS.find(function (c) { return c.key === key; });
        return col ? col.label : key;
    }

    function getDefaultVisibleColumns() {
        var cols = [];
        for (var i = 0; i < ALL_COLUMNS.length; i++) {
            if (ALL_COLUMNS[i].defaultVisible) cols.push(ALL_COLUMNS[i].key);
        }
        return cols;
    }

    // ---- Init ----

    function init() {
        root = document.getElementById('content-audit-root');
        if (!root) return;

        // Set default visible columns
        state.visibleColumns = getDefaultVisibleColumns();

        EPT.showLoading(root);

        loadPreferences().then(function () {
            fetchData();
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
