/**
 * Personalization Audit - Main UI
 */
(function () {
    const API = window.EPT_API_URL + '/personalization';
    let allUsages = [];
    let allVisitorGroups = [];
    let tableInstance = null;
    let jobStatus = null;

    // Filters
    let searchQuery = '';
    let usageTypeFilter = '';
    let visitorGroupFilter = '';

    async function init() {
        EPT.showLoading(document.getElementById('personalization-content'));
        try {
            const [usages, groups, status] = await Promise.all([
                EPT.fetchJson(`${API}/usages`),
                EPT.fetchJson(`${API}/visitor-groups`),
                EPT.fetchJson(`${API}/job-status`).catch(() => null)
            ]);
            allUsages = usages;
            allVisitorGroups = groups;
            jobStatus = status;
            renderJobAlert();
            renderStats();
            renderToolbar();
            renderTable();
        } catch (err) {
            document.getElementById('personalization-content').innerHTML =
                `<div class="ept-empty"><p>Error loading personalization data: ${err.message}</p></div>`;
        }
    }

    // ── Job Status Alert ───────────────────────────────────────────
    function renderJobAlert() {
        let existing = document.getElementById('pers-job-alert');
        if (existing) existing.remove();

        if (!jobStatus) return;

        const el = document.createElement('div');
        el.id = 'pers-job-alert';

        const runBtn = `<button class="ept-btn ept-btn--sm" id="ept-pers-run-job-btn" style="margin-left:8px">${EPT.s('personalizationaudit.btn_runnow', 'Run now')}</button>`;

        if (jobStatus.isRunning) {
            el.className = 'ept-alert ept-alert--info';
            el.innerHTML = `<strong>${EPT.s('personalizationaudit.alert_running', 'Personalization analysis job is currently running. Results will be updated when it completes.')}</strong> <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">${EPT.s('personalizationaudit.btn_refresh', 'Refresh')}</button>`;
        } else if (!jobStatus.hasRun) {
            el.className = 'ept-alert ept-alert--warning';
            el.innerHTML = `<strong>${EPT.s('personalizationaudit.alert_notrun', 'Personalization usage has not been analyzed yet.')}</strong> Run the analysis job to scan content for audience usage. ${runBtn}`;
        } else {
            const ago = timeAgo(new Date(jobStatus.lastRunUtc));
            const isOld = (Date.now() - new Date(jobStatus.lastRunUtc).getTime()) > 24 * 60 * 60 * 1000;
            if (isOld) {
                el.className = 'ept-alert ept-alert--warning';
                el.innerHTML = EPT.s('personalizationaudit.alert_old', 'Analysis was last run {0}. Consider running the job again for fresh data.').replace('{0}', `<strong>${ago}</strong>`) + ` ${runBtn}`;
            } else {
                el.className = 'ept-alert ept-alert--info';
                el.innerHTML = EPT.s('personalizationaudit.alert_lastrun', 'Analysis was last run {0}.').replace('{0}', `<strong>${ago}</strong>`) + ` ${runBtn}`;
            }
        }

        const container = document.getElementById('personalization-stats');
        container.parentNode.insertBefore(el, container);

        // Wire up "Run now" button
        const btn = document.getElementById('ept-pers-run-job-btn');
        if (btn) {
            btn.addEventListener('click', async () => {
                btn.disabled = true;
                btn.textContent = EPT.s('personalizationaudit.btn_starting', 'Starting...');
                try {
                    await EPT.postJson(`${API}/job-start`);
                    el.className = 'ept-alert ept-alert--info';
                    el.innerHTML = `<strong>${EPT.s('personalizationaudit.alert_jobstarted', 'Personalization analysis job has been started. Results will be updated when it completes.')}</strong> <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">${EPT.s('personalizationaudit.btn_refresh', 'Refresh')}</button>`;
                } catch (err) {
                    btn.textContent = EPT.s('shared.failed', 'Failed');
                    console.error('Failed to start job:', err);
                }
            });
        }
    }

    function timeAgo(date) {
        const diff = Date.now() - date.getTime();
        const mins = Math.floor(diff / 60000);
        if (mins < 60) return `${mins} minute${mins !== 1 ? 's' : ''} ago`;
        const hours = Math.floor(mins / 60);
        if (hours < 24) return `${hours} hour${hours !== 1 ? 's' : ''} ago`;
        const days = Math.floor(hours / 24);
        return `${days} day${days !== 1 ? 's' : ''} ago`;
    }

    function getFiltered() {
        return allUsages.filter(u => {
            if (usageTypeFilter && u.usageType !== usageTypeFilter) return false;
            if (visitorGroupFilter && u.visitorGroupId !== visitorGroupFilter) return false;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                return (u.contentName?.toLowerCase().includes(q) ||
                    u.visitorGroupName?.toLowerCase().includes(q) ||
                    u.propertyName?.toLowerCase().includes(q) ||
                    u.contentTypeName?.toLowerCase().includes(q) ||
                    u.breadcrumb?.toLowerCase().includes(q) ||
                    String(u.contentId) === q);
            }
            return true;
        });
    }

    function renderStats() {
        const filtered = getFiltered();
        const uniqueVGs = new Set(allUsages.map(u => u.visitorGroupId)).size;
        const uniqueContent = new Set(allUsages.map(u => u.contentId)).size;
        const accessRights = allUsages.filter(u => u.usageType === 'AccessRight').length;
        const contentAreas = allUsages.filter(u => u.usageType === 'ContentArea').length;
        const xhtmlStrings = allUsages.filter(u => u.usageType === 'XhtmlString').length;

        const el = document.getElementById('personalization-stats');
        el.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${allUsages.length}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_total', 'Total Usages')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${uniqueVGs}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_groups', 'Visitor Groups Used')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${uniqueContent}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_content', 'Content Items')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${accessRights}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_accessrights', 'Access Rights')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${contentAreas}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_contentareas', 'Content Areas')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${xhtmlStrings}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_xhtmlstrings', 'XHTML Strings')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${filtered.length}</div><div class="ept-stat__label">${EPT.s('personalizationaudit.stat_showing', 'Showing')}</div></div>
        `;
    }

    function renderToolbar() {
        const usageTypes = [...new Set(allUsages.map(u => u.usageType))].sort();
        const vgNames = [...new Set(allUsages.map(u => JSON.stringify({ id: u.visitorGroupId, name: u.visitorGroupName })))].map(s => JSON.parse(s));
        vgNames.sort((a, b) => a.name.localeCompare(b.name));

        const toolbar = document.getElementById('personalization-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="pers-search" placeholder="${EPT.s('personalizationaudit.lbl_search', 'Search...')}" />
            </div>
            <select id="pers-type-filter" class="ept-select">
                <option value="">${EPT.s('personalizationaudit.opt_allusagetypes', 'All usage types')}</option>
                ${usageTypes.map(t => `<option value="${t}">${t} (${allUsages.filter(u => u.usageType === t).length})</option>`).join('')}
            </select>
            <select id="pers-vg-filter" class="ept-select">
                <option value="">${EPT.s('personalizationaudit.opt_allaudiences', 'All audiences')}</option>
                ${vgNames.map(vg => `<option value="${escAttr(vg.id)}">${escHtml(vg.name)} (${allUsages.filter(u => u.visitorGroupId === vg.id).length})</option>`).join('')}
            </select>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="pers-export" title="Export to CSV">${EPT.icons.download} ${EPT.s('personalizationaudit.btn_export', 'Export')}</button>
        `;

        document.getElementById('pers-search').addEventListener('input', (e) => {
            searchQuery = e.target.value;
            renderStats();
            renderTable();
        });

        document.getElementById('pers-type-filter').addEventListener('change', (e) => {
            usageTypeFilter = e.target.value;
            renderStats();
            renderTable();
        });

        document.getElementById('pers-vg-filter').addEventListener('change', (e) => {
            visitorGroupFilter = e.target.value;
            renderStats();
            renderTable();
        });

        document.getElementById('pers-export').addEventListener('click', exportCsv);
    }

    function renderTable() {
        const data = getFiltered();

        if (data.length === 0 && allUsages.length === 0) {
            const content = document.getElementById('personalization-content');
            EPT.showEmpty(content, EPT.s('personalizationaudit.empty_nousages', 'No personalization usage data available. Run the analysis job first.'));
            return;
        }

        const columns = [
            {
                key: 'contentName', label: EPT.s('personalizationaudit.col_content', 'Content'), render: (r) => {
                    let html = '';
                    if (r.editUrl) {
                        html += `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.contentName)}</strong> <span style="opacity:.4;display:inline-block;width:14px;height:14px;vertical-align:middle">${EPT.icons.edit}</span></a>`;
                    } else {
                        html += `<strong>${escHtml(r.contentName)}</strong>`;
                    }
                    if (r.breadcrumb) {
                        html += `<div class="ept-muted" style="font-size:11px;margin-top:2px">${escHtml(r.breadcrumb)}</div>`;
                    }
                    return html;
                }
            },
            {
                key: 'visitorGroupName', label: EPT.s('personalizationaudit.col_groups', 'Visitor Groups'), render: (r) => {
                    return `<a href="${window.EPT_VG_URL || '/EPiServer/EPiServer.Cms.UI.VisitorGroups/ManageVisitorGroups'}#/group/${encodeURIComponent(r.visitorGroupId)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.visitorGroupName)}</strong> <span style="opacity:.4;display:inline-block;width:14px;height:14px;vertical-align:middle">${EPT.icons.link}</span></a>`;
                }
            },
            {
                key: 'usageType', label: EPT.s('personalizationaudit.col_type', 'Usage Type'), render: (r) => {
                    let cls = 'default';
                    if (r.usageType === 'ContentArea') cls = 'primary';
                    else if (r.usageType === 'AccessRight') cls = 'warning';
                    return `<span class="ept-badge ept-badge--${cls}">${escHtml(r.usageType)}</span>`;
                }
            },
            { key: 'propertyName', label: EPT.s('personalizationaudit.col_location', 'Location') },
            { key: 'contentTypeName', label: EPT.s('personalizationaudit.col_contenttype', 'Content Type') }
        ];

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'contentName'
        });

        const content = document.getElementById('personalization-content');
        content.innerHTML = '';
        const card = document.createElement('div');
        card.className = 'ept-card';

        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        body.style.overflow = 'auto';
        body.style.maxHeight = 'calc(100vh - 300px)';
        body.appendChild(tableInstance.table);
        card.appendChild(body);
        content.appendChild(card);
    }

    // ── CSV Export ──────────────────────────────────────────────────
    function exportCsv() {
        const data = getFiltered();
        const columns = [
            { key: 'contentId', label: 'Content ID' },
            { key: 'contentName', label: 'Content Name' },
            { key: 'contentTypeName', label: 'Content Type' },
            { key: 'language', label: 'Language' },
            { key: 'visitorGroupId', label: 'Audience ID' },
            { key: 'visitorGroupName', label: 'Audience Name' },
            { key: 'usageType', label: 'Usage Type' },
            { key: 'propertyName', label: 'Property' },
            { key: 'breadcrumb', label: 'Breadcrumb' },
            { key: 'editUrl', label: 'Edit URL' },
            { key: 'parentContentId', label: 'Parent Content ID' },
            { key: 'parentContentName', label: 'Parent Content Name' }
        ];
        const ts = new Date().toISOString().slice(0, 19).replace(/[T:]/g, '-');
        EPT.downloadCsv(`PersonalizationAudit_${ts}.csv`, columns, data);
    }

    // ── Helpers ────────────────────────────────────────────────────
    function escHtml(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    function escAttr(s) {
        if (!s) return '';
        return s.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // Boot
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
