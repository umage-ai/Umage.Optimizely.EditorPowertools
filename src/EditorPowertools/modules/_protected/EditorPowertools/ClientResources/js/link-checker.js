/**
 * Link Checker - Main UI
 */
(function () {
    const API = '/editorpowertools/api/link-checker';
    let allLinks = [];
    let tableInstance = null;
    let jobStatus = null;

    let searchQuery = '';
    let linkTypeFilter = '';
    let statusFilter = '';

    async function init() {
        EPT.showLoading(document.getElementById('linkchecker-content'));
        try {
            const [links, status] = await Promise.all([
                EPT.fetchJson(`${API}/links`),
                EPT.fetchJson(`${API}/job-status`).catch(() => null)
            ]);
            allLinks = links || [];
            jobStatus = status;
            renderJobAlert();
            renderStats();
            renderToolbar();
            renderTable();
        } catch (err) {
            document.getElementById('linkchecker-content').innerHTML =
                `<div class="ept-empty"><p>Error loading links: ${err.message}</p></div>`;
        }
    }

    function renderJobAlert() {
        let existing = document.getElementById('lc-job-alert');
        if (existing) existing.remove();
        if (!jobStatus) return;

        const el = document.createElement('div');
        el.id = 'lc-job-alert';

        const runBtn = `<button class="ept-btn ept-btn--sm" id="lc-run-job-btn" style="margin-left:8px">Run now</button>`;

        if (jobStatus.isRunning) {
            el.className = 'ept-alert ept-alert--info';
            el.innerHTML = `<strong>Link checker job is currently running.</strong> Results will be updated when it completes. <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">Refresh</button>`;
        } else if (!jobStatus.hasRun) {
            el.className = 'ept-alert ept-alert--warning';
            el.innerHTML = `<strong>Link checker has not been run yet.</strong> Run the scheduled job to scan content for links. ${runBtn}`;
        } else {
            const ago = timeAgo(jobStatus.lastRunUtc);
            el.className = 'ept-alert ept-alert--info';
            el.innerHTML = `Link check last ran <strong>${ago}</strong>. ${runBtn}`;
        }

        const container = document.getElementById('linkchecker-stats');
        container.parentNode.insertBefore(el, container);

        const btn = document.getElementById('lc-run-job-btn');
        if (btn) {
            btn.addEventListener('click', async () => {
                btn.disabled = true;
                btn.textContent = 'Starting...';
                try {
                    await EPT.postJson(`${API}/job-start`);
                    el.className = 'ept-alert ept-alert--info';
                    el.innerHTML = `<strong>Job started.</strong> <button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">Refresh</button>`;
                } catch { btn.textContent = 'Failed'; }
            });
        }
    }

    function getFiltered() {
        return allLinks.filter(l => {
            if (linkTypeFilter && l.linkType !== linkTypeFilter) return false;
            if (statusFilter === 'broken' && l.isValid) return false;
            if (statusFilter === 'valid' && !l.isValid) return false;
            if (searchQuery) {
                const q = searchQuery.toLowerCase();
                return l.url?.toLowerCase().includes(q) ||
                    l.contentName?.toLowerCase().includes(q) ||
                    l.propertyName?.toLowerCase().includes(q);
            }
            return true;
        });
    }

    function renderStats() {
        const total = allLinks.length;
        const broken = allLinks.filter(l => !l.isValid).length;
        const valid = allLinks.filter(l => l.isValid).length;
        const internal = allLinks.filter(l => l.linkType === 'Internal').length;
        const external = allLinks.filter(l => l.linkType === 'External').length;

        document.getElementById('linkchecker-stats').innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">Total Links</div></div>
            <div class="ept-stat"><div class="ept-stat__value" style="color:var(--ept-danger,#dc2626)">${broken}</div><div class="ept-stat__label">Broken</div></div>
            <div class="ept-stat"><div class="ept-stat__value" style="color:var(--ept-success,#16a34a)">${valid}</div><div class="ept-stat__label">Valid</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${internal}</div><div class="ept-stat__label">Internal</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${external}</div><div class="ept-stat__label">External</div></div>
        `;
    }

    function renderToolbar() {
        const toolbar = document.getElementById('linkchecker-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="lc-search" placeholder="Search URLs, content, properties..." />
            </div>
            <select id="lc-type-filter" class="ept-select">
                <option value="">All types</option>
                <option value="Internal">Internal</option>
                <option value="External">External</option>
            </select>
            <select id="lc-status-filter" class="ept-select">
                <option value="">All status</option>
                <option value="broken">Broken only</option>
                <option value="valid">Valid only</option>
            </select>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="lc-export">${EPT.icons.download} Export</button>
        `;

        document.getElementById('lc-search').addEventListener('input', e => { searchQuery = e.target.value; renderTable(); });
        document.getElementById('lc-type-filter').addEventListener('change', e => { linkTypeFilter = e.target.value; renderTable(); });
        document.getElementById('lc-status-filter').addEventListener('change', e => { statusFilter = e.target.value; renderTable(); });
        document.getElementById('lc-export').addEventListener('click', exportCsv);
    }

    function renderTable() {
        const data = getFiltered();

        const columns = [
            { key: 'isValid', label: 'Status', render: r => {
                if (r.isValid) return `<span style="color:var(--ept-success,#16a34a)" title="OK">&#10003; ${r.statusCode}</span>`;
                return `<span class="ept-badge ept-badge--danger" title="${escHtml(r.statusText)}">${r.statusCode} ${escHtml(r.statusText)}</span>`;
            }},
            { key: 'friendlyUrl', label: 'URL', render: r => {
                const displayUrl = r.friendlyUrl || r.url;
                const truncated = displayUrl.length > 60 ? displayUrl.substring(0, 60) + '...' : displayUrl;
                let html = '';
                if (r.linkType === 'External') {
                    html += `<a href="${escHtml(r.url)}" target="_blank" title="${escHtml(r.url)}">${escHtml(truncated)}</a>`;
                } else if (r.targetContentId) {
                    html += `<a href="/EPiServer/CMS/#/content/${r.targetContentId}" target="_blank" title="${escHtml(displayUrl)}">${escHtml(truncated)}</a>`;
                } else {
                    html += `<span title="${escHtml(r.url)}">${escHtml(truncated)}</span>`;
                }
                return html;
            }},
            { key: 'linkType', label: 'Type', render: r => {
                const cls = r.linkType === 'Internal' ? 'primary' : 'default';
                return `<span class="ept-badge ept-badge--${cls}">${escHtml(r.linkType)}</span>`;
            }},
            { key: 'contentName', label: 'Found In', render: r => {
                let html = '';
                if (r.editUrl) {
                    html += `<a href="${escHtml(r.editUrl)}" target="_blank" style="color:inherit"><strong>${escHtml(r.contentName)}</strong></a>`;
                } else {
                    html += `<strong>${escHtml(r.contentName)}</strong>`;
                }
                html += ` <span class="ept-muted" style="font-size:11px">via ${escHtml(r.propertyName)}</span>`;
                if (r.breadcrumb) html += `<div class="ept-muted" style="font-size:11px">${escHtml(r.breadcrumb)}</div>`;
                return html;
            }},
            { key: 'lastChecked', label: 'Checked', render: r => `<span title="${r.lastChecked}">${timeAgo(r.lastChecked)}</span>` }
        ];

        tableInstance = EPT.createTable(columns, data, {
            defaultSort: 'isValid',
            defaultSortDir: 'asc',
            rowClass: r => r.isValid ? '' : 'ept-row--orphaned'
        });

        const content = document.getElementById('linkchecker-content');
        content.innerHTML = '';
        const card = document.createElement('div');
        card.className = 'ept-card';
        const body = document.createElement('div');
        body.className = 'ept-card__body ept-card__body--flush';
        body.appendChild(tableInstance.table);
        card.appendChild(body);
        content.appendChild(card);
    }

    function exportCsv() {
        const data = getFiltered();
        const cols = [
            { key: 'url', label: 'URL' },
            { key: 'statusCode', label: 'Status Code' },
            { key: 'statusText', label: 'Status' },
            { key: 'isValid', label: 'Valid' },
            { key: 'linkType', label: 'Type' },
            { key: 'contentName', label: 'Content' },
            { key: 'propertyName', label: 'Property' },
            { key: 'breadcrumb', label: 'Breadcrumb' },
            { key: 'lastChecked', label: 'Last Checked' }
        ];
        EPT.downloadCsv(`LinkChecker_${new Date().toISOString().slice(0,10)}.csv`, cols, data);
    }

    function timeAgo(dateStr) {
        if (!dateStr) return 'never';
        const d = new Date(dateStr);
        const now = new Date();
        const diff = Math.floor((now - d) / 1000);
        if (diff < 60) return 'just now';
        if (diff < 3600) return Math.floor(diff / 60) + 'm ago';
        if (diff < 86400) return Math.floor(diff / 3600) + 'h ago';
        return Math.floor(diff / 86400) + 'd ago';
    }

    function escHtml(s) {
        if (!s) return '';
        const d = document.createElement('div');
        d.textContent = s;
        return d.innerHTML;
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
