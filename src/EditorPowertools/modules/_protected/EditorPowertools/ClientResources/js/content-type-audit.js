/**
 * Content Type Audit - Main UI
 */
(function () {
    const API = '/editorpowertools/api';
    let allTypes = [];
    let tableInstance = null;
    let currentView = 'table';
    let jobStatus = null;

    // Filters
    let showSystem = false;
    let baseFilter = '';
    let searchQuery = '';

    async function init() {
        EPT.showLoading(document.getElementById('audit-content'));
        try {
            const [types, status] = await Promise.all([
                EPT.fetchJson(`${API}/content-types`),
                EPT.fetchJson(`${API}/aggregation-status`).catch(() => null)
            ]);
            allTypes = types;
            jobStatus = status;
            renderJobAlert();
            renderToolbar();
            renderStats();
            renderTable();
        } catch (err) {
            document.getElementById('audit-content').innerHTML =
                `<div class="ept-empty"><p>Error loading content types: ${err.message}</p></div>`;
        }
    }

    // ── Job Status Alert ───────────────────────────────────────────
    function renderJobAlert() {
        let existing = document.getElementById('audit-job-alert');
        if (existing) existing.remove();

        if (!jobStatus) return;

        const el = document.createElement('div');
        el.id = 'audit-job-alert';

        const runBtn = `<button class="ept-btn ept-btn--sm" id="ept-run-job-btn" style="margin-left:8px">Run now</button>`;

        if (jobStatus.isRunning) {
            el.className = 'ept-alert ept-alert--info';
            el.innerHTML = `⏳ <strong>Aggregation job is currently running.</strong> Content counts will be updated when it completes. <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">Refresh</button>`;
        } else if (!jobStatus.hasRun) {
            el.className = 'ept-alert ept-alert--warning';
            el.innerHTML = `<strong>Content statistics have not been collected yet.</strong> The "Content" column will show data after the aggregation job has been run. ${runBtn}`;
        } else {
            const ago = timeAgo(new Date(jobStatus.lastRunUtc));
            const isOld = (Date.now() - new Date(jobStatus.lastRunUtc).getTime()) > 24 * 60 * 60 * 1000;
            if (isOld) {
                el.className = 'ept-alert ept-alert--warning';
                el.innerHTML = `Statistics were last updated <strong>${ago}</strong>. Consider running the aggregation job for fresh data. ${runBtn}`;
            } else {
                return;
            }
        }

        const container = document.getElementById('audit-stats');
        container.parentNode.insertBefore(el, container);

        // Wire up "Run now" button
        const btn = document.getElementById('ept-run-job-btn');
        if (btn) {
            btn.addEventListener('click', async () => {
                btn.disabled = true;
                btn.textContent = 'Starting...';
                try {
                    await EPT.postJson(`${API}/aggregation-start`);
                    el.className = 'ept-alert ept-alert--info';
                    el.innerHTML = `⏳ <strong>Aggregation job has been started.</strong> Content counts will be updated when it completes. <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">Refresh</button>`;
                } catch (err) {
                    btn.textContent = 'Failed';
                    console.error('Failed to start job:', err);
                }
            });
        }
    }

    function timeAgo(date) {
        const diff = Date.now() - date.getTime();
        const mins = Math.floor(diff / 60000);
        if (mins < 60) return `${mins} minute${mins !== 1 ? 's' : ''} ago`;
        const hours = Math.floor(mins / 60);
        if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
        const days = Math.floor(hours / 24);
        return `${days} day${days !== 1 ? 's' : ''} ago`;
    }

    function getFiltered() {
        return allTypes.filter(t => {
            if (!showSystem && t.isSystemType) return false;
            if (baseFilter && t.base !== baseFilter) return false;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                if (q.startsWith('property:')) return false;
                return (t.name?.toLowerCase().includes(q) ||
                    t.displayName?.toLowerCase().includes(q) ||
                    t.description?.toLowerCase().includes(q) ||
                    t.groupName?.toLowerCase().includes(q) ||
                    t.modelType?.toLowerCase().includes(q) ||
                    String(t.id) === q);
            }
            return true;
        });
    }

    function renderStats() {
        const filtered = getFiltered();
        const total = allTypes.filter(t => !t.isSystemType).length;
        const system = allTypes.filter(t => t.isSystemType).length;
        const orphaned = allTypes.filter(t => t.isOrphaned).length;
        const hasStats = allTypes.some(t => t.statisticsUpdated != null);
        const unused = hasStats ? filtered.filter(t => t.contentCount === 0).length : null;

        const el = document.getElementById('audit-stats');
        el.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">Content Types</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${system}</div><div class="ept-stat__label">System Types</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${orphaned}</div><div class="ept-stat__label">Orphaned</div></div>
            ${unused != null ? `<div class="ept-stat"><div class="ept-stat__value">${unused}</div><div class="ept-stat__label">Unused (0 content)</div></div>` : ''}
            <div class="ept-stat"><div class="ept-stat__value">${filtered.length}</div><div class="ept-stat__label">Showing</div></div>
        `;
    }

    function renderToolbar() {
        const bases = [...new Set(allTypes.map(t => t.base))].sort();

        const toolbar = document.getElementById('audit-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="audit-search" placeholder="Search types... (or property:name)" />
            </div>
            <select id="audit-base-filter" class="ept-select">
                <option value="">All base types</option>
                ${bases.map(b => `<option value="${b}">${b} (${allTypes.filter(t => t.base === b).length})</option>`).join('')}
            </select>
            <label class="ept-toggle">
                <input type="checkbox" id="audit-show-system" ${showSystem ? 'checked' : ''} />
                Show system types
            </label>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="audit-view-table" title="Table view">${EPT.icons.list} Table</button>
            <button class="ept-btn" id="audit-view-tree" title="Inheritance tree">${EPT.icons.tree} Tree</button>
            <button class="ept-btn" id="audit-export" title="Export to CSV">${EPT.icons.download} Export</button>
        `;

        document.getElementById('audit-search').addEventListener('input', (e) => {
            searchQuery = e.target.value;
            renderStats();
            if (currentView === 'table') renderTable();
        });

        document.getElementById('audit-base-filter').addEventListener('change', (e) => {
            baseFilter = e.target.value;
            renderStats();
            if (currentView === 'table') renderTable();
        });

        document.getElementById('audit-show-system').addEventListener('change', (e) => {
            showSystem = e.target.checked;
            renderStats();
            if (currentView === 'table') renderTable();
        });

        document.getElementById('audit-view-table').addEventListener('click', () => {
            currentView = 'table';
            document.getElementById('audit-view-table').classList.add('ept-btn--primary');
            document.getElementById('audit-view-tree').classList.remove('ept-btn--primary');
            renderTable();
        });

        document.getElementById('audit-view-tree').addEventListener('click', () => {
            currentView = 'tree';
            document.getElementById('audit-view-tree').classList.add('ept-btn--primary');
            document.getElementById('audit-view-table').classList.remove('ept-btn--primary');
            renderTree();
        });

        document.getElementById('audit-export').addEventListener('click', exportCsv);
        document.getElementById('audit-view-table').classList.add('ept-btn--primary');
    }

    function renderTable() {
        const data = getFiltered();

        // Collect distinct values for filterable columns
        const distinctBases = [...new Set(data.map(r => r.base).filter(Boolean))].sort();
        const distinctGroups = [...new Set(data.map(r => r.groupName).filter(Boolean))].sort();

        const columns = [
            { key: 'iconUrl', label: '', sortable: false, render: (r) => r.iconUrl ? `<img src="${escHtml(r.iconUrl)}" alt="" style="height:36px;width:36px;object-fit:contain;">` : '' },
            { key: 'name', label: 'Name', render: (r) => renderTypeName(r) },
            { key: 'base', label: 'Base', filterable: distinctBases },
            { key: 'groupName', label: 'Group', filterable: distinctGroups },
            { key: 'propertyCount', label: 'Properties', align: 'right' },
            { key: 'contentCount', label: 'Content', align: 'right', render: (r) => renderCount(r.contentCount) },
            { key: 'actions', label: '', sortable: false, render: (r) => renderActions(r) }
        ];

        // Column filters state
        const colFilters = {};

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'name',
            rowClass: (r) => {
                if (r.isSystemType) return 'ept-row--system';
                if (r.isOrphaned) return 'ept-row--orphaned';
                return '';
            }
        });

        const content = document.getElementById('audit-content');
        content.innerHTML = '';
        const card = document.createElement('div');
        card.className = 'ept-card';

        // Add filter row below header
        const thead = tableInstance.table.querySelector('thead');
        const filterRow = document.createElement('tr');
        filterRow.className = 'ept-filter-row';
        columns.forEach(col => {
            const td = document.createElement('td');
            td.style.padding = '2px 4px';
            td.style.background = 'var(--ept-bg, #f5f6f8)';
            if (col.filterable) {
                const sel = document.createElement('select');
                sel.className = 'ept-select';
                sel.style.cssText = 'width:100%;font-size:11px;padding:2px 20px 2px 4px';
                sel.innerHTML = `<option value="">All</option>${col.filterable.map(v => `<option value="${v}">${v}</option>`).join('')}`;
                sel.addEventListener('change', () => {
                    colFilters[col.key] = sel.value || null;
                    applyColumnFilters();
                });
                td.appendChild(sel);
            }
            filterRow.appendChild(td);
        });
        thead.appendChild(filterRow);

        function applyColumnFilters() {
            const activeFilters = Object.entries(colFilters).filter(([, v]) => v);
            if (activeFilters.length === 0) {
                tableInstance.render(null);
            } else {
                tableInstance.render(row => activeFilters.every(([key, val]) => row[key] === val));
            }
        }

        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        body.appendChild(tableInstance.table);
        card.appendChild(body);
        content.appendChild(card);
    }

    function renderTypeName(r) {
        const name = r.displayName || r.name;
        const badges = [];
        if (r.isSystemType) badges.push('<span class="ept-badge ept-badge--default">System</span>');
        else if (r.isOrphaned) badges.push('<span class="ept-badge ept-badge--danger">Orphaned</span>');
        return `<div>
            <strong>${escHtml(name)}</strong>
            ${r.displayName && r.displayName !== r.name ? `<span class="ept-muted"> (${escHtml(r.name)})</span>` : ''}
            ${badges.join(' ')}
            ${r.description ? `<div class="ept-muted" style="font-size:11px;margin-top:2px">${escHtml(r.description)}</div>` : ''}
        </div>`;
    }

    function renderCount(val) {
        if (val == null) return '<span class="ept-muted">-</span>';
        if (val === 0) return '<span class="ept-badge ept-badge--warning">0</span>';
        return String(val);
    }

    function renderActions(r) {
        const div = document.createElement('div');
        div.className = 'ept-flex';

        if (r.editUrl) {
            const editBtn = createIconBtn(EPT.icons.edit, 'Edit content type', () => window.open(r.editUrl, '_blank'));
            div.appendChild(editBtn);
        }

        const propsBtn = createIconBtn(EPT.icons.props, 'Properties', () => showProperties(r));
        div.appendChild(propsBtn);

        const contentBtn = createIconBtn(EPT.icons.list, 'Content of this type', () => showContentOfType(r));
        div.appendChild(contentBtn);

        return div;
    }

    function createIconBtn(iconSvg, title, onClick) {
        const btn = document.createElement('button');
        btn.className = 'ept-btn ept-btn--sm ept-btn--icon';
        btn.title = title;
        btn.innerHTML = iconSvg;
        btn.addEventListener('click', (e) => { e.stopPropagation(); onClick(); });
        return btn;
    }

    // ── Properties Dialog ──────────────────────────────────────────
    async function showProperties(type) {
        const { body, close } = EPT.openDialog(`Properties: ${type.displayName || type.name}`, { wide: true, flush: true });
        EPT.showLoading(body);

        try {
            const props = await EPT.fetchJson(`${API}/content-types/${type.id}/properties`);
            body.innerHTML = ''; // Clear loading spinner

            if (props.length === 0) {
                EPT.showEmpty(body, 'No properties defined');
                return;
            }

            const legend = document.createElement('div');
            legend.style.padding = '12px 16px';
            legend.innerHTML = `
                <span class="ept-badge ept-badge--success">Inherited</span>
                <span class="ept-badge ept-badge--default">Defined</span>
                <span class="ept-badge ept-badge--danger">Orphaned (not in code)</span>
            `;
            body.appendChild(legend);

            const columns = [
                { key: 'name', label: 'Name', render: (r) => `<strong>${escHtml(r.name)}</strong>` },
                { key: 'typeName', label: 'Type' },
                { key: 'tabName', label: 'Tab' },
                { key: 'sortOrder', label: 'Order', align: 'right' },
                { key: 'required', label: 'Required', render: (r) => r.required ? '✓' : '' },
                { key: 'searchable', label: 'Searchable', render: (r) => r.searchable ? '✓' : '' },
                { key: 'languageSpecific', label: 'Language', render: (r) => r.languageSpecific ? '✓' : '' },
                {
                    key: 'origin', label: 'Origin', render: (r) => {
                        const label = r.origin === 0 ? 'Defined' : r.origin === 1 ? 'Inherited' : 'Orphaned';
                        const cls = r.origin === 0 ? 'default' : r.origin === 1 ? 'success' : 'danger';
                        return `<span class="ept-badge ept-badge--${cls}">${label}</span>`;
                    }
                }
            ];

            const { table } = EPT.createTable(columns, props, {
                defaultSort: 'sortOrder',
                rowClass: (r) => {
                    if (r.origin === 2) return 'ept-row--orphaned';
                    if (r.origin === 1) return 'ept-row--inherited';
                    return '';
                }
            });
            body.appendChild(table);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── Content of Type Dialog ─────────────────────────────────────
    async function showContentOfType(type) {
        const { body, close } = EPT.openDialog(`Content: ${type.displayName || type.name}`, { wide: true, flush: true });
        EPT.showLoading(body);

        try {
            const items = await EPT.fetchJson(`${API}/content-types/${type.id}/content`);
            body.innerHTML = ''; // Clear loading spinner

            if (items.length === 0) {
                EPT.showEmpty(body, 'No content of this type');
                return;
            }

            // Language filter
            const languages = [...new Set(items.map(i => i.language).filter(Boolean))].sort();
            if (languages.length > 1) {
                const filterBar = document.createElement('div');
                filterBar.style.padding = '8px 16px';
                filterBar.className = 'ept-flex';
                filterBar.innerHTML = `<label class="ept-muted" style="font-size:12px">Language:</label>
                    <select class="ept-select ept-content-lang-filter">
                        <option value="">All (${items.length})</option>
                        ${languages.map(l => `<option value="${l}">${l} (${items.filter(i => i.language === l).length})</option>`).join('')}
                    </select>`;
                body.appendChild(filterBar);

                filterBar.querySelector('select').addEventListener('change', (e) => {
                    const lang = e.target.value;
                    tbl.render(lang ? (r => r.language === lang) : null);
                });
            }

            const columns = [
                { key: 'contentId', label: 'ID', align: 'right' },
                {
                    key: 'name', label: 'Name', render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.name)}</strong> <span style="opacity:.4">↗</span></a>`;
                        return `<strong>${escHtml(r.name)}</strong>`;
                    }
                },
                { key: 'language', label: 'Lang' },
                {
                    key: 'breadcrumb', label: 'Location', render: (r) =>
                        `<span class="ept-truncate" title="${escAttr(r.breadcrumb)}">${escHtml(r.breadcrumb || '')}</span>`
                },
                {
                    key: 'isPublished', label: 'Status', render: (r) =>
                        r.isPublished
                            ? '<span class="ept-badge ept-badge--success">Published</span>'
                            : '<span class="ept-badge ept-badge--default">Draft</span>'
                },
                {
                    key: 'referenceCount', label: 'References', align: 'right', render: (r) => {
                        if (r.referenceCount === 0) return '<span class="ept-badge ept-badge--warning">0</span>';
                        const btn = createIconBtn(
                            `${r.referenceCount} ${EPT.icons.link}`,
                            'Show references',
                            () => showReferences(r)
                        );
                        return btn;
                    }
                }
            ];

            const tbl = EPT.createTable(columns, items, { defaultSort: 'name' });
            body.appendChild(tbl.table);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── References Dialog ──────────────────────────────────────────
    async function showReferences(content) {
        const { body, close } = EPT.openDialog(`References to: ${content.name}`, { flush: true });
        EPT.showLoading(body);

        try {
            const refs = await EPT.fetchJson(`${API}/content/${content.contentId}/references`);
            body.innerHTML = ''; // Clear loading spinner

            if (refs.length === 0) {
                EPT.showEmpty(body, 'No references found');
                return;
            }

            // Language filter
            const languages = [...new Set(refs.map(r => r.language).filter(Boolean))].sort();
            if (languages.length > 1) {
                const filterBar = document.createElement('div');
                filterBar.style.padding = '8px 16px';
                filterBar.className = 'ept-flex';
                filterBar.innerHTML = `<label class="ept-muted" style="font-size:12px">Language:</label>
                    <select class="ept-select ept-refs-lang-filter">
                        <option value="">All (${refs.length})</option>
                        ${languages.map(l => `<option value="${l}">${l} (${refs.filter(r => r.language === l).length})</option>`).join('')}
                    </select>`;
                body.appendChild(filterBar);

                filterBar.querySelector('select').addEventListener('change', (e) => {
                    const lang = e.target.value;
                    tbl.render(lang ? (r => r.language === lang) : null);
                });
            }

            const columns = [
                { key: 'ownerContentId', label: 'ID', align: 'right' },
                {
                    key: 'ownerName', label: 'Referenced By', render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.ownerName)}</strong> <span style="opacity:.4">↗</span></a>`;
                        return `<strong>${escHtml(r.ownerName)}</strong>`;
                    }
                },
                { key: 'ownerTypeName', label: 'Type' },
                { key: 'language', label: 'Lang' },
                { key: 'propertyName', label: 'Property' },
            ];

            const tbl = EPT.createTable(columns, refs, { defaultSort: 'ownerName' });
            body.appendChild(tbl.table);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── Inheritance Tree View ──────────────────────────────────────
    async function renderTree() {
        const content = document.getElementById('audit-content');
        content.innerHTML = '<div class="ept-card"><div class="ept-card__body"></div></div>';
        const body = content.querySelector('.ept-card__body');
        EPT.showLoading(body);

        try {
            const tree = await EPT.fetchJson(`${API}/content-types/inheritance-tree`);
            body.innerHTML = '';

            if (tree.length === 0) {
                EPT.showEmpty(body, 'No inheritance data available');
                return;
            }

            const ul = buildTreeUl(tree);
            body.appendChild(ul);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    function buildTreeUl(nodes) {
        const ul = document.createElement('ul');
        ul.className = 'ept-tree';

        nodes.forEach(node => {
            const li = document.createElement('li');
            li.className = 'ept-tree__item';

            const hasChildren = node.children && node.children.length > 0;
            let childUl = null;
            let expanded = true;

            const line = document.createElement('span');
            line.className = 'ept-flex';

            if (hasChildren) {
                const toggle = document.createElement('button');
                toggle.className = 'ept-tree__toggle';
                toggle.innerHTML = EPT.icons.chevronDown;
                toggle.addEventListener('click', () => {
                    expanded = !expanded;
                    toggle.innerHTML = expanded ? EPT.icons.chevronDown : EPT.icons.chevronRight;
                    if (childUl) childUl.style.display = expanded ? '' : 'none';
                });
                line.appendChild(toggle);
            } else {
                const spacer = document.createElement('span');
                spacer.style.cssText = 'width:24px;display:inline-block';
                line.appendChild(spacer);
            }

            const label = document.createElement('span');
            label.className = 'ept-tree__label';
            label.textContent = node.displayName || node.name;
            if (node.isOrphaned) {
                label.innerHTML += ' <span class="ept-badge ept-badge--danger">Orphaned</span>';
            }
            label.title = 'Click to view content of this type';
            label.addEventListener('click', () => {
                showContentOfType({ id: node.id, displayName: node.displayName, name: node.name });
            });
            line.appendChild(label);

            if (node.contentCount != null) {
                const count = document.createElement('span');
                count.className = 'ept-tree__count';
                count.textContent = `(${node.contentCount})`;
                line.appendChild(count);
            }

            // Edit content type link
            const editBtn = document.createElement('a');
            editBtn.className = 'ept-btn ept-btn--sm ept-btn--icon';
            editBtn.title = 'Edit content type';
            editBtn.href = `${window.EPT_ADMIN_URL}#/ContentType/${node.id}`;
            editBtn.target = '_blank';
            editBtn.innerHTML = EPT.icons.edit;
            editBtn.style.marginLeft = '4px';
            line.appendChild(editBtn);

            li.appendChild(line);

            if (hasChildren) {
                childUl = buildTreeUl(node.children);
                li.appendChild(childUl);
            }

            ul.appendChild(li);
        });

        return ul;
    }

    // ── CSV Export ──────────────────────────────────────────────────
    function exportCsv() {
        const data = getFiltered();
        const columns = [
            { key: 'name', label: 'Name' },
            { key: 'displayName', label: 'Display Name' },
            { key: 'base', label: 'Base' },
            { key: 'groupName', label: 'Group' },
            { key: 'propertyCount', label: 'Properties' },
            { key: 'contentCount', label: 'Content Count' },
            { key: 'publishedCount', label: 'Published' },
            { key: 'referencedCount', label: 'Referenced' },
            { key: 'unreferencedCount', label: 'Unreferenced' },
            { key: 'isOrphaned', label: 'Orphaned' },
            { key: 'isSystemType', label: 'System Type' },
            { key: 'modelType', label: 'Model Type' },
            { key: 'description', label: 'Description' },
            { key: 'id', label: 'ID' },
            { key: 'guid', label: 'GUID' },
        ];
        const ts = new Date().toISOString().slice(0, 19).replace(/[T:]/g, '-');
        EPT.downloadCsv(`ContentTypeAudit_${ts}.csv`, columns, data);
    }

    // ── Helpers ────────────────────────────────────────────────────
    function escHtml(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function escAttr(s) {
        if (!s) return '';
        return s.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // Boot
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
