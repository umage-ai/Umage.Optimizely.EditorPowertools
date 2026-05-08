/**
 * Editor Powertools — Submissions Timeline
 * Cross-form view of recent submissions, expandable per-row to show data,
 * filterable by form (dropdown), with deep-links to the form's data view.
 */
(function () {
    'use strict';

    const TIMELINE_URL = window.EPT_FORMS_BASE_URL + 'FormsApi/GetSubmissionsTimeline';
    const FORMS_URL    = window.EPT_FORMS_BASE_URL + 'FormsApi/GetFormChoices';
    const STREAM_URL   = window.EPT_FORMS_BASE_URL + 'FormsApi/SubmissionsStream';
    const root = document.getElementById('ept-submissions-timeline-root');
    if (!root) return;

    const state = {
        events: [],
        formChoices: [],
        days: 30,
        top: 200,
        formGuid: '',                 // empty = all forms
        finalizedOnly: false,
        expanded: new Set(),          // submissionIds whose details panel is open
        detailsCache: new Map(),      // submissionId -> SubmissionEventDto with .data
        flashIds: new Set(),          // submissionIds to highlight on next render
        live: true,                   // SSE on/off
    };

    let eventSource = null;

    EPT.showLoading(root);
    init();

    async function init() {
        try {
            const [events, choices] = await Promise.all([
                fetchEvents(),
                EPT.fetchJson(FORMS_URL).catch(() => [])
            ]);
            state.events = events || [];
            state.formChoices = choices || [];
            render();
            openLiveStream();
        } catch (err) {
            console.error('[EditorPowertools.Forms] Failed to load timeline', err);
            root.innerHTML = `<div class="ept-card"><div class="ept-card__body"><p style="color:var(--ept-danger)">${EPT.s('submissionstimeline.error_load', 'Failed to load submissions timeline.')}</p></div></div>`;
        }
    }

    function openLiveStream() {
        if (eventSource) { try { eventSource.close(); } catch (e) {} eventSource = null; }
        if (!state.live) return;
        try {
            eventSource = new EventSource(STREAM_URL);
            eventSource.addEventListener('submission', (e) => {
                try {
                    const ev = JSON.parse(e.data);
                    if (!ev || !ev.submissionId) return;
                    // Form filter applies to live updates too.
                    if (state.formGuid && state.formGuid !== ev.formGuid) return;
                    // Dedup: if we already have this submissionId, replace it
                    // (a finalize event arriving after a step event).
                    const idx = state.events.findIndex(x => x.submissionId === ev.submissionId);
                    if (idx >= 0) state.events[idx] = ev;
                    else state.events.unshift(ev);
                    state.flashIds.add(ev.submissionId);
                    setTimeout(() => state.flashIds.delete(ev.submissionId), 2000);
                    render();
                } catch (err) {
                    console.warn('[EditorPowertools.Forms] Bad SSE payload', err);
                }
            });
            eventSource.onerror = () => {
                // Browser auto-reconnects; just log.
                console.debug('[EditorPowertools.Forms] SSE reconnecting…');
            };
        } catch (err) {
            console.warn('[EditorPowertools.Forms] SSE not available', err);
        }
    }

    function fetchEvents() {
        const params = new URLSearchParams();
        params.set('top', String(state.top));
        params.set('days', String(state.days));
        if (state.formGuid) params.set('formGuid', state.formGuid);
        return EPT.fetchJson(TIMELINE_URL + '?' + params.toString());
    }

    async function reload() {
        EPT.showLoading(root);
        state.detailsCache.clear();
        state.expanded.clear();
        try {
            state.events = await fetchEvents();
            render();
        } catch (err) {
            console.error('[EditorPowertools.Forms] Reload failed', err);
            render();
        }
    }

    function render() {
        const events = applyFinalizedFilter(state.events);
        root.innerHTML = '';

        const card = document.createElement('div');
        card.className = 'ept-card';

        // Toolbar
        const header = document.createElement('div');
        header.className = 'ept-card__header ept-forms-toolbar';
        header.innerHTML = `
            <div class="ept-forms-toolbar__counts">
                <strong>${events.length}</strong> ${EPT.s('submissionstimeline.events', 'submissions')}
            </div>
            <div class="ept-forms-toolbar__filters">
                <select id="ept-tl-form" class="ept-input" title="${EPT.s('submissionstimeline.form_filter_tip', 'Filter by form')}">
                    <option value="">${EPT.s('submissionstimeline.all_forms', 'All forms')}</option>
                    ${state.formChoices.map(f => `<option value="${escAttr(f.formGuid)}" ${state.formGuid === f.formGuid ? 'selected' : ''}>${esc(f.name)}</option>`).join('')}
                </select>
                <select id="ept-tl-days" class="ept-input">
                    <option value="7">${EPT.s('submissionstimeline.range_7', 'Last 7 days')}</option>
                    <option value="30">${EPT.s('submissionstimeline.range_30', 'Last 30 days')}</option>
                    <option value="90">${EPT.s('submissionstimeline.range_90', 'Last 90 days')}</option>
                    <option value="365">${EPT.s('submissionstimeline.range_365', 'Last year')}</option>
                </select>
                <label class="ept-checkbox">
                    <input type="checkbox" id="ept-tl-finalized" ${state.finalizedOnly ? 'checked' : ''} />
                    ${EPT.s('submissionstimeline.finalized_only', 'Finalized only')}
                </label>
                <label class="ept-checkbox" title="${EPT.s('submissionstimeline.live_tip', 'Stream new submissions as they arrive')}">
                    <input type="checkbox" id="ept-tl-live" ${state.live ? 'checked' : ''} />
                    <span class="ept-live-indicator${state.live ? ' is-on' : ''}">●</span>
                    ${EPT.s('submissionstimeline.live', 'Live')}
                </label>
            </div>
        `;
        card.appendChild(header);

        // Timeline body
        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        if (events.length === 0) {
            body.innerHTML = `<p class="ept-empty-state">${EPT.s('submissionstimeline.empty', 'No submissions found in the selected range.')}</p>`;
        } else {
            body.appendChild(buildTimeline(events));
        }
        card.appendChild(body);
        root.appendChild(card);

        // Wiring
        document.getElementById('ept-tl-form').addEventListener('change', e => {
            state.formGuid = e.target.value || '';
            reload();
        });
        const daysEl = document.getElementById('ept-tl-days');
        daysEl.value = String(state.days);
        daysEl.addEventListener('change', e => { state.days = parseInt(e.target.value, 10) || 30; reload(); });
        document.getElementById('ept-tl-finalized').addEventListener('change', e => {
            state.finalizedOnly = e.target.checked;
            render();
        });
        document.getElementById('ept-tl-live').addEventListener('change', e => {
            state.live = e.target.checked;
            openLiveStream();
            render();
        });
    }

    function applyFinalizedFilter(list) {
        return state.finalizedOnly ? list.filter(e => e.finalized) : list;
    }

    function buildTimeline(events) {
        const wrap = document.createElement('div');
        wrap.className = 'ept-timeline';

        let currentDay = '';
        for (const ev of events) {
            const d = new Date(ev.submittedUtc);
            const day = d.toLocaleDateString();
            if (day !== currentDay) {
                currentDay = day;
                const h = document.createElement('div');
                h.className = 'ept-timeline__day';
                h.textContent = day;
                wrap.appendChild(h);
            }
            wrap.appendChild(buildItem(ev, d));
            if (state.expanded.has(ev.submissionId)) {
                wrap.appendChild(buildDetails(ev));
            }
        }
        return wrap;
    }

    function buildItem(ev, d) {
        const item = document.createElement('div');
        item.className = 'ept-timeline__item' +
            (ev.finalized ? ' is-finalized' : ' is-partial') +
            (state.flashIds.has(ev.submissionId) ? ' is-new' : '');

        const time = document.createElement('div');
        time.className = 'ept-timeline__time';
        time.textContent = d.toLocaleTimeString();
        time.title = d.toISOString();
        item.appendChild(time);

        const body = document.createElement('div');
        body.className = 'ept-timeline__body';
        body.innerHTML = `
            <a href="${escAttr(ev.formEditUrl || '#')}" class="ept-link">${esc(ev.formName || '(unnamed)')}</a>
            ${ev.finalized
                ? `<span class="ept-badge ept-badge--success">${EPT.s('submissionstimeline.finalized', 'finalized')}</span>`
                : `<span class="ept-badge ept-badge--neutral">${EPT.s('submissionstimeline.partial', 'partial')}</span>`}
            ${ev.submittedBy ? `<span class="ept-muted ept-small">— ${esc(ev.submittedBy)}</span>` : ''}
            ${ev.language ? `<span class="ept-muted ept-small">[${esc(ev.language)}]</span>` : ''}
        `;
        item.appendChild(body);

        const actions = document.createElement('div');
        actions.className = 'ept-timeline__actions';

        const isOpen = state.expanded.has(ev.submissionId);
        const detailsBtn = document.createElement('button');
        detailsBtn.type = 'button';
        detailsBtn.className = 'ept-btn ept-btn--sm';
        detailsBtn.title = EPT.s('submissionstimeline.tip_details', 'Show submission data');
        detailsBtn.innerHTML = `${isOpen ? '▴' : '▾'} ${EPT.s('submissionstimeline.btn_details', 'Details')}`;
        detailsBtn.addEventListener('click', () => toggleDetails(ev));
        actions.appendChild(detailsBtn);

        const viewBtn = document.createElement('a');
        viewBtn.className = 'ept-btn ept-btn--sm';
        viewBtn.href = ev.submissionViewUrl || ev.formEditUrl || '#';
        viewBtn.title = EPT.s('submissionstimeline.tip_view', 'Open this form\'s submission view in CMS');
        viewBtn.innerHTML = '↗ ' + EPT.s('submissionstimeline.btn_view', 'View');
        actions.appendChild(viewBtn);

        item.appendChild(actions);
        return item;
    }

    async function toggleDetails(ev) {
        const id = ev.submissionId;
        if (state.expanded.has(id)) {
            state.expanded.delete(id);
            render();
            return;
        }
        state.expanded.add(id);
        if (!state.detailsCache.has(id)) {
            // Fetch the timeline again with includeData=true filtered to this form
            try {
                const params = new URLSearchParams();
                params.set('top', '500');
                params.set('days', String(state.days));
                params.set('formGuid', ev.formGuid);
                params.set('includeData', 'true');
                const events = await EPT.fetchJson(TIMELINE_URL + '?' + params.toString());
                for (const e of (events || [])) {
                    if (e.submissionId) state.detailsCache.set(e.submissionId, e);
                }
            } catch (err) {
                console.error('[EditorPowertools.Forms] Details fetch failed', err);
            }
        }
        render();
    }

    function buildDetails(ev) {
        const wrap = document.createElement('div');
        wrap.className = 'ept-timeline__item ept-timeline__details-wrap';

        const enriched = state.detailsCache.get(ev.submissionId);
        const fields = enriched && enriched.fields ? enriched.fields : null;

        const card = document.createElement('div');
        card.className = 'ept-submission-card';

        if (!enriched) {
            card.innerHTML = `<div class="ept-submission-card__loading">${EPT.s('submissionstimeline.loading_details', 'Loading details…')}</div>`;
            wrap.appendChild(card);
            return wrap;
        }

        // Use the enriched event for everything — even meta — so live updates
        // (which arrive through the SSE pipe and may be richer) win out.
        const e = enriched;
        const submitted = new Date(e.submittedUtc);
        const ago = humanAgo(submitted);

        // Header band: form name + finalized state + submission id
        const header = document.createElement('div');
        header.className = 'ept-submission-card__header';
        header.innerHTML = `
            <div class="ept-submission-card__title">
                <a href="${escAttr(e.formEditUrl || '#')}" class="ept-link">${esc(e.formName || '(unnamed)')}</a>
                ${e.finalized
                    ? `<span class="ept-badge ept-badge--success">${EPT.s('submissionstimeline.finalized', 'finalized')}</span>`
                    : `<span class="ept-badge ept-badge--neutral">${EPT.s('submissionstimeline.partial', 'partial')}</span>`}
            </div>
            <div class="ept-submission-card__id" title="${EPT.s('submissionstimeline.submission_id', 'Submission ID')}">
                ${esc(e.submissionId || '—')}
            </div>
        `;
        card.appendChild(header);

        // Metadata strip: when, who, language, page
        const meta = document.createElement('div');
        meta.className = 'ept-submission-card__meta';
        meta.appendChild(metaCell(
            EPT.s('submissionstimeline.meta_when', 'Submitted'),
            `${escAttr(submitted.toLocaleString())}<span class="ept-muted ept-small"> · ${esc(ago)}</span>`,
            'clock'
        ));
        meta.appendChild(metaCell(
            EPT.s('submissionstimeline.meta_user', 'User'),
            esc(e.submittedBy || EPT.s('submissionstimeline.anonymous', 'anonymous')),
            'user'
        ));
        if (e.language) {
            meta.appendChild(metaCell(
                EPT.s('submissionstimeline.meta_language', 'Language'),
                esc(e.language),
                'globe'
            ));
        }
        if (e.hostedPageUrl) {
            meta.appendChild(metaCell(
                EPT.s('submissionstimeline.meta_page', 'Page'),
                `<span title="${escAttr(e.hostedPageUrl)}">${esc(shortenUrl(e.hostedPageUrl))}</span>`,
                'page'
            ));
        }
        card.appendChild(meta);

        // Field grid
        const body = document.createElement('div');
        body.className = 'ept-submission-card__body';
        if (!fields || fields.length === 0) {
            body.innerHTML = `<p class="ept-muted">${EPT.s('submissionstimeline.no_data', 'No data captured for this submission.')}</p>`;
        } else {
            body.appendChild(buildFieldGrid(fields));
        }
        card.appendChild(body);

        wrap.appendChild(card);
        return wrap;
    }

    function metaCell(label, valueHtml, iconKind) {
        const cell = document.createElement('div');
        cell.className = 'ept-submission-card__meta-cell';
        cell.innerHTML = `
            <span class="ept-submission-card__meta-icon">${metaSvg(iconKind)}</span>
            <span class="ept-submission-card__meta-text">
                <span class="ept-submission-card__meta-label">${esc(label)}</span>
                <span class="ept-submission-card__meta-value">${valueHtml}</span>
            </span>
        `;
        return cell;
    }

    function buildFieldGrid(fields) {
        const tbl = document.createElement('table');
        tbl.className = 'ept-submission-fields';
        const tbody = document.createElement('tbody');
        for (const f of fields) {
            const tr = document.createElement('tr');
            const empty = (f.value == null || f.value === '');
            tr.innerHTML = `
                <th class="ept-submission-fields__label" scope="row">
                    ${esc(f.label || f.key || '')}
                    ${f.format && f.format !== 'Text' ? `<span class="ept-submission-fields__format">${esc(f.format)}</span>` : ''}
                </th>
                <td class="ept-submission-fields__value${empty ? ' is-empty' : ''}">
                    ${empty ? '<span class="ept-muted">—</span>' : renderValue(f)}
                </td>
            `;
            tbody.appendChild(tr);
        }
        tbl.appendChild(tbody);
        return tbl;
    }

    function renderValue(f) {
        const v = String(f.value ?? '');
        switch ((f.format || '').toLowerCase()) {
            case 'date':
            case 'datetime': {
                const d = new Date(v);
                if (!isNaN(d.getTime())) {
                    return `<span title="${escAttr(d.toISOString())}">${esc(d.toLocaleString())}</span>`;
                }
                return esc(v);
            }
            case 'multilinetext':
            case 'paragraph':
                return `<pre class="ept-submission-fields__pre">${esc(v)}</pre>`;
            case 'fileupload':
                return `<a href="${escAttr(v)}" target="_blank" rel="noopener" class="ept-link">${esc(shortenUrl(v))}</a>`;
            case 'url':
                return /^https?:\/\//i.test(v)
                    ? `<a href="${escAttr(v)}" target="_blank" rel="noopener" class="ept-link">${esc(v)}</a>`
                    : esc(v);
            default:
                return esc(v);
        }
    }

    function humanAgo(d) {
        const ms = Date.now() - d.getTime();
        const s = Math.round(ms / 1000);
        if (s < 60) return s + 's ago';
        const m = Math.round(s / 60);
        if (m < 60) return m + 'm ago';
        const h = Math.round(m / 60);
        if (h < 48) return h + 'h ago';
        const days = Math.round(h / 24);
        return days + 'd ago';
    }

    function shortenUrl(u) {
        if (!u) return '';
        try {
            const url = new URL(u, location.origin);
            const path = (url.pathname || '/') + (url.search || '');
            if (path.length > 64) return path.slice(0, 30) + '…' + path.slice(-30);
            return path;
        } catch { return String(u).length > 64 ? String(u).slice(0, 60) + '…' : String(u); }
    }

    function metaSvg(kind) {
        switch (kind) {
            case 'clock':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><polyline points="12 7 12 12 15 14"/></svg>';
            case 'user':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="8" r="4"/><path d="M4 21v-1a8 8 0 0 1 16 0v1"/></svg>';
            case 'globe':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="9"/><path d="M3 12h18"/><path d="M12 3a14 14 0 0 1 0 18"/><path d="M12 3a14 14 0 0 0 0 18"/></svg>';
            case 'page':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>';
        }
        return '';
    }

    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' })[c]);
    }
    function escAttr(s) { return esc(s); }
})();
