/**
 * Editor Powertools - Shared UI utilities
 */

// Derive module base URL for widgets running outside EPT pages (no layout).
// require.toUrl('editorpowertools/') resolves to e.g. /_protected/EditorPowertools/ClientResources/js/
// Strip 'ClientResources/js/' to get the module root.
if (!window.EPT_BASE_URL && typeof require !== 'undefined' && typeof require.toUrl === 'function') {
    try {
        window.EPT_BASE_URL = require.toUrl('editorpowertools/').replace(/ClientResources\/js\/?$/, '');
    } catch(e) {}
}

const EPT = {
    /**
     * Fetch JSON from an API endpoint with error handling.
     */
    async fetchJson(url) {
        const resp = await fetch(url);
        if (!resp.ok) throw new Error(`HTTP ${resp.status}: ${resp.statusText}`);
        return resp.json();
    },

    async postJson(url, body) {
        const resp = await fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: body ? JSON.stringify(body) : undefined
        });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}: ${resp.statusText}`);
        return resp.json();
    },

    /**
     * Show a loading indicator inside an element.
     */
    showLoading(el) {
        var msg = (window.EPT_STRINGS && window.EPT_STRINGS.editorpowertools && window.EPT_STRINGS.editorpowertools.loading)
            ? window.EPT_STRINGS.editorpowertools.loading : 'Loading...';
        el.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>' + msg + '</p></div>';
    },

    /**
     * Show an empty state inside an element.
     */
    showEmpty(el, message) {
        el.innerHTML = `<div class="ept-empty">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M9 12h6m-3-3v6m-7 4h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
            <p>${message}</p>
        </div>`;
    },

    /**
     * Open a modal dialog. Returns the dialog body element.
     * Call the returned close() function to dismiss.
     */
    openDialog(title, opts = {}) {
        const container = document.getElementById('ept-dialog-container');
        const wide = opts.wide ? ' ept-dialog--wide' : '';
        const flush = opts.flush ? ' ept-dialog__body--flush' : '';

        const backdrop = document.createElement('div');
        backdrop.className = 'ept-dialog-backdrop';
        backdrop.innerHTML = `
            <div class="ept-dialog${wide}">
                <div class="ept-dialog__header">
                    <span class="ept-dialog__title">${title}</span>
                    <button class="ept-dialog__close" title="Close">
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12"/></svg>
                    </button>
                </div>
                <div class="ept-dialog__body${flush}"></div>
            </div>`;

        container.appendChild(backdrop);

        const body = backdrop.querySelector('.ept-dialog__body');
        const close = () => backdrop.remove();

        backdrop.querySelector('.ept-dialog__close').addEventListener('click', close);
        backdrop.addEventListener('click', (e) => {
            if (e.target === backdrop) close();
        });

        // ESC key
        const onKey = (e) => { if (e.key === 'Escape') { close(); document.removeEventListener('keydown', onKey); } };
        document.addEventListener('keydown', onKey);

        return { body, close };
    },

    /**
     * Create a sortable, searchable table.
     * columns: [{ key, label, sortable, align, render }]
     * data: array of row objects
     */
    createTable(columns, data, opts = {}) {
        const table = document.createElement('table');
        table.className = 'ept-table';

        // State
        let sortKey = opts.defaultSort || null;
        let sortDir = opts.defaultSortDir || 'asc';
        let filtered = [...data];

        const thead = document.createElement('thead');
        const headerRow = document.createElement('tr');
        columns.forEach(col => {
            const th = document.createElement('th');
            th.textContent = col.label;
            if (col.align === 'right') th.classList.add('num');
            if (col.sortable !== false) {
                th.addEventListener('click', () => {
                    if (sortKey === col.key) {
                        sortDir = sortDir === 'asc' ? 'desc' : 'asc';
                    } else {
                        sortKey = col.key;
                        sortDir = 'asc';
                    }
                    render();
                });
            }
            headerRow.appendChild(th);
        });
        thead.appendChild(headerRow);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        table.appendChild(tbody);

        let lastFilterFn = null;

        function render(filterFn) {
            if (filterFn !== undefined) lastFilterFn = filterFn;
            // Sort indicators
            headerRow.querySelectorAll('th').forEach((th, i) => {
                th.removeAttribute('data-sort-dir');
                if (columns[i].key === sortKey) th.setAttribute('data-sort-dir', sortDir);
            });

            let rows = lastFilterFn ? data.filter(lastFilterFn) : [...data];

            if (sortKey) {
                rows.sort((a, b) => {
                    let va = a[sortKey], vb = b[sortKey];
                    if (va == null) va = '';
                    if (vb == null) vb = '';
                    if (typeof va === 'number' && typeof vb === 'number') return sortDir === 'asc' ? va - vb : vb - va;
                    va = String(va).toLowerCase();
                    vb = String(vb).toLowerCase();
                    return sortDir === 'asc' ? va.localeCompare(vb) : vb.localeCompare(va);
                });
            }

            tbody.innerHTML = '';
            if (rows.length === 0) {
                const tr = document.createElement('tr');
                var noResultsMsg = (window.EPT_STRINGS && window.EPT_STRINGS.editorpowertools && window.EPT_STRINGS.editorpowertools.noresults)
                    ? window.EPT_STRINGS.editorpowertools.noresults : 'No results found';
                tr.innerHTML = '<td colspan="' + columns.length + '" class="ept-empty"><p>' + noResultsMsg + '</p></td>';
                tbody.appendChild(tr);
                return;
            }

            rows.forEach(row => {
                const tr = document.createElement('tr');
                if (opts.rowClass) {
                    const cls = opts.rowClass(row);
                    if (cls) tr.className = cls;
                }
                columns.forEach(col => {
                    const td = document.createElement('td');
                    if (col.align === 'right') td.classList.add('num');
                    if (col.render) {
                        const content = col.render(row);
                        if (typeof content === 'string') td.innerHTML = content;
                        else if (content instanceof Node) td.appendChild(content);
                    } else {
                        td.textContent = row[col.key] ?? '';
                    }
                    tr.appendChild(td);
                });
                if (opts.onRowClick) {
                    tr.style.cursor = 'pointer';
                    tr.addEventListener('click', () => opts.onRowClick(row));
                }
                tbody.appendChild(tr);
            });
        }

        render();
        return { table, render, getData: () => data };
    },

    /**
     * Download data as CSV file.
     */
    downloadCsv(filename, columns, data) {
        const escape = (v) => {
            if (v == null) return '';
            const s = String(v);
            return s.includes(',') || s.includes('"') || s.includes('\n')
                ? '"' + s.replace(/"/g, '""') + '"'
                : s;
        };

        const header = columns.map(c => escape(c.label)).join(',');
        const rows = data.map(row => columns.map(c => escape(row[c.key])).join(','));
        const csv = [header, ...rows].join('\r\n');

        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        a.click();
        URL.revokeObjectURL(url);
    },

    /**
     * Load user preferences for a tool. Returns parsed JSON or empty object.
     */
    async loadPreferences(toolName) {
        try {
            return await this.fetchJson(`${window.EPT_API_URL}/preferences/${encodeURIComponent(toolName)}`);
        } catch { return {}; }
    },

    /**
     * Save user preferences for a tool. Debounced - call freely on every change.
     */
    savePreferences(toolName, prefs) {
        if (this._prefTimers && this._prefTimers[toolName]) {
            clearTimeout(this._prefTimers[toolName]);
        }
        if (!this._prefTimers) this._prefTimers = {};
        this._prefTimers[toolName] = setTimeout(() => {
            fetch(`${window.EPT_API_URL}/preferences/${encodeURIComponent(toolName)}`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
                body: JSON.stringify(prefs)
            }).catch(() => {});
        }, 1000);
    },

    /** SVG icon helpers */
    icons: {
        search: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>',
        edit: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>',
        link: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>',
        list: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="8" y1="6" x2="21" y2="6"/><line x1="8" y1="12" x2="21" y2="12"/><line x1="8" y1="18" x2="21" y2="18"/><line x1="3" y1="6" x2="3.01" y2="6"/><line x1="3" y1="12" x2="3.01" y2="12"/><line x1="3" y1="18" x2="3.01" y2="18"/></svg>',
        download: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>',
        tree: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 3h7v7H3zm11 0h7v7h-7zM3 14h7v7H3z"/><path d="M14 17.5h7M14 14v7"/></svg>',
        props: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M14 3v4a1 1 0 0 0 1 1h4"/></svg>',
        chevronRight: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="9 18 15 12 9 6"/></svg>',
        chevronDown: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="6 9 12 15 18 9"/></svg>',
    },

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

    /**
     * Escape HTML special characters for safe insertion into innerHTML.
     */
    escHtml: function(str) {
        return String(str || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    },

    /**
     * Open the help drawer for the given tool key.
     * Reads the page h1 for the title and EPT_STRINGS.help.{toolKey} for the body.
     */
    openHelp: function(toolKey) {
        EPT.closeHelp();
        var h1 = document.querySelector('.ept-page-header h1');
        var title = h1 ? h1.textContent.replace('?', '').trim() : '';
        var body = EPT.s('help.' + toolKey, '');

        var drawer = document.createElement('div');
        drawer.className = 'ept-help-drawer';
        drawer.id = 'ept-help-drawer';
        drawer.innerHTML =
            '<div class="ept-help-drawer__overlay"></div>' +
            '<div class="ept-help-drawer__panel">' +
                '<div class="ept-help-drawer__header">' +
                    '<span class="ept-help-drawer__title">' + EPT.escHtml(title) + '</span>' +
                    '<button class="ept-help-drawer__close" aria-label="Close">' +
                        '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M18 6L6 18M6 6l12 12"/></svg>' +
                    '</button>' +
                '</div>' +
                '<div class="ept-help-drawer__body">' + EPT.escHtml(body) + '</div>' +
            '</div>';

        document.body.appendChild(drawer);
        requestAnimationFrame(function() { drawer.classList.add('ept-help-drawer--open'); });

        drawer.querySelector('.ept-help-drawer__close').addEventListener('click', EPT.closeHelp);
        drawer.querySelector('.ept-help-drawer__overlay').addEventListener('click', EPT.closeHelp);

        var onKey = function(e) {
            if (e.key === 'Escape') {
                EPT.closeHelp();
                document.removeEventListener('keydown', onKey);
            }
        };
        document.addEventListener('keydown', onKey);
    },

    /**
     * Close and remove the help drawer if open.
     */
    closeHelp: function() {
        var existing = document.getElementById('ept-help-drawer');
        if (existing) existing.remove();
    },
};

// Event delegation: open help drawer for any [data-ept-help] button.
document.addEventListener('click', function(e) {
    var btn = e.target.closest('[data-ept-help]');
    if (btn) EPT.openHelp(btn.getAttribute('data-ept-help'));
});

// Dojo's BorderContainer calculates a narrower applicationContainer on initial load
// due to a brief layout artifact. Watch for the wrong width and correct it.
(function() {
    var _eptLayoutFixed = false;
    function doFix() {
        if (_eptLayoutFixed) return;
        if (typeof dijit === 'undefined' || !dijit.registry) return;
        var rc = dijit.registry.byId('rootContainer');
        if (!rc || !rc.resize) return;
        _eptLayoutFixed = true;
        // Dojo caches the wrong size; clear it to force a full recompute.
        var parent = rc.domNode && rc.domNode.parentElement;
        var w = parent ? parent.clientWidth : null;
        var h = rc.domNode ? rc.domNode.clientHeight : null;
        rc._borderBox = null;
        rc._contentBox = null;
        if (w && h) rc.resize({ w: w, h: h });
        setTimeout(function() {
            rc._borderBox = null;
            rc._contentBox = null;
            if (w && h) rc.resize({ w: w, h: h });
        }, 0);
    }
    // Watch applicationContainer for when Dojo sets a narrow width, then correct it
    var observer = new MutationObserver(function(mutations) {
        var appContainer = document.getElementById('applicationContainer');
        if (!appContainer) return;
        var parent = appContainer.parentElement;
        if (!parent) return;
        var available = parent.clientWidth;
        var current = appContainer.offsetWidth;
        // If there's more than 20px unaccounted for, layout is wrong
        if (available > 0 && (available - current) > 20) {
            observer.disconnect();
            setTimeout(doFix, 0);
            setTimeout(doFix, 50);
        }
    });
    // Start observing once applicationContainer exists
    function startObserving() {
        var appContainer = document.getElementById('applicationContainer');
        if (appContainer) {
            observer.observe(appContainer, { attributes: true, attributeFilter: ['style'] });
        } else {
            setTimeout(startObserving, 50);
        }
    }
    startObserving();
    // Fallback: also try after a delay in case the mutation is missed
    setTimeout(function() {
        observer.disconnect();
        doFix();
    }, 2000);
})();

