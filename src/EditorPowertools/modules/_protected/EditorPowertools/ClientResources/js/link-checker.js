/**
 * Link Checker - Main UI
 */
(function () {
    const API = window.EPT_API_URL + '/link-checker';
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
        const existing = document.getElementById('lc-job-alert');
        if (existing) existing.remove();
        if (!jobStatus) return;
        const el = EPT.renderJobAlert(jobStatus, `${API}/job-start`);
        if (!el) return;
        el.id = 'lc-job-alert';
        const container = document.getElementById('linkchecker-stats');
        container.parentNode.insertBefore(el, container);
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
            <div class="ept-stat"><div class="ept-stat__value">${total}</div><div class="ept-stat__label">${EPT.s('linkchecker.stat_total', 'Total Links')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value" style="color:var(--ept-danger,#dc2626)">${broken}</div><div class="ept-stat__label">${EPT.s('linkchecker.stat_broken', 'Broken')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value" style="color:var(--ept-success,#16a34a)">${valid}</div><div class="ept-stat__label">${EPT.s('linkchecker.stat_valid', 'Valid')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${internal}</div><div class="ept-stat__label">${EPT.s('linkchecker.stat_internal', 'Internal')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${external}</div><div class="ept-stat__label">${EPT.s('linkchecker.stat_external', 'External')}</div></div>
        `;
    }

    function renderToolbar() {
        const toolbar = document.getElementById('linkchecker-toolbar');
        toolbar.innerHTML = `
            <div class="ept-search">
                <span class="ept-search__icon">${EPT.icons.search}</span>
                <input type="text" id="lc-search" placeholder="${EPT.s('linkchecker.lbl_search', 'Search URLs, content, properties...')}" />
            </div>
            <select id="lc-type-filter" class="ept-select">
                <option value="">${EPT.s('linkchecker.opt_alltypes', 'All types')}</option>
                <option value="Internal">${EPT.s('linkchecker.opt_internal', 'Internal')}</option>
                <option value="External">${EPT.s('linkchecker.opt_external', 'External')}</option>
            </select>
            <select id="lc-status-filter" class="ept-select">
                <option value="">${EPT.s('linkchecker.opt_allstatus', 'All status')}</option>
                <option value="broken">${EPT.s('linkchecker.opt_brokenonly', 'Broken only')}</option>
                <option value="valid">${EPT.s('linkchecker.opt_validonly', 'Valid only')}</option>
            </select>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="lc-export">${EPT.icons.download} ${EPT.s('linkchecker.btn_export', 'Export')}</button>
        `;

        document.getElementById('lc-search').addEventListener('input', e => { searchQuery = e.target.value; renderTable(); });
        document.getElementById('lc-type-filter').addEventListener('change', e => { linkTypeFilter = e.target.value; renderTable(); });
        document.getElementById('lc-status-filter').addEventListener('change', e => { statusFilter = e.target.value; renderTable(); });
        document.getElementById('lc-export').addEventListener('click', exportCsv);
    }

    function renderTable() {
        const data = getFiltered();

        const columns = [
            { key: 'isValid', label: EPT.s('linkchecker.col_status', 'Status'), render: r => {
                if (r.isValid) return `<span style="color:var(--ept-success,#16a34a)" title="OK">&#10003; ${r.statusCode}</span>`;
                return `<span class="ept-badge ept-badge--danger" title="${escHtml(r.statusText)}">${r.statusCode} ${escHtml(r.statusText)}</span>`;
            }},
            { key: 'friendlyUrl', label: EPT.s('linkchecker.col_url', 'URL'), render: r => {
                const displayUrl = r.friendlyUrl || r.url;
                const truncated = displayUrl.length > 60 ? displayUrl.substring(0, 60) + '...' : displayUrl;
                let html = '';
                if (r.linkType === 'External') {
                    html += `<a href="${escHtml(r.url)}" target="_blank" title="${escHtml(r.url)}">${escHtml(truncated)}</a>`;
                } else if (r.targetContentId) {
                    html += `<a href="${window.EPT_CMS_URL || '/EPiServer/CMS/'}#/content/${r.targetContentId}" target="_blank" title="${escHtml(displayUrl)}">${escHtml(truncated)}</a>`;
                } else {
                    html += `<span title="${escHtml(r.url)}">${escHtml(truncated)}</span>`;
                }
                return html;
            }},
            { key: 'linkType', label: EPT.s('linkchecker.col_type', 'Type'), render: r => {
                const cls = r.linkType === 'Internal' ? 'primary' : 'default';
                return `<span class="ept-badge ept-badge--${cls}">${escHtml(r.linkType)}</span>`;
            }},
            { key: 'contentName', label: EPT.s('linkchecker.col_foundin', 'Found In'), render: r => {
                let html = '';
                if (r.editUrl) {
                    html += `<a href="${escHtml(r.editUrl)}" target="_blank" style="color:inherit"><strong>${escHtml(r.contentName)}</strong></a>`;
                } else {
                    html += `<strong>${escHtml(r.contentName)}</strong>`;
                }
                html += ` <span class="ept-muted" style="font-size:11px">via ${escHtml(r.propertyName)}</span>`;
                if (r.breadcrumb) html += `<div class="ept-muted" style="font-size:11px">${escHtml(r.breadcrumb)}</div>`;
                if (r.usedOn) {
                    html += `<div style="font-size:11px;margin-top:2px">${EPT.s('linkchecker.cell_usedon', 'Used on: ')}`;
                    if (r.usedOnEditUrls) {
                        const pages = r.usedOnEditUrls.split(';;').map(p => {
                            const parts = p.split('|');
                            return `<a href="${escHtml(parts[2] || '')}" target="_blank" title="${escHtml(parts[1] || '')}" style="color:var(--ept-primary,#3b82f6)">${escHtml(parts[0])}</a>`;
                        });
                        html += pages.join(', ');
                    } else {
                        html += escHtml(r.usedOn);
                    }
                    html += `</div>`;
                }
                return html;
            }},
            { key: 'lastChecked', label: EPT.s('linkchecker.col_checked', 'Checked'), render: r => `<span title="${r.lastChecked}">${timeAgo(r.lastChecked)}</span>` }
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
        if (!dateStr) return EPT.s('linkchecker.time_never', 'never');
        const d = new Date(dateStr);
        const now = new Date();
        const diff = Math.floor((now - d) / 1000);
        if (diff < 60) return EPT.s('linkchecker.time_justnow', 'just now');
        if (diff < 3600) return EPT.s('linkchecker.time_mago', '{0}m ago').replace('{0}', Math.floor(diff / 60));
        if (diff < 86400) return EPT.s('linkchecker.time_hago', '{0}h ago').replace('{0}', Math.floor(diff / 3600));
        return EPT.s('linkchecker.time_dago', '{0}d ago').replace('{0}', Math.floor(diff / 86400));
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
