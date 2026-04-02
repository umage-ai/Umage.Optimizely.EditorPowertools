/**
 * Activity Timeline - Main UI
 */
(function () {
    var API = window.EPT_API_URL + '/activity';
    var urlParams = new URLSearchParams(window.location.search);
    var state = {
        activities: [],
        totalCount: 0,
        hasMore: false,
        isLoading: false,
        skip: 0,
        take: 50,
        // Filters
        user: '',
        action: '',
        contentType: '',
        fromDate: '',
        toDate: '',
        contentId: urlParams.get('contentId') || '',
        contentName: '',
        // Filter options
        users: [],
        contentTypes: []
    };

    var observer = null;

    function init() {
        EPT.showLoading(document.getElementById('timeline-content'));
        Promise.all([
            EPT.fetchJson(API + '/stats').catch(function () { return null; }),
            EPT.fetchJson(API + '/users').catch(function () { return []; }),
            EPT.fetchJson(API + '/content-types').catch(function () { return []; })
        ]).then(function (results) {
            var stats = results[0];
            var users = results[1];
            var contentTypes = results[2];
            state.users = users;
            state.contentTypes = contentTypes;
            renderStats(stats);
            renderToolbar();
            loadActivities(true);
        }).catch(function (err) {
            document.getElementById('timeline-content').innerHTML =
                '<div class="ept-empty"><p>Error loading timeline: ' + err.message + '</p></div>';
        });
    }

    // ── Stats Bar ──────────────────────────────────────────────────
    function renderStats(stats) {
        var el = document.getElementById('timeline-stats');
        if (!stats) {
            el.innerHTML = '';
            return;
        }
        el.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + stats.totalToday + '</div><div class="ept-stat__label">Activities Today</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + stats.activeEditorsToday + '</div><div class="ept-stat__label">Active Editors</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + stats.publishesToday + '</div><div class="ept-stat__label">Publishes Today</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + stats.draftsToday + '</div><div class="ept-stat__label">Drafts Today</div></div>';
    }

    // ── Toolbar ────────────────────────────────────────────────────
    function renderToolbar() {
        var el = document.getElementById('timeline-toolbar');

        var userOpts = '<option value="">All users</option>';
        state.users.forEach(function (u) {
            userOpts += '<option value="' + escHtml(u) + '">' + escHtml(u) + '</option>';
        });

        var actionOpts =
            '<option value="">All actions</option>' +
            '<option value="Published">Published</option>' +
            '<option value="Draft">Draft saved</option>' +
            '<option value="ReadyToPublish">Ready to publish</option>' +
            '<option value="Scheduled">Scheduled</option>' +
            '<option value="Rejected">Rejected</option>' +
            '<option value="PreviouslyPublished">Previously published</option>' +
            '<option value="Comment">Comments</option>';

        var typeOpts = '<option value="">All content types</option>';
        state.contentTypes.forEach(function (t) {
            typeOpts += '<option value="' + escHtml(t) + '">' + escHtml(t) + '</option>';
        });

        el.innerHTML =
            '<select id="tl-filter-user" class="ept-select">' + userOpts + '</select>' +
            '<select id="tl-filter-action" class="ept-select">' + actionOpts + '</select>' +
            '<select id="tl-filter-type" class="ept-select">' + typeOpts + '</select>' +
            '<input type="date" id="tl-filter-from" class="ept-input" placeholder="From" title="From date" />' +
            '<input type="date" id="tl-filter-to" class="ept-input" placeholder="To" title="To date" />' +
            '<button id="tl-filter-apply" class="ept-btn ept-btn--primary">Filter</button>' +
            '<button id="tl-filter-clear" class="ept-btn">Clear</button>';

        document.getElementById('tl-filter-apply').addEventListener('click', applyFilters);
        document.getElementById('tl-filter-clear').addEventListener('click', clearFilters);
    }

    function applyFilters() {
        state.user = document.getElementById('tl-filter-user').value;
        state.action = document.getElementById('tl-filter-action').value;
        state.contentType = document.getElementById('tl-filter-type').value;
        state.fromDate = document.getElementById('tl-filter-from').value;
        state.toDate = document.getElementById('tl-filter-to').value;
        loadActivities(true);
    }

    function clearFilters() {
        document.getElementById('tl-filter-user').value = '';
        document.getElementById('tl-filter-action').value = '';
        document.getElementById('tl-filter-type').value = '';
        document.getElementById('tl-filter-from').value = '';
        document.getElementById('tl-filter-to').value = '';
        state.user = '';
        state.action = '';
        state.contentType = '';
        state.fromDate = '';
        state.toDate = '';
        state.contentId = '';
        state.contentName = '';
        var banner = document.getElementById('content-filter-banner');
        if (banner) banner.remove();
        // Clean URL
        var url = new URL(window.location);
        url.searchParams.delete('contentId');
        window.history.replaceState({}, '', url);
        loadActivities(true);
    }

    // ── Content Item Banner ──────────────────────────────────────────
    function renderContentBanner() {
        var existing = document.getElementById('content-filter-banner');
        if (!state.contentId) {
            if (existing) existing.remove();
            return;
        }
        if (existing) return; // already rendered

        var banner = document.createElement('div');
        banner.id = 'content-filter-banner';
        banner.className = 'ept-banner';
        banner.innerHTML =
            '<span>Showing timeline for <strong>' + escHtml(state.contentName || 'Content #' + state.contentId) + '</strong></span> ' +
            '<button id="content-filter-clear" class="ept-btn ept-btn--sm">Show all activity</button>';

        var contentEl = document.getElementById('timeline-content');
        contentEl.parentNode.insertBefore(banner, contentEl);

        document.getElementById('content-filter-clear').addEventListener('click', function () {
            state.contentId = '';
            state.contentName = '';
            // Update URL without the contentId param
            var url = new URL(window.location);
            url.searchParams.delete('contentId');
            window.history.replaceState({}, '', url);
            banner.remove();
            loadActivities(true);
        });
    }

    // ── Load Activities ────────────────────────────────────────────
    function loadActivities(reset) {
        if (state.isLoading) return;
        state.isLoading = true;

        if (reset) {
            state.skip = 0;
            state.activities = [];
        }

        var contentEl = document.getElementById('timeline-content');
        if (reset) {
            EPT.showLoading(contentEl);
        }

        var url = API + '/timeline?skip=' + state.skip + '&take=' + state.take;
        if (state.contentId) url += '&contentId=' + encodeURIComponent(state.contentId);
        if (state.user) url += '&user=' + encodeURIComponent(state.user);
        if (state.action) url += '&action=' + encodeURIComponent(state.action);
        if (state.contentType) url += '&contentType=' + encodeURIComponent(state.contentType);
        if (state.fromDate) url += '&from=' + encodeURIComponent(state.fromDate + 'T00:00:00Z');
        if (state.toDate) url += '&to=' + encodeURIComponent(state.toDate + 'T23:59:59Z');

        EPT.fetchJson(url).then(function (result) {
            if (result.contentName) state.contentName = result.contentName;
            state.activities = state.activities.concat(result.activities);
            state.totalCount = result.totalCount;
            state.hasMore = result.hasMore;
            state.skip += result.activities.length;
            state.isLoading = false;
            renderContentBanner();
            renderTimeline(reset);
        }).catch(function (err) {
            state.isLoading = false;
            if (reset) {
                contentEl.innerHTML =
                    '<div class="ept-empty"><p>Error loading activities: ' + err.message + '</p></div>';
            }
        });
    }

    function loadMore() {
        if (!state.hasMore || state.isLoading) return;
        loadActivities(false);
    }

    // ── Render Timeline ────────────────────────────────────────────
    function renderTimeline(reset) {
        var contentEl = document.getElementById('timeline-content');

        if (state.activities.length === 0) {
            EPT.showEmpty(contentEl, 'No activities found matching the current filters.');
            return;
        }

        if (reset) {
            contentEl.innerHTML = '';
            var timeline = document.createElement('div');
            timeline.className = 'timeline-dual';
            timeline.id = 'timeline-feed';
            timeline.innerHTML =
                '<div class="timeline-col timeline-col--left" id="timeline-left"></div>' +
                '<div class="timeline-spine"></div>' +
                '<div class="timeline-col timeline-col--right" id="timeline-right"></div>';
            contentEl.appendChild(timeline);

            // Sentinel for infinite scroll
            var sentinel = document.createElement('div');
            sentinel.id = 'timeline-sentinel';
            sentinel.style.height = '1px';
            contentEl.appendChild(sentinel);

            // Loading indicator
            var loadingMore = document.createElement('div');
            loadingMore.id = 'timeline-loading-more';
            loadingMore.className = 'ept-loading';
            loadingMore.style.display = 'none';
            loadingMore.innerHTML = '<div class="ept-spinner"></div><p>Loading more...</p>';
            contentEl.appendChild(loadingMore);

            setupInfiniteScroll(sentinel);
        }

        var leftCol = document.getElementById('timeline-left');
        var rightCol = document.getElementById('timeline-right');
        leftCol.innerHTML = '';
        rightCol.innerHTML = '';

        // Group activities by date
        var grouped = groupByDate(state.activities);

        Object.keys(grouped).forEach(function (dateLabel) {
            // Add date separator spanning both columns
            var sepLeft = document.createElement('div');
            sepLeft.className = 'timeline-date-separator';
            sepLeft.innerHTML = '<span>' + escHtml(dateLabel) + '</span>';
            leftCol.appendChild(sepLeft);

            var sepRight = document.createElement('div');
            sepRight.className = 'timeline-date-separator';
            sepRight.innerHTML = '<span>&nbsp;</span>';
            rightCol.appendChild(sepRight);

            grouped[dateLabel].forEach(function (activity) {
                var entry = createTimelineEntry(activity);
                var spacer = document.createElement('div');
                spacer.className = 'timeline-entry timeline-entry--spacer';

                if (activity.action === 'Published') {
                    // Published goes right
                    entry.classList.add('timeline-entry--right');
                    rightCol.appendChild(entry);
                    leftCol.appendChild(spacer);
                } else {
                    // Everything else goes left
                    entry.classList.add('timeline-entry--left');
                    leftCol.appendChild(entry);
                    rightCol.appendChild(spacer);
                }
            });
        });

        // Update loading-more visibility
        var loadingMore = document.getElementById('timeline-loading-more');
        if (loadingMore) {
            loadingMore.style.display = state.isLoading ? '' : 'none';
        }
    }

    function setupInfiniteScroll(sentinel) {
        if (observer) observer.disconnect();
        observer = new IntersectionObserver(function (entries) {
            if (entries[0].isIntersecting && !state.isLoading && state.hasMore) {
                var loadingMore = document.getElementById('timeline-loading-more');
                if (loadingMore) loadingMore.style.display = '';
                loadMore();
            }
        });
        observer.observe(sentinel);
    }

    function createTimelineEntry(activity) {
        var entry = document.createElement('div');
        entry.className = 'timeline-entry';

        var markerClass = 'timeline-marker timeline-marker--' + getMarkerClass(activity.action);
        var badgeClass = 'ept-badge ' + getBadgeClass(activity.action);
        var actionLabel = getActionLabel(activity.action);
        var timeStr = formatRelativeTime(activity.timestampUtc);
        var absTime = formatAbsoluteTime(activity.timestampUtc);

        var compareBtn = '';
        if (activity.hasPreviousVersion) {
            compareBtn = '<button class="ept-btn ept-btn--sm timeline-compare-btn" ' +
                'data-content-id="' + activity.contentId + '" ' +
                'data-version-id="' + activity.versionId + '" ' +
                'data-language="' + escHtml(activity.language || '') + '">Compare</button>';
        }

        entry.innerHTML =
            '<div class="' + markerClass + '"></div>' +
            '<div class="timeline-content">' +
                '<div class="timeline-header">' +
                    '<span class="' + badgeClass + '">' + actionLabel + '</span> ' +
                    '<strong>' + escHtml(activity.contentName) + '</strong> ' +
                    '<span class="ept-muted">' + escHtml(activity.contentTypeName) + '</span>' +
                    (activity.language ? ' <span class="ept-badge ept-badge--default">' + escHtml(activity.language) + '</span>' : '') +
                '</div>' +
                (activity.message ? '<div class="timeline-message">' + escHtml(activity.message) + '</div>' : '') +
                '<div class="timeline-meta">' +
                    '<span class="timeline-user">' + escHtml(activity.user) + '</span>' +
                    '<span class="timeline-time" title="' + absTime + '">' + timeStr + '</span>' +
                '</div>' +
                '<div class="timeline-actions">' +
                    (activity.editUrl ? '<a href="' + escHtml(activity.editUrl) + '" class="ept-btn ept-btn--sm" target="_blank">Edit</a> ' : '') +
                    compareBtn +
                '</div>' +
            '</div>';

        // Bind compare button
        var btn = entry.querySelector('.timeline-compare-btn');
        if (btn) {
            btn.addEventListener('click', function () {
                var cid = this.getAttribute('data-content-id');
                var vid = this.getAttribute('data-version-id');
                var lang = this.getAttribute('data-language');
                showComparison(cid, vid, lang);
            });
        }

        return entry;
    }

    // ── Version Comparison ─────────────────────────────────────────
    function showComparison(contentId, versionId, language) {
        var dialog = EPT.openDialog('Version Comparison', { wide: true });
        EPT.showLoading(dialog.body);

        var url = API + '/compare/' + contentId + '/' + versionId;
        if (language) url += '?language=' + encodeURIComponent(language);

        EPT.fetchJson(url).then(function (result) {
            if (!result.hasPrevious) {
                dialog.body.innerHTML = '<div class="ept-empty"><p>No previous version available for comparison.</p></div>';
                return;
            }

            var html = '<div class="timeline-comparison">' +
                '<p class="ept-muted">Comparing version ' + result.currentVersion + ' with version ' + result.previousVersion +
                ' of <strong>' + escHtml(result.contentName) + '</strong></p>';

            if (result.changes.length === 0) {
                html += '<div class="ept-empty"><p>No property changes detected between these versions.</p></div>';
            } else {
                html += '<table class="ept-table">' +
                    '<thead><tr><th>Property</th><th>Previous Value</th><th>New Value</th></tr></thead>' +
                    '<tbody>';

                result.changes.forEach(function (change) {
                    if (change.isHtml) {
                        html += '<tr>' +
                            '<td><strong>' + escHtml(change.propertyName) + '</strong> <span class="ept-badge ept-badge--default">HTML</span></td>' +
                            '<td class="timeline-diff-old"><iframe sandbox="allow-same-origin" srcdoc="' + escAttr(change.oldValue || '(empty)') + '" style="width:100%;min-height:80px;border:1px solid var(--ept-border,#e0e0e0);border-radius:4px;"></iframe></td>' +
                            '<td class="timeline-diff-new"><iframe sandbox="allow-same-origin" srcdoc="' + escAttr(change.newValue || '(empty)') + '" style="width:100%;min-height:80px;border:1px solid var(--ept-border,#e0e0e0);border-radius:4px;"></iframe></td>' +
                            '</tr>';
                    } else {
                        html += '<tr>' +
                            '<td><strong>' + escHtml(change.propertyName) + '</strong></td>' +
                            '<td class="timeline-diff-old">' + escHtml(change.oldValue || '(empty)') + '</td>' +
                            '<td class="timeline-diff-new">' + escHtml(change.newValue || '(empty)') + '</td>' +
                            '</tr>';
                    }
                });

                html += '</tbody></table>';
            }
            html += '</div>';

            dialog.body.innerHTML = html;
        }).catch(function (err) {
            dialog.body.innerHTML = '<div class="ept-empty"><p>Error loading comparison: ' + err.message + '</p></div>';
        });
    }

    // ── Helpers ────────────────────────────────────────────────────
    function getMarkerClass(action) {
        switch (action) {
            case 'Published': return 'published';
            case 'Draft': return 'draft';
            case 'ReadyToPublish': return 'ready';
            case 'Scheduled': return 'scheduled';
            case 'Rejected': return 'rejected';
            case 'PreviouslyPublished': return 'previously-published';
            case 'Comment': return 'comment';
            default: return 'default';
        }
    }

    function getBadgeClass(action) {
        switch (action) {
            case 'Published': return 'ept-badge--success';
            case 'Draft': return 'ept-badge--primary';
            case 'ReadyToPublish': return 'ept-badge--warning';
            case 'Scheduled': return 'ept-badge--purple';
            case 'Rejected': return 'ept-badge--danger';
            case 'PreviouslyPublished': return 'ept-badge--default';
            case 'Comment': return 'ept-badge--primary';
            default: return 'ept-badge--default';
        }
    }

    function getActionLabel(action) {
        switch (action) {
            case 'Published': return 'Published';
            case 'Draft': return 'Draft saved';
            case 'ReadyToPublish': return 'Ready to publish';
            case 'Scheduled': return 'Scheduled';
            case 'Rejected': return 'Rejected';
            case 'PreviouslyPublished': return 'Previously published';
            case 'Comment': return 'Comment';
            default: return action;
        }
    }

    function formatRelativeTime(utcStr) {
        var date = new Date(utcStr);
        var now = new Date();
        var diffMs = now - date;
        var diffSec = Math.floor(diffMs / 1000);
        var diffMin = Math.floor(diffSec / 60);
        var diffHour = Math.floor(diffMin / 60);
        var diffDay = Math.floor(diffHour / 24);

        if (diffSec < 60) return 'Just now';
        if (diffMin < 60) return diffMin + ' minute' + (diffMin !== 1 ? 's' : '') + ' ago';
        if (diffHour < 24) return diffHour + ' hour' + (diffHour !== 1 ? 's' : '') + ' ago';

        var yesterday = new Date(now);
        yesterday.setDate(yesterday.getDate() - 1);
        if (date.toDateString() === yesterday.toDateString()) {
            return 'Yesterday at ' + pad(date.getHours()) + ':' + pad(date.getMinutes());
        }

        var months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
        var timeStr = pad(date.getHours()) + ':' + pad(date.getMinutes());

        if (date.getFullYear() === now.getFullYear()) {
            return months[date.getMonth()] + ' ' + date.getDate() + ' at ' + timeStr;
        }

        return months[date.getMonth()] + ' ' + date.getDate() + ', ' + date.getFullYear() + ' at ' + timeStr;
    }

    function formatAbsoluteTime(utcStr) {
        var date = new Date(utcStr);
        return date.toLocaleString();
    }

    function groupByDate(activities) {
        var groups = {};
        var now = new Date();
        var today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
        var yesterday = new Date(today);
        yesterday.setDate(yesterday.getDate() - 1);

        var months = ['January', 'February', 'March', 'April', 'May', 'June',
            'July', 'August', 'September', 'October', 'November', 'December'];

        activities.forEach(function (a) {
            var date = new Date(a.timestampUtc);
            var dayStart = new Date(date.getFullYear(), date.getMonth(), date.getDate());
            var label;

            if (dayStart.getTime() === today.getTime()) {
                label = 'Today';
            } else if (dayStart.getTime() === yesterday.getTime()) {
                label = 'Yesterday';
            } else if (date.getFullYear() === now.getFullYear()) {
                label = months[date.getMonth()] + ' ' + date.getDate();
            } else {
                label = months[date.getMonth()] + ' ' + date.getDate() + ', ' + date.getFullYear();
            }

            if (!groups[label]) groups[label] = [];
            groups[label].push(a);
        });

        return groups;
    }

    function pad(n) {
        return n < 10 ? '0' + n : '' + n;
    }

    function escHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function escAttr(str) {
        if (!str) return '';
        return str.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/'/g, '&#39;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    }

    // ── Inline Styles ──────────────────────────────────────────────
    function injectStyles() {
        var style = document.createElement('style');
        style.textContent =
            /* Dual-column layout: left | spine | right */
            '.timeline-dual { display: flex; gap: 0; position: relative; }' +
            '.timeline-col { flex: 1; min-width: 0; }' +
            '.timeline-col--left { padding-right: 24px; }' +
            '.timeline-col--right { padding-left: 24px; }' +
            '.timeline-spine { width: 3px; background: var(--ept-border, #e0e0e0); flex-shrink: 0; position: relative; border-radius: 2px; }' +

            '.timeline-date-separator { margin: 20px 0 12px; }' +
            '.timeline-date-separator span { display: inline-block; background: var(--ept-bg, #f5f5f5); color: var(--ept-text-muted, #666); font-size: 12px; font-weight: 600; padding: 3px 10px; border-radius: 10px; text-transform: uppercase; letter-spacing: 0.5px; }' +

            '.timeline-entry { position: relative; margin-bottom: 12px; }' +
            '.timeline-entry--spacer { min-height: 1px; visibility: hidden; }' +

            /* Marker dots on the spine side */
            '.timeline-entry--left .timeline-marker { position: absolute; right: -31px; top: 10px; }' +
            '.timeline-entry--right .timeline-marker { position: absolute; left: -31px; top: 10px; }' +
            '.timeline-entry--spacer .timeline-marker { display: none; }' +

            '.timeline-marker { width: 14px; height: 14px; border-radius: 50%; border: 2px solid #fff; z-index: 1; box-shadow: 0 0 0 2px var(--ept-border, #e0e0e0); }' +
            '.timeline-marker--published { background: #22c55e; }' +
            '.timeline-marker--draft { background: #3b82f6; }' +
            '.timeline-marker--ready { background: #eab308; }' +
            '.timeline-marker--scheduled { background: #a855f7; }' +
            '.timeline-marker--rejected { background: #ef4444; }' +
            '.timeline-marker--previously-published { background: #9ca3af; }' +
            '.timeline-marker--default { background: #9ca3af; }' +
            '.timeline-marker--comment { background: #06b6d4; }' +

            '.timeline-message { margin: 6px 0; padding: 8px 12px; background: var(--ept-bg, #f8f9fa); border-left: 3px solid #06b6d4; border-radius: 4px; font-size: 13px; color: var(--ept-text, #333); white-space: pre-wrap; }' +

            '.timeline-content { flex: 1; background: #fff; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; padding: 12px 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.04); }' +

            '.timeline-header { display: flex; align-items: center; gap: 8px; flex-wrap: wrap; margin-bottom: 6px; }' +
            '.timeline-header strong { font-size: 14px; }' +

            '.timeline-meta { display: flex; align-items: center; gap: 12px; margin-bottom: 8px; font-size: 13px; color: var(--ept-text-muted, #666); }' +
            '.timeline-user::before { content: ""; display: inline-block; width: 12px; height: 12px; margin-right: 4px; background: currentColor; mask: url("data:image/svg+xml,' + encodeURIComponent('<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>') + '"); -webkit-mask: url("data:image/svg+xml,' + encodeURIComponent('<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>') + '"); }' +

            '.timeline-actions { display: flex; gap: 6px; }' +

            '.timeline-diff-old { background: #fef2f2; color: #991b1b; }' +
            '.timeline-diff-new { background: #f0fdf4; color: #166534; }' +

            '.timeline-comparison { padding: 8px 0; }' +
            '.timeline-comparison .ept-table td { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-size: 13px; }' +

            '.ept-badge--purple { background: #f3e8ff; color: #7c3aed; }' +

            '.ept-select, .ept-input { padding: 6px 10px; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 4px; font-size: 13px; background: #fff; }' +
            '.ept-select:focus, .ept-input:focus { outline: none; border-color: var(--ept-primary, #3b82f6); }' +

            '#timeline-sentinel { margin-top: 8px; }' +
            '#timeline-loading-more { text-align: center; padding: 16px 0; }' +

            '.ept-banner { display: flex; align-items: center; gap: 12px; padding: 10px 16px; margin-bottom: 12px; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; font-size: 14px; color: #1e40af; }' +
            '.ept-banner .ept-btn { margin-left: auto; }';

        document.head.appendChild(style);
    }

    // ── Init ───────────────────────────────────────────────────────
    injectStyles();
    init();
})();
