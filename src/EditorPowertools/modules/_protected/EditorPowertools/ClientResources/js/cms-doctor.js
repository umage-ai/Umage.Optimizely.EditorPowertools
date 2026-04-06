(function () {
    'use strict';
    var API = window.EPT_API_URL + '/cms-doctor';
    var root = document.getElementById('cms-doctor-root');
    if (!root) return;

    var state = { dashboard: null, loading: false, filterTag: null };

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

    function postJson(url) {
        return fetch(url, { method: 'POST', headers: { 'X-Requested-With': 'XMLHttpRequest' } }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    var statusConfig = {
        'OK':          { color: '#2e7d32', bg: '#e8f5e9', border: '#a5d6a7', icon: '\u2713', label: EPT.s('cmsdoctor.status_ok', 'Healthy') },
        'Warning':     { color: '#ef6c00', bg: '#fff3e0', border: '#ffcc80', icon: '\u26A0', label: EPT.s('cmsdoctor.status_warning', 'Warning') },
        'BadPractice': { color: '#e65100', bg: '#fff3e0', border: '#ffcc80', icon: '\u26A0', label: EPT.s('cmsdoctor.status_badpractice', 'Bad Practice') },
        'Fault':       { color: '#c62828', bg: '#ffebee', border: '#ef9a9a', icon: '\u2717', label: EPT.s('cmsdoctor.status_fault', 'Fault') },
        'Performance': { color: '#1565c0', bg: '#e3f2fd', border: '#90caf9', icon: '\u26A1', label: EPT.s('cmsdoctor.status_performance', 'Performance') },
        'NotChecked':  { color: '#757575', bg: '#f5f5f5', border: '#e0e0e0', icon: '\u2026', label: EPT.s('cmsdoctor.status_notchecked', 'Not Checked') }
    };

    function render() {
        if (!state.dashboard) {
            root.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>' + EPT.s('shared.loading', 'Loading...') + '</p></div>';
            return;
        }

        var d = state.dashboard;
        var html = '';

        // Header
        html += '<div class="ept-page-header" style="display:flex;align-items:center;justify-content:space-between">';
        html += '<div><h1>' + EPT.s('cmsdoctor.header_title', 'CMS Doctor') + ' <button class="ept-help-btn" data-ept-help="cmsdoctor" title="' + EPT.s('help.helpbtn', 'Help') + '">?</button></h1>';
        html += '<p>' + EPT.s('cmsdoctor.header_desc', 'Health checks for your Optimizely CMS. Extensible by third-party packages.') + '</p></div>';
        html += '<div style="display:flex;gap:8px;align-items:center">';
        if (d.lastFullCheck) html += '<span class="ept-muted" style="font-size:12px">' + EPT.s('cmsdoctor.lbl_lastrun', 'Last run: {0}').replace('{0}', new Date(d.lastFullCheck).toLocaleString()) + '</span>';
        html += '<button class="ept-btn ept-btn--primary" id="run-all">' + (state.loading ? EPT.s('cmsdoctor.btn_running', 'Running...') : EPT.s('cmsdoctor.btn_runall', 'Run All Checks')) + '</button>';
        html += '</div></div>';

        // Summary bar
        html += '<div class="doc-summary">';
        html += summaryPill(d.okCount, EPT.s('cmsdoctor.sum_healthy', 'Healthy'), 'OK');
        html += summaryPill(d.warningCount, EPT.s('cmsdoctor.sum_warnings', 'Warnings'), 'Warning');
        html += summaryPill(d.faultCount, EPT.s('cmsdoctor.sum_faults', 'Faults'), 'Fault');
        html += summaryPill(d.notCheckedCount, EPT.s('cmsdoctor.sum_notchecked', 'Not Checked'), 'NotChecked');
        html += '</div>';

        // Tag filter
        var allTags = [];
        d.groups.forEach(function (g) {
            g.checks.forEach(function (c) {
                (c.tags || []).forEach(function (t) { if (allTags.indexOf(t) === -1) allTags.push(t); });
            });
        });
        if (allTags.length > 0) {
            html += '<div class="doc-tags">';
            html += '<button class="doc-tag' + (!state.filterTag ? ' doc-tag--active' : '') + '" data-tag="">' + EPT.s('cmsdoctor.tag_all', 'All') + '</button>';
            for (var t = 0; t < allTags.length; t++) {
                html += '<button class="doc-tag' + (state.filterTag === allTags[t] ? ' doc-tag--active' : '') + '" data-tag="' + escHtml(allTags[t]) + '">' + escHtml(allTags[t]) + '</button>';
            }
            html += '</div>';
        }

        // Groups with cards
        for (var g = 0; g < d.groups.length; g++) {
            var group = d.groups[g];
            var checks = group.checks;

            if (state.filterTag) {
                checks = checks.filter(function (c) {
                    return c.tags && c.tags.indexOf(state.filterTag) >= 0;
                });
                if (checks.length === 0) continue;
            }

            html += '<div class="doc-group">';
            html += '<h2 class="doc-group-title">' + escHtml(group.name) + '</h2>';
            html += '<div class="doc-cards">';

            for (var c = 0; c < checks.length; c++) {
                html += renderCard(checks[c]);
            }

            html += '</div></div>';
        }

        root.innerHTML = html;
        bindEvents();
    }

    function summaryPill(count, label, status) {
        var sc = statusConfig[status];
        return '<div class="doc-pill" style="background:' + sc.bg + ';border-color:' + sc.border + ';color:' + sc.color + '">' +
            '<span class="doc-pill-count">' + count + '</span> ' + label + '</div>';
    }

    function renderCard(check) {
        var sc = statusConfig[check.status] || statusConfig['NotChecked'];
        var dismissed = check.isDismissed ? ' doc-card--dismissed' : '';
        var html = '<div class="doc-card' + dismissed + '" style="border-left:4px solid ' + sc.border + ';background:' + sc.bg + '" data-type="' + escHtml(check.checkType) + '">';

        html += '<div class="doc-card-header">';
        html += '<span class="doc-card-icon" style="color:' + sc.color + '">' + sc.icon + '</span>';
        html += '<div class="doc-card-title">' + escHtml(check.checkName) + '</div>';
        html += '<span class="doc-card-badge" style="background:' + sc.color + '">' + sc.label + '</span>';
        html += '</div>';

        html += '<div class="doc-card-text">' + escHtml(check.statusText) + '</div>';

        // Tags + timestamp
        html += '<div class="doc-card-meta">';
        if (check.tags && check.tags.length > 0) {
            for (var i = 0; i < check.tags.length; i++) {
                html += '<span class="doc-card-tag">' + escHtml(check.tags[i]) + '</span>';
            }
        }
        if (check.checkTime && check.status !== 'NotChecked') {
            html += '<span class="doc-card-time">' + new Date(check.checkTime).toLocaleString() + '</span>';
        }
        html += '</div>';

        // Actions
        html += '<div class="doc-card-actions">';
        html += '<button class="doc-card-btn run-check" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_run', 'Run') + '</button>';
        if (check.canFix && check.status !== 'OK' && check.status !== 'NotChecked') {
            html += '<button class="doc-card-btn doc-card-btn--fix fix-check" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_fix', 'Fix') + '</button>';
        }
        html += '<button class="doc-card-btn doc-card-btn--detail detail-check" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_details', 'Details') + '</button>';
        if (check.isDismissed) {
            html += '<button class="doc-card-btn restore-check" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_restore', 'Restore') + '</button>';
        } else if (check.status !== 'OK' && check.status !== 'NotChecked') {
            html += '<button class="doc-card-btn dismiss-check" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_dismiss', 'Dismiss') + '</button>';
        }
        html += '</div>';

        html += '</div>';
        return html;
    }

    function showDetailDialog(check) {
        var sc = statusConfig[check.status] || statusConfig['NotChecked'];

        var overlay = document.createElement('div');
        overlay.className = 'ept-dialog-backdrop';

        var html = '<div class="ept-dialog" style="max-width:600px">';
        html += '<div class="ept-dialog__header" style="border-left:4px solid ' + sc.color + '">';
        html += '<div class="ept-dialog__title">' + sc.icon + ' ' + escHtml(check.checkName) + '</div>';
        html += '<button class="ept-dialog__close">&times;</button>';
        html += '</div>';
        html += '<div class="ept-dialog__body">';

        // Status
        html += '<div style="margin-bottom:16px">';
        html += '<span class="doc-card-badge" style="background:' + sc.color + ';font-size:13px;padding:4px 12px">' + sc.label + '</span>';
        html += '</div>';

        // Message
        html += '<div style="margin-bottom:12px"><strong>' + EPT.s('cmsdoctor.dlg_result', 'Result:') + '</strong><br>' + escHtml(check.statusText) + '</div>';

        // Details
        if (check.details) {
            html += '<div style="margin-bottom:12px"><strong>' + EPT.s('cmsdoctor.dlg_details', 'Details:') + '</strong><br>';
            html += '<div style="font-family:var(--ept-font-mono);font-size:12px;background:#f5f5f5;padding:10px;border-radius:4px;white-space:pre-wrap">' + escHtml(check.details) + '</div></div>';
        }

        // Tags
        if (check.tags && check.tags.length > 0) {
            html += '<div style="margin-bottom:12px"><strong>' + EPT.s('cmsdoctor.dlg_categories', 'Categories:') + '</strong> ';
            for (var i = 0; i < check.tags.length; i++) {
                html += '<span class="doc-card-tag">' + escHtml(check.tags[i]) + '</span> ';
            }
            html += '</div>';
        }

        // Timestamp
        if (check.checkTime) {
            html += '<div class="ept-muted" style="font-size:11px">' + EPT.s('cmsdoctor.dlg_checked', 'Checked: {0}').replace('{0}', new Date(check.checkTime).toLocaleString()) + '</div>';
        }

        // Actions
        html += '<div style="margin-top:16px;display:flex;gap:8px;justify-content:flex-end">';
        if (check.canFix && check.status !== 'OK' && check.status !== 'NotChecked') {
            html += '<button class="ept-btn" id="dialog-fix" data-type="' + escHtml(check.checkType) + '" style="color:var(--ept-success)">' + EPT.s('cmsdoctor.btn_applyfix', 'Apply Fix') + '</button>';
        }
        html += '<button class="ept-btn" id="dialog-run" data-type="' + escHtml(check.checkType) + '">' + EPT.s('cmsdoctor.btn_rerun', 'Re-run Check') + '</button>';
        html += '<button class="ept-btn" id="dialog-close">' + EPT.s('cmsdoctor.btn_close', 'Close') + '</button>';
        html += '</div>';

        html += '</div></div>';
        overlay.innerHTML = html;
        document.body.appendChild(overlay);

        overlay.querySelector('.ept-dialog__close').onclick = function () { document.body.removeChild(overlay); };
        overlay.querySelector('#dialog-close').onclick = function () { document.body.removeChild(overlay); };
        overlay.onclick = function (e) { if (e.target === overlay) document.body.removeChild(overlay); };

        var runBtn = overlay.querySelector('#dialog-run');
        if (runBtn) {
            runBtn.onclick = function () {
                runBtn.textContent = EPT.s('cmsdoctor.btn_running', 'Running...');
                runBtn.disabled = true;
                postJson(API + '/run/' + encodeURIComponent(check.checkType)).then(function (result) {
                    document.body.removeChild(overlay);
                    loadDashboard();
                    // Re-open with updated result
                    setTimeout(function () {
                        var updated = findCheck(result.checkType);
                        if (updated) showDetailDialog(updated);
                    }, 300);
                });
            };
        }

        var fixBtn = overlay.querySelector('#dialog-fix');
        if (fixBtn) {
            fixBtn.onclick = function () {
                if (!confirm(EPT.s('cmsdoctor.confirm_applyfix', 'Apply fix for this check?'))) return;
                fixBtn.textContent = EPT.s('cmsdoctor.btn_fixing', 'Fixing...');
                fixBtn.disabled = true;
                postJson(API + '/fix/' + encodeURIComponent(check.checkType)).then(function () {
                    document.body.removeChild(overlay);
                    loadDashboard();
                });
            };
        }
    }

    function findCheck(checkType) {
        if (!state.dashboard) return null;
        for (var g = 0; g < state.dashboard.groups.length; g++) {
            for (var c = 0; c < state.dashboard.groups[g].checks.length; c++) {
                if (state.dashboard.groups[g].checks[c].checkType === checkType)
                    return state.dashboard.groups[g].checks[c];
            }
        }
        return null;
    }

    function bindEvents() {
        document.getElementById('run-all').onclick = function () {
            state.loading = true;
            render();
            postJson(API + '/run-all').then(function (dashboard) {
                state.dashboard = dashboard;
                state.loading = false;
                render();
            }).catch(function () { state.loading = false; render(); });
        };

        var runBtns = document.querySelectorAll('.run-check');
        for (var i = 0; i < runBtns.length; i++) {
            runBtns[i].onclick = function (e) {
                e.stopPropagation();
                var type = this.getAttribute('data-type');
                this.textContent = '...';
                this.disabled = true;
                postJson(API + '/run/' + encodeURIComponent(type)).then(function () { loadDashboard(); });
            };
        }

        var fixBtns = document.querySelectorAll('.fix-check');
        for (var j = 0; j < fixBtns.length; j++) {
            fixBtns[j].onclick = function (e) {
                e.stopPropagation();
                var type = this.getAttribute('data-type');
                if (!confirm(EPT.s('cmsdoctor.confirm_fix', 'Apply fix?'))) return;
                this.disabled = true;
                postJson(API + '/fix/' + encodeURIComponent(type)).then(function () { loadDashboard(); });
            };
        }

        var detailBtns = document.querySelectorAll('.detail-check');
        for (var k = 0; k < detailBtns.length; k++) {
            detailBtns[k].onclick = function (e) {
                e.stopPropagation();
                var check = findCheck(this.getAttribute('data-type'));
                if (check) showDetailDialog(check);
            };
        }

        var dismissBtns = document.querySelectorAll('.dismiss-check');
        for (var d = 0; d < dismissBtns.length; d++) {
            dismissBtns[d].onclick = function (e) {
                e.stopPropagation();
                postJson(API + '/dismiss/' + encodeURIComponent(this.getAttribute('data-type'))).then(function () { loadDashboard(); });
            };
        }

        var restoreBtns = document.querySelectorAll('.restore-check');
        for (var r = 0; r < restoreBtns.length; r++) {
            restoreBtns[r].onclick = function (e) {
                e.stopPropagation();
                postJson(API + '/restore/' + encodeURIComponent(this.getAttribute('data-type'))).then(function () { loadDashboard(); });
            };
        }

        var tagBtns = document.querySelectorAll('.doc-tag');
        for (var t = 0; t < tagBtns.length; t++) {
            tagBtns[t].onclick = function () {
                state.filterTag = this.getAttribute('data-tag') || null;
                render();
            };
        }
    }

    function loadDashboard() {
        fetchJson(API + '/dashboard').then(function (d) {
            state.dashboard = d;
            render();
        });
    }

    // CSS
    var style = document.createElement('style');
    style.textContent = [
        '.doc-summary { display:flex; gap:10px; margin-bottom:20px; flex-wrap:wrap; }',
        '.doc-pill { display:flex; align-items:center; gap:6px; padding:8px 16px; border-radius:8px; border:1px solid; font-size:13px; font-weight:500; }',
        '.doc-pill-count { font-size:20px; font-weight:700; }',

        '.doc-tags { display:flex; gap:4px; margin-bottom:16px; flex-wrap:wrap; }',
        '.doc-tag { padding:4px 10px; border:1px solid var(--ept-border); border-radius:12px; font-size:11px; cursor:pointer; background:var(--ept-surface); color:var(--ept-text-secondary); transition:all .15s; }',
        '.doc-tag:hover { border-color:var(--ept-primary); color:var(--ept-primary); }',
        '.doc-tag--active { background:var(--ept-primary); color:#fff; border-color:var(--ept-primary); }',

        '.doc-group { margin-bottom:24px; }',
        '.doc-group-title { font-size:16px; font-weight:600; margin-bottom:12px; color:var(--ept-text-secondary); }',
        '.doc-cards { display:grid; grid-template-columns:repeat(auto-fill, minmax(320px, 1fr)); gap:12px; }',

        '.doc-card { background:#fff; border-radius:8px; padding:16px; box-shadow:0 1px 3px rgba(0,0,0,.08); transition:box-shadow .15s, transform .15s; cursor:default; }',
        '.doc-card:hover { box-shadow:0 3px 8px rgba(0,0,0,.12); transform:translateY(-1px); }',
        '.doc-card-header { display:flex; align-items:center; gap:8px; margin-bottom:8px; }',
        '.doc-card-icon { font-size:20px; line-height:1; }',
        '.doc-card-title { flex:1; font-weight:600; font-size:14px; }',
        '.doc-card-badge { color:#fff; font-size:10px; font-weight:600; padding:2px 8px; border-radius:10px; text-transform:uppercase; letter-spacing:.3px; }',
        '.doc-card-text { font-size:12px; color:#555; line-height:1.4; margin-bottom:8px; }',
        '.doc-card-meta { display:flex; gap:4px; margin-bottom:8px; flex-wrap:wrap; align-items:center; }',
        '.doc-card-tag { font-size:10px; padding:1px 6px; border-radius:8px; background:rgba(0,0,0,.06); color:#666; }',
        '.doc-card-time { font-size:10px; color:#999; margin-left:auto; }',
        '.doc-card-actions { display:flex; gap:4px; }',
        '.doc-card-btn { padding:3px 10px; border:1px solid #ddd; border-radius:4px; font-size:11px; cursor:pointer; background:#fff; color:#555; transition:all .15s; }',
        '.doc-card-btn:hover { border-color:#999; color:#333; }',
        '.doc-card-btn--fix { color:var(--ept-success); border-color:var(--ept-success); }',
        '.doc-card-btn--fix:hover { background:var(--ept-success); color:#fff; }',
        '.doc-card-btn--detail { color:var(--ept-primary); border-color:var(--ept-primary); }',
        '.doc-card-btn--detail:hover { background:var(--ept-primary); color:#fff; }',
        '.doc-card--dismissed { opacity:.45; }'
    ].join('\n');
    document.head.appendChild(style);

    loadDashboard();
})();
