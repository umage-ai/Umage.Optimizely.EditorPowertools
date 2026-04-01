/**
 * Audience Manager - Main UI
 */
(function () {
    const API = window.EPT_API_URL + '/audience';
    const EPT_BASE_URL = '/editorpowertools/';
    let allGroups = [];
    let tableInstance = null;

    // Filters
    let searchQuery = '';
    let categoryFilter = '';
    let statsOnlyFilter = false;

    async function init() {
        EPT.showLoading(document.getElementById('audience-content'));
        try {
            const groups = await EPT.fetchJson(`${API}/visitor-groups`);
            allGroups = groups;
            renderStats();
            renderToolbar();
            renderTable();
        } catch (err) {
            document.getElementById('audience-content').innerHTML =
                `<div class="ept-empty"><p>Error loading audiences: ${err.message}</p></div>`;
        }
    }

    function getFiltered() {
        return allGroups.filter(g => {
            if (categoryFilter && g.category !== categoryFilter) return false;
            if (statsOnlyFilter && !g.statisticsEnabled) return false;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                return (g.name?.toLowerCase().includes(q) ||
                    g.cleanName?.toLowerCase().includes(q) ||
                    g.notes?.toLowerCase().includes(q) ||
                    g.category?.toLowerCase().includes(q));
            }
            return true;
        });
    }

    function renderStats() {
        const total = allGroups.length;
        const withStats = allGroups.filter(g => g.statisticsEnabled).length;
        const categories = new Set(allGroups.map(g => g.category).filter(Boolean));
        const totalCriteria = allGroups.reduce((sum, g) => sum + g.criteriaCount, 0);
        const filtered = getFiltered();

        const el = document.getElementById('audience-stats');
        el.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">Audiences</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${withStats}</div><div class="ept-stat__label">With Statistics</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${categories.size}</div><div class="ept-stat__label">Categories</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${totalCriteria}</div><div class="ept-stat__label">Total Criteria</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${filtered.length}</div><div class="ept-stat__label">Showing</div></div>
        `;
    }

    function renderToolbar() {
        const categories = [...new Set(allGroups.map(g => g.category).filter(Boolean))].sort();

        const toolbar = document.getElementById('audience-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="audience-search" placeholder="Search audiences..." />
            </div>
            <select id="audience-category-filter" class="ept-select">
                <option value="">All categories</option>
                ${categories.map(c => `<option value="${escAttr(c)}">${escHtml(c)} (${allGroups.filter(g => g.category === c).length})</option>`).join('')}
            </select>
            <label class="ept-toggle">
                <input type="checkbox" id="audience-stats-filter" ${statsOnlyFilter ? 'checked' : ''} />
                Has Statistics
            </label>
            <div class="ept-toolbar__spacer"></div>
            <a href="${EPT_BASE_URL}EditorPowertools/PersonalizationAudit" class="ept-btn" title="View Personalization Usage">
                ${EPT.icons.link} Personalization Audit
            </a>
            <button class="ept-btn" id="audience-export" title="Export to CSV">${EPT.icons.download} Export</button>
        `;

        document.getElementById('audience-search').addEventListener('input', (e) => {
            searchQuery = e.target.value;
            renderStats();
            renderTable();
        });

        document.getElementById('audience-category-filter').addEventListener('change', (e) => {
            categoryFilter = e.target.value;
            renderStats();
            renderTable();
        });

        document.getElementById('audience-stats-filter').addEventListener('change', (e) => {
            statsOnlyFilter = e.target.checked;
            renderStats();
            renderTable();
        });

        document.getElementById('audience-export').addEventListener('click', exportCsv);
    }

    function renderTable() {
        const data = getFiltered();

        const distinctCategories = [...new Set(data.map(r => r.category).filter(Boolean))].sort();
        const distinctOperators = [...new Set(data.map(r => r.criteriaOperator).filter(Boolean))].sort();

        const columns = [
            { key: 'cleanName', label: 'Name', render: (r) => renderGroupName(r) },
            { key: 'criteriaCount', label: 'Criteria', align: 'right' },
            { key: 'criteriaOperator', label: 'Operator', filterable: distinctOperators, render: (r) => renderOperator(r) },
            { key: 'statisticsEnabled', label: 'Statistics', render: (r) => r.statisticsEnabled ? '<span style="color:var(--ept-success, #2e7d32)">&#10003;</span>' : '<span class="ept-muted">&mdash;</span>' },
            { key: 'usageCount', label: 'Usage', align: 'right', render: (r) => renderUsageCount(r) },
            { key: 'actions', label: '', sortable: false, render: (r) => renderActions(r) }
        ];

        const colFilters = {};

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'cleanName',
            rowClass: () => ''
        });

        const content = document.getElementById('audience-content');
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
                sel.innerHTML = `<option value="">All</option>${col.filterable.map(v => `<option value="${escAttr(v)}">${escHtml(v)}</option>`).join('')}`;
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
        body.style.overflow = 'auto';
        body.style.maxHeight = 'calc(100vh - 300px)';
        body.appendChild(tableInstance.table);
        card.appendChild(body);
        content.appendChild(card);
    }

    function renderGroupName(r) {
        const name = escHtml(r.cleanName || r.name);
        const badges = [];
        if (r.category) {
            badges.push(`<span class="ept-badge ept-badge--primary">${escHtml(r.category)}</span>`);
        }
        return `<div>
            <strong>${name}</strong> ${badges.join(' ')}
            ${r.notes ? `<div class="ept-muted" style="font-size:11px;margin-top:2px">${escHtml(r.notes)}</div>` : ''}
        </div>`;
    }

    function renderOperator(r) {
        const cls = r.criteriaOperator === 'And' ? 'success' : 'warning';
        return `<span class="ept-badge ept-badge--${cls}">${escHtml(r.criteriaOperator)}</span>`;
    }

    function renderUsageCount(r) {
        if (r.usageCount == null) {
            return '<span class="ept-muted" title="Run the Personalization Analysis job first">&mdash;</span>';
        }
        if (r.usageCount === 0) {
            return '<span class="ept-badge ept-badge--warning" title="Not used in any personalized content">0</span>';
        }
        const btn = document.createElement('button');
        btn.className = 'ept-btn ept-btn--sm';
        btn.title = 'Show usage details';
        btn.textContent = String(r.usageCount);
        btn.addEventListener('click', (e) => { e.stopPropagation(); showUsages(r); });
        return btn;
    }

    function renderActions(r) {
        const div = document.createElement('div');
        div.className = 'ept-flex';

        if (r.editUrl) {
            const editBtn = createIconBtn(EPT.icons.edit, 'Edit audience', () => window.open(r.editUrl, '_blank'));
            div.appendChild(editBtn);
        }

        const criteriaBtn = createIconBtn(EPT.icons.list, 'View criteria', () => showCriteria(r));
        div.appendChild(criteriaBtn);

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

    // ── Criteria Dialog ───────────────────────────────────────────
    async function showCriteria(group) {
        const { body, close } = EPT.openDialog(`Criteria: ${group.cleanName || group.name}`, { flush: true });
        EPT.showLoading(body);

        try {
            const criteria = await EPT.fetchJson(`${API}/visitor-groups/${group.id}/criteria`);
            body.innerHTML = '';

            if (criteria.length === 0) {
                EPT.showEmpty(body, 'No criteria defined');
                return;
            }

            const info = document.createElement('div');
            info.style.padding = '12px 16px';
            info.innerHTML = `<span class="ept-muted">Operator:</span> <span class="ept-badge ept-badge--${group.criteriaOperator === 'And' ? 'success' : 'warning'}">${escHtml(group.criteriaOperator)}</span>`;
            body.appendChild(info);

            const columns = [
                { key: 'typeName', label: 'Criterion Type' },
                { key: 'description', label: 'Description', render: (r) => r.description ? escHtml(r.description) : '<span class="ept-muted">&mdash;</span>' }
            ];

            const { table } = EPT.createTable(columns, criteria, { defaultSort: 'typeName' });
            body.appendChild(table);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── Usage Dialog ──────────────────────────────────────────────
    async function showUsages(group) {
        const { body, close } = EPT.openDialog(`Usage: ${group.cleanName || group.name}`, { wide: true, flush: true });
        EPT.showLoading(body);

        try {
            const usages = await EPT.fetchJson(`${API}/visitor-groups/${group.id}/usages`);
            body.innerHTML = '';

            if (usages.length === 0) {
                EPT.showEmpty(body, 'No usages found');
                return;
            }

            const columns = [
                { key: 'contentId', label: 'ID', align: 'right' },
                {
                    key: 'contentName', label: 'Content', render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.contentName)}</strong> <span style="opacity:.4">&nearr;</span></a>`;
                        return `<strong>${escHtml(r.contentName)}</strong>`;
                    }
                },
                { key: 'propertyName', label: 'Property' },
                { key: 'usageType', label: 'Type' }
            ];

            const { table } = EPT.createTable(columns, usages, { defaultSort: 'contentName' });
            body.appendChild(table);
        } catch (err) {
            body.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── CSV Export ──────────────────────────────────────────────────
    function exportCsv() {
        const data = getFiltered();
        const columns = [
            { key: 'name', label: 'Name' },
            { key: 'cleanName', label: 'Clean Name' },
            { key: 'category', label: 'Category' },
            { key: 'notes', label: 'Notes' },
            { key: 'criteriaCount', label: 'Criteria Count' },
            { key: 'criteriaOperator', label: 'Operator' },
            { key: 'statisticsEnabled', label: 'Statistics Enabled' },
            { key: 'usageCount', label: 'Usage Count' },
            { key: 'id', label: 'ID' }
        ];
        const ts = new Date().toISOString().slice(0, 19).replace(/[T:]/g, '-');
        EPT.downloadCsv(`AudienceManager_${ts}.csv`, columns, data);
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
