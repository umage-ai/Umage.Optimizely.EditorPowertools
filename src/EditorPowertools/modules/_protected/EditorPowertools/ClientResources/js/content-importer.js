(function () {
    'use strict';
    var API = window.EPT_BASE_URL + 'ContentImporterApi';
    var root = document.getElementById('content-importer-root');
    if (!root) return;

    var state = {
        step: 1,
        sessionId: null,
        fileName: null,
        fileType: null,
        columns: [],
        sampleRows: [],
        totalRowCount: 0,
        contentTypes: [],
        blockTypes: [],
        languages: [],
        targetContentTypeId: null,
        targetContentType: null,
        parentContentId: null,
        parentContentName: '',
        language: '',
        publishAfterImport: false,
        nameSourceColumn: null,
        mappings: [],
        dryRunResult: null,
        importProgress: null,
        pollTimer: null
    };

    // ── Helpers ──
    function escHtml(s) {
        if (!s && s !== 0) return '';
        var d = document.createElement('div');
        d.textContent = String(s);
        return d.innerHTML;
    }

    function fetchJson(url) {
        return fetch(url).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    function postJson(url, data) {
        return fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Requested-With': 'XMLHttpRequest' },
            body: JSON.stringify(data)
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (e) { throw new Error(e.error || 'HTTP ' + r.status); });
            return r.json();
        });
    }

    // ── Render ──
    function render() {
        switch (state.step) {
            case 1: renderUpload(); break;
            case 2: renderPreview(); break;
            case 3: renderTarget(); break;
            case 4: renderMapping(); break;
            case 5: renderDryRun(); break;
            case 6: renderExecute(); break;
        }
    }

    function renderStepBar() {
        var steps = [
            EPT.s('contentimporter.step_upload', '1. Upload File'),
            EPT.s('contentimporter.step_preview', '2. Preview'),
            EPT.s('contentimporter.step_target', '3. Target'),
            EPT.s('contentimporter.step_mapping', '4. Mapping'),
            EPT.s('contentimporter.step_dryrun', '5. Dry Run'),
            EPT.s('contentimporter.step_import', '6. Import')
        ];
        var html = '<div class="ept-importer-steps">';
        for (var i = 0; i < steps.length; i++) {
            var cls = 'ept-importer-step';
            if (i + 1 === state.step) cls += ' ept-importer-step--active';
            else if (i + 1 < state.step) cls += ' ept-importer-step--done';
            html += '<div class="' + cls + '"><span class="ept-importer-step-num">' + (i + 1) + '</span> ' + steps[i] + '</div>';
            if (i < steps.length - 1) html += '<div class="ept-importer-step-line"></div>';
        }
        html += '</div>';
        return html;
    }

    // ── Step 1: Upload ──
    function renderUpload() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__body">';
        html += '<div class="ept-importer-dropzone" id="dropzone">';
        html += '<div style="text-align:center;padding:40px">';
        html += '<p style="font-size:16px;font-weight:600;margin-bottom:8px">' + EPT.s('contentimporter.lbl_dropfile', 'Drop a CSV, JSON, or Excel file here, or click to browse') + '</p>';
        html += '<p style="color:var(--ept-text-muted)">' + EPT.s('contentimporter.lbl_supportedformats', 'Supported formats: CSV, XLSX, JSON') + '</p>';
        html += '<input type="file" id="file-input" accept=".csv,.tsv,.xlsx,.xls,.json" style="display:none">';
        html += '<button class="ept-btn ept-btn--primary" id="browse-btn" style="margin-top:16px">' + EPT.s('contentimporter.btn_browse', 'Browse...') + '</button>';
        html += '</div></div>';
        html += '<div id="upload-status"></div>';
        html += '</div></div>';
        root.innerHTML = html;

        var dropzone = document.getElementById('dropzone');
        var fileInput = document.getElementById('file-input');
        var browseBtn = document.getElementById('browse-btn');

        browseBtn.onclick = function () { fileInput.click(); };
        fileInput.onchange = function () { if (fileInput.files.length) uploadFile(fileInput.files[0]); };

        dropzone.ondragover = function (e) { e.preventDefault(); dropzone.style.borderColor = 'var(--ept-primary)'; };
        dropzone.ondragleave = function () { dropzone.style.borderColor = ''; };
        dropzone.ondrop = function (e) {
            e.preventDefault();
            dropzone.style.borderColor = '';
            if (e.dataTransfer.files.length) uploadFile(e.dataTransfer.files[0]);
        };
    }

    function uploadFile(file) {
        var status = document.getElementById('upload-status');
        status.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>' + EPT.s('contentimporter.lbl_uploading', 'Uploading and parsing {0}...').replace('{0}', escHtml(file.name)) + '</p></div>';

        var formData = new FormData();
        formData.append('file', file);

        fetch(API + '/Upload', { method: 'POST', headers: { 'X-Requested-With': 'XMLHttpRequest' }, body: formData })
            .then(function (r) {
                if (!r.ok) return r.json().then(function (e) { throw new Error(e.error || 'Upload failed'); });
                return r.json();
            })
            .then(function (data) {
                state.sessionId = data.sessionId;
                state.fileName = data.fileName;
                state.fileType = data.fileType;
                state.columns = data.columns;
                state.sampleRows = data.sampleRows;
                state.totalRowCount = data.totalRowCount;
                state.step = 2;
                render();
            })
            .catch(function (err) {
                status.innerHTML = '<div class="ept-alert ept-alert--danger">Error: ' + escHtml(err.message) + '</div>';
            });
    }

    // ── Step 2: Preview ──
    function renderPreview() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__header"><div class="ept-card__title">' +
            escHtml(state.fileName) + ' &middot; ' + EPT.s('contentimporter.lbl_rows', '{0} rows').replace('{0}', state.totalRowCount) + ' &middot; ' +
            EPT.s('contentimporter.lbl_columns', '{0} columns').replace('{0}', state.columns.length) + '</div></div>';
        html += '<div class="ept-card__body ept-card__body--flush">';

        // Data table
        var cols = state.columns.map(function (c) { return c.name; });
        html += '<div style="overflow-x:auto"><table class="ept-table"><thead><tr>';
        for (var c = 0; c < cols.length; c++) {
            html += '<th>' + escHtml(cols[c]) + '</th>';
        }
        html += '</tr></thead><tbody>';
        for (var r = 0; r < state.sampleRows.length; r++) {
            html += '<tr>';
            for (var c2 = 0; c2 < cols.length; c2++) {
                var val = state.sampleRows[r][cols[c2]] || '';
                html += '<td>' + escHtml(val.length > 60 ? val.substring(0, 60) + '...' : val) + '</td>';
            }
            html += '</tr>';
        }
        html += '</tbody></table></div>';

        html += '</div></div>';
        html += renderNav(1, 3);
        root.innerHTML = html;
        bindNav();
    }

    // ── Step 3: Target ──
    function renderTarget() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__header"><div class="ept-card__title">' + EPT.s('contentimporter.lbl_selecttarget', 'Select Target') + '</div></div>';
        html += '<div class="ept-card__body">';

        // Content type filter
        html += '<div class="ept-importer-field"><label>' + EPT.s('contentimporter.lbl_contenttype', 'Content Type:') + '</label>';
        html += '<div style="display:flex;gap:8px;margin-bottom:8px">';
        html += '<button class="ept-btn ept-btn--sm ct-filter" data-filter="Page">' + EPT.s('contentimporter.filter_pages', 'Pages') + '</button>';
        html += '<button class="ept-btn ept-btn--sm ct-filter" data-filter="Block">' + EPT.s('contentimporter.filter_blocks', 'Blocks') + '</button>';
        html += '<button class="ept-btn ept-btn--sm ct-filter" data-filter="">' + EPT.s('contentimporter.filter_all', 'All') + '</button>';
        html += '</div>';
        html += '<select class="ept-select" id="ct-select" style="width:100%"><option value="">' + EPT.s('contentimporter.opt_selectcontenttype', '-- Select content type --') + '</option></select>';
        html += '</div>';

        // Parent content
        html += '<div class="ept-importer-field"><label>' + EPT.s('contentimporter.lbl_parent', 'Parent Location:') + '</label>';
        html += '<div style="display:flex;gap:8px;align-items:center">';
        html += '<span id="parent-name" style="font-size:13px">' + (state.parentContentName ? escHtml(state.parentContentName) + ' (ID: ' + state.parentContentId + ')' : '<em style="color:var(--ept-text-muted)">' + EPT.s('contentimporter.lbl_noparent', 'No parent selected') + '</em>') + '</span>';
        html += '<button class="ept-btn ept-btn--sm" id="pick-parent-btn">' + EPT.s('contentimporter.btn_browse', 'Browse...') + '</button>';
        html += '</div></div>';

        // Language
        html += '<div class="ept-importer-field"><label>' + EPT.s('contentimporter.lbl_language', 'Language:') + '</label>';
        html += '<select class="ept-select" id="lang-select" style="width:100%"><option value="">Loading...</option></select>';
        html += '</div>';

        // Publish option
        html += '<div class="ept-importer-field"><label class="ept-toggle">';
        html += '<input type="checkbox" id="publish-check"' + (state.publishAfterImport ? ' checked' : '') + '>';
        html += ' ' + EPT.s('contentimporter.lbl_publishafter', 'Publish after import') + '</label></div>';

        html += '</div></div>';
        html += renderNav(2, 4);
        root.innerHTML = html;
        bindNav();

        // Load content types and languages
        loadContentTypes('Page');
        loadLanguages();

        // Filter buttons
        var filterBtns = document.querySelectorAll('.ct-filter');
        for (var i = 0; i < filterBtns.length; i++) {
            filterBtns[i].onclick = function () { loadContentTypes(this.getAttribute('data-filter')); };
        }

        // Parent content picker
        document.getElementById('pick-parent-btn').onclick = function () {
            EPT.contentPicker({ title: 'Select Parent Content' }).then(function (selected) {
                if (selected) {
                    state.parentContentId = selected.id;
                    state.parentContentName = selected.name;
                    document.getElementById('parent-name').innerHTML =
                        escHtml(selected.name) + ' (ID: ' + selected.id + ')';
                }
            });
        };
    }

    function loadContentTypes(filter) {
        var url = API + '/GetContentTypes' + (filter ? '?filter=' + filter : '');
        fetchJson(url).then(function (types) {
            state.contentTypes = types;
            var select = document.getElementById('ct-select');
            var html = '<option value="">' + EPT.s('contentimporter.opt_selectcontenttype', '-- Select content type --') + '</option>';
            for (var i = 0; i < types.length; i++) {
                var sel = types[i].id === state.targetContentTypeId ? ' selected' : '';
                html += '<option value="' + types[i].id + '"' + sel + '>' + escHtml(types[i].displayName) + ' (' + types[i].baseType + ')</option>';
            }
            select.innerHTML = html;
        });
    }

    function loadLanguages() {
        fetchJson(API + '/GetLanguages').then(function (langs) {
            state.languages = langs;
            var select = document.getElementById('lang-select');
            var html = '';
            for (var i = 0; i < langs.length; i++) {
                var sel = langs[i] === state.language ? ' selected' : '';
                html += '<option value="' + langs[i] + '"' + sel + '>' + langs[i] + '</option>';
            }
            select.innerHTML = html;
            if (!state.language && langs.length) {
                state.language = langs[0];
                select.value = langs[0];
            }
        });
    }

    function saveTargetState() {
        var ctSelect = document.getElementById('ct-select');
        var langSelect = document.getElementById('lang-select');
        var publishCheck = document.getElementById('publish-check');

        state.targetContentTypeId = ctSelect ? parseInt(ctSelect.value) || null : null;
        // parentContentId is set by the content picker
        state.language = langSelect ? langSelect.value : '';
        state.publishAfterImport = publishCheck ? publishCheck.checked : false;
    }

    // ── Step 4: Mapping ──
    function renderMapping() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__header"><div class="ept-card__title">' + EPT.s('contentimporter.lbl_mapproperties', 'Map Properties') + '</div></div>';
        html += '<div class="ept-card__body" id="mapping-body"><div class="ept-loading"><div class="ept-spinner"></div><p>' + EPT.s('contentimporter.lbl_loadingproperties', 'Loading properties...') + '</p></div></div></div>';
        html += renderNav(3, 5);
        root.innerHTML = html;
        bindNav();

        fetchJson(API + '/GetContentType/' + state.targetContentTypeId).then(function (ct) {
            state.targetContentType = ct;
            renderMappingFields(ct);
        });
    }

    function renderMappingFields(ct) {
        var body = document.getElementById('mapping-body');
        var cols = state.columns.map(function (c) { return c.name; });
        var html = '';

        var customProps = ct.properties.filter(function (p) { return !p.isBuiltIn; });
        var builtInProps = ct.properties.filter(function (p) { return p.isBuiltIn; });

        // Name mapping
        html += '<div class="ept-importer-mapping-row">';
        html += '<div class="ept-importer-mapping-label"><strong>' + EPT.s('contentimporter.lbl_contentname', 'Content Name') + '</strong> <span class="ept-badge ept-badge--danger">' + EPT.s('contentimporter.lbl_required', 'required') + '</span></div>';
        html += '<select class="ept-select" id="name-col">';
        html += '<option value="">' + EPT.s('contentimporter.opt_selectcolumn', '-- Select column --') + '</option>';
        for (var c = 0; c < cols.length; c++) {
            var sel = state.nameSourceColumn === cols[c] ? ' selected' : '';
            html += '<option value="' + escHtml(cols[c]) + '"' + sel + '>' + escHtml(cols[c]) + '</option>';
        }
        html += '</select>';
        html += '<div class="ept-importer-hint">Or use template: <code>{Column1} - {Column2}</code></div>';
        html += '</div>';

        html += '<hr style="margin:12px 0;border:none;border-top:1px solid var(--ept-border-light)">';

        // Custom property mappings
        html += renderPropertyMappings(customProps, cols);

        // Built-in properties section
        if (builtInProps.length > 0) {
            html += '<details style="margin-top:12px"><summary style="cursor:pointer;font-size:13px;font-weight:600;color:var(--ept-text-secondary);padding:6px 0">' + EPT.s('contentimporter.lbl_builtinprops', 'Built-in Properties') + '</summary>';
            html += renderPropertyMappings(builtInProps, cols);
            html += '</details>';
        }

        body.innerHTML = html;

        // Bind mapping type changes
        var typeSelects = body.querySelectorAll('.mapping-type');
        for (var t = 0; t < typeSelects.length; t++) {
            typeSelects[t].onchange = onMappingTypeChange;
            // Trigger for existing values
            onMappingTypeChange.call(typeSelects[t]);
        }
    }

    function renderPropertyMappings(props, cols) {
        var html = '';
        for (var p = 0; p < props.length; p++) {
            var prop = props[p];
            var existing = state.mappings.find(function (m) { return m.targetProperty === prop.name; });

            html += '<div class="ept-importer-mapping-row" data-prop="' + escHtml(prop.name) + '">';
            html += '<div class="ept-importer-mapping-label">';
            html += escHtml(prop.displayName);
            html += ' <span class="ept-badge ept-badge--default">' + escHtml(prop.typeName) + '</span>';
            if (prop.isRequired) html += ' <span class="ept-badge ept-badge--danger">' + EPT.s('contentimporter.lbl_required', 'required') + '</span>';
            if (prop.isBuiltIn) html += ' <span class="ept-badge ept-badge--warning">' + EPT.s('contentimporter.lbl_builtin', 'built-in') + '</span>';
            html += '</div>';

            html += '<select class="ept-select mapping-type" data-prop="' + escHtml(prop.name) + '">';
            html += '<option value="skip"' + (!existing || existing.mappingType === 'skip' ? ' selected' : '') + '>' + EPT.s('contentimporter.opt_skip2', 'Skip') + '</option>';
            html += '<option value="column"' + (existing && existing.mappingType === 'column' ? ' selected' : '') + '>' + EPT.s('contentimporter.opt_mapcolumn', 'Map to column') + '</option>';
            html += '<option value="hardcoded"' + (existing && existing.mappingType === 'hardcoded' ? ' selected' : '') + '>' + EPT.s('contentimporter.opt_setvalue', 'Set value') + '</option>';
            if (prop.isContentArea) {
                html += '<option value="inline-block"' + (existing && existing.mappingType === 'inline-block' ? ' selected' : '') + '>' + EPT.s('contentimporter.opt_inlineblocks', 'Create inline blocks') + '</option>';
            }
            html += '</select>';

            html += '<select class="ept-select mapping-col" data-prop="' + escHtml(prop.name) + '" style="display:none">';
            html += '<option value="">' + EPT.s('contentimporter.opt_selectcolumn2', '-- Column --') + '</option>';
            for (var c2 = 0; c2 < cols.length; c2++) {
                var sel2 = existing && existing.sourceColumn === cols[c2] ? ' selected' : '';
                html += '<option value="' + escHtml(cols[c2]) + '"' + sel2 + '>' + escHtml(cols[c2]) + '</option>';
            }
            html += '</select>';

            if (prop.isBoolean) {
                var isChecked = existing && existing.hardcodedValue === 'true' ? ' checked' : '';
                html += '<label class="ept-toggle mapping-bool" data-prop="' + escHtml(prop.name) + '" style="display:none"><input type="checkbox" class="mapping-bool-check" data-prop="' + escHtml(prop.name) + '"' + isChecked + '> ' + EPT.s('contentimporter.lbl_settotrue', 'Set to true') + '</label>';
            }
            html += '<input class="ept-importer-input mapping-val" data-prop="' + escHtml(prop.name) + '" placeholder="' + EPT.s('contentimporter.lbl_valuetemplate', 'Value or {ColumnName} template') + '" style="display:none" value="' + escHtml((existing && existing.hardcodedValue) || '') + '">';
            html += '<div class="ept-importer-hint mapping-val-hint" data-prop="' + escHtml(prop.name) + '" style="display:none">Use <code>{ColumnName}</code> to insert column values</div>';

            // Inline block section with add button
            html += '<div class="mapping-blocks-container" data-prop="' + escHtml(prop.name) + '" style="display:none">';
            html += '<div class="mapping-blocks-list" data-prop="' + escHtml(prop.name) + '"></div>';
            html += '<button class="ept-btn ept-btn--sm mapping-add-block" data-prop="' + escHtml(prop.name) + '">' + EPT.s('contentimporter.btn_addblock', '+ Add block') + '</button>';
            html += '</div>';

            html += '</div>';
        }
        return html;
    }

    function onMappingTypeChange() {
        var prop = this.getAttribute('data-prop');
        var type = this.value;
        var row = this.closest('.ept-importer-mapping-row');

        var colSel = row.querySelector('.mapping-col');
        var valInput = row.querySelector('.mapping-val');
        var valHint = row.querySelector('.mapping-val-hint');
        var boolLabel = row.querySelector('.mapping-bool');
        var blocksContainer = row.querySelector('.mapping-blocks-container');

        colSel.style.display = type === 'column' ? '' : 'none';
        // For booleans, show checkbox instead of text input
        var hasBool = boolLabel !== null;
        if (hasBool) {
            boolLabel.style.display = type === 'hardcoded' ? '' : 'none';
            valInput.style.display = 'none';
            if (valHint) valHint.style.display = 'none';
        } else {
            valInput.style.display = type === 'hardcoded' ? '' : 'none';
            if (valHint) valHint.style.display = type === 'hardcoded' ? '' : 'none';
        }
        if (blocksContainer) blocksContainer.style.display = type === 'inline-block' ? '' : 'none';

        if (type === 'inline-block' && blocksContainer) {
            // Bind add block button
            var addBtn = blocksContainer.querySelector('.mapping-add-block');
            if (addBtn && !addBtn._bound) {
                addBtn._bound = true;
                addBtn.onclick = function () { addInlineBlock(prop); };
            }
            // Add first block if empty
            var list = blocksContainer.querySelector('.mapping-blocks-list');
            if (list && list.children.length === 0) {
                addInlineBlock(prop);
            }
        }
    }

    var _blockTypesCache = null;

    function addInlineBlock(propName) {
        var list = document.querySelector('.mapping-blocks-list[data-prop="' + propName + '"]');
        if (!list) return;

        var blockIndex = list.children.length;
        var wrapper = document.createElement('div');
        wrapper.className = 'ept-importer-block-entry';
        wrapper.setAttribute('data-block-index', blockIndex);
        wrapper.innerHTML = '<div style="display:flex;align-items:center;gap:8px;margin-bottom:4px">' +
            '<strong style="font-size:11px">' + EPT.s('contentimporter.lbl_block', 'Block {0}').replace('{0}', blockIndex + 1) + '</strong>' +
            '<select class="ept-select mapping-block-type" style="font-size:12px"></select>' +
            '<button class="ept-btn ept-btn--sm remove-block-btn" style="color:var(--ept-danger)">&times;</button>' +
            '</div>' +
            '<div class="mapping-block-props"></div>';
        list.appendChild(wrapper);

        // Remove button
        wrapper.querySelector('.remove-block-btn').onclick = function () {
            list.removeChild(wrapper);
        };

        // Load block types
        var select = wrapper.querySelector('.mapping-block-type');
        loadBlockTypesIntoSelect(select, propName, blockIndex);
    }

    function loadBlockTypesIntoSelect(select, propName, blockIndex) {
        var loadTypes = _blockTypesCache
            ? Promise.resolve(_blockTypesCache)
            : fetchJson(API + '/GetBlockTypes').then(function (types) { _blockTypesCache = types; return types; });

        loadTypes.then(function (types) {
            var html = '<option value="">' + EPT.s('contentimporter.opt_selectblocktype', '-- Select block type --') + '</option>';
            for (var i = 0; i < types.length; i++) {
                html += '<option value="' + types[i].id + '">' + escHtml(types[i].displayName) + '</option>';
            }
            select.innerHTML = html;

            select.onchange = function () {
                var blockTypeId = parseInt(this.value);
                if (!blockTypeId) return;
                var propsContainer = select.closest('.ept-importer-block-entry').querySelector('.mapping-block-props');
                loadBlockTypePropertiesInto(propsContainer, propName, blockIndex, blockTypeId);
            };
        });
    }

    function loadBlockTypePropertiesInto(container, propName, blockIndex, blockTypeId) {
        fetchJson(API + '/GetContentType/' + blockTypeId).then(function (bt) {
            var cols = state.columns.map(function (c) { return c.name; });
            var bid = propName + '-' + blockIndex;
            var html = '<div style="margin-top:4px;padding:8px;background:var(--ept-bg);border-radius:4px;font-size:11px">';

            for (var p = 0; p < bt.properties.length; p++) {
                var prop = bt.properties[p];
                html += '<div class="ept-importer-mapping-row" style="margin-bottom:4px">';
                html += '<span>' + escHtml(prop.displayName) + '</span> ';
                html += '<select class="ept-select block-mapping-type" data-bid="' + escHtml(bid) + '" data-prop="' + escHtml(prop.name) + '" style="font-size:11px;padding:2px 4px">';
                html += '<option value="skip">Skip</option>';
                html += '<option value="column">Column</option>';
                html += '<option value="hardcoded">Value / {template}</option>';
                html += '</select> ';
                html += '<select class="ept-select block-mapping-col" data-bid="' + escHtml(bid) + '" data-prop="' + escHtml(prop.name) + '" style="display:none;font-size:11px;padding:2px 4px">';
                html += '<option value="">--</option>';
                for (var c = 0; c < cols.length; c++) {
                    html += '<option value="' + escHtml(cols[c]) + '">' + escHtml(cols[c]) + '</option>';
                }
                html += '</select>';
                html += '<input class="ept-importer-input block-mapping-val" data-bid="' + escHtml(bid) + '" data-prop="' + escHtml(prop.name) + '" style="display:none;font-size:11px;padding:2px 4px" placeholder="Value or {Column}">';
                html += '</div>';
            }
            html += '</div>';
            container.innerHTML = html;

            var selects = container.querySelectorAll('.block-mapping-type');
            for (var s = 0; s < selects.length; s++) {
                selects[s].onchange = function () {
                    var brow = this.closest('.ept-importer-mapping-row');
                    brow.querySelector('.block-mapping-col').style.display = this.value === 'column' ? '' : 'none';
                    brow.querySelector('.block-mapping-val').style.display = this.value === 'hardcoded' ? '' : 'none';
                };
            }
        });
    }

    function collectMappings() {
        var mappings = [];
        var rows = document.querySelectorAll('.mapping-type');
        for (var i = 0; i < rows.length; i++) {
            var propName = rows[i].getAttribute('data-prop');
            var type = rows[i].value;
            if (type === 'skip') continue;

            var mapping = { targetProperty: propName, mappingType: type };

            if (type === 'column') {
                var colSel = document.querySelector('.mapping-col[data-prop="' + propName + '"]');
                mapping.sourceColumn = colSel ? colSel.value : '';
            } else if (type === 'hardcoded') {
                var boolCheck = document.querySelector('.mapping-bool-check[data-prop="' + propName + '"]');
                if (boolCheck) {
                    mapping.hardcodedValue = boolCheck.checked ? 'true' : 'false';
                } else {
                    var valInput = document.querySelector('.mapping-val[data-prop="' + propName + '"]');
                    mapping.hardcodedValue = valInput ? valInput.value : '';
                }
            } else if (type === 'inline-block') {
                // Collect all block entries for this property
                var blockEntries = document.querySelectorAll('.mapping-blocks-list[data-prop="' + propName + '"] .ept-importer-block-entry');
                var inlineBlocks = [];
                for (var be = 0; be < blockEntries.length; be++) {
                    var entry = blockEntries[be];
                    var blockTypeSel = entry.querySelector('.mapping-block-type');
                    var blockTypeId = blockTypeSel ? parseInt(blockTypeSel.value) : 0;
                    if (!blockTypeId) continue;

                    var bid = propName + '-' + be;
                    var blockMappings = [];
                    var blockRows = entry.querySelectorAll('.block-mapping-type');
                    for (var b = 0; b < blockRows.length; b++) {
                        var bprop = blockRows[b].getAttribute('data-prop');
                        var btype = blockRows[b].value;
                        if (btype === 'skip') continue;
                        var bm = { targetProperty: bprop, mappingType: btype };
                        if (btype === 'column') {
                            var bcolSel = entry.querySelector('.block-mapping-col[data-prop="' + bprop + '"]');
                            bm.sourceColumn = bcolSel ? bcolSel.value : '';
                        } else if (btype === 'hardcoded') {
                            var bvalInput = entry.querySelector('.block-mapping-val[data-prop="' + bprop + '"]');
                            bm.hardcodedValue = bvalInput ? bvalInput.value : '';
                        }
                        blockMappings.push(bm);
                    }
                    inlineBlocks.push({ blockTypeId: blockTypeId, mappings: blockMappings });
                }
                mapping.inlineBlocks = inlineBlocks;
            }
            mappings.push(mapping);
        }

        var nameCol = document.getElementById('name-col');
        state.nameSourceColumn = nameCol ? nameCol.value : null;
        state.mappings = mappings;
    }

    // ── Step 5: Dry Run ──
    function renderDryRun() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__header"><div class="ept-card__title">Import Preview</div></div>';
        html += '<div class="ept-card__body" id="dryrun-body"><div class="ept-loading"><div class="ept-spinner"></div><p>Running preview...</p></div></div></div>';
        html += renderNav(4, 6, EPT.s('contentimporter.btn_import', 'Start Import'));
        root.innerHTML = html;
        bindNav();

        var request = {
            sessionId: state.sessionId,
            targetContentTypeId: state.targetContentTypeId,
            parentContentId: state.parentContentId,
            language: state.language,
            publishAfterImport: state.publishAfterImport,
            nameSourceColumn: state.nameSourceColumn,
            mappings: state.mappings
        };

        postJson(API + '/DryRun', request)
            .then(function (result) {
                state.dryRunResult = result;
                var body = document.getElementById('dryrun-body');
                var html = '<div style="margin-bottom:12px"><strong>' + result.totalCount + '</strong> items will be imported</div>';

                if (result.errors && result.errors.length > 0) {
                    html += '<div class="ept-alert ept-alert--warning">';
                    for (var e = 0; e < result.errors.length; e++) {
                        html += '<div>' + escHtml(result.errors[e]) + '</div>';
                    }
                    html += '</div>';
                }

                html += '<div style="overflow-x:auto"><table class="ept-table"><thead><tr>';
                html += '<th>#</th><th>Name</th>';
                if (result.previewItems.length > 0) {
                    var props = Object.keys(result.previewItems[0].properties || {});
                    for (var p = 0; p < props.length; p++) {
                        html += '<th>' + escHtml(props[p]) + '</th>';
                    }
                }
                html += '</tr></thead><tbody>';

                for (var i = 0; i < result.previewItems.length; i++) {
                    var item = result.previewItems[i];
                    html += '<tr><td>' + item.rowIndex + '</td><td>' + escHtml(item.name) + '</td>';
                    if (props) {
                        for (var p2 = 0; p2 < props.length; p2++) {
                            var val = item.properties[props[p2]] || '';
                            html += '<td>' + escHtml(val.length > 50 ? val.substring(0, 50) + '...' : val) + '</td>';
                        }
                    }
                    html += '</tr>';
                }
                html += '</tbody></table></div>';

                body.innerHTML = html;
            })
            .catch(function (err) {
                document.getElementById('dryrun-body').innerHTML =
                    '<div class="ept-alert ept-alert--danger">Error: ' + escHtml(err.message) + '</div>';
            });
    }

    // ── Step 6: Execute ──
    function renderExecute() {
        var html = renderStepBar();
        html += '<div class="ept-card"><div class="ept-card__header"><div class="ept-card__title">' + EPT.s('contentimporter.lbl_importing', 'Importing...') + '</div></div>';
        html += '<div class="ept-card__body" id="exec-body"><div class="ept-loading"><div class="ept-spinner"></div><p>Starting import...</p></div></div></div>';
        root.innerHTML = html;

        postJson(API + '/Execute', { sessionId: state.sessionId })
            .then(function () {
                pollProgress();
            })
            .catch(function (err) {
                document.getElementById('exec-body').innerHTML =
                    '<div class="ept-alert ept-alert--danger">Error: ' + escHtml(err.message) + '</div>';
            });
    }

    function pollProgress() {
        state.pollTimer = setInterval(function () {
            fetchJson(API + '/GetProgress/' + state.sessionId)
                .then(function (progress) {
                    state.importProgress = progress;
                    renderProgress(progress);
                    if (progress.status !== 'running') {
                        clearInterval(state.pollTimer);
                    }
                })
                .catch(function () {
                    clearInterval(state.pollTimer);
                });
        }, 1000);
    }

    function renderProgress(progress) {
        var body = document.getElementById('exec-body');
        var pct = progress.total > 0 ? Math.round(progress.processed / progress.total * 100) : 0;

        var html = '<div class="ept-importer-progress">';
        html += '<div class="ept-importer-progress-bar"><div class="ept-importer-progress-fill" style="width:' + pct + '%"></div></div>';
        html += '<div style="margin-top:8px;font-size:14px"><strong>' + progress.processed + '</strong> / ' + progress.total + ' items (' + pct + '%)</div>';

        if (progress.status === 'completed') {
            html += '<div class="ept-alert ept-alert--success" style="margin-top:12px">Import completed! Created ' + progress.createdContentIds.length + ' content items.</div>';
            html += '<button class="ept-btn ept-btn--primary" id="new-import-btn" style="margin-top:8px">' + EPT.s('contentimporter.btn_startover', 'Start Over') + '</button>';
        } else if (progress.status === 'failed') {
            html += '<div class="ept-alert ept-alert--danger" style="margin-top:12px">Import failed.</div>';
        }

        if (progress.errors && progress.errors.length > 0) {
            html += '<div style="margin-top:12px"><strong>Errors (' + progress.errors.length + '):</strong></div>';
            html += '<div style="max-height:200px;overflow-y:auto;font-size:12px;margin-top:4px">';
            for (var e = 0; e < progress.errors.length; e++) {
                html += '<div style="padding:2px 0;color:var(--ept-danger)">Row ' + progress.errors[e].rowIndex + ': ' + escHtml(progress.errors[e].message) + '</div>';
            }
            html += '</div>';
        }

        html += '</div>';
        body.innerHTML = html;

        var newBtn = document.getElementById('new-import-btn');
        if (newBtn) {
            newBtn.onclick = function () {
                state.step = 1;
                state.sessionId = null;
                render();
            };
        }
    }

    // ── Navigation ──
    function renderNav(prevStep, nextStep, nextLabel) {
        var html = '<div class="ept-importer-nav">';
        if (prevStep) html += '<button class="ept-btn" id="nav-back">' + EPT.s('contentimporter.btn_back', 'Back') + '</button>';
        html += '<div style="flex:1"></div>';
        if (nextStep) html += '<button class="ept-btn ept-btn--primary" id="nav-next">' + (nextLabel || EPT.s('contentimporter.btn_next', 'Next')) + '</button>';
        html += '</div>';
        return html;
    }

    function bindNav() {
        var backBtn = document.getElementById('nav-back');
        var nextBtn = document.getElementById('nav-next');

        if (backBtn) {
            backBtn.onclick = function () {
                state.step--;
                render();
            };
        }
        if (nextBtn) {
            nextBtn.onclick = function () {
                // Validate and save state before advancing
                if (state.step === 3) {
                    saveTargetState();
                    if (!state.targetContentTypeId) {
                        alert('Please select a content type');
                        return;
                    }
                    if (!state.parentContentId) {
                        alert('Please enter a parent content ID');
                        return;
                    }
                }
                if (state.step === 4) {
                    collectMappings();
                }
                state.step++;
                render();
            };
        }
    }

    // ── CSS ──
    var style = document.createElement('style');
    style.textContent = [
        '.ept-importer-steps { display:flex; align-items:center; gap:4px; margin-bottom:20px; flex-wrap:wrap; }',
        '.ept-importer-step { display:flex; align-items:center; gap:6px; font-size:12px; color:var(--ept-text-muted); white-space:nowrap; }',
        '.ept-importer-step--active { color:var(--ept-primary); font-weight:600; }',
        '.ept-importer-step--done { color:var(--ept-success); }',
        '.ept-importer-step-num { width:22px; height:22px; border-radius:50%; display:flex; align-items:center; justify-content:center; font-size:11px; font-weight:600; background:var(--ept-surface-active); }',
        '.ept-importer-step--active .ept-importer-step-num { background:var(--ept-primary); color:#fff; }',
        '.ept-importer-step--done .ept-importer-step-num { background:var(--ept-success); color:#fff; }',
        '.ept-importer-step-line { flex:0 0 20px; height:2px; background:var(--ept-border); }',
        '.ept-importer-dropzone { border:2px dashed var(--ept-border); border-radius:8px; cursor:pointer; transition:border-color .15s; }',
        '.ept-importer-dropzone:hover { border-color:var(--ept-primary); }',
        '.ept-importer-field { margin-bottom:16px; }',
        '.ept-importer-field label { display:block; font-size:13px; font-weight:600; margin-bottom:4px; }',
        '.ept-importer-input { padding:6px 10px; border:1px solid var(--ept-border); border-radius:var(--ept-radius); font-size:13px; width:100%; outline:none; font-family:var(--ept-font); }',
        '.ept-importer-input:focus { border-color:var(--ept-primary); box-shadow:0 0 0 3px var(--ept-primary-light); }',
        '.ept-importer-mapping-row { display:flex; align-items:center; gap:8px; padding:6px 0; border-bottom:1px solid var(--ept-border-light); flex-wrap:wrap; }',
        '.ept-importer-mapping-label { min-width:200px; font-size:13px; }',
        '.ept-importer-nav { display:flex; align-items:center; margin-top:16px; gap:8px; }',
        '.ept-importer-hint { font-size:11px; color:var(--ept-text-muted); margin-top:2px; width:100%; }',
        '.ept-importer-hint code { background:var(--ept-surface-active); padding:1px 4px; border-radius:3px; font-size:10px; }',
        '.ept-importer-block-entry { padding:8px; margin-bottom:8px; border:1px solid var(--ept-border-light); border-radius:4px; }',
        '.ept-importer-progress-bar { height:8px; background:var(--ept-surface-active); border-radius:4px; overflow:hidden; }',
        '.ept-importer-progress-fill { height:100%; background:var(--ept-primary); border-radius:4px; transition:width .3s; }'
    ].join('\n');
    document.head.appendChild(style);

    // ── Init ──
    render();
})();
