/**
 * Editor Powertools Graph - PowerSearch
 * Faceted, Graph-backed content search. Uses the core EPT helpers (EPT.s, EPT.escHtml, EPT.safeUrl, EPT.timeAgo).
 */
(function () {
    'use strict';

    var root = document.getElementById('ept-powersearch-root');
    if (!root) return;

    var API_URL = window.EPT_GRAPH_BASE_URL + 'PowerSearchApi/Search';
    var DOCTOR_URL = window.EPT_BASE_URL + 'EditorPowertools/CmsDoctor';
    var DEBOUNCE_MS = 300;
    var FACET_TOP = 8;
    var TABS = ['Pages', 'Blocks', 'Media', 'Other'];
    var GROUPS = [
        { key: 'types', param: 'type', label: 'facet_type', fallback: 'Type' },
        { key: 'languages', param: 'lang', label: 'facet_language', fallback: 'Language' },
        { key: 'sections', param: 'section', label: 'facet_section', fallback: 'Section' }
    ];

    var esc = EPT.escHtml;
    var t = function (key, fallback) { return EPT.s('powersearch.' + key, fallback); };

    var state = {
        q: '', tab: 'Pages',
        sel: { types: [], languages: [], sections: [] },
        expanded: {},          // group key -> bool
        hits: [], response: null, status: 'hint', // hint | loading | ok | unavailable | error
        loadingMore: false
    };
    var seq = 0, debounceTimer = 0;

    // ---- pure rules (small and side-effect free) ----

    /** A tab badge is shown only for a known (>= 0) total; -1 means unknown. */
    function tabBadge(tabs, tab) {
        var m = (tabs || []).filter(function (x) { return x.tab === tab; })[0];
        return (m && typeof m.total === 'number' && m.total >= 0) ? String(m.total) : '';
    }

    /** Zero counts dropped unless selected; top N unless expanded; selected values always visible. */
    function visibleFacets(values, selected, expanded) {
        var list = (values || []).filter(function (v) { return v.count > 0 || selected.indexOf(v.value) >= 0; });
        if (expanded || list.length <= FACET_TOP) return { shown: list, hidden: 0 };
        var shown = list.slice(0, FACET_TOP);
        list.slice(FACET_TOP).forEach(function (v) { if (selected.indexOf(v.value) >= 0) shown.push(v); });
        return { shown: shown, hidden: list.length - shown.length };
    }

    // ---- hash sync ----

    function readHash() {
        try {
            var p = new URLSearchParams((location.hash || '').replace(/^#/, ''));
            state.q = p.get('q') || '';
            var tab = p.get('tab');
            if (tab && TABS.indexOf(tab) >= 0) state.tab = tab;
            GROUPS.forEach(function (g) { state.sel[g.key] = p.getAll(g.param); });
        } catch (e) { /* ignore malformed hash */ }
    }

    function writeHash() {
        try {
            var p = new URLSearchParams();
            if (state.q) p.set('q', state.q);
            if (state.tab !== 'Pages') p.set('tab', state.tab);
            GROUPS.forEach(function (g) { state.sel[g.key].forEach(function (v) { p.append(g.param, v); }); });
            var h = p.toString();
            history.replaceState(null, '', location.pathname + location.search + (h ? '#' + h : ''));
        } catch (e) { /* ignore */ }
    }

    // ---- data ----

    function buildUrl(skip) {
        var p = new URLSearchParams();
        p.set('q', state.q);
        p.set('tab', state.tab);
        p.set('skip', String(skip));
        GROUPS.forEach(function (g) { state.sel[g.key].forEach(function (v) { p.append(g.param, v); }); });
        return API_URL + '?' + p.toString();
    }

    function search(append) {
        var mine = ++seq;
        if (!state.q.trim()) {
            state.status = 'hint'; state.hits = []; state.response = null; state.loadingMore = false;
            render();
            return;
        }
        var skip = append && state.response && state.response.nextSkip != null ? state.response.nextSkip : 0;
        if (append) state.loadingMore = true; else state.status = 'loading';
        render();

        fetch(buildUrl(skip), { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
            .then(function (data) {
                if (mine !== seq) return; // a newer search superseded this one
                state.loadingMore = false;
                state.response = data;
                state.hits = append ? state.hits.concat(data.hits || []) : (data.hits || []);
                state.status = data.state === 'unavailable' ? 'unavailable' : data.state === 'error' ? 'error' : 'ok';
                render();
            })
            .catch(function (err) {
                if (mine !== seq) return;
                console.error('[EditorPowertools.Graph] PowerSearch failed', err);
                state.loadingMore = false; state.status = 'error';
                render();
            });
    }

    function submit() {
        writeHash();
        search(false);
    }

    // ---- rendering ----

    // The search bar is built once and never re-rendered: rebuilding the input while a request is in
    // flight would overwrite characters typed since the debounce fired (and break IME composition).
    // Only the tab strip, facet rail and results are replaced on each render.
    var els = {};

    function buildShell() {
        root.innerHTML = '<div class="ept-card"><div class="ept-card__body">' +
            '<form class="ept-ps__searchbar" id="ept-ps-form" role="search">' +
            '<input type="search" id="ept-ps-input" class="ept-input" autocomplete="off" placeholder="' +
            esc(t('placeholder', 'Search pages, blocks and media…')) + '" aria-label="' + esc(t('search', 'Search')) + '" value="' + esc(state.q) + '" />' +
            '<button type="submit" class="ept-btn ept-btn--primary">' + esc(t('search', 'Search')) + '</button></form>' +
            '<div id="ept-ps-tabs"></div>' +
            '<div class="ept-ps__layout"><aside class="ept-ps__rail" id="ept-ps-rail"></aside>' +
            '<div class="ept-ps__main" id="ept-ps-main" aria-live="polite"></div></div>' +
            '<p class="ept-ps__note">' + esc(t('index_lag', 'Results come from the Optimizely Graph index and may lag behind recent publishing.')) + '</p>' +
            '</div></div>';
        els.input = document.getElementById('ept-ps-input');
        els.tabs = document.getElementById('ept-ps-tabs');
        els.rail = document.getElementById('ept-ps-rail');
        els.main = document.getElementById('ept-ps-main');
        bindSearchBar();
    }

    function render() {
        var focusSel = focusSelector(document.activeElement);

        els.tabs.innerHTML = renderTabs();
        els.rail.innerHTML = renderRail();
        els.main.innerHTML = renderMain();
        bindRegions();

        if (focusSel) {
            var target = root.querySelector(focusSel);
            if (target && !target.disabled) target.focus();
        }
    }

    // Identify the focused control so focus can be restored after innerHTML is rebuilt.
    function focusSelector(el) {
        if (!el || el === document.body || !root.contains(el)) return null;
        function q(v) { return String(v).replace(/\\/g, '\\\\').replace(/"/g, '\\"'); }
        if (el.id === 'ept-ps-clear' || el.id === 'ept-ps-more') return '#' + el.id;
        if (el.hasAttribute('data-tab')) return '[data-tab="' + q(el.getAttribute('data-tab')) + '"]';
        if (el.hasAttribute('data-toggle')) return '[data-toggle="' + q(el.getAttribute('data-toggle')) + '"]';
        if (el.hasAttribute('data-group')) {
            return 'input[data-group="' + q(el.getAttribute('data-group')) + '"][data-value="' + q(el.getAttribute('data-value')) + '"]';
        }
        return null;
    }

    function tabLabel(tab) {
        return t('tab_' + tab.toLowerCase(), tab);
    }

    function renderTabs() {
        var tabs = state.response ? state.response.tabs : [];
        return '<div class="ept-ps__tabs" role="tablist">' + TABS.map(function (tab) {
            var badge = tabBadge(tabs, tab);
            var on = tab === state.tab;
            return '<button type="button" role="tab" class="ept-ps__tab' + (on ? ' ept-ps__tab--active' : '') +
                '" aria-selected="' + on + '" data-tab="' + tab + '">' + esc(tabLabel(tab)) +
                (badge ? ' <span class="ept-badge ept-badge--default">' + esc(badge) + '</span>' : '') + '</button>';
        }).join('') + '</div>';
    }

    function renderRail() {
        var facets = state.response && state.response.facets;
        var anySel = GROUPS.some(function (g) { return state.sel[g.key].length > 0; });
        if (!facets && !anySel) return '';
        var out = '';
        GROUPS.forEach(function (g) {
            var values = (facets && facets[g.key]) || [];
            var selected = state.sel[g.key];
            var vis = visibleFacets(values, selected, !!state.expanded[g.key]);
            if (!vis.shown.length) return;
            out += '<fieldset class="ept-ps__facet"><legend>' + esc(t(g.label, g.fallback)) + '</legend>';
            vis.shown.forEach(function (v) {
                var checked = selected.indexOf(v.value) >= 0;
                out += '<label class="ept-ps__facetvalue"><input type="checkbox" data-group="' + g.key + '" data-value="' + esc(v.value) + '"' +
                    (checked ? ' checked' : '') + ' /> <span class="ept-ps__facetlabel">' + esc(v.label) + '</span> <span class="ept-ps__count">' + esc(String(v.count)) + '</span></label>';
            });
            if (vis.hidden > 0) {
                out += '<button type="button" class="ept-ps__link" data-toggle="' + g.key + '">' +
                    esc(t('show_more', 'Show {0} more').replace('{0}', vis.hidden)) + '</button>';
            } else if (state.expanded[g.key] && values.length > FACET_TOP) {
                out += '<button type="button" class="ept-ps__link" data-toggle="' + g.key + '">' + esc(t('show_less', 'Show less')) + '</button>';
            }
            out += '</fieldset>';
        });
        if (anySel) out += '<button type="button" class="ept-ps__link" id="ept-ps-clear">' + esc(t('clear_filters', 'Clear filters')) + '</button>';
        return out;
    }

    function message(text, cls) { return '<p class="ept-ps__message' + (cls ? ' ' + cls : '') + '">' + esc(text) + '</p>'; }

    function renderMain() {
        switch (state.status) {
            case 'hint': return message(t('hint', 'Type to search the whole site.'));
            case 'loading': return '<div class="ept-loading" role="status">…</div>';
            case 'unavailable':
                return message(t('unavailable', 'Graph search is not available. Check the Optimizely Graph configuration.')) +
                    '<p><a href="' + esc(EPT.safeUrl(DOCTOR_URL)) + '">' + esc(t('unavailable_link', 'Open CMS Doctor')) + '</a></p>';
            case 'error': return message(t('error', 'Something went wrong while searching. Please try again.'), 'ept-ps__message--error');
        }
        var r = state.response;
        if (!state.hits.length) return message(t('no_results', 'No results found.'));
        var out = '';
        if (r && typeof r.total === 'number' && r.total >= 0) {
            out += '<p class="ept-ps__summary">' + esc(t('results_count', '{0} results').replace('{0}', r.total)) + '</p>';
        }
        out += '<ul class="ept-ps__results">' + state.hits.map(renderHit).join('') + '</ul>';
        if (r && r.nextSkip != null) {
            out += '<button type="button" class="ept-btn" id="ept-ps-more"' + (state.loadingMore ? ' disabled' : '') + '>' +
                esc(t('load_more', 'Load more')) + '</button>';
        }
        return out;
    }

    function renderHit(h) {
        var meta = [h.typeName, h.language].filter(Boolean).map(esc).join(' · ');
        var badges = '';
        if (h.status) badges += ' <span class="ept-badge ept-badge--default">' + esc(h.status) + '</span>';
        if (h.isDraft) badges += ' <span class="ept-badge ept-badge--warning">' + esc(t('draft', 'Draft')) + '</span>';
        var changed = '';
        if (h.changed) {
            changed = esc(EPT.timeAgo(h.changed)) + (h.changedBy ? ' ' + esc(t('by', 'by')) + ' ' + esc(h.changedBy) : '');
        }
        return '<li class="ept-ps__hit"><a class="ept-ps__name" target="_top" href="' + esc(EPT.safeUrl(h.editUrl)) + '">' + esc(h.name || '') + '</a>' +
            '<div class="ept-ps__meta">' + meta + badges + '</div>' +
            (h.breadcrumb ? '<div class="ept-ps__breadcrumb">' + esc(h.breadcrumb) + '</div>' : '') +
            (changed ? '<div class="ept-ps__changed">' + changed + '</div>' : '') + '</li>';
    }

    // ---- events ----

    function bindSearchBar() {
        var form = document.getElementById('ept-ps-form');
        var input = els.input;
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            clearTimeout(debounceTimer);
            state.q = input.value;
            submit();
        });
        input.addEventListener('input', function (e) {
            if (e && e.isComposing) return; // wait for the IME to commit
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(function () { state.q = input.value; submit(); }, DEBOUNCE_MS);
        });
        input.addEventListener('compositionend', function () {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(function () { state.q = input.value; submit(); }, DEBOUNCE_MS);
        });
    }

    function bindRegions() {
        Array.prototype.forEach.call(els.tabs.querySelectorAll('[data-tab]'), function (b) {
            b.addEventListener('click', function () {
                if (state.tab === b.getAttribute('data-tab')) return;
                state.tab = b.getAttribute('data-tab');
                GROUPS.forEach(function (g) { state.sel[g.key] = []; }); // facets are per tab
                clearTimeout(debounceTimer);
                state.q = els.input.value;
                submit();
            });
        });
        Array.prototype.forEach.call(els.rail.querySelectorAll('input[data-group]'), function (cb) {
            cb.addEventListener('change', function () {
                var list = state.sel[cb.getAttribute('data-group')], v = cb.getAttribute('data-value');
                var i = list.indexOf(v);
                if (cb.checked && i < 0) list.push(v);
                if (!cb.checked && i >= 0) list.splice(i, 1);
                submit(); // resets skip: a fresh search starts at 0
            });
        });
        Array.prototype.forEach.call(els.rail.querySelectorAll('[data-toggle]'), function (b) {
            b.addEventListener('click', function () {
                var k = b.getAttribute('data-toggle');
                state.expanded[k] = !state.expanded[k];
                render();
            });
        });
        var clear = document.getElementById('ept-ps-clear');
        if (clear) clear.addEventListener('click', function () {
            GROUPS.forEach(function (g) { state.sel[g.key] = []; });
            submit();
        });
        var more = document.getElementById('ept-ps-more');
        if (more) more.addEventListener('click', function () { search(true); });
    }

    window.addEventListener('hashchange', function () {
        readHash();
        if (els.input && els.input.value !== state.q) els.input.value = state.q;
        search(false);
    });

    // Exposed so the Node specs (Tests.Graph/JavaScript) can pin the facet and badge rules.
    window.EPT_POWERSEARCH_RULES = { tabBadge: tabBadge, visibleFacets: visibleFacets };

    readHash();
    buildShell();
    if (state.q.trim()) search(false); else render();
    els.input.focus();
})();
