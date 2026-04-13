/**
 * Content Statistics Dashboard - SVG charts and data visualization
 */
(function () {
    'use strict';

    var API_URL = window.EPT_BASE_URL + 'ContentStatisticsApi/GetDashboard';
    var CMS_URL = window.EPT_CMS_URL || '';
    var jobStatus = null;

    var CHART_COLORS = [
        '#4e79a7', '#f28e2b', '#e15759', '#76b7b2',
        '#59a14f', '#edc948', '#b07aa1', '#ff9da7',
        '#9c755f', '#bab0ac'
    ];

    var root;

    function init() {
        root = document.getElementById('content-statistics-root');
        if (!root) return;
        EPT.showLoading(root);
        loadDashboard();
    }

    async function loadDashboard() {
        var results = await Promise.allSettled([
            EPT.fetchJson(API_URL),
            EPT.fetchJson(window.EPT_BASE_URL + 'ContentStatisticsApi/GetAggregationStatus')
        ]);
        jobStatus = results[1].status === 'fulfilled' ? results[1].value : null;

        if (results[0].status === 'rejected') {
            root.innerHTML = '<div class="ept-card"><div class="ept-card__body">' +
                '<p style="color:var(--ept-danger)">' + EPT.s('contentstatistics.error_load', 'Failed to load statistics from API.') + '</p>' +
                '</div></div>';
            prependJobAlert();
            return;
        }

        try {
            render(results[0].value);
        } catch (err) {
            console.error('[EditorPowertools] Content Statistics render error:', err);
            root.innerHTML = '<div class="ept-card"><div class="ept-card__body">' +
                '<p style="color:var(--ept-danger)">' + EPT.s('contentstatistics.error_render', 'Error rendering statistics dashboard.') + '</p>' +
                '<pre style="font-size:11px;color:#999;margin-top:8px">' + (err.stack || err.message) + '</pre>' +
                '</div></div>';
        }
    }

    function prependJobAlert() {
        if (!jobStatus) return;
        var alertEl = EPT.renderJobAlert(jobStatus, window.EPT_BASE_URL + 'ContentStatisticsApi/StartAggregationJob');
        if (!alertEl) return;
        root.insertBefore(alertEl, root.firstChild);
    }

    function render(data) {
        root.innerHTML = '';

        // If no data at all, show empty state with run-job prompt
        if (!data || !data.summary) {
            root.innerHTML = '<div class="ept-card"><div class="ept-card__body">' +
                '<p style="text-align:center;color:var(--ept-muted,#888);">' + EPT.s('contentstatistics.empty_nodata', 'No statistics data available yet.') + '</p>' +
                '</div></div>';
            prependJobAlert();
            return;
        }

        // Summary stat cards
        renderSummary(data.summary);

        // Charts row
        var chartsRow = el('div', { className: 'ept-stats-charts-row' });
        root.appendChild(chartsRow);

        // Type distribution (pie/donut)
        var pieCard = createCard(EPT.s('contentstatistics.chart_byctype', 'Content Type Distribution'));
        chartsRow.appendChild(pieCard.card);
        if (data.typeDistribution && data.typeDistribution.length > 0) {
            renderDonutChart(pieCard.body, data.typeDistribution);
        } else {
            pieCard.body.innerHTML = '<p class="ept-empty-msg">' + EPT.s('contentstatistics.empty_notype', 'No type statistics available. Run the scheduled job first.') + '</p>';
        }

        // Creation over time (bar chart)
        var barCard = createCard(EPT.s('contentstatistics.chart_created', 'Content Created per Month'));
        chartsRow.appendChild(barCard.card);
        if (data.creationOverTime && data.creationOverTime.length > 0) {
            renderBarChart(barCard.body, data.creationOverTime);
        } else {
            barCard.body.innerHTML = '<p class="ept-empty-msg">' + EPT.s('contentstatistics.empty_nocreation', 'No creation data available.') + '</p>';
        }

        // Stale content (horizontal bars)
        var staleCard = createCard(EPT.s('contentstatistics.chart_staleness', 'Oldest Content (by Last Modified)'));
        root.appendChild(staleCard.card);
        if (data.staleContent && data.staleContent.length > 0) {
            renderStaleChart(staleCard.body, data.staleContent);
        } else {
            staleCard.body.innerHTML = '<p class="ept-empty-msg">' + EPT.s('contentstatistics.empty_nopage', 'No page data available.') + '</p>';
        }

        // Editor activity table
        var editorCard = createCard(EPT.s('contentstatistics.chart_editoractivity', 'Top Editors by Activity'));
        root.appendChild(editorCard.card);
        if (data.topEditors && data.topEditors.length > 0) {
            renderEditorTable(editorCard.body, data.topEditors);
        } else {
            editorCard.body.innerHTML = '<p class="ept-empty-msg">' + EPT.s('contentstatistics.empty_noeditor', 'No editor activity data available.') + '</p>';
        }

        injectStyles();
        prependJobAlert();
    }

    // ── Summary Cards ────────────────────────────────────────────

    function renderSummary(s) {
        var stats = el('div', { className: 'ept-stats' });
        stats.appendChild(statCard(EPT.s('contentstatistics.stat_totalcontent', 'Total Content'), s.totalContent));
        stats.appendChild(statCard(EPT.s('contentstatistics.stat_pages', 'Pages'), s.totalPages));
        stats.appendChild(statCard(EPT.s('contentstatistics.stat_blocks', 'Blocks'), s.totalBlocks));
        stats.appendChild(statCard(EPT.s('contentstatistics.stat_media', 'Media'), s.totalMedia));
        stats.appendChild(statCard(EPT.s('contentstatistics.stat_avgversions', 'Avg Versions'), s.averageVersionsPerItem));
        root.appendChild(stats);
    }

    function statCard(label, value) {
        var d = el('div', { className: 'ept-stat' });
        var v = el('div', { className: 'ept-stat__value' });
        v.textContent = typeof value === 'number' ? formatNumber(value) : (value || '-');
        var l = el('div', { className: 'ept-stat__label' });
        l.textContent = label;
        d.appendChild(v);
        d.appendChild(l);
        return d;
    }

    // ── Donut Chart ──────────────────────────────────────────────

    function renderDonutChart(container, data) {
        var total = data.reduce(function (sum, d) { return sum + d.count; }, 0);
        if (total === 0) return;

        var size = 220;
        var cx = size / 2;
        var cy = size / 2;
        var outerR = 95;
        var innerR = 55;

        var svg = createSvg(size, size);
        var angle = -Math.PI / 2;

        for (var i = 0; i < data.length; i++) {
            var slice = data[i];
            var sliceAngle = (slice.count / total) * 2 * Math.PI;
            var endAngle = angle + sliceAngle;
            var largeArc = sliceAngle > Math.PI ? 1 : 0;

            var x1o = cx + outerR * Math.cos(angle);
            var y1o = cy + outerR * Math.sin(angle);
            var x2o = cx + outerR * Math.cos(endAngle);
            var y2o = cy + outerR * Math.sin(endAngle);
            var x1i = cx + innerR * Math.cos(endAngle);
            var y1i = cy + innerR * Math.sin(endAngle);
            var x2i = cx + innerR * Math.cos(angle);
            var y2i = cy + innerR * Math.sin(angle);

            var pathD = [
                'M', x1o, y1o,
                'A', outerR, outerR, 0, largeArc, 1, x2o, y2o,
                'L', x1i, y1i,
                'A', innerR, innerR, 0, largeArc, 0, x2i, y2i,
                'Z'
            ].join(' ');

            var path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
            path.setAttribute('d', pathD);
            path.setAttribute('fill', CHART_COLORS[i % CHART_COLORS.length]);
            path.setAttribute('stroke', '#fff');
            path.setAttribute('stroke-width', '2');

            var titleEl = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            titleEl.textContent = slice.category + ': ' + formatNumber(slice.count) + ' (' + Math.round(slice.count / total * 100) + '%)';
            path.appendChild(titleEl);

            svg.appendChild(path);
            angle = endAngle;
        }

        // Center label
        var centerText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        centerText.setAttribute('x', cx);
        centerText.setAttribute('y', cy - 5);
        centerText.setAttribute('text-anchor', 'middle');
        centerText.setAttribute('font-size', '22');
        centerText.setAttribute('font-weight', 'bold');
        centerText.setAttribute('fill', 'var(--ept-text, #333)');
        centerText.textContent = formatNumber(total);
        svg.appendChild(centerText);

        var subText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        subText.setAttribute('x', cx);
        subText.setAttribute('y', cy + 16);
        subText.setAttribute('text-anchor', 'middle');
        subText.setAttribute('font-size', '11');
        subText.setAttribute('fill', 'var(--ept-muted, #888)');
        subText.textContent = EPT.s('contentstatistics.chart_label_total', 'total');
        svg.appendChild(subText);

        var chartWrap = el('div', { className: 'cst-chart-wrap' });
        chartWrap.appendChild(svg);

        // Legend
        var legend = el('div', { className: 'cst-legend' });
        for (var j = 0; j < data.length; j++) {
            var item = el('div', { className: 'cst-legend__item' });
            var swatch = el('span', { className: 'cst-legend__swatch' });
            swatch.style.backgroundColor = CHART_COLORS[j % CHART_COLORS.length];
            var label = el('span');
            label.textContent = data[j].category + ' (' + formatNumber(data[j].count) + ')';
            item.appendChild(swatch);
            item.appendChild(label);
            legend.appendChild(item);
        }
        chartWrap.appendChild(legend);
        container.appendChild(chartWrap);
    }

    // ── Bar Chart (vertical) ─────────────────────────────────────

    function renderBarChart(container, data) {
        var maxVal = Math.max.apply(null, data.map(function (d) { return d.count; }));
        if (maxVal === 0) maxVal = 1;

        var barWidth = 32;
        var gap = 8;
        var chartH = 180;
        var labelH = 50;
        var svgW = data.length * (barWidth + gap) + gap + 40;
        var svgH = chartH + labelH + 20;

        var svg = createSvg(svgW, svgH);

        // Horizontal grid lines
        for (var g = 0; g <= 4; g++) {
            var gy = chartH - (g / 4) * chartH + 10;
            var gridLine = document.createElementNS('http://www.w3.org/2000/svg', 'line');
            gridLine.setAttribute('x1', 35);
            gridLine.setAttribute('y1', gy);
            gridLine.setAttribute('x2', svgW);
            gridLine.setAttribute('y2', gy);
            gridLine.setAttribute('stroke', 'var(--ept-border, #e0e0e0)');
            gridLine.setAttribute('stroke-width', '1');
            svg.appendChild(gridLine);

            var gridLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            gridLabel.setAttribute('x', 30);
            gridLabel.setAttribute('y', gy + 4);
            gridLabel.setAttribute('text-anchor', 'end');
            gridLabel.setAttribute('font-size', '10');
            gridLabel.setAttribute('fill', 'var(--ept-muted, #888)');
            gridLabel.textContent = Math.round(maxVal * g / 4);
            svg.appendChild(gridLabel);
        }

        for (var i = 0; i < data.length; i++) {
            var d = data[i];
            var barH = (d.count / maxVal) * chartH;
            var x = 40 + i * (barWidth + gap);
            var y = chartH - barH + 10;

            var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', x);
            rect.setAttribute('y', y);
            rect.setAttribute('width', barWidth);
            rect.setAttribute('height', Math.max(barH, 1));
            rect.setAttribute('fill', CHART_COLORS[0]);
            rect.setAttribute('rx', '3');

            var titleEl = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            titleEl.textContent = d.month + ': ' + EPT.s('contentstatistics.chart_tooltip_items', '{0} items').replace('{0}', d.count);
            rect.appendChild(titleEl);
            svg.appendChild(rect);

            // Value on top
            if (d.count > 0) {
                var valText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
                valText.setAttribute('x', x + barWidth / 2);
                valText.setAttribute('y', y - 4);
                valText.setAttribute('text-anchor', 'middle');
                valText.setAttribute('font-size', '10');
                valText.setAttribute('fill', 'var(--ept-text, #333)');
                valText.textContent = d.count;
                svg.appendChild(valText);
            }

            // Month label (rotated)
            var monthLabel = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            var labelX = x + barWidth / 2;
            var labelY = chartH + 22;
            monthLabel.setAttribute('x', labelX);
            monthLabel.setAttribute('y', labelY);
            monthLabel.setAttribute('text-anchor', 'end');
            monthLabel.setAttribute('font-size', '10');
            monthLabel.setAttribute('fill', 'var(--ept-muted, #888)');
            monthLabel.setAttribute('transform', 'rotate(-45,' + labelX + ',' + labelY + ')');
            monthLabel.textContent = formatMonth(d.month);
            svg.appendChild(monthLabel);
        }

        var scrollWrap = el('div', { className: 'cst-bar-scroll' });
        scrollWrap.appendChild(svg);
        container.appendChild(scrollWrap);
    }

    // ── Stale Content (horizontal bar chart) ─────────────────────

    function renderStaleChart(container, data) {
        var maxDays = Math.max.apply(null, data.map(function (d) { return d.daysSinceModified; }));
        if (maxDays === 0) maxDays = 1;

        var barH = 24;
        var gap = 4;
        var labelW = 220;
        var chartW = 500;
        var svgW = labelW + chartW + 80;
        var svgH = data.length * (barH + gap) + 10;

        var svg = createSvg(svgW, svgH);
        svg.style.width = '100%';
        svg.setAttribute('viewBox', '0 0 ' + svgW + ' ' + svgH);

        for (var i = 0; i < data.length; i++) {
            var d = data[i];
            var y = i * (barH + gap) + 2;
            var w = (d.daysSinceModified / maxDays) * chartW;

            // Name label
            var nameText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            nameText.setAttribute('x', labelW - 8);
            nameText.setAttribute('y', y + barH / 2 + 4);
            nameText.setAttribute('text-anchor', 'end');
            nameText.setAttribute('font-size', '11');
            nameText.setAttribute('fill', 'var(--ept-text, #333)');
            nameText.textContent = truncate(d.name, 30);

            var nameTitleEl = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            nameTitleEl.textContent = d.name + ' (' + d.contentTypeName + ')';
            nameText.appendChild(nameTitleEl);
            svg.appendChild(nameText);

            // Bar
            var color = d.daysSinceModified > 365 * 2 ? '#e15759' :
                        d.daysSinceModified > 365 ? '#f28e2b' : '#4e79a7';

            var rect = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
            rect.setAttribute('x', labelW);
            rect.setAttribute('y', y);
            rect.setAttribute('width', Math.max(w, 2));
            rect.setAttribute('height', barH);
            rect.setAttribute('fill', color);
            rect.setAttribute('rx', '3');
            rect.style.cursor = 'pointer';
            rect.onclick = (function (url) { return function () { if (url) window.location.href = url; }; })(d.editUrl);

            var barTitle = document.createElementNS('http://www.w3.org/2000/svg', 'title');
            barTitle.textContent = EPT.s('contentstatistics.chart_tooltip_daymod', '{0} - {1} days since modified ({2})').replace('{0}', d.name).replace('{1}', d.daysSinceModified).replace('{2}', formatDate(d.lastModified));
            rect.appendChild(barTitle);
            svg.appendChild(rect);

            // Days label
            var daysText = document.createElementNS('http://www.w3.org/2000/svg', 'text');
            daysText.setAttribute('x', labelW + Math.max(w, 2) + 6);
            daysText.setAttribute('y', y + barH / 2 + 4);
            daysText.setAttribute('font-size', '10');
            daysText.setAttribute('fill', 'var(--ept-muted, #888)');
            daysText.textContent = EPT.s('contentstatistics.chart_label_days', '{0} days').replace('{0}', d.daysSinceModified);
            svg.appendChild(daysText);
        }

        container.appendChild(svg);
    }

    // ── Editor Activity Table ────────────────────────────────────

    function renderEditorTable(container, editors) {
        var columns = [
            { key: 'username', label: EPT.s('contentstatistics.col_editor', 'Editor'), sortable: true },
            { key: 'editCount', label: EPT.s('contentstatistics.col_edits', 'Edits'), sortable: true, align: 'right' },
            { key: 'publishCount', label: EPT.s('contentstatistics.col_publishes', 'Publishes'), sortable: true, align: 'right' },
            {
                key: 'lastActive', label: EPT.s('contentstatistics.col_lastactive', 'Last Active'), sortable: true,
                render: function (val) { return val ? formatDate(val) : '-'; }
            }
        ];

        var result = EPT.createTable(columns, editors, { defaultSort: 'editCount', defaultSortDir: 'desc' });
        container.appendChild(result.table);
    }

    // ── SVG / DOM Helpers ────────────────────────────────────────

    function createSvg(w, h) {
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('width', w);
        svg.setAttribute('height', h);
        svg.setAttribute('xmlns', 'http://www.w3.org/2000/svg');
        return svg;
    }

    function el(tag, attrs) {
        var e = document.createElement(tag);
        if (attrs) {
            for (var k in attrs) {
                if (attrs.hasOwnProperty(k)) e[k] = attrs[k];
            }
        }
        return e;
    }

    function createCard(title) {
        var card = el('div', { className: 'ept-card' });
        var header = el('div', { className: 'ept-card__header' });
        var h3 = el('h3');
        h3.textContent = title;
        header.appendChild(h3);
        card.appendChild(header);
        var body = el('div', { className: 'ept-card__body' });
        card.appendChild(body);
        return { card: card, body: body };
    }

    function formatNumber(n) {
        if (n == null) return '-';
        return n.toLocaleString();
    }

    function formatMonth(ym) {
        // "2025-04" -> "Apr 25"
        var parts = ym.split('-');
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
                      'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        var mi = parseInt(parts[1], 10) - 1;
        return months[mi] + ' ' + parts[0].substring(2);
    }

    function formatDate(dateStr) {
        if (!dateStr) return '-';
        var d = new Date(dateStr);
        return d.toLocaleDateString();
    }

    function truncate(str, maxLen) {
        if (!str) return '';
        return str.length > maxLen ? str.substring(0, maxLen - 1) + '\u2026' : str;
    }

    // ── Inject tool-specific styles ──────────────────────────────

    function injectStyles() {
        if (document.getElementById('cst-styles')) return;
        var style = document.createElement('style');
        style.id = 'cst-styles';
        style.textContent = [
            '.ept-stats-charts-row { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 16px; }',
            '@media (max-width: 900px) { .ept-stats-charts-row { grid-template-columns: 1fr; } }',
            '.cst-chart-wrap { display: flex; align-items: center; gap: 24px; flex-wrap: wrap; justify-content: center; padding: 12px 0; }',
            '.cst-legend { display: flex; flex-direction: column; gap: 6px; }',
            '.cst-legend__item { display: flex; align-items: center; gap: 8px; font-size: 13px; }',
            '.cst-legend__swatch { width: 14px; height: 14px; border-radius: 3px; flex-shrink: 0; }',
            '.cst-bar-scroll { overflow-x: auto; padding: 8px 0; }',
            '.ept-empty-msg { color: var(--ept-muted, #888); font-style: italic; text-align: center; padding: 24px 0; }'
        ].join('\n');
        document.head.appendChild(style);
    }

    // ── Bootstrap ────────────────────────────────────────────────

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
