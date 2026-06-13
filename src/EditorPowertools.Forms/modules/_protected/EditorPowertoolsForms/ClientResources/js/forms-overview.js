/**
 * Editor Powertools — Forms Overview
 * Inventory of all Optimizely Forms with submissions, fields, usage, retention.
 * Reuses the base library's UI primitives (.ept-card, .ept-table, .ept-btn).
 */
(function () {
    'use strict';

    const API_URL = window.EPT_FORMS_BASE_URL + 'FormsApi/GetForms';
    const root = document.getElementById('ept-forms-overview-root');
    if (!root) return;

    const state = {
        all: [],
        filter: '',
        retention: 'all',           // 'all' | 'default' | 'custom'
        handlerFilter: 'all',       // 'all' | 'with' | 'without'
        language: '',               // '' = all languages
        usageOnly: false,
        riskOnly: false,            // privacy/GDPR risks only
        sortBy: 'lastSubmission',
        sortDir: 'desc',
        expandedUsage: new Set(),   // contentIds whose usage panel is expanded
    };

    EPT.showLoading(root);
    fetch(API_URL)
        .then(r => { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); })
        .then(data => { state.all = Array.isArray(data) ? data : []; render(); })
        .catch(err => {
            console.error('[EditorPowertools.Forms] Failed to load forms', err);
            root.innerHTML = '<div class="ept-card"><div class="ept-card__body"><p style="color:var(--ept-danger)">' +
                EPT.s('formsoverview.error_load', 'Failed to load forms.') + '</p></div></div>';
        });

    function render() {
        const rows = applyFilters(state.all);
        const langs = [...new Set(state.all.map(r => r.language).filter(Boolean))].sort();
        root.innerHTML = '';

        const card = el('div', { className: 'ept-card' });

        // Toolbar
        const toolbar = el('div', { className: 'ept-card__header ept-forms-toolbar' });
        toolbar.innerHTML = `
            <div class="ept-forms-toolbar__counts">
                <strong>${rows.length}</strong> / ${state.all.length}
                ${EPT.s('formsoverview.forms_label', 'forms')}
            </div>
            <div class="ept-forms-toolbar__filters">
                <input type="search" id="ept-forms-filter" class="ept-input"
                       placeholder="${EPT.s('formsoverview.filter_placeholder', 'Filter by name or location…')}"
                       value="${escAttr(state.filter)}" />
                <select id="ept-forms-retention" class="ept-input">
                    <option value="all">${EPT.s('formsoverview.retention_all', 'Any retention')}</option>
                    <option value="default">${EPT.s('formsoverview.retention_default', 'Default retention only')}</option>
                    <option value="custom">${EPT.s('formsoverview.retention_custom', 'Custom retention only')}</option>
                </select>
                <select id="ept-forms-handler" class="ept-input">
                    <option value="all">${EPT.s('formsoverview.handler_all', 'Any handlers')}</option>
                    <option value="with">${EPT.s('formsoverview.handler_with', 'With email/webhook')}</option>
                    <option value="without">${EPT.s('formsoverview.handler_without', 'No notification handler')}</option>
                </select>
                ${langs.length > 1 ? `
                <select id="ept-forms-language" class="ept-input" title="${EPT.s('formsoverview.lang_filter_tip', 'Filter by form language')}">
                    <option value="">${EPT.s('formsoverview.lang_all', 'All languages')}</option>
                    ${langs.map(l => `<option value="${escAttr(l)}" ${state.language === l ? 'selected' : ''}>${esc(l)}</option>`).join('')}
                </select>` : ''}
                <label class="ept-checkbox">
                    <input type="checkbox" id="ept-forms-usage-only" ${state.usageOnly ? 'checked' : ''} />
                    ${EPT.s('formsoverview.usage_only', 'Only forms used somewhere')}
                </label>
                <label class="ept-checkbox">
                    <input type="checkbox" id="ept-forms-risk-only" ${state.riskOnly ? 'checked' : ''} />
                    ${EPT.s('formsoverview.risk_only', 'Privacy risks only')}
                </label>
            </div>
        `;
        card.appendChild(toolbar);

        // Body
        const body = el('div', { className: 'ept-card__body ept-card__body--flush' });
        if (rows.length === 0) {
            body.innerHTML = `<p class="ept-empty-state">${EPT.s('formsoverview.empty', 'No forms match the current filters.')}</p>`;
        } else {
            const table = buildTable(rows);
            body.appendChild(table);
        }
        card.appendChild(body);
        root.appendChild(card);

        // Wire interactions
        const filt = document.getElementById('ept-forms-filter');
        filt.addEventListener('input', e => {
            state.filter = e.target.value.toLowerCase();
            render();
            setTimeout(() => {
                const f = document.getElementById('ept-forms-filter');
                if (f) { f.focus(); f.setSelectionRange(f.value.length, f.value.length); }
            }, 0);
        });
        const ret = document.getElementById('ept-forms-retention');
        ret.value = state.retention;
        ret.addEventListener('change', e => { state.retention = e.target.value; render(); });
        const hand = document.getElementById('ept-forms-handler');
        hand.value = state.handlerFilter;
        hand.addEventListener('change', e => { state.handlerFilter = e.target.value; render(); });
        const langEl = document.getElementById('ept-forms-language');
        if (langEl) {
            langEl.value = state.language;
            langEl.addEventListener('change', e => { state.language = e.target.value; render(); });
        }
        document.getElementById('ept-forms-usage-only').addEventListener('change', e => {
            state.usageOnly = e.target.checked;
            render();
        });
        document.getElementById('ept-forms-risk-only').addEventListener('change', e => {
            state.riskOnly = e.target.checked;
            render();
        });
    }

    function applyFilters(list) {
        let out = list;
        if (state.filter) {
            const f = state.filter;
            out = out.filter(r =>
                (r.name || '').toLowerCase().includes(f) ||
                (r.breadcrumb || '').toLowerCase().includes(f) ||
                (r.usage || []).some(u => (u.ownerName || '').toLowerCase().includes(f))
            );
        }
        if (state.retention === 'default') out = out.filter(r => r.usesDefaultRetention);
        if (state.retention === 'custom') out = out.filter(r => !r.usesDefaultRetention);
        if (state.handlerFilter === 'with') out = out.filter(r => r.hasEmailHandler || r.hasWebhookHandler);
        if (state.handlerFilter === 'without') out = out.filter(r => !r.hasEmailHandler && !r.hasWebhookHandler);
        if (state.language) out = out.filter(r => r.language === state.language);
        if (state.usageOnly) out = out.filter(r => (r.usageCount || 0) > 0);
        if (state.riskOnly) out = out.filter(r => r.privacyRisk);

        const dir = state.sortDir === 'asc' ? 1 : -1;
        out = out.slice().sort((a, b) => {
            switch (state.sortBy) {
                case 'name': return dir * (a.name || '').localeCompare(b.name || '');
                case 'submissions': return dir * ((a.submissionCount || 0) - (b.submissionCount || 0));
                case 'fields': return dir * ((a.fieldCount || 0) - (b.fieldCount || 0));
                case 'usage': return dir * ((a.usageCount || 0) - (b.usageCount || 0));
                case 'lastSubmission':
                default:
                    const ta = a.lastSubmissionUtc ? new Date(a.lastSubmissionUtc).getTime() : 0;
                    const tb = b.lastSubmissionUtc ? new Date(b.lastSubmissionUtc).getTime() : 0;
                    return dir * (ta - tb);
            }
        });
        return out;
    }

    function buildTable(rows) {
        const table = el('table', { className: 'ept-table' });
        table.appendChild(buildHeader());

        const tbody = el('tbody', {});
        for (const r of rows) {
            tbody.appendChild(buildRow(r));
            if (state.expandedUsage.has(r.contentId)) {
                tbody.appendChild(buildUsagePanelRow(r));
            }
        }
        table.appendChild(tbody);
        return table;
    }

    function buildHeader() {
        const thead = el('thead', {});
        const tr = el('tr', {});
        const cols = [
            { key: 'name',           label: EPT.s('formsoverview.col_name', 'Form'),                sortable: true },
            { key: 'fields',         label: EPT.s('formsoverview.col_fields', 'Fields'),            sortable: true, num: true },
            { key: 'submissions',    label: EPT.s('formsoverview.col_submissions', 'Submissions'),  sortable: true, num: true },
            { key: 'lastSubmission', label: EPT.s('formsoverview.col_last', 'Last submission'),     sortable: true },
            { key: 'handlers',       label: EPT.s('formsoverview.col_handlers', 'Handlers'),        sortable: false },
            { key: 'retention',      label: EPT.s('formsoverview.col_retention', 'Retention'),      sortable: false },
            { key: 'actions',        label: '',                                                     sortable: false }
        ];
        for (const c of cols) {
            const th = el('th', { textContent: c.label });
            if (c.num) th.classList.add('num');
            if (c.sortable) {
                th.style.cursor = 'pointer';
                if (state.sortBy === c.key) th.dataset.sortDir = state.sortDir;
                th.addEventListener('click', () => {
                    if (state.sortBy === c.key) {
                        state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
                    } else {
                        state.sortBy = c.key;
                        state.sortDir = c.key === 'name' ? 'asc' : 'desc';
                    }
                    render();
                });
            }
            tr.appendChild(th);
        }
        thead.appendChild(tr);
        return thead;
    }

    function buildRow(r) {
        const tr = el('tr', { dataset: { formId: r.contentId } });

        // Name + breadcrumb
        const tdName = el('td', {});
        tdName.innerHTML = `
            <div class="ept-form-name">
                <a href="${escAttr(r.editUrl)}" class="ept-link" title="${EPT.s('formsoverview.tip_open', 'Open form in CMS edit mode')}">${esc(r.name || '(unnamed)')}</a>
            </div>
            ${r.breadcrumb ? `<div class="ept-muted ept-small">${esc(r.breadcrumb)}</div>` : ''}
            ${buildFlags(r)}
        `;
        tr.appendChild(tdName);

        // Fields
        tr.appendChild(td(r.fieldCount, 'num'));
        // Submissions
        tr.appendChild(td(r.submissionCount, 'num'));

        // Last submission
        const tdLast = el('td', {});
        if (r.lastSubmissionUtc) {
            const d = new Date(r.lastSubmissionUtc);
            tdLast.innerHTML = `<span title="${escAttr(d.toISOString())}">${esc(d.toLocaleString())}</span>`;
        } else {
            tdLast.innerHTML = `<span class="ept-muted">${EPT.s('formsoverview.no_submissions', 'no submissions')}</span>`;
        }
        tr.appendChild(tdLast);

        // Handlers (icons)
        const tdHandlers = el('td', { className: 'ept-handler-cell' });
        if (r.hasEmailHandler) {
            tdHandlers.appendChild(handlerIcon('email', r.emailHandlerCount,
                EPT.s('formsoverview.handler_email_tip', 'Email notification configured')));
        }
        if (r.hasWebhookHandler) {
            tdHandlers.appendChild(handlerIcon('webhook', r.webhookHandlerCount,
                EPT.s('formsoverview.handler_webhook_tip', 'Webhook configured')));
        }
        if (!r.hasEmailHandler && !r.hasWebhookHandler) {
            tdHandlers.innerHTML = `<span class="ept-muted ept-small">${EPT.s('formsoverview.handler_none', '—')}</span>`;
        }
        tr.appendChild(tdHandlers);

        // Retention
        const tdRet = el('td', {});
        const partial = prettyRetention(r.partialRetentionPolicy);
        const final = prettyRetention(r.finalizedRetentionPolicy);
        const badge = r.usesDefaultRetention
            ? `<span class="ept-badge ept-badge--neutral">${EPT.s('formsoverview.retention_default_short', 'Default')}</span>`
            : `<span class="ept-badge ept-badge--success">${EPT.s('formsoverview.retention_custom_short', 'Custom')}</span>`;
        tdRet.innerHTML = `
            ${badge}
            <div class="ept-small ept-muted ept-retention-detail">
                <div>${EPT.s('formsoverview.retention_partial', 'Partial:')} ${esc(partial)}</div>
                <div>${EPT.s('formsoverview.retention_final', 'Finalized:')} ${esc(final)}</div>
            </div>
        `;
        tr.appendChild(tdRet);

        // Actions: edit, where-used toggle
        const tdActions = el('td', { className: 'ept-form-actions' });

        const editBtn = el('a', {
            href: r.editUrl || '#',
            className: 'ept-btn ept-btn--sm',
            title: EPT.s('formsoverview.tip_edit', 'Open form in CMS edit mode')
        });
        editBtn.innerHTML = svgIcon('edit') + ' <span>' + EPT.s('formsoverview.btn_edit', 'Edit') + '</span>';
        tdActions.appendChild(editBtn);

        const usageBtn = el('button', {
            className: 'ept-btn ept-btn--sm',
            type: 'button',
            title: EPT.s('formsoverview.tip_usage', 'Show where this form is used')
        });
        const isOpen = state.expandedUsage.has(r.contentId);
        usageBtn.innerHTML = svgIcon('usage') +
            ` <span>${EPT.s('formsoverview.btn_usage', 'Used on')} ${r.usageCount || 0}${isOpen ? ' ▴' : ' ▾'}</span>`;
        usageBtn.disabled = (r.usageCount || 0) === 0;
        usageBtn.addEventListener('click', () => {
            if (state.expandedUsage.has(r.contentId)) state.expandedUsage.delete(r.contentId);
            else state.expandedUsage.add(r.contentId);
            render();
        });
        tdActions.appendChild(usageBtn);

        tr.appendChild(tdActions);
        return tr;
    }

    function buildUsagePanelRow(r) {
        const tr = el('tr', { className: 'ept-usage-panel-row' });
        const td = el('td', { colSpan: 7 });
        const list = (r.usage || []);
        if (list.length === 0) {
            td.innerHTML = `<div class="ept-usage-panel"><span class="ept-muted">${EPT.s('formsoverview.unused', 'unused')}</span></div>`;
        } else {
            const items = list.map(u =>
                `<li>
                    <a href="${escAttr(u.editUrl || '#')}" class="ept-link">${esc(u.ownerName || '')}</a>
                    ${u.ownerTypeName ? `<span class="ept-muted ept-small">(${esc(u.ownerTypeName)})</span>` : ''}
                    ${u.language ? `<span class="ept-muted ept-small">[${esc(u.language)}]</span>` : ''}
                </li>`
            ).join('');
            td.innerHTML = `<div class="ept-usage-panel"><ul>${items}</ul></div>`;
        }
        tr.appendChild(td);
        return tr;
    }

    // Warning badges shown under a form name: privacy/GDPR risk and duplicate fields.
    function buildFlags(r) {
        const flags = [];
        if (r.privacyRisk) {
            const live = r.privacyRiskIsLive;
            const label = live
                ? EPT.s('formsoverview.flag_privacy_live', 'Privacy risk — live data')
                : EPT.s('formsoverview.flag_privacy', 'Privacy risk');
            const tipBase = live
                ? EPT.s('formsoverview.flag_privacy_live_tip', 'Published form that already holds submissions of personal data on the default (indefinite) retention policy.')
                : EPT.s('formsoverview.flag_privacy_tip', 'Captures personal data and stores submissions on the default (indefinite) retention policy.');
            const fields = r.piiFieldLabels || [];
            const tip = tipBase + (fields.length
                ? ' ' + EPT.s('formsoverview.flag_pii_fields', 'Personal-data fields:') + ' ' + fields.join(', ')
                : '');
            flags.push(`<span class="ept-badge ${live ? 'ept-badge--danger' : 'ept-badge--warning'}" title="${escAttr(tip)}">${svgIcon('warn')}${esc(label)}</span>`);
        }
        if (r.hasDuplicateFields) {
            const dups = r.duplicateFieldLabels || [];
            const dtip = EPT.s('formsoverview.flag_duplicate_tip', 'These field labels appear more than once and collide in the submission data:') + ' ' + dups.join(', ');
            flags.push(`<span class="ept-badge ept-badge--warning" title="${escAttr(dtip)}">${svgIcon('duplicate')}${esc(EPT.s('formsoverview.flag_duplicate', 'Duplicate fields'))}</span>`);
        }
        return flags.length ? `<div class="ept-form-flags">${flags.join('')}</div>` : '';
    }

    // ── helpers ─────────────────────────────────────────────────────
    function el(tag, opts) {
        const e = document.createElement(tag);
        if (!opts) return e;
        if (opts.className) e.className = opts.className;
        if (opts.textContent != null) e.textContent = opts.textContent;
        if (opts.colSpan) e.colSpan = opts.colSpan;
        if (opts.href) e.href = opts.href;
        if (opts.type) e.type = opts.type;
        if (opts.title) e.title = opts.title;
        if (opts.dataset) Object.assign(e.dataset, opts.dataset);
        return e;
    }
    function td(value, cls) {
        const e = document.createElement('td');
        if (cls) e.className = cls;
        e.textContent = (value == null) ? '' : String(value);
        return e;
    }
    function handlerIcon(kind, count, tip) {
        const wrap = document.createElement('span');
        wrap.className = 'ept-handler-icon ept-handler-icon--' + kind;
        wrap.title = tip + (count > 1 ? ' × ' + count : '');
        wrap.innerHTML = svgIcon(kind);
        if (count > 1) {
            const c = document.createElement('span');
            c.className = 'ept-handler-icon__count';
            c.textContent = count;
            wrap.appendChild(c);
        }
        return wrap;
    }
    function svgIcon(kind) {
        // 14×14 SVG strings, single-color, currentColor stroke
        switch (kind) {
            case 'email':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><rect x="2" y="4" width="20" height="16" rx="2"/><polyline points="2 6 12 13 22 6"/></svg>';
            case 'webhook':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><circle cx="6" cy="18" r="3"/><circle cx="18" cy="18" r="3"/><circle cx="12" cy="6" r="3"/><path d="M7.5 16 11 9"/><path d="M16.5 16 13 9"/><path d="M9 18h6"/></svg>';
            case 'edit':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.12 2.12 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>';
            case 'usage':
                return '<svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>';
            case 'warn':
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>';
            case 'duplicate':
                return '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>';
        }
        return '';
    }
    function prettyRetention(s) {
        if (!s) return '—';
        return s.replace(/^EPiServer\.RetentionPolicy\./i, '');
    }
    function esc(s) {
        return String(s ?? '').replace(/[&<>"']/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;' })[c]);
    }
    function escAttr(s) { return esc(s); }
})();
