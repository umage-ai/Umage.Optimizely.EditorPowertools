/**
 * Content Type Audit - Main UI
 */
(function () {
    const API = window.EPT_BASE_URL + 'ContentTypeAuditApi';
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
                EPT.fetchJson(`${API}/GetTypes`),
                EPT.fetchJson(`${API}/GetAggregationStatus`).catch(() => null)
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
        const existing = document.getElementById('audit-job-alert');
        if (existing) existing.remove();
        if (!jobStatus) return;
        const el = EPT.renderJobAlert(jobStatus, `${API}/StartAggregationJob`);
        if (!el) return;
        el.id = 'audit-job-alert';
        const container = document.getElementById('audit-stats');
        container.parentNode.insertBefore(el, container);
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
        const orphaned = allTypes.filter(t => t.isCodeless).length;
        const hasStats = allTypes.some(t => t.statisticsUpdated != null);
        const unused = hasStats ? filtered.filter(t => t.contentCount === 0).length : null;

        const el = document.getElementById('audit-stats');
        el.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_contenttypes', 'Content Types')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${system}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_systemtypes', 'System Types')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${orphaned}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_codeless', 'Code-less')}</div></div>
            ${unused != null ? `<div class="ept-stat"><div class="ept-stat__value">${unused}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_unused', 'Unused (0 content)')}</div></div>` : ''}
            <div class="ept-stat"><div class="ept-stat__value">${filtered.length}</div><div class="ept-stat__label">${EPT.s('contenttypeaudit.stat_showing', 'Showing')}</div></div>
        `;
    }

    function renderToolbar() {
        const bases = [...new Set(allTypes.map(t => t.base))].sort();

        const toolbar = document.getElementById('audit-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="audit-search" placeholder="${EPT.s('contenttypeaudit.lbl_search', 'Search types... (or property:name)')}" />
            </div>
            <select id="audit-base-filter" class="ept-select">
                <option value="">${EPT.s('contenttypeaudit.opt_allbases', 'All base types')}</option>
                ${bases.map(b => `<option value="${b}">${b} (${allTypes.filter(t => t.base === b).length})</option>`).join('')}
            </select>
            <label class="ept-toggle">
                <input type="checkbox" id="audit-show-system" ${showSystem ? 'checked' : ''} />
                ${EPT.s('contenttypeaudit.chk_showsystem', 'Show system types')}
            </label>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="audit-view-table" title="Table view">${EPT.icons.list} ${EPT.s('contenttypeaudit.btn_table', 'Table')}</button>
            <button class="ept-btn" id="audit-view-tree" title="Inheritance tree">${EPT.icons.tree} ${EPT.s('contenttypeaudit.btn_tree', 'Tree')}</button>
            <button class="ept-btn" id="audit-export" title="Export to CSV">${EPT.icons.download} ${EPT.s('contenttypeaudit.btn_export', 'Export')}</button>
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
            { key: 'name', label: EPT.s('contenttypeaudit.col_name', 'Name'), render: (r) => renderTypeName(r) },
            { key: 'base', label: EPT.s('contenttypeaudit.col_base', 'Base'), filterable: distinctBases },
            { key: 'groupName', label: EPT.s('contenttypeaudit.col_group', 'Group'), filterable: distinctGroups },
            { key: 'propertyCount', label: EPT.s('contenttypeaudit.col_properties', 'Properties'), align: 'right' },
            { key: 'contentCount', label: EPT.s('contenttypeaudit.col_content', 'Content'), align: 'right', render: (r) => renderCount(r.contentCount) },
            { key: 'actions', label: '', sortable: false, render: (r) => renderActions(r) }
        ];

        // Column filters state
        const colFilters = {};

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'name',
            rowClass: (r) => {
                if (r.isSystemType) return 'ept-row--system';
                if (r.isCodeless) return 'ept-row--orphaned';
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
                sel.innerHTML = `<option value="">${EPT.s('contenttypeaudit.opt_all', 'All')}</option>${col.filterable.map(v => `<option value="${escAttr(v)}">${escHtml(v)}</option>`).join('')}`;
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
        if (r.isSystemType) badges.push(`<span class="ept-badge ept-badge--default">${EPT.s('contenttypeaudit.badge_system', 'System')}</span>`);
        else if (r.isCodeless) badges.push(`<span class="ept-badge ept-badge--danger">${EPT.s('contenttypeaudit.badge_codeless', 'Code-less')}</span>`);
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
            const editBtn = createIconBtn(EPT.icons.edit, EPT.s('contenttypeaudit.title_edittype', 'Edit content type'), () => window.open(r.editUrl, '_blank'));
            div.appendChild(editBtn);
        }

        const propsBtn = createIconBtn(EPT.icons.props, EPT.s('contenttypeaudit.title_properties', 'Properties'), () => showProperties(r));
        div.appendChild(propsBtn);

        const contentBtn = createIconBtn(EPT.icons.list, EPT.s('contenttypeaudit.title_contentoftype', 'Content of this type'), () => showContentOfType(r));
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
        const { body, close } = EPT.openDialog(EPT.s('contenttypeaudit.dlg_properties', 'Properties: {0}').replace('{0}', type.displayName || type.name), { wide: true, flush: true });
        EPT.showLoading(body);

        try {
            const props = await EPT.fetchJson(`${API}/GetProperties/${type.id}`);
            body.innerHTML = ''; // Clear loading spinner

            if (props.length === 0) {
                EPT.showEmpty(body, EPT.s('contenttypeaudit.empty_noproperties', 'No properties defined'));
                return;
            }

            const legend = document.createElement('div');
            legend.style.padding = '12px 16px';
            legend.innerHTML = `
                <span class="ept-badge ept-badge--success">${EPT.s('contenttypeaudit.legend_inherited', 'Inherited')}</span>
                <span class="ept-badge ept-badge--default">${EPT.s('contenttypeaudit.legend_defined', 'Defined')}</span>
                <span class="ept-badge ept-badge--danger">${EPT.s('contenttypeaudit.legend_codelessdesc', 'Code-less (not in code)')}</span>
            `;
            body.appendChild(legend);

            const columns = [
                { key: 'name', label: EPT.s('contenttypeaudit.col_propname', 'Name'), render: (r) => `<strong>${escHtml(r.name)}</strong>` },
                { key: 'typeName', label: EPT.s('contenttypeaudit.col_proptype', 'Type') },
                { key: 'tabName', label: EPT.s('contenttypeaudit.col_proptab', 'Tab') },
                { key: 'sortOrder', label: EPT.s('contenttypeaudit.col_proporder', 'Order'), align: 'right' },
                { key: 'required', label: EPT.s('contenttypeaudit.col_proprequired', 'Required'), render: (r) => r.required ? '✓' : '' },
                { key: 'searchable', label: EPT.s('contenttypeaudit.col_propsearchable', 'Searchable'), render: (r) => r.searchable ? '✓' : '' },
                { key: 'languageSpecific', label: EPT.s('contenttypeaudit.col_proplanguage', 'Language'), render: (r) => r.languageSpecific ? '✓' : '' },
                {
                    key: 'origin', label: EPT.s('contenttypeaudit.col_proporigin', 'Origin'), render: (r) => {
                        const label = r.origin === 0 ? EPT.s('contenttypeaudit.origin_defined', 'Defined') : r.origin === 1 ? EPT.s('contenttypeaudit.origin_inherited', 'Inherited') : EPT.s('contenttypeaudit.origin_codeless', 'Code-less');
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
        const { body, close } = EPT.openDialog(EPT.s('contenttypeaudit.dlg_content', 'Content: {0}').replace('{0}', type.displayName || type.name), { wide: true, flush: true });
        EPT.showLoading(body);

        try {
            const items = await EPT.fetchJson(`${API}/GetContentOfType/${type.id}`);
            body.innerHTML = ''; // Clear loading spinner

            if (items.length === 0) {
                EPT.showEmpty(body, EPT.s('contenttypeaudit.empty_nocontent', 'No content of this type'));
                return;
            }

            // Language filter
            const languages = [...new Set(items.map(i => i.language).filter(Boolean))].sort();
            if (languages.length > 1) {
                const filterBar = document.createElement('div');
                filterBar.style.padding = '8px 16px';
                filterBar.className = 'ept-flex';
                filterBar.innerHTML = `<label class="ept-muted" style="font-size:12px">${EPT.s('contenttypeaudit.lbl_language', 'Language:')}</label>
                    <select class="ept-select ept-content-lang-filter">
                        <option value="">${EPT.s('contenttypeaudit.opt_alllang', 'All ({0})').replace('{0}', items.length)}</option>
                        ${languages.map(l => `<option value="${l}">${l} (${items.filter(i => i.language === l).length})</option>`).join('')}
                    </select>`;
                body.appendChild(filterBar);

                filterBar.querySelector('select').addEventListener('change', (e) => {
                    const lang = e.target.value;
                    tbl.render(lang ? (r => r.language === lang) : null);
                });
            }

            const columns = [
                { key: 'contentId', label: EPT.s('contenttypeaudit.col_contentid', 'ID'), align: 'right' },
                {
                    key: 'name', label: EPT.s('contenttypeaudit.col_contentname', 'Name'), render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.name)}</strong> <span style="opacity:.4">↗</span></a>`;
                        return `<strong>${escHtml(r.name)}</strong>`;
                    }
                },
                { key: 'language', label: EPT.s('contenttypeaudit.col_contentlang', 'Lang') },
                {
                    key: 'breadcrumb', label: EPT.s('contenttypeaudit.col_contentlocation', 'Location'), render: (r) =>
                        `<span class="ept-truncate" title="${escAttr(r.breadcrumb)}">${escHtml(r.breadcrumb || '')}</span>`
                },
                {
                    key: 'isPublished', label: EPT.s('contenttypeaudit.col_contentstatus', 'Status'), render: (r) =>
                        r.isPublished
                            ? `<span class="ept-badge ept-badge--success">${EPT.s('contenttypeaudit.badge_published', 'Published')}</span>`
                            : `<span class="ept-badge ept-badge--default">${EPT.s('contenttypeaudit.badge_draft', 'Draft')}</span>`
                },
                {
                    key: 'referenceCount', label: EPT.s('contenttypeaudit.col_contentrefs', 'References'), align: 'right', render: (r) => {
                        if (r.referenceCount === 0) return '<span class="ept-badge ept-badge--warning">0</span>';
                        const btn = createIconBtn(
                            `${r.referenceCount} ${EPT.icons.link}`,
                            EPT.s('contenttypeaudit.title_showrefs', 'Show references'),
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
        const { body, close } = EPT.openDialog(EPT.s('contenttypeaudit.dlg_references', 'References to: {0}').replace('{0}', content.name), { flush: true });
        EPT.showLoading(body);

        try {
            const refs = await EPT.fetchJson(`${API}/GetContentReferences/${content.contentId}`);
            body.innerHTML = ''; // Clear loading spinner

            if (refs.length === 0) {
                EPT.showEmpty(body, EPT.s('contenttypeaudit.empty_norefs', 'No references found'));
                return;
            }

            // Language filter
            const languages = [...new Set(refs.map(r => r.language).filter(Boolean))].sort();
            if (languages.length > 1) {
                const filterBar = document.createElement('div');
                filterBar.style.padding = '8px 16px';
                filterBar.className = 'ept-flex';
                filterBar.innerHTML = `<label class="ept-muted" style="font-size:12px">${EPT.s('contenttypeaudit.lbl_language', 'Language:')}</label>
                    <select class="ept-select ept-refs-lang-filter">
                        <option value="">${EPT.s('contenttypeaudit.opt_alllang', 'All ({0})').replace('{0}', refs.length)}</option>
                        ${languages.map(l => `<option value="${l}">${l} (${refs.filter(r => r.language === l).length})</option>`).join('')}
                    </select>`;
                body.appendChild(filterBar);

                filterBar.querySelector('select').addEventListener('change', (e) => {
                    const lang = e.target.value;
                    tbl.render(lang ? (r => r.language === lang) : null);
                });
            }

            const columns = [
                { key: 'ownerContentId', label: EPT.s('contenttypeaudit.col_refid', 'ID'), align: 'right' },
                {
                    key: 'ownerName', label: EPT.s('contenttypeaudit.col_refby', 'Referenced By'), render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.ownerName)}</strong> <span style="opacity:.4">↗</span></a>`;
                        return `<strong>${escHtml(r.ownerName)}</strong>`;
                    }
                },
                { key: 'ownerTypeName', label: EPT.s('contenttypeaudit.col_reftype', 'Type') },
                { key: 'language', label: EPT.s('contenttypeaudit.col_reflang', 'Lang') },
                { key: 'propertyName', label: EPT.s('contenttypeaudit.col_refprop', 'Property') },
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
            const tree = await EPT.fetchJson(`${API}/GetInheritanceTree`);
            body.innerHTML = '';

            if (tree.length === 0) {
                EPT.showEmpty(body, EPT.s('contenttypeaudit.empty_notree', 'No inheritance data available'));
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
            if (node.isCodeless) {
                label.innerHTML += ` <span class="ept-badge ept-badge--danger">${EPT.s('contenttypeaudit.badge_codeless', 'Code-less')}</span>`;
            }
            label.title = EPT.s('contenttypeaudit.title_viewcontent', 'Click to view content of this type');
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
            editBtn.title = EPT.s('contenttypeaudit.title_edittype', 'Edit content type');
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
            { key: 'isCodeless', label: 'Code-less' },
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
