(function () {
    'use strict';
    var API = '/editorpowertools/api/cms-doctor';
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
        return fetch(url, { method: 'POST' }).then(function (r) {
            if (!r.ok) throw new Error('HTTP ' + r.status);
            return r.json();
        });
    }

    var statusConfig = {
        'OK': { icon: '&#10003;', cls: 'ept-doc-ok', badge: 'ept-badge--success', label: 'OK' },
        'Warning': { icon: '&#9888;', cls: 'ept-doc-warn', badge: 'ept-badge--warning', label: 'Warning' },
        'BadPractice': { icon: '&#9888;', cls: 'ept-doc-bad', badge: 'ept-badge--warning', label: 'Bad Practice' },
        'Fault': { icon: '&#10007;', cls: 'ept-doc-fault', badge: 'ept-badge--danger', label: 'Fault' },
        'Performance': { icon: '&#9889;', cls: 'ept-doc-perf', badge: 'ept-badge--primary', label: 'Performance' },
        'NotChecked': { icon: '&#8943;', cls: 'ept-doc-unchecked', badge: 'ept-badge--default', label: 'Not Checked' }
    };

    function render() {
        if (!state.dashboard) {
            root.innerHTML = '<div class="ept-loading"><div class="ept-spinner"></div><p>Loading...</p></div>';
            return;
        }

        var d = state.dashboard;
        var html = '';

        // Header
        html += '<div class="ept-page-header" style="display:flex;align-items:center;justify-content:space-between">';
        html += '<div><h1>CMS Doctor</h1>';
        html += '<p>Health checks for your Optimizely CMS instance. Extensible — third-party packages can add checks.</p></div>';
        html += '<div style="display:flex;gap:8px;align-items:center">';
        if (d.lastFullCheck) html += '<span class="ept-muted" style="font-size:12px">Last run: ' + new Date(d.lastFullCheck).toLocaleString() + '</span>';
        html += '<button class="ept-btn ept-btn--primary" id="run-all">' + (state.loading ? 'Running...' : 'Run All Checks') + '</button>';
        html += '</div></div>';

        // Summary stats
        html += '<div class="ept-stats">';
        html += statCard(d.totalChecks, 'Total Checks', '');
        html += statCard(d.okCount, 'Healthy', 'ept-doc-ok');
        html += statCard(d.warningCount, 'Warnings', 'ept-doc-warn');
        html += statCard(d.faultCount, 'Faults', 'ept-doc-fault');
        html += statCard(d.notCheckedCount, 'Not Checked', '');
        html += '</div>';

        // Tag filter
        var allTags = [];
        d.groups.forEach(function (g) {
            g.checks.forEach(function (c) {
                (c.tags || []).forEach(function (t) { if (allTags.indexOf(t) === -1) allTags.push(t); });
            });
        });
        if (allTags.length > 0) {
            html += '<div class="ept-toolbar">';
            html += '<span style="font-size:12px;font-weight:600;margin-right:8px">Filter by tag:</span>';
            html += '<button class="ept-btn ept-btn--sm tag-filter' + (!state.filterTag ? ' ept-btn--primary' : '') + '" data-tag="">All</button>';
            for (var t = 0; t < allTags.length; t++) {
                var active = state.filterTag === allTags[t] ? ' ept-btn--primary' : '';
                html += '<button class="ept-btn ept-btn--sm tag-filter' + active + '" data-tag="' + escHtml(allTags[t]) + '">' + escHtml(allTags[t]) + '</button>';
            }
            html += '</div>';
        }

        // Groups
        for (var g = 0; g < d.groups.length; g++) {
            var group = d.groups[g];
            var checks = group.checks;

            // Filter by tag
            if (state.filterTag) {
                checks = checks.filter(function (c) {
                    return c.tags && c.tags.indexOf(state.filterTag) >= 0;
                });
                if (checks.length === 0) continue;
            }

            html += '<div class="ept-card" style="margin-bottom:16px">';
            html += '<div class="ept-card__header"><div class="ept-card__title">' + escHtml(group.name) + '</div></div>';
            html += '<div class="ept-card__body ept-card__body--flush">';

            for (var c = 0; c < checks.length; c++) {
                html += renderCheck(checks[c]);
            }

            html += '</div></div>';
        }

        root.innerHTML = html;
        bindEvents();
    }

    function statCard(value, label, cls) {
        return '<div class="ept-stat"><div class="ept-stat__value ' + cls + '">' + value + '</div>' +
            '<div class="ept-stat__label">' + label + '</div></div>';
    }

    function renderCheck(check) {
        var sc = statusConfig[check.status] || statusConfig['NotChecked'];
        var html = '<div class="ept-doc-check ' + sc.cls + '">';

        // Status icon
        html += '<div class="ept-doc-check-icon">' + sc.icon + '</div>';

        // Content
        html += '<div class="ept-doc-check-body">';
        html += '<div class="ept-doc-check-header">';
        html += '<strong>' + escHtml(check.checkName) + '</strong>';
        html += ' <span class="ept-badge ' + sc.badge + '">' + sc.label + '</span>';
        if (check.tags) {
            for (var t = 0; t < check.tags.length; t++) {
                html += ' <span class="ept-badge ept-badge--default" style="font-size:9px">' + escHtml(check.tags[t]) + '</span>';
            }
        }
        html += '</div>';
        html += '<div class="ept-doc-check-text">' + escHtml(check.statusText) + '</div>';
        if (check.details) {
            html += '<div class="ept-doc-check-details">' + escHtml(check.details) + '</div>';
        }
        html += '</div>';

        // Actions
        html += '<div class="ept-doc-check-actions">';
        html += '<button class="ept-btn ept-btn--sm run-check" data-type="' + escHtml(check.checkType) + '">Run</button>';
        if (check.canFix && check.status !== 'OK' && check.status !== 'NotChecked') {
            html += '<button class="ept-btn ept-btn--sm fix-check" data-type="' + escHtml(check.checkType) + '" style="color:var(--ept-success)">Fix</button>';
        }
        html += '</div>';

        html += '</div>';
        return html;
    }

    function bindEvents() {
        // Run all
        var runAllBtn = document.getElementById('run-all');
        if (runAllBtn) {
            runAllBtn.onclick = function () {
                state.loading = true;
                render();
                postJson(API + '/run-all').then(function (dashboard) {
                    state.dashboard = dashboard;
                    state.loading = false;
                    render();
                }).catch(function () { state.loading = false; render(); });
            };
        }

        // Run individual
        var runBtns = document.querySelectorAll('.run-check');
        for (var i = 0; i < runBtns.length; i++) {
            runBtns[i].onclick = function () {
                var type = this.getAttribute('data-type');
                this.disabled = true;
                this.textContent = '...';
                postJson(API + '/run/' + encodeURIComponent(type)).then(function () {
                    loadDashboard();
                });
            };
        }

        // Fix
        var fixBtns = document.querySelectorAll('.fix-check');
        for (var j = 0; j < fixBtns.length; j++) {
            fixBtns[j].onclick = function () {
                var type = this.getAttribute('data-type');
                if (!confirm('Apply fix for this check?')) return;
                this.disabled = true;
                postJson(API + '/fix/' + encodeURIComponent(type)).then(function () {
                    loadDashboard();
                });
            };
        }

        // Tag filters
        var tagBtns = document.querySelectorAll('.tag-filter');
        for (var k = 0; k < tagBtns.length; k++) {
            tagBtns[k].onclick = function () {
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
        '.ept-doc-check { display:flex; align-items:flex-start; gap:12px; padding:12px 16px; border-bottom:1px solid var(--ept-border-light); }',
        '.ept-doc-check:last-child { border-bottom:none; }',
        '.ept-doc-check-icon { font-size:18px; line-height:1; min-width:24px; text-align:center; padding-top:2px; }',
        '.ept-doc-check-body { flex:1; min-width:0; }',
        '.ept-doc-check-header { display:flex; align-items:center; gap:6px; flex-wrap:wrap; margin-bottom:2px; }',
        '.ept-doc-check-text { font-size:13px; color:var(--ept-text-secondary); }',
        '.ept-doc-check-details { font-size:11px; color:var(--ept-text-muted); margin-top:2px; font-family:var(--ept-font-mono); }',
        '.ept-doc-check-actions { display:flex; gap:4px; flex-shrink:0; }',
        '.ept-doc-ok .ept-doc-check-icon { color:var(--ept-success); }',
        '.ept-doc-warn .ept-doc-check-icon { color:var(--ept-warning); }',
        '.ept-doc-bad .ept-doc-check-icon { color:var(--ept-warning); }',
        '.ept-doc-fault .ept-doc-check-icon { color:var(--ept-danger); }',
        '.ept-doc-perf .ept-doc-check-icon { color:var(--ept-primary); }',
        '.ept-doc-unchecked .ept-doc-check-icon { color:var(--ept-text-muted); }'
    ].join('\n');
    document.head.appendChild(style);

    // Init
    loadDashboard();
})();
