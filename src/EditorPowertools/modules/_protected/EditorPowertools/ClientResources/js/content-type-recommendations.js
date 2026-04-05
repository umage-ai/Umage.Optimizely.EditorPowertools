/**
 * Content Type Recommendations - Admin CRUD UI
 */
(function () {
    const API = window.EPT_API_URL + '/recommendations';
    let allRules = [];
    let allContentTypes = [];

    async function init() {
        const container = document.getElementById('recommendations-content');
        EPT.showLoading(container);

        try {
            const [rules, types] = await Promise.all([
                EPT.fetchJson(`${API}/rules`),
                EPT.fetchJson(`${API}/content-types`)
            ]);
            allRules = rules;
            allContentTypes = types;
            renderPage();
        } catch (err) {
            container.innerHTML =
                `<div class="ept-empty"><p>Error loading recommendations: ${err.message}</p></div>`;
        }
    }

    function renderPage() {
        const container = document.getElementById('recommendations-content');
        container.innerHTML = '';

        // Info banner
        const info = document.createElement('div');
        info.className = 'ept-alert ept-alert--info';
        info.innerHTML = EPT.s('recommendations.banner_info', 'When editors create new content, Optimizely can suggest which content types to use. Define rules below to control these suggestions based on where content is being created.');
        container.appendChild(info);

        // Toolbar
        const toolbar = document.createElement('div');
        toolbar.className = 'ept-toolbar';
        toolbar.innerHTML = `<div class="ept-toolbar__spacer"></div>`;

        const addBtn = document.createElement('button');
        addBtn.className = 'ept-btn ept-btn--primary';
        addBtn.textContent = EPT.s('recommendations.btn_addrule', 'Add Rule');
        addBtn.addEventListener('click', () => openRuleDialog(null));
        toolbar.appendChild(addBtn);
        container.appendChild(toolbar);

        // Rules table
        renderRulesTable(container);
    }

    function renderRulesTable(container) {
        if (allRules.length === 0) {
            const empty = document.createElement('div');
            empty.className = 'ept-card';
            empty.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>' + EPT.s('recommendations.empty_norules', 'No recommendation rules defined yet. Click "Add Rule" to create one.') + '</p></div></div>';
            container.appendChild(empty);
            return;
        }

        const columns = [
            {
                key: 'parentContentTypeName', label: 'Parent Content Type',
                render: (r) => escHtml(r.parentContentTypeName || 'Any')
            },
            {
                key: 'parentContentName', label: 'Parent Content',
                render: (r) => r.parentContentName
                    ? `${escHtml(r.parentContentName)} <span class="ept-muted">(ID: ${r.parentContentId})</span>`
                    : '<span class="ept-muted">Any location</span>'
            },
            {
                key: 'includeDescendants', label: 'Include Descendants',
                render: (r) => r.includeDescendants ? 'Yes' : 'No'
            },
            {
                key: 'forThisContentFolder', label: 'Content Folder Only',
                render: (r) => r.forThisContentFolder ? 'Yes' : 'No'
            },
            {
                key: 'suggestedTypes', label: 'Suggested Types', sortable: false,
                render: (r) => r.suggestedTypes.map(t =>
                    `<span class="ept-badge ept-badge--primary">${escHtml(t.name)}</span>`
                ).join(' ')
            },
            {
                key: 'actions', label: '', sortable: false,
                render: (r) => {
                    const div = document.createElement('div');
                    div.className = 'ept-flex';

                    const editBtn = document.createElement('button');
                    editBtn.className = 'ept-btn ept-btn--sm ept-btn--icon';
                    editBtn.title = EPT.s('recommendations.btn_edit', 'Edit');
                    editBtn.innerHTML = EPT.icons.edit;
                    editBtn.addEventListener('click', (e) => { e.stopPropagation(); openRuleDialog(r); });
                    div.appendChild(editBtn);

                    const delBtn = document.createElement('button');
                    delBtn.className = 'ept-btn ept-btn--sm ept-btn--icon';
                    delBtn.title = EPT.s('recommendations.btn_delete', 'Delete');
                    delBtn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/></svg>';
                    delBtn.addEventListener('click', (e) => { e.stopPropagation(); deleteRule(r.id); });
                    div.appendChild(delBtn);

                    return div;
                }
            }
        ];

        const { table } = EPT.createTable(columns, allRules, { defaultSort: 'parentContentTypeName' });

        const card = document.createElement('div');
        card.className = 'ept-card';
        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        body.appendChild(table);
        card.appendChild(body);
        container.appendChild(card);
    }

    // ── Add/Edit Dialog ────────────────────────────────────────────

    function openRuleDialog(existingRule) {
        const isEdit = !!existingRule;
        const { body, close } = EPT.openDialog(isEdit ? EPT.s('recommendations.dlg_editrule', 'Edit Rule') : EPT.s('recommendations.dlg_addrule', 'Add Rule'), { wide: true });

        // State
        let selectedParentContentType = existingRule ? existingRule.parentContentType : -1;
        let selectedParentContentId = existingRule ? existingRule.parentContentId : null;
        let selectedParentContentName = existingRule ? existingRule.parentContentName : null;
        let includeDescendants = existingRule ? existingRule.includeDescendants : false;
        let forThisContentFolder = existingRule ? existingRule.forThisContentFolder : false;
        let selectedTypeIds = new Set(existingRule ? existingRule.suggestedTypes.map(t => t.id) : []);

        body.innerHTML = `
            <div style="display:flex;flex-direction:column;gap:16px;padding:8px 0">
                <div>
                    <label style="font-weight:600;display:block;margin-bottom:4px">${EPT.s('recommendations.lbl_parenttype', 'Parent Content Type:')}</label>
                    <select class="ept-select" id="dlg-parent-type" style="width:100%">
                        <option value="-1">Any</option>
                        ${allContentTypes.map(t => `<option value="${t.id}" ${t.id === selectedParentContentType ? 'selected' : ''}>${escHtml(t.displayName)}</option>`).join('')}
                    </select>
                </div>
                <div>
                    <label style="font-weight:600;display:block;margin-bottom:4px">Parent Content</label>
                    <div class="ept-flex" style="gap:8px;align-items:center">
                        <span id="dlg-parent-content-name" class="ept-muted">${selectedParentContentName ? escHtml(selectedParentContentName) + ' (ID: ' + selectedParentContentId + ')' : 'Any location'}</span>
                        <button class="ept-btn ept-btn--sm" id="dlg-browse-btn">Browse...</button>
                        <button class="ept-btn ept-btn--sm" id="dlg-clear-btn">Clear</button>
                    </div>
                </div>
                <div class="ept-flex" style="gap:24px">
                    <label class="ept-toggle">
                        <input type="checkbox" id="dlg-include-descendants" ${includeDescendants ? 'checked' : ''} />
                        Include Descendants
                    </label>
                    <label class="ept-toggle">
                        <input type="checkbox" id="dlg-content-folder" ${forThisContentFolder ? 'checked' : ''} />
                        For This Content Folder Only
                    </label>
                </div>
                <div>
                    <label style="font-weight:600;display:block;margin-bottom:4px">${EPT.s('recommendations.lbl_allowedtypes', 'Allowed Child Types:')}</label>
                    <div class="ept-search ept-mb-md" style="margin-bottom:8px">
                        <span class="ept-search__icon">${EPT.icons.search}</span>
                        <input type="text" id="dlg-type-filter" placeholder="Filter types..." style="width:100%" />
                    </div>
                    <div id="dlg-type-list" style="max-height:250px;overflow-y:auto;border:1px solid var(--ept-border, #e0e0e0);border-radius:4px"></div>
                </div>
                <div class="ept-flex" style="justify-content:flex-end;gap:8px;margin-top:8px">
                    <button class="ept-btn" id="dlg-cancel">${EPT.s('recommendations.btn_cancel', 'Cancel')}</button>
                    <button class="ept-btn ept-btn--primary" id="dlg-save">${EPT.s('recommendations.btn_save', 'Save')}</button>
                </div>
            </div>
        `;

        // Wire up parent type dropdown
        body.querySelector('#dlg-parent-type').addEventListener('change', (e) => {
            selectedParentContentType = parseInt(e.target.value);
        });

        // Wire up browse button
        body.querySelector('#dlg-browse-btn').addEventListener('click', async () => {
            const selected = await EPT.contentPicker({ title: 'Select Parent Content' });
            if (selected) {
                selectedParentContentId = selected.id;
                selectedParentContentName = selected.name;
                body.querySelector('#dlg-parent-content-name').textContent = `${selected.name} (ID: ${selected.id})`;
                body.querySelector('#dlg-parent-content-name').className = '';
            }
        });

        // Wire up clear button
        body.querySelector('#dlg-clear-btn').addEventListener('click', () => {
            selectedParentContentId = null;
            selectedParentContentName = null;
            const nameEl = body.querySelector('#dlg-parent-content-name');
            nameEl.textContent = 'Any location';
            nameEl.className = 'ept-muted';
        });

        // Wire up checkboxes
        body.querySelector('#dlg-include-descendants').addEventListener('change', (e) => {
            includeDescendants = e.target.checked;
        });
        body.querySelector('#dlg-content-folder').addEventListener('change', (e) => {
            forThisContentFolder = e.target.checked;
        });

        // Render type checkboxes grouped by GroupName
        function renderTypeCheckboxes(filter) {
            const listEl = body.querySelector('#dlg-type-list');
            listEl.innerHTML = '';

            let types = allContentTypes;
            if (filter) {
                const q = filter.toLowerCase();
                types = types.filter(t =>
                    t.displayName.toLowerCase().includes(q) ||
                    t.name.toLowerCase().includes(q)
                );
            }

            if (types.length === 0) {
                listEl.innerHTML = '<div class="ept-muted" style="padding:12px;text-align:center">No matching types</div>';
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
                header.style.cssText = 'font-size:11px;font-weight:600;padding:8px 12px 4px;text-transform:uppercase;letter-spacing:.5px;background:var(--ept-bg, #f5f6f8)';
                header.textContent = groupName;
                listEl.appendChild(header);

                groups[groupName].forEach(type => {
                    const row = document.createElement('label');
                    row.style.cssText = 'display:flex;align-items:center;gap:8px;padding:6px 12px;cursor:pointer';
                    row.addEventListener('mouseenter', () => { row.style.background = 'var(--ept-hover, #f0f1f3)'; });
                    row.addEventListener('mouseleave', () => { row.style.background = ''; });

                    const cb = document.createElement('input');
                    cb.type = 'checkbox';
                    cb.checked = selectedTypeIds.has(type.id);
                    cb.addEventListener('change', () => {
                        if (cb.checked) {
                            selectedTypeIds.add(type.id);
                        } else {
                            selectedTypeIds.delete(type.id);
                        }
                    });

                    const label = document.createElement('span');
                    label.innerHTML = `<strong>${escHtml(type.displayName)}</strong>`;
                    if (type.displayName !== type.name) {
                        label.innerHTML += ` <span class="ept-muted">(${escHtml(type.name)})</span>`;
                    }

                    row.appendChild(cb);
                    row.appendChild(label);
                    listEl.appendChild(row);
                });
            });
        }

        renderTypeCheckboxes('');

        // Filter types
        body.querySelector('#dlg-type-filter').addEventListener('input', (e) => {
            renderTypeCheckboxes(e.target.value.trim());
        });

        // Cancel
        body.querySelector('#dlg-cancel').addEventListener('click', close);

        // Save
        body.querySelector('#dlg-save').addEventListener('click', async () => {
            if (selectedTypeIds.size === 0) {
                alert('Please select at least one content type to suggest.');
                return;
            }

            const saveBtn = body.querySelector('#dlg-save');
            saveBtn.disabled = true;
            saveBtn.textContent = 'Saving...';

            try {
                const request = {
                    id: isEdit ? existingRule.id : null,
                    parentContentType: selectedParentContentType,
                    parentContentId: selectedParentContentId,
                    includeDescendants: includeDescendants,
                    forThisContentFolder: forThisContentFolder,
                    contentTypesToSuggest: Array.from(selectedTypeIds)
                };

                await EPT.postJson(`${API}/rules`, request);
                close();
                await reload();
            } catch (err) {
                saveBtn.disabled = false;
                saveBtn.textContent = EPT.s('recommendations.btn_save', 'Save');
                alert('Error saving rule: ' + err.message);
            }
        });
    }

    // ── Delete ─────────────────────────────────────────────────────

    async function deleteRule(ruleId) {
        if (!confirm(EPT.s('recommendations.confirm_delete', 'Delete this rule?'))) return;

        try {
            const resp = await fetch(`${API}/rules/${ruleId}`, {
                method: 'DELETE',
                headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' }
            });
            if (!resp.ok) throw new Error(`HTTP ${resp.status}: ${resp.statusText}`);
            await reload();
        } catch (err) {
            alert('Error deleting rule: ' + err.message);
        }
    }

    // ── Reload ─────────────────────────────────────────────────────

    async function reload() {
        try {
            allRules = await EPT.fetchJson(`${API}/rules`);
            renderPage();
        } catch (err) {
            console.error('Failed to reload rules:', err);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────

    function escHtml(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    // Boot
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
