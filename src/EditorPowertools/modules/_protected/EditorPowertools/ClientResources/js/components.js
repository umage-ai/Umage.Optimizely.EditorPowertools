/**
 * Editor Powertools - Reusable UI Components
 *
 * Content Picker:  EPT.contentPicker(opts)  → Promise<{id, name}>
 * Content Type Picker: EPT.contentTypePicker(opts) → Promise<{id, name, displayName}>
 */
(function () {
    const API = window.EPT_BASE_URL + 'ComponentsApi';

    // ── Content Picker ─────────────────────────────────────────────
    /**
     * Opens a dialog with a content tree browser + search.
     * Returns a promise that resolves with the selected content item, or null if cancelled.
     *
     * Options:
     *   rootId: number (default 0 = RootPage)
     *   title: string (default "Select Content")
     */
    EPT.contentPicker = function (opts = {}) {
        return new Promise((resolve) => {
            const rootId = opts.rootId || 0;
            const { body, close } = EPT.openDialog(opts.title || EPT.s('components.picker_title', 'Select Content'), { wide: false });

            let selectedItem = null;
            let mode = 'tree'; // 'tree' or 'search'

            // Build UI
            body.innerHTML = `
                <div class="ept-search ept-mb-md">
                    <span class="ept-search__icon">${EPT.icons.search}</span>
                    <input type="text" class="ept-picker-search" placeholder="${EPT.s('components.picker_search', 'Search content by name...')}" style="width:100%" />
                </div>
                <div class="ept-picker-tree" style="max-height:400px;overflow-y:auto"></div>
                <div class="ept-picker-results ept-hidden" style="max-height:400px;overflow-y:auto"></div>
                <div class="ept-flex ept-mt-md" style="justify-content:flex-end;gap:8px">
                    <button class="ept-btn ept-picker-cancel">${EPT.s('components.btn_cancel', 'Cancel')}</button>
                    <button class="ept-btn ept-btn--primary ept-picker-select" disabled>${EPT.s('components.btn_select', 'Select')}</button>
                </div>
            `;

            const searchInput = body.querySelector('.ept-picker-search');
            const treeContainer = body.querySelector('.ept-picker-tree');
            const resultsContainer = body.querySelector('.ept-picker-results');
            const selectBtn = body.querySelector('.ept-picker-select');
            const cancelBtn = body.querySelector('.ept-picker-cancel');

            // Cancel
            cancelBtn.addEventListener('click', () => { close(); resolve(null); });

            // Select
            selectBtn.addEventListener('click', () => { close(); resolve(selectedItem); });

            // Search
            let searchTimeout;
            searchInput.addEventListener('input', () => {
                clearTimeout(searchTimeout);
                const q = searchInput.value.trim();
                if (q.length < 2) {
                    mode = 'tree';
                    treeContainer.classList.remove('ept-hidden');
                    resultsContainer.classList.add('ept-hidden');
                    return;
                }
                searchTimeout = setTimeout(() => searchContent(q), 300);
            });

            async function searchContent(q) {
                mode = 'search';
                treeContainer.classList.add('ept-hidden');
                resultsContainer.classList.remove('ept-hidden');
                EPT.showLoading(resultsContainer);

                try {
                    const items = await EPT.fetchJson(`${API}/SearchContent?q=${encodeURIComponent(q)}&rootId=${rootId}`);
                    if (items.length === 0) {
                        EPT.showEmpty(resultsContainer, EPT.s('components.picker_noresults', 'No results found'));
                        return;
                    }
                    renderSearchResults(items);
                } catch (err) {
                    resultsContainer.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
                }
            }

            function renderSearchResults(items) {
                resultsContainer.innerHTML = '';
                const list = document.createElement('div');
                items.forEach(item => {
                    const row = document.createElement('div');
                    row.className = 'ept-picker-item';
                    row.innerHTML = `<strong>${esc(item.name)}</strong> <span class="ept-muted">(${esc(item.typeName)})</span>`;
                    row.addEventListener('click', () => selectItem(item, row));
                    list.appendChild(row);
                });
                resultsContainer.appendChild(list);
            }

            function selectItem(item, el) {
                body.querySelectorAll('.ept-picker-item--selected').forEach(e => e.classList.remove('ept-picker-item--selected'));
                el.classList.add('ept-picker-item--selected');
                selectedItem = item;
                selectBtn.disabled = false;
            }

            // Load tree
            loadTreeNode(treeContainer, rootId);

            async function loadTreeNode(container, parentId) {
                EPT.showLoading(container);
                try {
                    const children = await EPT.fetchJson(`${API}/GetChildren/${parentId}`);
                    container.innerHTML = '';
                    if (children.length === 0) {
                        container.innerHTML = '<div class="ept-muted" style="padding:8px">No children</div>';
                        return;
                    }

                    const ul = document.createElement('ul');
                    ul.className = 'ept-tree';

                    children.forEach(child => {
                        const li = document.createElement('li');
                        li.className = 'ept-tree__item';

                        const line = document.createElement('div');
                        line.className = 'ept-flex';

                        if (child.hasChildren) {
                            const toggle = document.createElement('button');
                            toggle.className = 'ept-tree__toggle';
                            toggle.innerHTML = EPT.icons.chevronRight;
                            let expanded = false;
                            let childContainer = null;

                            toggle.addEventListener('click', (e) => {
                                e.stopPropagation();
                                expanded = !expanded;
                                toggle.innerHTML = expanded ? EPT.icons.chevronDown : EPT.icons.chevronRight;
                                if (expanded && !childContainer) {
                                    childContainer = document.createElement('div');
                                    childContainer.style.marginLeft = '20px';
                                    li.appendChild(childContainer);
                                    loadTreeNode(childContainer, child.id);
                                } else if (childContainer) {
                                    childContainer.style.display = expanded ? '' : 'none';
                                }
                            });
                            line.appendChild(toggle);
                        } else {
                            const spacer = document.createElement('span');
                            spacer.style.width = '24px';
                            spacer.style.display = 'inline-block';
                            line.appendChild(spacer);
                        }

                        const label = document.createElement('span');
                        label.className = 'ept-picker-item';
                        label.innerHTML = `${esc(child.name)} <span class="ept-muted" style="font-size:11px">${esc(child.typeName)}</span>`;
                        label.addEventListener('click', (e) => {
                            e.stopPropagation();
                            selectItem(child, label);
                        });
                        line.appendChild(label);

                        li.appendChild(line);
                        ul.appendChild(li);
                    });

                    container.appendChild(ul);
                } catch (err) {
                    container.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
                }
            }
        });
    };

    // ── Content Type Picker ────────────────────────────────────────
    /**
     * Opens a dialog with a filterable content type list.
     * Returns a promise that resolves with the selected type, or null if cancelled.
     *
     * Options:
     *   title: string (default "Select Content Type")
     *   includeSystem: boolean (default false)
     */
    EPT.contentTypePicker = function (opts = {}) {
        return new Promise(async (resolve) => {
            const { body, close } = EPT.openDialog(opts.title || EPT.s('components.typepicker_title', 'Select Content Type'));

            let selectedType = null;
            let allTypes = [];

            body.innerHTML = `
                <div class="ept-search ept-mb-md">
                    <span class="ept-search__icon">${EPT.icons.search}</span>
                    <input type="text" class="ept-picker-search" placeholder="${EPT.s('components.typepicker_search', 'Search content types...')}" style="width:100%" />
                </div>
                <div class="ept-picker-list" style="max-height:400px;overflow-y:auto"></div>
                <div class="ept-flex ept-mt-md" style="justify-content:flex-end;gap:8px">
                    <button class="ept-btn ept-picker-cancel">${EPT.s('components.btn_cancel', 'Cancel')}</button>
                    <button class="ept-btn ept-btn--primary ept-picker-select" disabled>${EPT.s('components.btn_select', 'Select')}</button>
                </div>
            `;

            const searchInput = body.querySelector('.ept-picker-search');
            const listContainer = body.querySelector('.ept-picker-list');
            const selectBtn = body.querySelector('.ept-picker-select');
            const cancelBtn = body.querySelector('.ept-picker-cancel');

            cancelBtn.addEventListener('click', () => { close(); resolve(null); });
            selectBtn.addEventListener('click', () => { close(); resolve(selectedType); });

            // Load types
            EPT.showLoading(listContainer);
            try {
                allTypes = await EPT.fetchJson(`${API}/GetContentTypes`);
                if (!opts.includeSystem) {
                    allTypes = allTypes.filter(t => !t.isSystemType);
                }
                renderTypes(allTypes);
            } catch (err) {
                listContainer.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
            }

            // Filter
            searchInput.addEventListener('input', () => {
                const q = searchInput.value.trim().toLowerCase();
                const filtered = q
                    ? allTypes.filter(t => t.displayName.toLowerCase().includes(q) || t.name.toLowerCase().includes(q))
                    : allTypes;
                renderTypes(filtered);
            });

            function renderTypes(types) {
                listContainer.innerHTML = '';
                if (types.length === 0) {
                    EPT.showEmpty(listContainer, EPT.s('components.typepicker_noresults', 'No content types found'));
                    return;
                }

                // Group by GroupName
                const groups = {};
                types.forEach(t => {
                    const g = t.groupName || 'Other';
                    if (!groups[g]) groups[g] = [];
                    groups[g].push(t);
                });

                Object.keys(groups).sort().forEach(groupName => {
                    const header = document.createElement('div');
                    header.className = 'ept-muted';
                    header.style.cssText = 'font-size:11px;font-weight:600;padding:8px 12px 4px;text-transform:uppercase;letter-spacing:.5px';
                    header.textContent = groupName;
                    listContainer.appendChild(header);

                    groups[groupName].forEach(type => {
                        const row = document.createElement('div');
                        row.className = 'ept-picker-item';
                        row.innerHTML = `<strong>${esc(type.displayName)}</strong>
                            ${type.displayName !== type.name ? `<span class="ept-muted">(${esc(type.name)})</span>` : ''}
                            ${type.base ? `<span class="ept-badge ept-badge--default">${esc(type.base)}</span>` : ''}`;
                        row.addEventListener('click', () => {
                            body.querySelectorAll('.ept-picker-item--selected').forEach(e => e.classList.remove('ept-picker-item--selected'));
                            row.classList.add('ept-picker-item--selected');
                            selectedType = type;
                            selectBtn.disabled = false;
                        });
                        listContainer.appendChild(row);
                    });
                });
            }
        });
    };

    function esc(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }
})();
