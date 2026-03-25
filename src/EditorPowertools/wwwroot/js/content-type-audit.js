/**
 * Content Type Audit - Main UI
 */
(function () {
    const API = '/editorpowertools/api';
    let allTypes = [];
    let tableInstance = null;
    let currentView = 'table'; // 'table' or 'tree'

    // Filters
    let showSystem = false;
    let baseFilter = '';
    let searchQuery = '';

    async function init() {
        EPT.showLoading(document.getElementById('audit-content'));
        try {
            allTypes = await EPT.fetchJson(`${API}/content-types`);
            renderToolbar();
            renderStats();
            renderTable();
        } catch (err) {
            document.getElementById('audit-content').innerHTML =
                `<div class="ept-empty"><p>Error loading content types: ${err.message}</p></div>`;
        }
    }

    function getFiltered() {
        return allTypes.filter(t => {
            if (!showSystem && t.isSystemType) return false;
            if (baseFilter && t.base !== baseFilter) return false;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                // Special syntax: property:name
                if (q.startsWith('property:')) return false; // handled via API later
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
        const unused = filtered.filter(t => t.contentCount === 0).length;
        const hasStats = allTypes.some(t => t.statisticsUpdated != null);

        const el = document.getElementById('audit-stats');
        el.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">Content Types</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${system}</div><div class="ept-stat__label">System Types</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${orphaned}</div><div class="ept-stat__label">Orphaned</div></div>
            ${hasStats ? `<div class="ept-stat"><div class="ept-stat__value">${unused}</div><div class="ept-stat__label">Unused (0 content)</div></div>` : ''}
            <div class="ept-stat"><div class="ept-stat__value">${filtered.length}</div><div class="ept-stat__label">Showing</div></div>
        `;
    }

    function renderToolbar() {
        // Collect distinct base types
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
            if (currentView === 'table') updateTable();
        });

        document.getElementById('audit-base-filter').addEventListener('change', (e) => {
            baseFilter = e.target.value;
            renderStats();
            if (currentView === 'table') updateTable();
        });

        document.getElementById('audit-show-system').addEventListener('change', (e) => {
            showSystem = e.target.checked;
            renderStats();
            if (currentView === 'table') updateTable();
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

        // Set initial active view button
        document.getElementById('audit-view-table').classList.add('ept-btn--primary');
    }

    function renderTable() {
        const data = getFiltered();
        const hasStats = allTypes.some(t => t.statisticsUpdated != null);

        const columns = [
            { key: 'name', label: 'Name', render: (r) => renderTypeName(r) },
            { key: 'base', label: 'Base' },
            { key: 'groupName', label: 'Group' },
            { key: 'propertyCount', label: 'Properties', align: 'right' },
        ];

        if (hasStats) {
            columns.push(
                { key: 'contentCount', label: 'Content', align: 'right', render: (r) => renderCount(r.contentCount) },
                { key: 'referencedCount', label: 'Referenced', align: 'right', render: (r) => renderCount(r.referencedCount) },
                { key: 'unreferencedCount', label: 'Unreferenced', align: 'right', render: (r) => renderUnreferenced(r) },
            );
        }

        columns.push({ key: 'actions', label: '', sortable: false, render: (r) => renderActions(r) });

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'name',
            rowClass: (r) => {
                if (r.isOrphaned) return 'ept-row--orphaned';
                if (r.isSystemType) return 'ept-row--system';
                return '';
            }
        });

        const content = document.getElementById('audit-content');
        content.innerHTML = '';
        const card = document.createElement('div');
        card.className = 'ept-card';
        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        body.style.overflow = 'auto';
        body.style.maxHeight = 'calc(100vh - 280px)';
        body.appendChild(tableInstance.table);
        card.appendChild(body);
        content.appendChild(card);
    }

    function updateTable() {
        if (!tableInstance) return;
        // Re-render with fresh data
        renderTable();
    }

    function renderTypeName(r) {
        const name = r.displayName || r.name;
        const badges = [];
        if (r.isOrphaned) badges.push('<span class="ept-badge ept-badge--danger">Orphaned</span>');
        if (r.isSystemType) badges.push('<span class="ept-badge ept-badge--default">System</span>');
        return `<div>
            <strong>${escHtml(name)}</strong>
            ${r.displayName && r.displayName !== r.name ? `<span class="ept-muted"> (${escHtml(r.name)})</span>` : ''}
            ${badges.join(' ')}
            ${r.description ? `<div class="ept-muted" style="font-size:11px;margin-top:2px">${escHtml(r.description)}</div>` : ''}
        </div>`;
    }

    function renderCount(val) {
        if (val == null) return '<span class="ept-muted">-</span>';
        return String(val);
    }

    function renderUnreferenced(r) {
        if (r.unreferencedCount == null) return '<span class="ept-muted">-</span>';
        if (r.unreferencedCount > 0 && r.contentCount > 0) {
            return `<span class="ept-badge ept-badge--warning">${r.unreferencedCount}</span>`;
        }
        return String(r.unreferencedCount);
    }

    function renderActions(r) {
        const div = document.createElement('div');
        div.className = 'ept-flex';

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
            if (props.length === 0) {
                EPT.showEmpty(body, 'No properties defined');
                return;
            }

            // Legend
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
                        const map = { Defined: 'default', Inherited: 'success', Orphaned: 'danger' };
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
            if (items.length === 0) {
                EPT.showEmpty(body, 'No content of this type');
                return;
            }

            const columns = [
                { key: 'contentId', label: 'ID', align: 'right' },
                { key: 'name', label: 'Name', render: (r) => `<strong>${escHtml(r.name)}</strong>` },
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

            const { table } = EPT.createTable(columns, items, { defaultSort: 'name' });
            body.appendChild(table);
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
            if (refs.length === 0) {
                EPT.showEmpty(body, 'No references found');
                return;
            }

            const columns = [
                { key: 'ownerContentId', label: 'ID', align: 'right' },
                { key: 'ownerName', label: 'Referenced By', render: (r) => `<strong>${escHtml(r.ownerName)}</strong>` },
                { key: 'ownerTypeName', label: 'Type' },
                { key: 'language', label: 'Lang' },
                { key: 'propertyName', label: 'Property' },
            ];

            const { table } = EPT.createTable(columns, refs, { defaultSort: 'ownerName' });
            body.appendChild(table);
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
                spacer.style.width = '24px';
                spacer.style.display = 'inline-block';
                line.appendChild(spacer);
            }

            const label = document.createElement('span');
            label.className = 'ept-tree__label';
            label.textContent = node.displayName || node.name;
            if (node.isOrphaned) {
                label.innerHTML += ' <span class="ept-badge ept-badge--danger">Orphaned</span>';
            }
            line.appendChild(label);

            if (node.contentCount != null) {
                const count = document.createElement('span');
                count.className = 'ept-tree__count';
                count.textContent = `(${node.contentCount})`;
                line.appendChild(count);
            }

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
        const div = document.createElement('div');
        div.textContent = s;
        return div.innerHTML;
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
