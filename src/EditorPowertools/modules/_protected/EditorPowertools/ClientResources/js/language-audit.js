/**
 * Language Audit - Main UI
 */
(function () {
    const API = window.EPT_API_URL;
    let overview = null;
    let currentTab = 'overview';
    let staleThreshold = 30;
    let selectedLanguage = '';

    async function init() {
        EPT.showLoading(document.getElementById('lang-audit-content'));
        try {
            overview = await EPT.fetchJson(`${API}/language-audit/overview`);
            if (!overview || (overview.totalContent === 0 && (!overview.enabledLanguages || overview.enabledLanguages.length === 0))) {
                renderNoDataBanner();
                return;
            }
            if (overview.enabledLanguages && overview.enabledLanguages.length > 0) {
                selectedLanguage = overview.enabledLanguages[0];
            }
            renderTabs();
            renderOverview();
        } catch (err) {
            renderNoDataBanner(err.message);
        }
    }

    function renderNoDataBanner(errorMsg) {
        const content = document.getElementById('lang-audit-content');
        content.innerHTML = `<div class="ept-banner" style="padding:24px;background:var(--ept-bg,#f8f9fa);border:1px solid var(--ept-border,#dee2e6);border-radius:6px;text-align:center;">
            ${errorMsg ? `<p style="margin:0 0 12px 0;color:var(--ept-danger,#dc3545);">Error: ${escHtml(errorMsg)}</p>` : ''}
            <p style="margin:0 0 12px 0;font-size:15px;">${EPT.s('languageaudit.banner_runjob', 'Run the [EditorPowertools] Content Analysis scheduled job to populate data.')}</p>
            <button id="ept-lang-run-job-btn" class="ept-btn ept-btn--primary">${EPT.s('languageaudit.btn_runnow', 'Run now')}</button>
        </div>`;
        const btn = document.getElementById('ept-lang-run-job-btn');
        if (btn) {
            btn.addEventListener('click', async () => {
                btn.disabled = true;
                btn.textContent = EPT.s('shared.starting', 'Starting...');
                try {
                    await EPT.postJson(window.EPT_API_URL + '/aggregation-start');
                    btn.textContent = EPT.s('languageaudit.btn_started', 'Job started, please refresh in a few minutes.');
                    btn.className = 'ept-btn';
                } catch (e) {
                    btn.textContent = EPT.s('languageaudit.btn_failed', 'Failed to start job');
                }
            });
        }
    }

    // ── Tabs ───────────────────────────────────────────────────────
    function renderTabs() {
        const tabs = document.getElementById('lang-audit-tabs');
        const tabDefs = [
            { id: 'overview', label: EPT.s('languageaudit.tab_overview', 'Overview') },
            { id: 'missing', label: EPT.s('languageaudit.tab_missing', 'Missing Translations') },
            { id: 'stale', label: EPT.s('languageaudit.tab_stale', 'Stale Translations') },
            { id: 'queue', label: EPT.s('languageaudit.tab_queue', 'Translation Queue') }
        ];

        tabs.innerHTML = tabDefs.map(t =>
            `<button class="ept-tab ${t.id === currentTab ? 'ept-tab--active' : ''}" data-tab="${t.id}">${t.label}</button>`
        ).join('');

        tabs.querySelectorAll('.ept-tab').forEach(btn => {
            btn.addEventListener('click', () => {
                currentTab = btn.dataset.tab;
                tabs.querySelectorAll('.ept-tab').forEach(b => b.classList.remove('ept-tab--active'));
                btn.classList.add('ept-tab--active');
                switchTab();
            });
        });
    }

    function switchTab() {
        document.getElementById('lang-audit-stats').innerHTML = '';
        document.getElementById('lang-audit-toolbar').innerHTML = '';
        document.getElementById('lang-audit-content').innerHTML = '';

        switch (currentTab) {
            case 'overview': renderOverview(); break;
            case 'missing': renderMissing(); break;
            case 'stale': renderStale(); break;
            case 'queue': renderQueue(); break;
        }
    }

    // ── Overview Tab ──────────────────────────────────────────────
    function renderOverview() {
        if (!overview) return;

        const stats = document.getElementById('lang-audit-stats');
        stats.innerHTML = `
            <div class="ept-stat"><div class="ept-stat__value">${overview.totalContent}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_totalcontent', 'Total Content')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${overview.enabledLanguages.length}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_languages', 'Languages')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${overview.missingTranslationsCount}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_missing', 'Missing Translations')}</div></div>
            <div class="ept-stat"><div class="ept-stat__value">${overview.staleTranslationsCount}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_stale', 'Stale (30+ days)')}</div></div>
        `;

        document.getElementById('lang-audit-toolbar').innerHTML = '';

        const content = document.getElementById('lang-audit-content');
        content.innerHTML = '';

        const card = document.createElement('div');
        card.className = 'ept-card';

        const header = document.createElement('div');
        header.className = 'ept-card__header';
        header.innerHTML = `<h3>${EPT.s('languageaudit.card_coverage', 'Language Coverage')}</h3>`;
        card.appendChild(header);

        const body = document.createElement('div');
        body.className = 'ept-card__body';

        if (overview.languageStats.length === 0) {
            EPT.showEmpty(body, EPT.s('languageaudit.empty_nodata', 'No language data available. Run the aggregation job to collect statistics.'));
        } else {
            const grid = document.createElement('div');
            grid.style.cssText = 'display:grid;grid-template-columns:repeat(auto-fill,minmax(280px,1fr));gap:16px;';

            overview.languageStats.forEach(stat => {
                const langCard = document.createElement('div');
                langCard.style.cssText = 'border:1px solid var(--ept-border,#ddd);border-radius:6px;padding:16px;';
                langCard.innerHTML = `
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px;">
                        <strong style="font-size:16px;">${escHtml(stat.language)}</strong>
                        <span class="ept-badge ${stat.coveragePercent >= 90 ? 'ept-badge--success' : stat.coveragePercent >= 50 ? 'ept-badge--warning' : 'ept-badge--danger'}">${stat.coveragePercent}%</span>
                    </div>
                    <div style="background:var(--ept-bg,#e9ecef);border-radius:4px;height:8px;margin-bottom:12px;overflow:hidden;">
                        <div style="background:${stat.coveragePercent >= 90 ? 'var(--ept-success,#28a745)' : stat.coveragePercent >= 50 ? 'var(--ept-warning,#ffc107)' : 'var(--ept-danger,#dc3545)'};height:100%;width:${stat.coveragePercent}%;transition:width 0.3s;"></div>
                    </div>
                    <div style="display:flex;justify-content:space-between;font-size:12px;color:var(--ept-muted,#6c757d);">
                        <span>${EPT.s('languageaudit.card_contentitems', '{0} content items').replace('{0}', stat.totalContent)}</span>
                        <span>${EPT.s('languageaudit.card_published', '{0} published').replace('{0}', stat.publishedCount)}</span>
                    </div>
                `;
                grid.appendChild(langCard);
            });

            body.appendChild(grid);
        }

        card.appendChild(body);
        content.appendChild(card);
    }

    // ── Missing Translations Tab ──────────────────────────────────
    async function renderMissing() {
        const toolbar = document.getElementById('lang-audit-toolbar');
        toolbar.innerHTML = `
            <label class="ept-muted" style="font-size:12px;margin-right:4px;">${EPT.s('languageaudit.lbl_language', 'Language:')}</label>
            <select id="lang-missing-select" class="ept-select">
                ${overview.enabledLanguages.map(l => `<option value="${l}" ${l === selectedLanguage ? 'selected' : ''}>${l}</option>`).join('')}
            </select>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="lang-missing-tree-btn">${EPT.icons.tree} ${EPT.s('languageaudit.btn_coveragetree', 'Coverage Tree')}</button>
        `;

        document.getElementById('lang-missing-select').addEventListener('change', (e) => {
            selectedLanguage = e.target.value;
            loadMissing();
        });

        document.getElementById('lang-missing-tree-btn').addEventListener('click', loadCoverageTree);

        await loadMissing();
    }

    async function loadMissing() {
        const content = document.getElementById('lang-audit-content');
        EPT.showLoading(content);

        const stats = document.getElementById('lang-audit-stats');
        stats.innerHTML = '';

        try {
            const data = await EPT.fetchJson(`${API}/language-audit/missing?language=${encodeURIComponent(selectedLanguage)}`);
            content.innerHTML = '';

            stats.innerHTML = `
                <div class="ept-stat"><div class="ept-stat__value">${data.length}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_missing_lang', 'Missing {0}').replace('{0}', escHtml(selectedLanguage))}</div></div>
            `;

            if (data.length === 0) {
                const card = document.createElement('div');
                card.className = 'ept-card';
                const body = document.createElement('div');
                body.className = 'ept-card__body';
                EPT.showEmpty(body, EPT.s('languageaudit.empty_all_translated', 'All content has been translated to {0}').replace('{0}', selectedLanguage));
                card.appendChild(body);
                content.appendChild(card);
                return;
            }

            const columns = [
                { key: 'contentId', label: EPT.s('languageaudit.col_id', 'ID'), align: 'right' },
                {
                    key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.contentName)}</strong> <span style="opacity:.4">\u2197</span></a>`;
                        return `<strong>${escHtml(r.contentName)}</strong>`;
                    }
                },
                { key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
                { key: 'masterLanguage', label: EPT.s('languageaudit.col_master', 'Master') },
                {
                    key: 'availableLanguages', label: EPT.s('languageaudit.col_available', 'Available Languages'), render: (r) =>
                        r.availableLanguages.map(l => `<span class="ept-badge ept-badge--default" style="margin-right:2px;">${escHtml(l)}</span>`).join('')
                },
                {
                    key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), render: (r) =>
                        `<span class="ept-truncate" title="${escAttr(r.breadcrumb)}">${escHtml(r.breadcrumb || '')}</span>`
                }
            ];

            const card = document.createElement('div');
            card.className = 'ept-card';
            const body = document.createElement('div');
            body.className = 'ept-card__body ept-card__body--flush';
            const tbl = EPT.createTable(columns, data, { defaultSort: 'breadcrumb' });
            body.appendChild(tbl.table);
            card.appendChild(body);
            content.appendChild(card);
        } catch (err) {
            content.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    async function loadCoverageTree() {
        const content = document.getElementById('lang-audit-content');
        EPT.showLoading(content);

        try {
            const tree = await EPT.fetchJson(`${API}/language-audit/coverage-tree?language=${encodeURIComponent(selectedLanguage)}`);
            content.innerHTML = '';

            if (tree.length === 0) {
                const card = document.createElement('div');
                card.className = 'ept-card';
                const body = document.createElement('div');
                body.className = 'ept-card__body';
                EPT.showEmpty(body, EPT.s('languageaudit.empty_no_tree', 'No coverage tree data available'));
                card.appendChild(body);
                content.appendChild(card);
                return;
            }

            const card = document.createElement('div');
            card.className = 'ept-card';
            const body = document.createElement('div');
            body.className = 'ept-card__body';
            body.appendChild(buildCoverageTreeUl(tree));
            card.appendChild(body);
            content.appendChild(card);
        } catch (err) {
            content.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    function buildCoverageTreeUl(nodes) {
        const ul = document.createElement('ul');
        ul.className = 'ept-tree';

        nodes.forEach(node => {
            const li = document.createElement('li');
            li.className = 'ept-tree__item';

            const hasChildren = node.children && node.children.length > 0;
            let childUl = null;
            let expanded = true;

            const line = document.createElement('span');
            line.className = 'ept-flex';
            line.style.alignItems = 'center';

            if (hasChildren) {
                const toggle = document.createElement('button');
                toggle.className = 'ept-tree__toggle';
                toggle.innerHTML = EPT.icons.chevronDown;
                toggle.addEventListener('click', () => {
                    expanded = !expanded;
                    toggle.innerHTML = expanded ? EPT.icons.chevronDown : EPT.icons.chevronRight;
                    if (childUl) childUl.style.display = expanded ? '' : 'none';
                });
                line.appendChild(toggle);
            } else {
                const spacer = document.createElement('span');
                spacer.style.cssText = 'width:24px;display:inline-block';
                line.appendChild(spacer);
            }

            const label = document.createElement('span');
            label.className = 'ept-tree__label';
            label.textContent = node.contentName;
            if (!node.hasLanguage) {
                label.style.color = 'var(--ept-danger, #dc3545)';
            }
            line.appendChild(label);

            // Coverage bar
            if (node.totalChildren > 0) {
                const barWrap = document.createElement('span');
                barWrap.style.cssText = 'display:inline-flex;align-items:center;margin-left:8px;gap:4px;';

                const bar = document.createElement('span');
                bar.style.cssText = `display:inline-block;width:80px;height:6px;background:var(--ept-bg,#e9ecef);border-radius:3px;overflow:hidden;`;
                const fill = document.createElement('span');
                const pct = node.coveragePercent;
                fill.style.cssText = `display:block;height:100%;width:${pct}%;background:${pct >= 90 ? 'var(--ept-success,#28a745)' : pct >= 50 ? 'var(--ept-warning,#ffc107)' : 'var(--ept-danger,#dc3545)'};`;
                bar.appendChild(fill);
                barWrap.appendChild(bar);

                const countLabel = document.createElement('span');
                countLabel.className = 'ept-muted';
                countLabel.style.fontSize = '11px';
                countLabel.textContent = `${node.childrenWithLanguage}/${node.totalChildren} (${pct}%)`;
                barWrap.appendChild(countLabel);

                line.appendChild(barWrap);
            }

            li.appendChild(line);

            if (hasChildren) {
                childUl = buildCoverageTreeUl(node.children);
                li.appendChild(childUl);
            }

            ul.appendChild(li);
        });

        return ul;
    }

    // ── Stale Translations Tab ────────────────────────────────────
    async function renderStale() {
        const toolbar = document.getElementById('lang-audit-toolbar');
        toolbar.innerHTML = `
            <label class="ept-muted" style="font-size:12px;margin-right:4px;">${EPT.s('languageaudit.lbl_threshold', 'Threshold (days):')}</label>
            <input type="number" id="lang-stale-threshold" class="ept-select" style="width:80px;" value="${staleThreshold}" min="1" />
            <label class="ept-muted" style="font-size:12px;margin-left:12px;margin-right:4px;">${EPT.s('languageaudit.lbl_language', 'Language:')}</label>
            <select id="lang-stale-lang" class="ept-select">
                <option value="">${EPT.s('languageaudit.lbl_alllanguages', 'All languages')}</option>
                ${overview.enabledLanguages.map(l => `<option value="${l}">${l}</option>`).join('')}
            </select>
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="lang-stale-apply">${EPT.icons.search} ${EPT.s('languageaudit.btn_apply', 'Apply')}</button>
        `;

        document.getElementById('lang-stale-apply').addEventListener('click', loadStale);
        document.getElementById('lang-stale-threshold').addEventListener('keydown', (e) => {
            if (e.key === 'Enter') loadStale();
        });

        await loadStale();
    }

    async function loadStale() {
        const threshold = parseInt(document.getElementById('lang-stale-threshold').value) || 30;
        staleThreshold = threshold;
        const lang = document.getElementById('lang-stale-lang')?.value || '';

        const content = document.getElementById('lang-audit-content');
        EPT.showLoading(content);

        const stats = document.getElementById('lang-audit-stats');
        stats.innerHTML = '';

        try {
            let url = `${API}/language-audit/stale?thresholdDays=${threshold}`;
            if (lang) url += `&language=${encodeURIComponent(lang)}`;

            const data = await EPT.fetchJson(url);
            content.innerHTML = '';

            stats.innerHTML = `
                <div class="ept-stat"><div class="ept-stat__value">${data.length}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_stale_count', 'Stale Translations')}</div></div>
            `;

            if (data.length === 0) {
                const card = document.createElement('div');
                card.className = 'ept-card';
                const body = document.createElement('div');
                body.className = 'ept-card__body';
                EPT.showEmpty(body, EPT.s('languageaudit.empty_no_stale', 'No stale translations found (threshold: {0} days)').replace('{0}', threshold));
                card.appendChild(body);
                content.appendChild(card);
                return;
            }

            const columns = [
                {
                    key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.contentName)}</strong> <span style="opacity:.4">\u2197</span></a>`;
                        return `<strong>${escHtml(r.contentName)}</strong>`;
                    }
                },
                { key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
                {
                    key: 'masterLanguage', label: EPT.s('languageaudit.col_masterlanguage', 'Master Language'), render: (r) =>
                        `${escHtml(r.masterLanguage)} <span class="ept-muted" style="font-size:11px;">(${formatDate(r.masterLastModified)})</span>`
                },
                {
                    key: 'otherLanguage', label: EPT.s('languageaudit.col_stalelanguage', 'Stale Language'), render: (r) =>
                        `${escHtml(r.otherLanguage)} <span class="ept-muted" style="font-size:11px;">(${formatDate(r.otherLastModified)})</span>`
                },
                {
                    key: 'daysBehind', label: EPT.s('languageaudit.col_daysbehind', 'Days Behind'), align: 'right', render: (r) => {
                        const cls = r.daysBehind > 90 ? 'danger' : r.daysBehind > 30 ? 'warning' : 'default';
                        return `<span class="ept-badge ept-badge--${cls}">${r.daysBehind}</span>`;
                    }
                },
                {
                    key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), render: (r) =>
                        `<span class="ept-truncate" title="${escAttr(r.breadcrumb)}">${escHtml(r.breadcrumb || '')}</span>`
                }
            ];

            const card = document.createElement('div');
            card.className = 'ept-card';
            const body = document.createElement('div');
            body.className = 'ept-card__body ept-card__body--flush';
            const tbl = EPT.createTable(columns, data, { defaultSort: 'daysBehind', defaultSortDir: 'desc' });
            body.appendChild(tbl.table);
            card.appendChild(body);
            content.appendChild(card);
        } catch (err) {
            content.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    // ── Translation Queue Tab ─────────────────────────────────────
    let queuePage = 1;
    const queuePageSize = 50;

    async function renderQueue() {
        const toolbar = document.getElementById('lang-audit-toolbar');
        toolbar.innerHTML = `
            <label class="ept-muted" style="font-size:12px;margin-right:4px;">${EPT.s('languageaudit.lbl_targetlang', 'Target Language:')}</label>
            <select id="lang-queue-target" class="ept-select">
                ${overview.enabledLanguages.map(l => `<option value="${l}" ${l === selectedLanguage ? 'selected' : ''}>${l}</option>`).join('')}
            </select>
            <label class="ept-muted" style="font-size:12px;margin-left:12px;margin-right:4px;">${EPT.s('languageaudit.lbl_contenttype', 'Content Type:')}</label>
            <input type="text" id="lang-queue-type" class="ept-select" style="width:150px;" placeholder="${EPT.s('languageaudit.lbl_alltypes', 'All types')}" />
            <div class="ept-toolbar__spacer"></div>
            <button class="ept-btn" id="lang-queue-export">${EPT.icons.download} ${EPT.s('languageaudit.btn_exportcsv', 'Export CSV')}</button>
        `;

        document.getElementById('lang-queue-target').addEventListener('change', () => { queuePage = 1; loadQueue(); });
        document.getElementById('lang-queue-type').addEventListener('keydown', (e) => {
            if (e.key === 'Enter') { queuePage = 1; loadQueue(); }
        });
        document.getElementById('lang-queue-export').addEventListener('click', exportQueue);

        queuePage = 1;
        await loadQueue();
    }

    async function loadQueue() {
        const targetLang = document.getElementById('lang-queue-target')?.value || selectedLanguage;
        const contentType = document.getElementById('lang-queue-type')?.value || '';

        const content = document.getElementById('lang-audit-content');
        EPT.showLoading(content);

        const stats = document.getElementById('lang-audit-stats');
        stats.innerHTML = '';

        try {
            let url = `${API}/language-audit/queue?targetLanguage=${encodeURIComponent(targetLang)}&page=${queuePage}&pageSize=${queuePageSize}`;
            if (contentType) url += `&contentType=${encodeURIComponent(contentType)}`;

            const result = await EPT.fetchJson(url);
            content.innerHTML = '';

            stats.innerHTML = `
                <div class="ept-stat"><div class="ept-stat__value">${result.totalCount}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_itemstotranslate', 'Items to Translate')}</div></div>
                <div class="ept-stat"><div class="ept-stat__value">${result.page}/${result.totalPages || 1}</div><div class="ept-stat__label">${EPT.s('languageaudit.stat_page', 'Page')}</div></div>
            `;

            if (result.items.length === 0) {
                const card = document.createElement('div');
                card.className = 'ept-card';
                const body = document.createElement('div');
                body.className = 'ept-card__body';
                EPT.showEmpty(body, EPT.s('languageaudit.empty_no_queue', 'No content needs translation to {0}').replace('{0}', targetLang));
                card.appendChild(body);
                content.appendChild(card);
                return;
            }

            const columns = [
                { key: 'contentId', label: EPT.s('languageaudit.col_id', 'ID'), align: 'right' },
                {
                    key: 'contentName', label: EPT.s('languageaudit.col_name', 'Name'), render: (r) => {
                        if (r.editUrl) return `<a href="${escAttr(r.editUrl)}" target="_blank" style="color:inherit;text-decoration:none"><strong>${escHtml(r.contentName)}</strong> <span style="opacity:.4">\u2197</span></a>`;
                        return `<strong>${escHtml(r.contentName)}</strong>`;
                    }
                },
                { key: 'contentTypeName', label: EPT.s('languageaudit.col_type', 'Type') },
                { key: 'masterLanguage', label: EPT.s('languageaudit.col_master', 'Master') },
                {
                    key: 'masterLastModified', label: EPT.s('languageaudit.col_lastupdated', 'Last Updated'), render: (r) => formatDate(r.masterLastModified)
                },
                {
                    key: 'availableLanguages', label: EPT.s('languageaudit.col_available_short', 'Available'), render: (r) =>
                        r.availableLanguages.map(l => `<span class="ept-badge ept-badge--default" style="margin-right:2px;">${escHtml(l)}</span>`).join('')
                },
                {
                    key: 'breadcrumb', label: EPT.s('languageaudit.col_location', 'Location'), render: (r) =>
                        `<span class="ept-truncate" title="${escAttr(r.breadcrumb)}">${escHtml(r.breadcrumb || '')}</span>`
                }
            ];

            const card = document.createElement('div');
            card.className = 'ept-card';
            const body = document.createElement('div');
            body.className = 'ept-card__body ept-card__body--flush';
            const tbl = EPT.createTable(columns, result.items, { defaultSort: 'masterLastModified', defaultSortDir: 'desc' });
            body.appendChild(tbl.table);
            card.appendChild(body);

            // Pagination
            if (result.totalPages > 1) {
                const pager = document.createElement('div');
                pager.style.cssText = 'display:flex;justify-content:center;gap:8px;padding:12px;';

                if (queuePage > 1) {
                    const prev = document.createElement('button');
                    prev.className = 'ept-btn ept-btn--sm';
                    prev.textContent = EPT.s('languageaudit.btn_previous', 'Previous');
                    prev.addEventListener('click', () => { queuePage--; loadQueue(); });
                    pager.appendChild(prev);
                }

                const info = document.createElement('span');
                info.className = 'ept-muted';
                info.style.cssText = 'line-height:32px;font-size:12px;';
                info.textContent = EPT.s('languageaudit.page_info', 'Page {0} of {1}').replace('{0}', result.page).replace('{1}', result.totalPages);
                pager.appendChild(info);

                if (queuePage < result.totalPages) {
                    const next = document.createElement('button');
                    next.className = 'ept-btn ept-btn--sm';
                    next.textContent = EPT.s('languageaudit.btn_next', 'Next');
                    next.addEventListener('click', () => { queuePage++; loadQueue(); });
                    pager.appendChild(next);
                }

                card.appendChild(pager);
            }

            content.appendChild(card);
        } catch (err) {
            content.innerHTML = `<div class="ept-empty"><p>Error: ${err.message}</p></div>`;
        }
    }

    async function exportQueue() {
        const targetLang = document.getElementById('lang-queue-target')?.value || selectedLanguage;
        try {
            const data = await EPT.fetchJson(`${API}/language-audit/export?targetLanguage=${encodeURIComponent(targetLang)}`);
            const columns = [
                { key: 'contentId', label: 'Content ID' },
                { key: 'contentName', label: 'Name' },
                { key: 'contentTypeName', label: 'Content Type' },
                { key: 'masterLanguage', label: 'Master Language' },
                { key: 'masterLastModified', label: 'Master Last Modified' },
                { key: 'availableLanguages', label: 'Available Languages' },
                { key: 'breadcrumb', label: 'Location' }
            ];
            // Flatten availableLanguages for CSV
            const flat = data.map(r => ({
                ...r,
                availableLanguages: Array.isArray(r.availableLanguages) ? r.availableLanguages.join(', ') : r.availableLanguages
            }));
            const ts = new Date().toISOString().slice(0, 19).replace(/[T:]/g, '-');
            EPT.downloadCsv(`TranslationQueue_${targetLang}_${ts}.csv`, columns, flat);
        } catch (err) {
            console.error('Export failed:', err);
        }
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

    function formatDate(dateStr) {
        if (!dateStr) return '-';
        try {
            const d = new Date(dateStr);
            return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
        } catch {
            return dateStr;
        }
    }

    // Boot
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
