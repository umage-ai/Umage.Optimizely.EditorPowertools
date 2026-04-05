/**
 * Scheduled Jobs Gantt - Interactive timeline of scheduled job executions
 */
(function () {
    'use strict';

    var API_BASE = window.EPT_API_URL + '/jobs-gantt/';

    var ROW_HEIGHT = 36;
    var ROW_GAP = 2;
    var HEADER_HEIGHT = 50;
    var JOB_COL_WIDTH = 240;
    var MIN_BAR_WIDTH = 4;
    var FETCH_PADDING_HOURS = 12;

    var state = {
        jobs: [],
        executions: [],
        centerDate: new Date(),
        viewRangeHours: 48,
        pixelsPerHour: 60,
        isLoading: false,
        fetchedFrom: null,
        fetchedTo: null
    };

    var container;
    var tooltip;
    var nowLineInterval;

    function init() {
        container = document.getElementById('gantt-content');
        if (!container) return;

        injectStyles();
        render();
        loadData();
    }

    // ── Data Loading ──────────────────────────────────────────────

    function getViewRange() {
        var halfMs = (state.viewRangeHours / 2) * 3600000;
        var center = state.centerDate.getTime();
        return {
            from: new Date(center - halfMs),
            to: new Date(center + halfMs)
        };
    }

    function getFetchRange() {
        var range = getViewRange();
        var pad = FETCH_PADDING_HOURS * 3600000;
        return {
            from: new Date(range.from.getTime() - pad),
            to: new Date(range.to.getTime() + pad)
        };
    }

    function needsFetch() {
        if (!state.fetchedFrom || !state.fetchedTo) return true;
        var range = getViewRange();
        return range.from < state.fetchedFrom || range.to > state.fetchedTo;
    }

    async function loadData() {
        if (state.isLoading) return;
        state.isLoading = true;
        renderLoading();

        try {
            var range = getFetchRange();
            var url = API_BASE + 'gantt-data?from=' +
                range.from.toISOString() + '&to=' + range.to.toISOString();

            var data = await EPT.fetchJson(url);

            state.jobs = data.jobs || [];
            state.executions = (data.executions || []).map(function (e) {
                e.startUtc = new Date(e.startUtc);
                e.endUtc = e.endUtc ? new Date(e.endUtc) : null;
                return e;
            });

            // Sort jobs: enabled first, then alphabetically
            state.jobs.sort(function (a, b) {
                if (a.isEnabled !== b.isEnabled) return a.isEnabled ? -1 : 1;
                return a.name.localeCompare(b.name);
            });

            state.fetchedFrom = range.from;
            state.fetchedTo = range.to;
        } catch (err) {
            container.innerHTML = '<div class="ept-alert ept-alert--danger">' +
                EPT.s('gantt.error_load', 'Failed to load Gantt data: {0}').replace('{0}', err.message || err) + '</div>';
            state.isLoading = false;
            return;
        }

        state.isLoading = false;
        render();
    }

    // ── Rendering ─────────────────────────────────────────────────

    function renderLoading() {
        // Only show loading if we have no data yet
        if (state.jobs.length === 0) {
            EPT.showLoading(container);
        }
    }

    function render() {
        container.innerHTML = '';

        if (state.jobs.length === 0 && !state.isLoading) {
            EPT.showEmpty(container, EPT.s('gantt.empty_nojobs', 'No scheduled jobs found.'));
            return;
        }

        // Stats
        renderStats();

        // Toolbar with navigation
        renderToolbar();

        // Gantt chart
        renderGantt();

        // Start now-line updater
        startNowLineUpdater();
    }

    function renderStats() {
        var total = state.jobs.length;
        var enabled = state.jobs.filter(function (j) { return j.isEnabled; }).length;
        var running = state.jobs.filter(function (j) { return j.isRunning; }).length;
        var execCount = state.executions.filter(function (e) { return !e.isPlanned; }).length;

        var statsEl = document.createElement('div');
        statsEl.className = 'ept-stats';
        statsEl.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + total + '</div><div class="ept-stat__label">' + EPT.s('gantt.stat_totaljobs', 'Total Jobs') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + enabled + '</div><div class="ept-stat__label">' + EPT.s('gantt.stat_enabled', 'Enabled') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + running + '</div><div class="ept-stat__label">' + EPT.s('gantt.stat_running', 'Running Now') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + execCount + '</div><div class="ept-stat__label">' + EPT.s('gantt.stat_executions', 'Executions in View') + '</div></div>';
        container.appendChild(statsEl);
    }

    function renderToolbar() {
        var range = getViewRange();
        var toolbar = document.createElement('div');
        toolbar.className = 'ept-toolbar';

        // Navigation buttons
        var prevBtn = createButton(EPT.s('gantt.btn_previous', 'Previous'), function () { navigate(-0.5); });
        var todayBtn = createButton(EPT.s('gantt.btn_today', 'Today'), function () { navigateToday(); });
        todayBtn.className += ' ept-btn--primary';
        var nextBtn = createButton(EPT.s('gantt.btn_next', 'Next'), function () { navigate(0.5); });

        // Date range display
        var rangeLabel = document.createElement('span');
        rangeLabel.style.cssText = 'font-size:13px;color:var(--ept-text-secondary);margin:0 8px;';
        rangeLabel.textContent = formatDateShort(range.from) + '  \u2013  ' + formatDateShort(range.to);

        // Zoom controls
        var zoomOutBtn = createButton('\u2212', function () { zoom(0.5); });
        zoomOutBtn.title = 'Zoom out (show more time)';
        var zoomInBtn = createButton('+', function () { zoom(2); });
        zoomInBtn.title = 'Zoom in (show less time)';
        var zoomLabel = document.createElement('span');
        zoomLabel.style.cssText = 'font-size:12px;color:var(--ept-text-muted);';
        zoomLabel.textContent = EPT.s('gantt.lbl_viewhours', '{0}h view').replace('{0}', state.viewRangeHours);

        toolbar.appendChild(prevBtn);
        toolbar.appendChild(todayBtn);
        toolbar.appendChild(nextBtn);
        toolbar.appendChild(rangeLabel);

        var spacer = document.createElement('span');
        spacer.className = 'ept-toolbar__spacer';
        toolbar.appendChild(spacer);

        toolbar.appendChild(zoomOutBtn);
        toolbar.appendChild(zoomLabel);
        toolbar.appendChild(zoomInBtn);

        container.appendChild(toolbar);
    }

    function renderGantt() {
        var range = getViewRange();
        var totalWidth = state.viewRangeHours * state.pixelsPerHour;
        var totalHeight = HEADER_HEIGHT + state.jobs.length * (ROW_HEIGHT + ROW_GAP);

        // Outer wrapper
        var wrapper = document.createElement('div');
        wrapper.className = 'gantt-wrapper';
        wrapper.style.cssText = 'display:flex;background:var(--ept-surface);border:1px solid var(--ept-border);border-radius:var(--ept-radius-lg);overflow:hidden;';

        // Left column: job names
        var jobCol = document.createElement('div');
        jobCol.className = 'gantt-job-col';
        jobCol.style.cssText = 'width:' + JOB_COL_WIDTH + 'px;min-width:' + JOB_COL_WIDTH + 'px;border-right:1px solid var(--ept-border);background:var(--ept-surface);z-index:2;';

        // Job column header
        var jobColHeader = document.createElement('div');
        jobColHeader.className = 'gantt-job-col-header';
        jobColHeader.style.cssText = 'height:' + HEADER_HEIGHT + 'px;display:flex;align-items:center;padding:0 12px;font-weight:600;font-size:12px;color:var(--ept-text-secondary);border-bottom:1px solid var(--ept-border);';
        jobColHeader.textContent = EPT.s('gantt.col_job', 'Job') + ' (' + state.jobs.length + ')';
        jobCol.appendChild(jobColHeader);

        // Job name rows
        var jobRows = document.createElement('div');
        jobRows.className = 'gantt-job-rows';
        state.jobs.forEach(function (job, i) {
            var row = document.createElement('div');
            row.className = 'gantt-job-row';
            row.style.cssText = 'height:' + ROW_HEIGHT + 'px;display:flex;align-items:center;padding:0 12px;gap:6px;' +
                'border-bottom:1px solid var(--ept-border-light);cursor:pointer;font-size:12px;' +
                (i % 2 === 0 ? '' : 'background:var(--ept-bg);');

            // Status dot
            var dot = document.createElement('span');
            dot.style.cssText = 'width:8px;height:8px;border-radius:50%;flex-shrink:0;';
            if (job.isRunning) {
                dot.style.background = 'var(--ept-primary)';
                dot.title = 'Running';
            } else if (job.isEnabled) {
                dot.style.background = 'var(--ept-success)';
                dot.title = 'Enabled';
            } else {
                dot.style.background = 'var(--ept-text-muted)';
                dot.title = 'Disabled';
            }
            row.appendChild(dot);

            var nameEl = document.createElement('span');
            nameEl.className = 'gantt-job-name';
            nameEl.style.cssText = 'overflow:hidden;text-overflow:ellipsis;white-space:nowrap;flex:1;';
            nameEl.textContent = job.name;
            nameEl.title = job.name;
            if (!job.isEnabled) nameEl.style.color = 'var(--ept-text-muted)';
            row.appendChild(nameEl);

            row.addEventListener('click', function () {
                window.open((window.EPT_ADMIN_URL || '/EPiServer/EPiServer.Cms.UI.Admin/default') + '#/ScheduledJobs', '_blank');
            });

            jobCol.appendChild(row);
        });

        wrapper.appendChild(jobCol);

        // Right area: scrollable chart
        var chartOuter = document.createElement('div');
        chartOuter.className = 'gantt-chart-outer';
        chartOuter.style.cssText = 'flex:1;overflow-x:auto;overflow-y:hidden;position:relative;';

        var chartInner = document.createElement('div');
        chartInner.className = 'gantt-chart-inner';
        chartInner.style.cssText = 'position:relative;width:' + totalWidth + 'px;min-height:' + totalHeight + 'px;';

        // Time axis header
        renderTimeAxis(chartInner, range, totalWidth);

        // Grid lines and rows
        renderGridAndBars(chartInner, range, totalWidth);

        // Now line
        renderNowLine(chartInner, range, totalWidth);

        chartOuter.appendChild(chartInner);
        wrapper.appendChild(chartOuter);

        container.appendChild(wrapper);

        // Create tooltip element
        createTooltip();

        // Scroll to center (now)
        var nowOffset = timeToX(new Date(), range, totalWidth);
        var visibleWidth = chartOuter.clientWidth;
        chartOuter.scrollLeft = Math.max(0, nowOffset - visibleWidth / 2);

        // Mouse wheel horizontal scroll on chart
        chartOuter.addEventListener('wheel', function (e) {
            if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
                e.preventDefault();
                chartOuter.scrollLeft += e.deltaY;
            }
        }, { passive: false });
    }

    function renderTimeAxis(chartInner, range, totalWidth) {
        var header = document.createElement('div');
        header.className = 'gantt-time-header';
        header.style.cssText = 'height:' + HEADER_HEIGHT + 'px;position:sticky;top:0;border-bottom:1px solid var(--ept-border);background:var(--ept-surface);z-index:1;';

        var fromMs = range.from.getTime();
        var toMs = range.to.getTime();
        var rangeMs = toMs - fromMs;

        // Determine appropriate tick interval
        var tickIntervalHours = 1;
        if (state.viewRangeHours > 168) tickIntervalHours = 24;
        else if (state.viewRangeHours > 72) tickIntervalHours = 6;
        else if (state.viewRangeHours > 24) tickIntervalHours = 3;

        // Start at first tick
        var tickStart = new Date(range.from);
        tickStart.setMinutes(0, 0, 0);
        if (tickIntervalHours >= 24) {
            tickStart.setHours(0);
        } else {
            tickStart.setHours(Math.floor(tickStart.getHours() / tickIntervalHours) * tickIntervalHours);
        }

        var t = new Date(tickStart);
        var prevDay = '';

        while (t.getTime() <= toMs) {
            var x = timeToX(t, range, totalWidth);

            if (x >= 0 && x <= totalWidth) {
                var isDay = t.getHours() === 0;
                var dayStr = formatDay(t);

                // Day label (top row)
                if (dayStr !== prevDay) {
                    var dayLabel = document.createElement('div');
                    dayLabel.className = 'gantt-day-label';
                    dayLabel.style.cssText = 'position:absolute;left:' + x + 'px;top:2px;font-size:11px;font-weight:600;color:var(--ept-text);white-space:nowrap;padding:0 4px;';
                    dayLabel.textContent = dayStr;
                    header.appendChild(dayLabel);
                    prevDay = dayStr;
                }

                // Hour label (bottom row)
                var hourLabel = document.createElement('div');
                hourLabel.className = 'gantt-hour-label';
                hourLabel.style.cssText = 'position:absolute;left:' + x + 'px;bottom:4px;font-size:10px;color:var(--ept-text-muted);white-space:nowrap;padding:0 4px;';
                hourLabel.textContent = padZero(t.getHours()) + ':00';
                header.appendChild(hourLabel);

                // Tick line
                var tick = document.createElement('div');
                tick.style.cssText = 'position:absolute;left:' + x + 'px;bottom:0;width:1px;height:6px;background:var(--ept-border);';
                header.appendChild(tick);
            }

            t = new Date(t.getTime() + tickIntervalHours * 3600000);
        }

        chartInner.appendChild(header);
    }

    function renderGridAndBars(chartInner, range, totalWidth) {
        var barsArea = document.createElement('div');
        barsArea.className = 'gantt-bars-area';
        barsArea.style.cssText = 'position:relative;';

        var fromMs = range.from.getTime();
        var toMs = range.to.getTime();

        // Draw grid lines (hour markers)
        var tickIntervalHours = 1;
        if (state.viewRangeHours > 168) tickIntervalHours = 24;
        else if (state.viewRangeHours > 72) tickIntervalHours = 6;
        else if (state.viewRangeHours > 24) tickIntervalHours = 3;

        var gridStart = new Date(range.from);
        gridStart.setMinutes(0, 0, 0);
        var gt = new Date(gridStart);

        while (gt.getTime() <= toMs) {
            var gx = timeToX(gt, range, totalWidth);
            if (gx >= 0 && gx <= totalWidth) {
                var isDay = gt.getHours() === 0;
                var gridLine = document.createElement('div');
                gridLine.style.cssText = 'position:absolute;left:' + gx + 'px;top:0;bottom:0;width:1px;' +
                    'background:' + (isDay ? 'var(--ept-border)' : 'var(--ept-border-light)') + ';';
                barsArea.appendChild(gridLine);
            }
            gt = new Date(gt.getTime() + tickIntervalHours * 3600000);
        }

        // Build job index map
        var jobIndex = {};
        state.jobs.forEach(function (job, i) {
            jobIndex[job.id] = i;
        });

        // Draw row backgrounds
        state.jobs.forEach(function (job, i) {
            var rowBg = document.createElement('div');
            rowBg.style.cssText = 'position:absolute;left:0;right:0;' +
                'top:' + (i * (ROW_HEIGHT + ROW_GAP)) + 'px;height:' + ROW_HEIGHT + 'px;' +
                (i % 2 === 0 ? '' : 'background:var(--ept-bg);') +
                'border-bottom:1px solid var(--ept-border-light);';
            barsArea.appendChild(rowBg);
        });

        // Set bars area height
        barsArea.style.height = (state.jobs.length * (ROW_HEIGHT + ROW_GAP)) + 'px';

        // Draw execution bars
        state.executions.forEach(function (exec) {
            var idx = jobIndex[exec.jobId];
            if (idx === undefined) return;

            var startMs = exec.startUtc.getTime();
            var endMs = exec.endUtc ? exec.endUtc.getTime() : Date.now();

            // Skip if outside view range
            if (endMs < fromMs || startMs > toMs) return;

            var x1 = timeToX(exec.startUtc, range, totalWidth);
            var x2 = timeToX(new Date(endMs), range, totalWidth);
            var barWidth = Math.max(x2 - x1, MIN_BAR_WIDTH);

            var bar = document.createElement('div');
            bar.className = 'gantt-bar';

            var barTop = idx * (ROW_HEIGHT + ROW_GAP) + 6;
            var barHeight = ROW_HEIGHT - 12;

            var bgColor, borderStyle, opacity;
            if (exec.isPlanned) {
                bgColor = 'var(--ept-primary-light)';
                borderStyle = '2px dashed var(--ept-primary)';
                opacity = '0.7';
            } else if (exec.isRunning) {
                bgColor = 'var(--ept-info-light)';
                borderStyle = '1px solid var(--ept-info)';
                opacity = '1';
            } else if (exec.succeeded) {
                bgColor = 'var(--ept-success-light)';
                borderStyle = '1px solid var(--ept-success)';
                opacity = '1';
            } else {
                bgColor = 'var(--ept-danger-light)';
                borderStyle = '1px solid var(--ept-danger)';
                opacity = '1';
            }

            bar.style.cssText = 'position:absolute;' +
                'left:' + Math.max(x1, 0) + 'px;' +
                'top:' + barTop + 'px;' +
                'width:' + barWidth + 'px;' +
                'height:' + barHeight + 'px;' +
                'background:' + bgColor + ';' +
                'border:' + borderStyle + ';' +
                'border-radius:3px;' +
                'opacity:' + opacity + ';' +
                'cursor:pointer;' +
                'transition:opacity .15s,box-shadow .15s;' +
                'z-index:1;';

            // Running animation
            if (exec.isRunning) {
                bar.style.animation = 'gantt-pulse 2s ease-in-out infinite';
            }

            // Inner label for wide bars
            if (barWidth > 60) {
                var label = document.createElement('span');
                label.style.cssText = 'position:absolute;left:4px;top:50%;transform:translateY(-50%);font-size:10px;color:var(--ept-text-secondary);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:' + (barWidth - 8) + 'px;';
                label.textContent = formatDuration(exec.durationSeconds);
                bar.appendChild(label);
            }

            // Hover events
            bar.addEventListener('mouseenter', function (e) {
                bar.style.opacity = '1';
                bar.style.boxShadow = 'var(--ept-shadow)';
                showTooltip(e, exec);
            });
            bar.addEventListener('mousemove', function (e) {
                positionTooltip(e);
            });
            bar.addEventListener('mouseleave', function () {
                bar.style.opacity = opacity;
                bar.style.boxShadow = 'none';
                hideTooltip();
            });

            // Click to open job admin
            bar.addEventListener('click', function () {
                window.open((window.EPT_ADMIN_URL || '/EPiServer/EPiServer.Cms.UI.Admin/default') + '#/ScheduledJobs', '_blank');
            });

            barsArea.appendChild(bar);
        });

        chartInner.appendChild(barsArea);
    }

    function renderNowLine(chartInner, range, totalWidth) {
        var now = new Date();
        var x = timeToX(now, range, totalWidth);

        if (x < 0 || x > totalWidth) return;

        var line = document.createElement('div');
        line.className = 'gantt-now-line';
        line.style.cssText = 'position:absolute;left:' + x + 'px;top:0;bottom:0;width:2px;background:#e65100;z-index:3;pointer-events:none;';

        // Now marker at top
        var marker = document.createElement('div');
        marker.style.cssText = 'position:absolute;top:' + (HEADER_HEIGHT - 14) + 'px;left:-8px;width:18px;height:14px;background:#e65100;border-radius:2px;color:#fff;font-size:9px;font-weight:600;text-align:center;line-height:14px;';
        marker.textContent = 'NOW';
        line.appendChild(marker);

        chartInner.appendChild(line);
    }

    function startNowLineUpdater() {
        if (nowLineInterval) clearInterval(nowLineInterval);
        nowLineInterval = setInterval(function () {
            var line = container.querySelector('.gantt-now-line');
            if (line) {
                var range = getViewRange();
                var totalWidth = state.viewRangeHours * state.pixelsPerHour;
                var x = timeToX(new Date(), range, totalWidth);
                line.style.left = x + 'px';
            }
        }, 30000); // Update every 30 seconds
    }

    // ── Tooltip ───────────────────────────────────────────────────

    function createTooltip() {
        tooltip = document.createElement('div');
        tooltip.className = 'gantt-tooltip';
        tooltip.style.cssText = 'display:none;position:fixed;z-index:1000;background:var(--ept-surface);border:1px solid var(--ept-border);border-radius:var(--ept-radius);box-shadow:var(--ept-shadow-lg);padding:10px 14px;font-size:12px;line-height:1.6;pointer-events:none;max-width:360px;';
        document.body.appendChild(tooltip);
    }

    function showTooltip(e, exec) {
        if (!tooltip) return;

        var statusIcon = exec.isPlanned ? '\u23F3' : exec.isRunning ? '\u25B6' : exec.succeeded ? '\u2713' : '\u2717';
        var statusText = exec.isPlanned ? 'Planned' : exec.isRunning ? 'Running' : exec.succeeded ? 'Success' : 'Failed';
        var statusColor = exec.isPlanned ? 'var(--ept-primary)' : exec.isRunning ? 'var(--ept-info)' : exec.succeeded ? 'var(--ept-success)' : 'var(--ept-danger)';

        var html = '<strong>' + escapeHtml(exec.jobName) + '</strong><br>';
        html += 'Start: ' + formatDateTime(exec.startUtc) + '<br>';

        if (exec.endUtc) {
            html += 'End: ' + formatDateTime(exec.endUtc) + '<br>';
        }

        html += 'Duration: ' + formatDuration(exec.durationSeconds) + '<br>';
        html += 'Status: <span style="color:' + statusColor + ';font-weight:600;">' + statusIcon + ' ' + statusText + '</span>';

        if (exec.message && !exec.isPlanned) {
            var msg = exec.message.length > 150 ? exec.message.substring(0, 150) + '...' : exec.message;
            html += '<br><span style="color:var(--ept-text-muted);">' + escapeHtml(msg) + '</span>';
        }

        tooltip.innerHTML = html;
        tooltip.style.display = 'block';
        positionTooltip(e);
    }

    function positionTooltip(e) {
        if (!tooltip) return;
        var x = e.clientX + 12;
        var y = e.clientY + 12;
        var rect = tooltip.getBoundingClientRect();

        // Keep tooltip on screen
        if (x + rect.width > window.innerWidth - 10) {
            x = e.clientX - rect.width - 12;
        }
        if (y + rect.height > window.innerHeight - 10) {
            y = e.clientY - rect.height - 12;
        }

        tooltip.style.left = x + 'px';
        tooltip.style.top = y + 'px';
    }

    function hideTooltip() {
        if (tooltip) tooltip.style.display = 'none';
    }

    // ── Navigation ────────────────────────────────────────────────

    function navigate(fraction) {
        var shiftMs = state.viewRangeHours * 3600000 * fraction;
        state.centerDate = new Date(state.centerDate.getTime() + shiftMs);
        if (needsFetch()) {
            loadData();
        } else {
            render();
        }
    }

    function navigateToday() {
        state.centerDate = new Date();
        if (needsFetch()) {
            loadData();
        } else {
            render();
        }
    }

    function zoom(factor) {
        // factor > 1 = zoom in (less hours), factor < 1 = zoom out (more hours)
        var newRange = Math.round(state.viewRangeHours / factor);
        newRange = Math.max(6, Math.min(720, newRange)); // 6h to 30 days
        state.viewRangeHours = newRange;
        if (needsFetch()) {
            loadData();
        } else {
            render();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    function timeToX(date, range, totalWidth) {
        var ms = date.getTime() - range.from.getTime();
        var rangeMs = range.to.getTime() - range.from.getTime();
        return (ms / rangeMs) * totalWidth;
    }

    function createButton(label, onClick) {
        var btn = document.createElement('button');
        btn.className = 'ept-btn ept-btn--sm';
        btn.textContent = label;
        btn.addEventListener('click', onClick);
        return btn;
    }

    function formatDateShort(d) {
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return months[d.getMonth()] + ' ' + d.getDate() + ', ' + padZero(d.getHours()) + ':00';
    }

    function formatDay(d) {
        var days = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        return days[d.getDay()] + ' ' + months[d.getMonth()] + ' ' + d.getDate();
    }

    function formatDateTime(d) {
        if (!d) return '-';
        return d.getFullYear() + '-' + padZero(d.getMonth() + 1) + '-' + padZero(d.getDate()) + ' ' +
            padZero(d.getHours()) + ':' + padZero(d.getMinutes()) + ':' + padZero(d.getSeconds());
    }

    function formatDuration(seconds) {
        if (seconds < 1) return '<1s';
        if (seconds < 60) return Math.round(seconds) + 's';
        if (seconds < 3600) {
            var m = Math.floor(seconds / 60);
            var s = Math.round(seconds % 60);
            return m + 'm ' + s + 's';
        }
        var h = Math.floor(seconds / 3600);
        var min = Math.round((seconds % 3600) / 60);
        return h + 'h ' + min + 'm';
    }

    function padZero(n) {
        return n < 10 ? '0' + n : '' + n;
    }

    function escapeHtml(str) {
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // ── Styles ────────────────────────────────────────────────────

    function injectStyles() {
        if (document.getElementById('gantt-styles')) return;
        var style = document.createElement('style');
        style.id = 'gantt-styles';
        style.textContent =
            '@keyframes gantt-pulse {' +
            '  0%, 100% { opacity: 1; }' +
            '  50% { opacity: 0.6; }' +
            '}' +
            '.gantt-wrapper { max-height: calc(100vh - 260px); }' +
            '.gantt-chart-outer::-webkit-scrollbar { height: 10px; }' +
            '.gantt-chart-outer::-webkit-scrollbar-track { background: var(--ept-bg); }' +
            '.gantt-chart-outer::-webkit-scrollbar-thumb { background: var(--ept-border); border-radius: 5px; }' +
            '.gantt-chart-outer::-webkit-scrollbar-thumb:hover { background: var(--ept-text-muted); }' +
            '.gantt-job-row:hover { background: var(--ept-surface-hover) !important; }' +
            '.gantt-bar:hover { z-index: 10 !important; }' +
            '.gantt-legend { display:flex;gap:16px;align-items:center;margin-top:12px;font-size:12px;color:var(--ept-text-secondary); }' +
            '.gantt-legend-item { display:flex;align-items:center;gap:4px; }' +
            '.gantt-legend-swatch { width:16px;height:10px;border-radius:2px; }';
        document.head.appendChild(style);
    }

    // ── Init ──────────────────────────────────────────────────────

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
