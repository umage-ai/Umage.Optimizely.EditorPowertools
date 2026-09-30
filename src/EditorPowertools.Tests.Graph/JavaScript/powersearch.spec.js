// Dependency-free specs for powersearch.js (Node's built-in runner: `node --test`).
// Driven from C# by PowerSearchJsSpecTests so `dotnet test` covers the client rules too.
'use strict';
const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const SCRIPT = process.env.EPT_POWERSEARCH_JS ||
    path.resolve(__dirname, '../../EditorPowertools.Graph/modules/_protected/EditorPowertoolsGraph/ClientResources/js/powersearch.js');
const code = fs.readFileSync(SCRIPT, 'utf8');

function element(id) {
    return {
        id, value: '', innerHTML: '', listeners: {}, disabled: false,
        addEventListener(type, fn) { (this.listeners[type] = this.listeners[type] || []).push(fn); },
        fire(type, e) { (this.listeners[type] || []).forEach(fn => fn(e || {})); },
        focus() {}, setSelectionRange() {}, hasAttribute() { return false; },
        querySelectorAll() { return []; }, querySelector() { return null; }
    };
}

/** Loads the script against a fake DOM. Returns handles to drive it and to inspect what it rendered. */
function load(hash) {
    const elements = {};
    const get = id => (elements[id] = elements[id] || element(id));
    let rootWrites = 0;
    const root = get('ept-powersearch-root');
    let rootHtml = '';
    Object.defineProperty(root, 'innerHTML', {
        get() { return rootHtml; },
        set(v) { rootHtml = v; rootWrites++; }
    });
    root.contains = () => false;

    const timers = [];
    const fetches = [];
    const sandbox = {
        console,
        URLSearchParams,
        window: { EPT_GRAPH_BASE_URL: '/graph/', EPT_BASE_URL: '/base/', addEventListener() {} },
        document: { getElementById: get, activeElement: {}, body: {} },
        location: { hash: hash || '', pathname: '/ps', search: '' },
        history: { replaceState() {} },
        EPT: {
            s: (k, f) => f,
            escHtml: s => String(s).replace(/[&<>"']/g, c => '&#' + c.charCodeAt(0) + ';'),
            safeUrl: u => u,
            timeAgo: x => String(x)
        },
        setTimeout(fn) { timers.push(fn); return timers.length; },
        clearTimeout(id) { if (id) timers[id - 1] = null; },
        fetch(url) {
            return new Promise(resolve => fetches.push({
                url,
                respond: data => resolve({ ok: true, json: () => Promise.resolve(data) })
            }));
        }
    };
    vm.runInNewContext(code, sandbox);

    return {
        get, root, fetches, sandbox,
        get rootWrites() { return rootWrites; },
        rules: sandbox.window.EPT_POWERSEARCH_RULES,
        flushTimers() { timers.splice(0).forEach(fn => fn && fn()); },
        settle: () => new Promise(r => setImmediate(r))
    };
}

const NO_FACETS = { types: [], languages: [], sections: [] };

// ---- Review Focus 5: render rules ----

test('tabBadge hides unknown (-1) and missing totals, shows known ones including zero', () => {
    const { tabBadge } = load().rules;
    const tabs = [{ tab: 'Pages', total: -1 }, { tab: 'Blocks', total: 0 }, { tab: 'Media', total: 7 }];
    assert.equal(tabBadge(tabs, 'Pages'), '');
    assert.equal(tabBadge(tabs, 'Blocks'), '0');
    assert.equal(tabBadge(tabs, 'Media'), '7');
    assert.equal(tabBadge(tabs, 'Other'), '');
    assert.equal(tabBadge(undefined, 'Pages'), '');
});

test('visibleFacets drops unselected zero counts but keeps a selected zero-count value', () => {
    const { visibleFacets } = load().rules;
    const values = [{ value: 'a', count: 3 }, { value: 'b', count: 0 }, { value: 'c', count: 0 }];
    const { shown } = visibleFacets(values, ['c'], false);
    assert.deepEqual(shown.map(v => v.value), ['a', 'c']);
});

test('visibleFacets caps at the top N but keeps selected values beyond the cap, and expands on request', () => {
    const { visibleFacets } = load().rules;
    const values = Array.from({ length: 12 }, (_, i) => ({ value: 'v' + i, count: 12 - i }));
    const capped = visibleFacets(values, ['v11'], false);
    assert.equal(capped.shown.length, 9);                 // 8 + the selected one from beyond the cap
    assert.ok(capped.shown.some(v => v.value === 'v11'));
    assert.equal(capped.hidden, 3);
    const all = visibleFacets(values, [], true);
    assert.equal(all.shown.length, 12);
    assert.equal(all.hidden, 0);
});

test('render: unknown tab total hides the badge, known shows it; unknown result total hides the summary', async () => {
    const ui = load('#q=x');
    assert.equal(ui.fetches.length, 1);
    ui.fetches[0].respond({
        state: 'ok', total: -1, nextSkip: null,
        hits: [{ name: 'Hit', typeName: 'T', language: 'en', editUrl: '/e/1', status: 'Published' }],
        tabs: [{ tab: 'Pages', total: -1 }, { tab: 'Blocks', total: 3 }],
        facets: NO_FACETS
    });
    await ui.settle();

    const tabsHtml = ui.get('ept-ps-tabs').innerHTML;
    const pages = tabsHtml.match(/data-tab="Pages"[^>]*>[^<]*(<span[^>]*>[^<]*<\/span>)?/)[0];
    const blocks = tabsHtml.match(/data-tab="Blocks"[^>]*>[^<]*(<span[^>]*>[^<]*<\/span>)?/)[0];
    assert.ok(!pages.includes('ept-badge'), 'unknown total must not render a badge');
    assert.ok(blocks.includes('ept-badge') && blocks.includes('>3<'));
    assert.ok(!ui.get('ept-ps-main').innerHTML.includes('ept-ps__summary'), 'total -1 must not render a summary');
});

test('render: a selected facet group keeps its alternatives and a selected value the response omits', async () => {
    const ui = load('#q=x&type=A&type=C');
    ui.fetches[0].respond({
        state: 'ok', total: 8, nextSkip: null, hits: [], tabs: [],
        facets: {
            types: [
                { value: 'A', label: 'Alpha', count: 5, selected: true },
                { value: 'B', label: 'Beta', count: 3, selected: false },
                { value: 'C', label: 'Gamma', count: 0, selected: true }
            ],
            languages: [], sections: []
        }
    });
    await ui.settle();

    const rail = ui.get('ept-ps-rail').innerHTML;
    assert.match(rail, /data-value="A" checked/);
    assert.match(rail, /data-value="B"(?! checked)/, 'alternative stays offered and unchecked');
    assert.match(rail, /data-value="C" checked/, 'selected zero-count value stays visible so it can be unchecked');
});

test('render: summary shows when the total is known', async () => {
    const ui = load('#q=x');
    ui.fetches[0].respond({
        state: 'ok', total: 12, nextSkip: null,
        hits: [{ name: 'Hit', editUrl: '/e/1' }], tabs: [], facets: NO_FACETS
    });
    await ui.settle();
    assert.ok(ui.get('ept-ps-main').innerHTML.includes('ept-ps__summary'));
});

// ---- typing must survive a re-render ----

test('text typed while a request is in flight survives the response render', async () => {
    const ui = load('');
    const input = ui.get('ept-ps-input');

    input.value = 'ab';
    input.fire('input', { isComposing: false });
    ui.flushTimers();                       // debounce fires: search for "ab" starts
    assert.equal(ui.fetches.length, 1);
    assert.match(ui.fetches[0].url, /q=ab/);

    input.value = 'abc';                    // typed while "ab" is still in flight
    input.fire('input', { isComposing: false });

    ui.fetches[0].respond({ state: 'ok', total: 0, nextSkip: null, hits: [], tabs: [], facets: NO_FACETS });
    await ui.settle();

    assert.equal(input.value, 'abc', 'the response render must not overwrite the live input');
    assert.equal(ui.rootWrites, 1, 'the search bar shell is built once and never re-rendered');

    ui.flushTimers();                       // pending debounce now searches for what was actually typed
    assert.match(ui.fetches[1].url, /q=abc/);
});

test('input events during IME composition do not trigger a search until composition ends', () => {
    const ui = load('');
    const input = ui.get('ept-ps-input');
    input.value = 'kana';
    input.fire('input', { isComposing: true });
    ui.flushTimers();
    assert.equal(ui.fetches.length, 0);
    input.fire('compositionend');
    ui.flushTimers();
    assert.equal(ui.fetches.length, 1);
});
