(function () {
    'use strict';
    var API = window.EPT_API_URL + '/manage-children';
    var root = document.getElementById('manage-children-root');
    if (!root) return;

    var state = {
        parentId: null,
        parentName: '',
        items: [],
        selected: new Set(),
        sortBy: null,
        sortDesc: false,
        loading: false
    };

    // Read parentId from URL
    var params = new URLSearchParams(window.location.search);
    state.parentId = parseInt(params.get('parentId')) || null;

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
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(data)
        }).then(function (r) {
            if (!r.ok) return r.json().then(function (e) { throw new Error(e.error || 'HTTP ' + r.status); });
            return r.json();
        });
    }

    function fmtDate(s) {
        if (!s) return '';
        var d = new Date(s);
        if (isNaN(d.getTime())) return s;
        return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    }

    // ── Render ──
    function render() {
        if (!state.parentId) {
            renderPickParent();
            return;
        }
        renderManager();
    }

    function renderPickParent() {
        root.innerHTML = '<div class="ept-page-header"><h1>Manage Child Items</h1>' +
            '<p>Select a content item to manage its children.</p></div>' +
            '<div class="ept-card"><div class="ept-card__body">' +
            '<button class="ept-btn ept-btn--primary" id="pick-parent">Browse Content Tree...</button>' +
            '</div></div>';

        document.getElementById('pick-parent').onclick = function () {
            EPT.contentPicker({ title: 'Select Parent Content' }).then(function (sel) {
                if (sel) {
                    state.parentId = sel.id;
                    state.parentName = sel.name;
                    loadChildren();
                }
            });
        };
    }

    function renderManager() {
        var selCount = state.selected.size;
        var html = '<div class="ept-page-header" style="display:flex;align-items:center;justify-content:space-between">';
        html += '<div><h1>Manage Children</h1>';
        html += '<p id="parent-info">Loading...</p></div>';
        html += '<button class="ept-btn" id="change-parent">Change Parent</button>';
        html += '</div>';

        // Toolbar
        html += '<div class="ept-toolbar" id="toolbar">';
        html += '<label class="ept-toggle"><input type="checkbox" id="select-all"> Select all</label>';
        html += '<div class="ept-toolbar__spacer"></div>';
        html += '<span id="sel-count" style="font-size:13px;color:var(--ept-text-secondary)">' +
            (selCount > 0 ? selCount + ' selected' : '') + '</span>';
        html += '<button class="ept-btn ept-btn--sm" id="btn-publish" disabled>Publish</button>';
        html += '<button class="ept-btn ept-btn--sm" id="btn-unpublish" disabled>Unpublish</button>';
        html += '<button class="ept-btn ept-btn--sm" id="btn-move" disabled>Move</button>';
        html += '<button class="ept-btn ept-btn--sm" id="btn-trash" disabled style="color:var(--ept-warning)">Move to Trash</button>';
        html += '<button class="ept-btn ept-btn--sm" id="btn-delete" disabled style="color:var(--ept-danger)">Delete Permanently</button>';
        html += '</div>';

        // Table
        html += '<div class="ept-card"><div class="ept-card__body ept-card__body--flush">';
        html += '<div style="overflow-x:auto"><table class="ept-table" id="children-table">';
        html += '<thead><tr>';
        html += '<th style="width:40px"></th>';
        html += '<th class="sortable" data-sort="name">Name</th>';
        html += '<th class="sortable" data-sort="type">Type</th>';
        html += '<th class="sortable" data-sort="status">Status</th>';
        html += '<th class="sortable" data-sort="changed">Changed</th>';
        html += '<th>Changed By</th>';
        html += '<th style="width:50px"></th>';
        html += '</tr></thead>';
        html += '<tbody id="children-body">';

        if (state.loading) {
            html += '<tr><td colspan="7"><div class="ept-loading"><div class="ept-spinner"></div></div></td></tr>';
        } else if (state.items.length === 0) {
            html += '<tr><td colspan="7"><div class="ept-empty">No children found</div></td></tr>';
        } else {
            for (var i = 0; i < state.items.length; i++) {
                var item = state.items[i];
                var checked = state.selected.has(item.contentId) ? ' checked' : '';
                var statusCls = item.status === 'Published' ? 'ept-badge--success' :
                    (item.status === 'Draft' || item.status === 'CheckedOut' ? 'ept-badge--primary' : 'ept-badge--default');

                html += '<tr data-id="' + item.contentId + '">';
                html += '<td><input type="checkbox" class="row-check" data-id="' + item.contentId + '"' + checked + '></td>';
                html += '<td><a href="' + escHtml(item.editUrl) + '" target="_blank" style="color:var(--ept-primary);text-decoration:none;font-weight:500">' + escHtml(item.name) + '</a>';
                if (item.hasChildren) html += ' <span class="ept-badge ept-badge--default" style="font-size:9px">has children</span>';
                html += '</td>';
                html += '<td><span class="ept-muted">' + escHtml(item.contentTypeName) + '</span></td>';
                html += '<td><span class="ept-badge ' + statusCls + '">' + escHtml(item.status) + '</span></td>';
                html += '<td>' + fmtDate(item.changed) + '</td>';
                html += '<td>' + escHtml(item.changedBy || '') + '</td>';
                html += '<td><a href="' + escHtml(item.editUrl) + '" target="_blank" class="ept-btn ept-btn--sm ept-btn--icon" title="Edit">' +
                    (EPT && EPT.icons ? EPT.icons.edit : 'Edit') + '</a></td>';
                html += '</tr>';
            }
        }

        html += '</tbody></table></div></div></div>';

        root.innerHTML = html;
        bindEvents();

        // Load parent info
        fetchJson(API + '/parent/' + state.parentId).then(function (info) {
            document.getElementById('parent-info').innerHTML =
                'Children of <strong>' + escHtml(info.name) + '</strong> (' + escHtml(info.contentTypeName) + ', ID: ' + info.contentId + ')';
        }).catch(function () {});
    }

    function bindEvents() {
        // Change parent
        var changeBtn = document.getElementById('change-parent');
        if (changeBtn) {
            changeBtn.onclick = function () {
                EPT.contentPicker({ title: 'Select Parent Content' }).then(function (sel) {
                    if (sel) {
                        state.parentId = sel.id;
                        state.parentName = sel.name;
                        state.selected.clear();
                        loadChildren();
                    }
                });
            };
        }

        // Select all
        var selectAll = document.getElementById('select-all');
        if (selectAll) {
            selectAll.onclick = function () {
                var checks = document.querySelectorAll('.row-check');
                for (var i = 0; i < checks.length; i++) {
                    checks[i].checked = selectAll.checked;
                    var id = parseInt(checks[i].getAttribute('data-id'));
                    if (selectAll.checked) state.selected.add(id);
                    else state.selected.delete(id);
                }
                updateToolbarState();
            };
        }

        // Row checkboxes
        var checks = document.querySelectorAll('.row-check');
        for (var i = 0; i < checks.length; i++) {
            checks[i].onclick = function () {
                var id = parseInt(this.getAttribute('data-id'));
                if (this.checked) state.selected.add(id);
                else state.selected.delete(id);
                updateToolbarState();
            };
        }

        // Sort headers
        var sortHeaders = document.querySelectorAll('.sortable');
        for (var s = 0; s < sortHeaders.length; s++) {
            sortHeaders[s].onclick = function () {
                var col = this.getAttribute('data-sort');
                if (state.sortBy === col) state.sortDesc = !state.sortDesc;
                else { state.sortBy = col; state.sortDesc = false; }
                loadChildren();
            };
        }

        // Bulk action buttons
        bindAction('btn-publish', 'publish', 'Publish ' + state.selected.size + ' items?');
        bindAction('btn-unpublish', 'unpublish', 'Unpublish ' + state.selected.size + ' items?');
        bindAction('btn-trash', 'delete', 'Move ' + state.selected.size + ' items to trash?');
        bindAction('btn-delete', 'delete-permanently', 'PERMANENTLY delete ' + state.selected.size + ' items? This cannot be undone!');

        // Move button
        var moveBtn = document.getElementById('btn-move');
        if (moveBtn) {
            moveBtn.onclick = function () {
                if (state.selected.size === 0) return;
                EPT.contentPicker({ title: 'Select Target Location' }).then(function (sel) {
                    if (!sel) return;
                    postJson(API + '/move', {
                        parentContentId: state.parentId,
                        contentIds: Array.from(state.selected),
                        targetParentId: sel.id
                    }).then(function (result) {
                        showResult(result, 'moved');
                        state.selected.clear();
                        loadChildren();
                    }).catch(function (err) { alert('Error: ' + err.message); });
                });
            };
        }
    }

    function bindAction(btnId, action, confirmMsg) {
        var btn = document.getElementById(btnId);
        if (!btn) return;
        btn.onclick = function () {
            if (state.selected.size === 0) return;
            if (!confirm(confirmMsg)) return;
            postJson(API + '/' + action, {
                parentContentId: state.parentId,
                contentIds: Array.from(state.selected)
            }).then(function (result) {
                showResult(result, action);
                state.selected.clear();
                loadChildren();
            }).catch(function (err) { alert('Error: ' + err.message); });
        };
    }

    function showResult(result, action) {
        var msg = action + ': ' + result.succeeded + ' succeeded';
        if (result.failed > 0) msg += ', ' + result.failed + ' failed';
        if (result.errors && result.errors.length > 0) msg += '\n' + result.errors.join('\n');
        alert(msg);
    }

    function updateToolbarState() {
        var count = state.selected.size;
        var countEl = document.getElementById('sel-count');
        if (countEl) countEl.textContent = count > 0 ? count + ' selected' : '';

        var btns = ['btn-publish', 'btn-unpublish', 'btn-move', 'btn-trash', 'btn-delete'];
        for (var i = 0; i < btns.length; i++) {
            var btn = document.getElementById(btns[i]);
            if (btn) btn.disabled = count === 0;
        }
    }

    function loadChildren() {
        state.loading = true;
        render();

        var url = API + '/' + state.parentId;
        if (state.sortBy) url += '?sortBy=' + state.sortBy + '&sortDesc=' + state.sortDesc;

        fetchJson(url).then(function (items) {
            state.items = items;
            state.loading = false;
            render();
        }).catch(function (err) {
            state.loading = false;
            root.innerHTML = '<div class="ept-alert ept-alert--danger">Error: ' + escHtml(err.message) + '</div>';
        });
    }

    // ── Init ──
    if (state.parentId) {
        loadChildren();
    } else {
        render();
    }
})();
