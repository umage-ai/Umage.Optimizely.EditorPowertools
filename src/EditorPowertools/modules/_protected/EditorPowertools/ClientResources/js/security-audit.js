/**
 * Security Audit - Tree view, Role explorer, Issues dashboard
 */
(function () {
    'use strict';
    var API = window.EPT_API_URL + '/security-audit';
    var root = document.getElementById('security-audit-root');
    if (!root) return;

    // ── State ──────────────────────────────────────────────────────
    var state = {
        activeTab: 'tree',
        // Tree tab
        treeNodes: {},          // parentId -> children array (cache)
        expandedNodes: {},      // contentId -> true
        selectedNode: null,     // contentId of node with detail open
        treeSearchQuery: '',
        treeHighlightRole: '',
        treeShowIssuesOnly: false,
        // Role explorer
        roles: [],
        selectedRole: null,
        roleAccessFilter: '',
        rolePage: 1,
        roleResult: null,
        // Issues
        issuesSummary: null,
        issueTypeFilter: '',
        issueSeverityFilter: '',
        issuesPage: 1,
        issuesResult: null,
        // Status
        status: null
    };

    var PAGE_SIZE = 50;

    // ── Helpers ────────────────────────────────────────────────────
    function escHtml(s) {
        if (!s && s !== 0) return '';
        var d = document.createElement('div');
        d.textContent = String(s);
        return d.innerHTML;
    }

    function escAttr(s) {
        if (!s) return '';
        return s.replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function timeAgo(date) {
        var diff = Date.now() - date.getTime();
        var mins = Math.floor(diff / 60000);
        if (mins < 1) return 'just now';
        if (mins < 60) return mins + ' minute' + (mins !== 1 ? 's' : '') + ' ago';
        var hours = Math.floor(mins / 60);
        if (hours < 24) return hours + ' hour' + (hours !== 1 ? 's' : '') + ' ago';
        var days = Math.floor(hours / 24);
        return days + ' day' + (days !== 1 ? 's' : '') + ' ago';
    }

    function accessBadgeClass(access) {
        if (!access) return 'ept-badge--default';
        var a = access.toLowerCase();
        if (a === 'fullaccesss' || a === 'fulaccess' || a === 'fullacess') return 'ept-badge--danger';
        if (a.indexOf('full') >= 0 || a.indexOf('administer') >= 0) return 'ept-badge--danger';
        if (a.indexOf('publish') >= 0) return 'ept-badge--warning';
        if (a.indexOf('edit') >= 0 || a.indexOf('create') >= 0 || a.indexOf('delete') >= 0) return 'ept-badge--primary';
        return 'ept-badge--default';
    }

    function severityBadge(severity) {
        if (!severity) return '';
        var s = severity.toLowerCase();
        if (s === 'critical') return '<span class="sa-severity sa-severity--critical" title="Critical"></span>';
        if (s === 'warning') return '<span class="sa-severity sa-severity--warning" title="Warning"></span>';
        return '<span class="sa-severity sa-severity--info" title="Info"></span>';
    }

    function severityLabel(severity) {
        if (!severity) return '';
        var s = severity.toLowerCase();
        var cls = s === 'critical' ? 'danger' : s === 'warning' ? 'warning' : 'default';
        return '<span class="ept-badge ept-badge--' + cls + '">' + escHtml(severity) + '</span>';
    }

    function renderPagination(page, totalPages, onPageChange) {
        if (totalPages <= 1) return '';
        var container = document.createElement('div');
        container.className = 'sa-pagination';

        var prevBtn = document.createElement('button');
        prevBtn.className = 'ept-btn ept-btn--sm';
        prevBtn.textContent = 'Previous';
        prevBtn.disabled = page <= 1;
        prevBtn.addEventListener('click', function () { onPageChange(page - 1); });
        container.appendChild(prevBtn);

        var info = document.createElement('span');
        info.className = 'sa-pagination__info';
        info.textContent = 'Page ' + page + ' of ' + totalPages;
        container.appendChild(info);

        var nextBtn = document.createElement('button');
        nextBtn.className = 'ept-btn ept-btn--sm';
        nextBtn.textContent = 'Next';
        nextBtn.disabled = page >= totalPages;
        nextBtn.addEventListener('click', function () { onPageChange(page + 1); });
        container.appendChild(nextBtn);

        return container;
    }

    // ── Init ───────────────────────────────────────────────────────
    async function init() {
        EPT.showLoading(root);

        try {
            // Load preferences and status in parallel
            var prefs, statusData;
            try {
                var results = await Promise.all([
                    EPT.loadPreferences('SecurityAudit'),
                    EPT.fetchJson(API + '/status').catch(function () { return null; })
                ]);
                prefs = results[0];
                statusData = results[1];
            } catch (e) {
                prefs = {};
                statusData = null;
            }

            state.status = statusData;

            // Restore preferences
            if (prefs.activeTab) state.activeTab = prefs.activeTab;
            if (prefs.selectedRole) state.selectedRole = prefs.selectedRole;
            if (prefs.roleAccessFilter) state.roleAccessFilter = prefs.roleAccessFilter;
            if (prefs.issueTypeFilter) state.issueTypeFilter = prefs.issueTypeFilter;
            if (prefs.issueSeverityFilter) state.issueSeverityFilter = prefs.issueSeverityFilter;

            renderShell();
            switchTab(state.activeTab);
        } catch (err) {
            root.innerHTML = '<div class="ept-empty"><p>Error loading Security Audit: ' + escHtml(err.message) + '</p></div>';
        }
    }

    function savePrefs() {
        EPT.savePreferences('SecurityAudit', {
            activeTab: state.activeTab,
            selectedRole: state.selectedRole ? state.selectedRole.roleOrUser : null,
            roleAccessFilter: state.roleAccessFilter,
            issueTypeFilter: state.issueTypeFilter,
            issueSeverityFilter: state.issueSeverityFilter
        });
    }

    // ── Shell (tabs + stats + content area) ───────────────────────
    function renderShell() {
        var html = '';

        // Status alert
        html += '<div id="sa-status-alert"></div>';

        // Stats bar
        html += '<div id="sa-stats" class="ept-stats"></div>';

        // Tabs
        html += '<div class="sa-tabs">';
        html += '<button class="sa-tab" data-tab="tree">Content Tree</button>';
        html += '<button class="sa-tab" data-tab="roles">Role/User Explorer</button>';
        html += '<button class="sa-tab" data-tab="issues">Issues <span id="sa-issues-count-badge" class="sa-tab-badge" style="display:none"></span></button>';
        html += '</div>';

        // Content
        html += '<div id="sa-tab-content"></div>';

        root.innerHTML = html;

        // Bind tab clicks
        var tabs = root.querySelectorAll('.sa-tab');
        for (var i = 0; i < tabs.length; i++) {
            tabs[i].addEventListener('click', function () {
                switchTab(this.getAttribute('data-tab'));
            });
        }

        renderStatusAlert();
        renderStats();
    }

    function renderStatusAlert() {
        var el = document.getElementById('sa-status-alert');
        if (!el) return;

        if (!state.status) {
            el.innerHTML = '<div class="ept-alert ept-alert--warning">' +
                '<strong>Security audit data has not been collected yet.</strong> Run the aggregation job to analyze content permissions. ' +
                '<button class="ept-btn ept-btn--sm" id="sa-run-job-btn" style="margin-left:8px">Run now</button></div>';
            wireRunJobButton();
            return;
        }

        if (state.status.lastAnalysisUtc) {
            var lastDate = new Date(state.status.lastAnalysisUtc);
            var isOld = (Date.now() - lastDate.getTime()) > 24 * 60 * 60 * 1000;
            if (isOld) {
                el.innerHTML = '<div class="ept-alert ept-alert--warning">' +
                    'Security data was last analyzed <strong>' + timeAgo(lastDate) + '</strong>. Consider running the aggregation job for fresh data. ' +
                    '<button class="ept-btn ept-btn--sm" id="sa-run-job-btn" style="margin-left:8px">Run now</button></div>';
                wireRunJobButton();
            } else {
                el.innerHTML = '';
            }
        }
    }

    function wireRunJobButton() {
        var btn = document.getElementById('sa-run-job-btn');
        if (!btn) return;
        btn.addEventListener('click', async function () {
            btn.disabled = true;
            btn.textContent = 'Starting...';
            try {
                await EPT.postJson(window.EPT_API_URL + '/aggregation-start');
                btn.parentElement.className = 'ept-alert ept-alert--info';
                btn.parentElement.innerHTML = '<strong>Aggregation job has been started.</strong> Data will update when it completes. ' +
                    '<button class="ept-btn ept-btn--sm" onclick="location.reload()" style="margin-left:8px">Refresh</button>';
            } catch (err) {
                btn.textContent = 'Failed';
            }
        });
    }

    function renderStats() {
        var el = document.getElementById('sa-stats');
        if (!el || !state.status) {
            if (el) el.innerHTML = '';
            return;
        }
        var s = state.status;
        var lastRun = s.lastAnalysisUtc ? timeAgo(new Date(s.lastAnalysisUtc)) : 'Never';
        el.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + (s.totalContentAnalyzed || 0) + '</div><div class="ept-stat__label">Content Analyzed</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + (s.uniqueRolesAndUsers || 0) + '</div><div class="ept-stat__label">Roles/Users</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + (s.totalIssues || 0) + '</div><div class="ept-stat__label">Issues Found</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value sa-stat--muted">' + escHtml(lastRun) + '</div><div class="ept-stat__label">Last Analysis</div></div>';

        // Update issues badge on tab
        var badge = document.getElementById('sa-issues-count-badge');
        if (badge && s.totalIssues > 0) {
            badge.textContent = s.totalIssues;
            badge.style.display = '';
        }
    }

    function switchTab(tab) {
        state.activeTab = tab;

        var tabs = root.querySelectorAll('.sa-tab');
        for (var i = 0; i < tabs.length; i++) {
            tabs[i].classList.toggle('sa-tab--active', tabs[i].getAttribute('data-tab') === tab);
        }

        var content = document.getElementById('sa-tab-content');
        EPT.showLoading(content);

        savePrefs();

        if (tab === 'tree') {
            renderTreeTab();
        } else if (tab === 'roles') {
            renderRolesTab();
        } else if (tab === 'issues') {
            renderIssuesTab();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TAB 1: Content Tree Permissions
    // ═══════════════════════════════════════════════════════════════

    async function renderTreeTab() {
        var content = document.getElementById('sa-tab-content');
        content.innerHTML = '';

        // Toolbar
        var toolbar = document.createElement('div');
        toolbar.className = 'ept-toolbar';
        toolbar.innerHTML =
            '<div class="ept-search">' +
                '<span class="ept-search__icon">' + EPT.icons.search + '</span>' +
                '<input type="text" id="sa-tree-search" placeholder="Filter by content name..." value="' + escAttr(state.treeSearchQuery) + '" />' +
            '</div>' +
            '<select id="sa-tree-role-highlight" class="ept-select">' +
                '<option value="">Highlight role...</option>' +
            '</select>' +
            '<label class="ept-toggle">' +
                '<input type="checkbox" id="sa-tree-issues-only" ' + (state.treeShowIssuesOnly ? 'checked' : '') + ' />' +
                'Issues only' +
            '</label>';
        content.appendChild(toolbar);

        // Tree container
        var treeContainer = document.createElement('div');
        treeContainer.id = 'sa-tree-container';
        treeContainer.className = 'ept-card';
        var treeBody = document.createElement('div');
        treeBody.className = 'ept-card__body sa-tree-body';
        treeContainer.appendChild(treeBody);
        content.appendChild(treeContainer);

        EPT.showLoading(treeBody);

        // Load roles for highlight dropdown
        loadRolesForDropdown();

        // Bind toolbar
        document.getElementById('sa-tree-search').addEventListener('input', function (e) {
            state.treeSearchQuery = e.target.value;
            filterTree();
        });

        document.getElementById('sa-tree-role-highlight').addEventListener('change', function (e) {
            state.treeHighlightRole = e.target.value;
            applyRoleHighlight();
        });

        document.getElementById('sa-tree-issues-only').addEventListener('change', function (e) {
            state.treeShowIssuesOnly = e.target.checked;
            filterTree();
        });

        // Load root children
        try {
            var children = await loadChildren(0);
            treeBody.innerHTML = '';
            renderTree(treeBody, children, 0);
        } catch (err) {
            treeBody.innerHTML = '<div class="ept-empty"><p>Error loading tree: ' + escHtml(err.message) + '</p></div>';
        }
    }

    async function loadRolesForDropdown() {
        try {
            if (state.roles.length === 0) {
                state.roles = await EPT.fetchJson(API + '/roles');
            }
            var select = document.getElementById('sa-tree-role-highlight');
            if (!select) return;
            var html = '<option value="">Highlight role...</option>';
            for (var i = 0; i < state.roles.length; i++) {
                var r = state.roles[i];
                var label = r.roleOrUser + ' (' + r.entityType + ')';
                html += '<option value="' + escAttr(r.roleOrUser) + '"' +
                    (state.treeHighlightRole === r.roleOrUser ? ' selected' : '') +
                    '>' + escHtml(label) + '</option>';
            }
            select.innerHTML = html;
        } catch (e) {
            // Roles dropdown not critical
        }
    }

    async function loadChildren(parentId) {
        if (state.treeNodes[parentId]) return state.treeNodes[parentId];
        var children = await EPT.fetchJson(API + '/tree/children?parentId=' + parentId);
        state.treeNodes[parentId] = children;
        return children;
    }

    function renderTree(container, nodes, depth) {
        var ul = document.createElement('ul');
        ul.className = 'sa-tree' + (depth === 0 ? ' sa-tree--root' : '');

        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            var li = createTreeNode(node, depth);
            ul.appendChild(li);
        }

        container.appendChild(ul);
    }

    function createTreeNode(node, depth) {
        var li = document.createElement('li');
        li.className = 'sa-tree__item';
        li.setAttribute('data-content-id', node.contentId);
        li.setAttribute('data-name', (node.name || '').toLowerCase());
        li.setAttribute('data-issue-count', node.issueCount || 0);
        li.setAttribute('data-subtree-issues', node.subtreeIssueCount || 0);

        var row = document.createElement('div');
        row.className = 'sa-tree__row';

        // Issue left-border
        if (node.issueCount > 0) {
            var hasCritical = node.everyoneCanPublish || node.everyoneCanEdit;
            li.classList.add('sa-tree__item--flagged');
            if (hasCritical) li.classList.add('sa-tree__item--critical');
        }

        // Toggle button
        var toggle = document.createElement('button');
        toggle.className = 'sa-tree__toggle';
        if (node.hasChildren) {
            toggle.innerHTML = EPT.icons.chevronRight;
            toggle.addEventListener('click', function (e) {
                e.stopPropagation();
                toggleNode(li, node);
            });
        } else {
            toggle.style.visibility = 'hidden';
        }
        row.appendChild(toggle);

        // Name
        var nameSpan = document.createElement('span');
        nameSpan.className = 'sa-tree__name';
        nameSpan.textContent = node.name || '(unnamed)';
        if (!node.isPage) {
            nameSpan.classList.add('sa-tree__name--nonpage');
        }
        row.appendChild(nameSpan);

        // Inheritance indicator
        if (node.isInheriting && !node.hasExplicitAcl) {
            var inheritLabel = document.createElement('span');
            inheritLabel.className = 'sa-tree__inherit';
            inheritLabel.textContent = 'inherited';
            row.appendChild(inheritLabel);
        }

        // Permission badges
        var badges = document.createElement('span');
        badges.className = 'sa-tree__badges';
        if (node.entries && node.entries.length > 0) {
            for (var j = 0; j < node.entries.length; j++) {
                var entry = node.entries[j];
                var badge = document.createElement('span');
                badge.className = 'ept-badge ' + accessBadgeClass(entry.access);
                badge.setAttribute('data-role', entry.name);
                badge.textContent = entry.name + ': ' + entry.access;
                if (entry.entityType === 'VisitorGroup') {
                    badge.classList.add('sa-badge--vg');
                }
                badges.appendChild(badge);
            }
        }
        row.appendChild(badges);

        // Issue indicators
        var indicators = document.createElement('span');
        indicators.className = 'sa-tree__indicators';
        if (node.everyoneCanPublish || node.everyoneCanEdit) {
            indicators.innerHTML += '<span class="sa-severity sa-severity--critical" title="Critical: Everyone has elevated access"></span>';
        }
        if (node.childMorePermissive) {
            indicators.innerHTML += '<span class="sa-severity sa-severity--warning" title="Warning: More permissive than parent"></span>';
        }
        if (node.hasNoRestrictions && node.isPage) {
            indicators.innerHTML += '<span class="sa-severity sa-severity--info" title="Info: No restrictions set"></span>';
        }
        // Subtree issue count badge on collapsed nodes
        if (node.subtreeIssueCount > 0) {
            var subtreeBadge = document.createElement('span');
            subtreeBadge.className = 'sa-subtree-badge';
            subtreeBadge.textContent = node.subtreeIssueCount + ' issue' + (node.subtreeIssueCount !== 1 ? 's' : '');
            subtreeBadge.title = node.subtreeIssueCount + ' issues in subtree';
            indicators.appendChild(subtreeBadge);
        }
        row.appendChild(indicators);

        li.appendChild(row);

        // Click row to show detail
        row.addEventListener('click', function () {
            showNodeDetail(li, node);
        });

        // Children container (populated on expand)
        var childContainer = document.createElement('div');
        childContainer.className = 'sa-tree__children';
        childContainer.style.display = 'none';
        li.appendChild(childContainer);

        return li;
    }

    async function toggleNode(li, node) {
        var childContainer = li.querySelector('.sa-tree__children');
        var toggleBtn = li.querySelector('.sa-tree__toggle');
        var isExpanded = state.expandedNodes[node.contentId];

        if (isExpanded) {
            // Collapse
            state.expandedNodes[node.contentId] = false;
            childContainer.style.display = 'none';
            toggleBtn.innerHTML = EPT.icons.chevronRight;
            // Show subtree badge again
            var subtreeBadge = li.querySelector('.sa-subtree-badge');
            if (subtreeBadge) subtreeBadge.style.display = '';
        } else {
            // Expand
            state.expandedNodes[node.contentId] = true;
            toggleBtn.innerHTML = EPT.icons.chevronDown;
            // Hide subtree badge when expanded
            var subtreeBadge2 = li.querySelector('.sa-subtree-badge');
            if (subtreeBadge2) subtreeBadge2.style.display = 'none';

            if (childContainer.children.length === 0) {
                // Load children
                EPT.showLoading(childContainer);
                childContainer.style.display = '';
                try {
                    var children = await loadChildren(node.contentId);
                    childContainer.innerHTML = '';
                    if (children.length === 0) {
                        childContainer.innerHTML = '<div class="sa-tree__empty">No child content</div>';
                    } else {
                        renderTree(childContainer, children, 1);
                        applyRoleHighlight();
                        filterTree();
                    }
                } catch (err) {
                    childContainer.innerHTML = '<div class="ept-empty"><p>Error: ' + escHtml(err.message) + '</p></div>';
                }
            } else {
                childContainer.style.display = '';
            }
        }
    }

    async function showNodeDetail(li, node) {
        // Toggle detail panel
        var existing = li.querySelector('.sa-detail-panel');
        if (existing) {
            existing.remove();
            state.selectedNode = null;
            return;
        }

        // Remove any other open detail panels
        var openPanels = root.querySelectorAll('.sa-detail-panel');
        for (var p = 0; p < openPanels.length; p++) {
            openPanels[p].remove();
        }

        state.selectedNode = node.contentId;

        var panel = document.createElement('div');
        panel.className = 'sa-detail-panel';
        EPT.showLoading(panel);
        li.appendChild(panel);

        try {
            var detail = await EPT.fetchJson(API + '/tree/node/' + node.contentId);

            var html = '<div class="sa-detail-header">';
            html += '<strong>' + escHtml(detail.name) + '</strong>';
            if (detail.contentTypeName) html += ' <span class="ept-muted">(' + escHtml(detail.contentTypeName) + ')</span>';
            if (detail.breadcrumb) html += '<div class="ept-muted" style="font-size:11px">' + escHtml(detail.breadcrumb) + '</div>';
            html += '</div>';

            // Inheritance status
            html += '<div class="sa-detail-section">';
            if (detail.isInheriting) {
                html += '<span class="ept-badge ept-badge--default">Inheriting from parent</span> ';
            }
            if (detail.hasExplicitAcl) {
                html += '<span class="ept-badge ept-badge--primary">Has explicit ACL</span> ';
            }
            html += '</div>';

            // ACL table
            if (detail.entries && detail.entries.length > 0) {
                html += '<table class="ept-table sa-acl-table">';
                html += '<thead><tr><th>Role/User</th><th>Type</th><th>Access Level</th></tr></thead>';
                html += '<tbody>';
                for (var e = 0; e < detail.entries.length; e++) {
                    var entry = detail.entries[e];
                    html += '<tr>';
                    html += '<td>' + escHtml(entry.name) + '</td>';
                    html += '<td>' + escHtml(entry.entityType) + '</td>';
                    html += '<td><span class="ept-badge ' + accessBadgeClass(entry.access) + '">' + escHtml(entry.access) + '</span></td>';
                    html += '</tr>';
                }
                html += '</tbody></table>';
            } else {
                html += '<p class="ept-muted">No ACL entries</p>';
            }

            // Issues on this node
            var issues = [];
            if (detail.everyoneCanPublish) issues.push({ severity: 'Critical', text: '"Everyone" role has Publish or higher access' });
            if (detail.everyoneCanEdit) issues.push({ severity: 'Critical', text: '"Everyone" role has Edit or higher access' });
            if (detail.childMorePermissive) issues.push({ severity: 'Warning', text: 'This node is more permissive than its parent' });
            if (detail.hasNoRestrictions && detail.isPage) issues.push({ severity: 'Info', text: 'No access restrictions set on this page' });

            if (issues.length > 0) {
                html += '<div class="sa-detail-issues">';
                html += '<strong>Issues:</strong>';
                for (var iss = 0; iss < issues.length; iss++) {
                    html += '<div class="sa-detail-issue">' + severityLabel(issues[iss].severity) + ' ' + escHtml(issues[iss].text) + '</div>';
                }
                html += '</div>';
            }

            panel.innerHTML = html;
        } catch (err) {
            panel.innerHTML = '<div class="ept-empty"><p>Error loading details: ' + escHtml(err.message) + '</p></div>';
        }
    }

    function filterTree() {
        var query = state.treeSearchQuery.toLowerCase();
        var issuesOnly = state.treeShowIssuesOnly;
        var items = root.querySelectorAll('.sa-tree__item');

        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var name = item.getAttribute('data-name') || '';
            var issueCount = parseInt(item.getAttribute('data-issue-count') || '0', 10);
            var subtreeIssues = parseInt(item.getAttribute('data-subtree-issues') || '0', 10);

            var visible = true;
            if (query && name.indexOf(query) < 0) visible = false;
            if (issuesOnly && issueCount === 0 && subtreeIssues === 0) visible = false;

            item.style.display = visible ? '' : 'none';
        }
    }

    function applyRoleHighlight() {
        var role = state.treeHighlightRole;
        var items = root.querySelectorAll('.sa-tree__item');

        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            item.classList.remove('sa-tree__item--dimmed');

            if (role) {
                var badges = item.querySelectorAll('.sa-tree__badges .ept-badge');
                var hasRole = false;
                for (var b = 0; b < badges.length; b++) {
                    var badgeRole = badges[b].getAttribute('data-role');
                    if (badgeRole === role) {
                        hasRole = true;
                        badges[b].classList.add('sa-badge--highlighted');
                    } else {
                        badges[b].classList.remove('sa-badge--highlighted');
                    }
                }
                if (!hasRole) {
                    item.classList.add('sa-tree__item--dimmed');
                }
            } else {
                // Clear all highlights
                var allBadges = item.querySelectorAll('.sa-badge--highlighted');
                for (var h = 0; h < allBadges.length; h++) {
                    allBadges[h].classList.remove('sa-badge--highlighted');
                }
            }
        }
    }

    async function showInTree(contentId) {
        // Switch to tree tab
        switchTab('tree');

        // Wait for tree tab to render
        await new Promise(function (resolve) { setTimeout(resolve, 200); });

        try {
            // Fetch the ancestor path
            var path = await EPT.fetchJson(API + '/tree/path/' + contentId);

            // Expand each ancestor in sequence
            for (var i = 0; i < path.length; i++) {
                var ancestorId = path[i];
                var li = root.querySelector('.sa-tree__item[data-content-id="' + ancestorId + '"]');
                if (li && !state.expandedNodes[ancestorId]) {
                    var toggleBtn = li.querySelector('.sa-tree__toggle');
                    if (toggleBtn && toggleBtn.style.visibility !== 'hidden') {
                        toggleBtn.click();
                        // Wait for children to load
                        await new Promise(function (resolve) { setTimeout(resolve, 300); });
                    }
                }
            }

            // Scroll to and highlight the target node
            var targetLi = root.querySelector('.sa-tree__item[data-content-id="' + contentId + '"]');
            if (targetLi) {
                targetLi.scrollIntoView({ behavior: 'smooth', block: 'center' });
                targetLi.classList.add('sa-tree__item--highlight');
                setTimeout(function () {
                    targetLi.classList.remove('sa-tree__item--highlight');
                }, 3000);
            }
        } catch (err) {
            console.error('Failed to navigate to content in tree:', err);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TAB 2: Role/User Explorer
    // ═══════════════════════════════════════════════════════════════

    async function renderRolesTab() {
        var content = document.getElementById('sa-tab-content');
        content.innerHTML = '';

        // Toolbar
        var toolbar = document.createElement('div');
        toolbar.className = 'ept-toolbar';
        toolbar.innerHTML =
            '<select id="sa-role-select" class="ept-select">' +
                '<option value="">Select role or user...</option>' +
            '</select>' +
            '<select id="sa-role-access-filter" class="ept-select">' +
                '<option value="">All access levels</option>' +
                '<option value="FullAccess"' + (state.roleAccessFilter === 'FullAccess' ? ' selected' : '') + '>Full Access</option>' +
                '<option value="Publish"' + (state.roleAccessFilter === 'Publish' ? ' selected' : '') + '>Publish</option>' +
                '<option value="Edit"' + (state.roleAccessFilter === 'Edit' ? ' selected' : '') + '>Edit</option>' +
                '<option value="Read"' + (state.roleAccessFilter === 'Read' ? ' selected' : '') + '>Read</option>' +
            '</select>';
        content.appendChild(toolbar);

        // Stats area
        var statsEl = document.createElement('div');
        statsEl.id = 'sa-role-stats';
        statsEl.className = 'ept-stats';
        content.appendChild(statsEl);

        // Table container
        var tableContainer = document.createElement('div');
        tableContainer.id = 'sa-role-table';
        tableContainer.className = 'ept-card';
        content.appendChild(tableContainer);

        // Load roles
        try {
            if (state.roles.length === 0) {
                state.roles = await EPT.fetchJson(API + '/roles');
            }

            var select = document.getElementById('sa-role-select');
            var html = '<option value="">Select role or user...</option>';
            for (var i = 0; i < state.roles.length; i++) {
                var r = state.roles[i];
                var label = r.roleOrUser + ' (' + r.entityType + ') - ' + r.totalContentCount + ' items';
                var selected = state.selectedRole && state.selectedRole.roleOrUser === r.roleOrUser ? ' selected' : '';
                html += '<option value="' + escAttr(r.roleOrUser) + '"' + selected + '>' + escHtml(label) + '</option>';
            }
            select.innerHTML = html;

            select.addEventListener('change', function () {
                var roleName = this.value;
                state.selectedRole = state.roles.find(function (r) { return r.roleOrUser === roleName; }) || null;
                state.rolePage = 1;
                loadRoleContent();
                savePrefs();
            });

            document.getElementById('sa-role-access-filter').addEventListener('change', function () {
                state.roleAccessFilter = this.value;
                state.rolePage = 1;
                loadRoleContent();
                savePrefs();
            });

            if (state.selectedRole) {
                loadRoleContent();
            } else {
                tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>Select a role or user to explore their content access.</p></div></div>';
            }
        } catch (err) {
            tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>Error loading roles: ' + escHtml(err.message) + '</p></div></div>';
        }
    }

    async function loadRoleContent() {
        if (!state.selectedRole) return;

        var statsEl = document.getElementById('sa-role-stats');
        var tableContainer = document.getElementById('sa-role-table');

        // Render stats
        var r = state.selectedRole;
        statsEl.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + (r.fullAccessCount || 0) + '</div><div class="ept-stat__label">Full Access</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + (r.publishCount || 0) + '</div><div class="ept-stat__label">Publish</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + (r.editCount || 0) + '</div><div class="ept-stat__label">Edit</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + (r.readOnlyCount || 0) + '</div><div class="ept-stat__label">Read Only</div></div>';

        EPT.showLoading(tableContainer);

        try {
            var url = API + '/roles/' + encodeURIComponent(r.roleOrUser) + '/content?entityType=' + encodeURIComponent(r.entityType) +
                '&page=' + state.rolePage + '&pageSize=' + PAGE_SIZE;
            if (state.roleAccessFilter) url += '&access=' + encodeURIComponent(state.roleAccessFilter);

            var result = await EPT.fetchJson(url);
            state.roleResult = result;

            tableContainer.innerHTML = '';

            if (!result.items || result.items.length === 0) {
                tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>No content found for this role with the current filter.</p></div></div>';
                return;
            }

            var body = document.createElement('div');
            body.className = 'ept-card__body ept-card__body--flush';

            var table = document.createElement('table');
            table.className = 'ept-table';
            table.innerHTML =
                '<thead><tr>' +
                    '<th>Content Name</th>' +
                    '<th>Breadcrumb</th>' +
                    '<th>Content Type</th>' +
                    '<th>Access Level</th>' +
                    '<th>Inherited?</th>' +
                    '<th>Actions</th>' +
                '</tr></thead>';

            var tbody = document.createElement('tbody');
            for (var i = 0; i < result.items.length; i++) {
                var item = result.items[i];
                var tr = document.createElement('tr');
                tr.innerHTML =
                    '<td><strong>' + escHtml(item.name) + '</strong></td>' +
                    '<td><span class="ept-truncate" title="' + escAttr(item.breadcrumb) + '">' + escHtml(item.breadcrumb || '') + '</span></td>' +
                    '<td>' + escHtml(item.contentTypeName || '') + '</td>' +
                    '<td><span class="ept-badge ' + accessBadgeClass(item.accessLevel) + '">' + escHtml(item.accessLevel) + '</span></td>' +
                    '<td>' + (item.isInherited ? 'Inherited' : 'Explicit') + '</td>' +
                    '<td class="sa-actions"></td>';

                var actionsCell = tr.querySelector('.sa-actions');

                if (item.editUrl) {
                    var editLink = document.createElement('a');
                    editLink.className = 'ept-btn ept-btn--sm ept-btn--icon';
                    editLink.href = item.editUrl;
                    editLink.target = '_blank';
                    editLink.title = 'Edit';
                    editLink.innerHTML = EPT.icons.edit;
                    actionsCell.appendChild(editLink);
                }

                var treeBtn = document.createElement('button');
                treeBtn.className = 'ept-btn ept-btn--sm ept-btn--icon';
                treeBtn.title = 'Show in tree';
                treeBtn.innerHTML = EPT.icons.tree;
                treeBtn.setAttribute('data-content-id', item.contentId);
                treeBtn.addEventListener('click', function () {
                    showInTree(parseInt(this.getAttribute('data-content-id'), 10));
                });
                actionsCell.appendChild(treeBtn);

                tbody.appendChild(tr);
            }
            table.appendChild(tbody);
            body.appendChild(table);
            tableContainer.appendChild(body);

            // Pagination
            var totalPages = Math.ceil((result.totalCount || result.items.length) / PAGE_SIZE);
            if (totalPages > 1) {
                var pag = renderPagination(state.rolePage, totalPages, function (newPage) {
                    state.rolePage = newPage;
                    loadRoleContent();
                });
                tableContainer.appendChild(pag);
            }
        } catch (err) {
            tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>Error: ' + escHtml(err.message) + '</p></div></div>';
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // TAB 3: Issues Dashboard
    // ═══════════════════════════════════════════════════════════════

    async function renderIssuesTab() {
        var content = document.getElementById('sa-tab-content');
        content.innerHTML = '';

        // Load summary
        try {
            state.issuesSummary = await EPT.fetchJson(API + '/issues/summary');
        } catch (e) {
            state.issuesSummary = null;
        }

        // Stats bar
        var statsEl = document.createElement('div');
        statsEl.className = 'ept-stats';
        if (state.issuesSummary) {
            var s = state.issuesSummary;
            statsEl.innerHTML =
                '<div class="ept-stat"><div class="ept-stat__value sa-stat--critical">' + (s.criticalCount || 0) + '</div><div class="ept-stat__label">Critical</div></div>' +
                '<div class="ept-stat"><div class="ept-stat__value sa-stat--warning">' + (s.warningCount || 0) + '</div><div class="ept-stat__label">Warning</div></div>' +
                '<div class="ept-stat"><div class="ept-stat__value">' + (s.infoCount || 0) + '</div><div class="ept-stat__label">Info</div></div>';
        }
        content.appendChild(statsEl);

        // Toolbar
        var toolbar = document.createElement('div');
        toolbar.className = 'ept-toolbar';
        toolbar.innerHTML =
            '<select id="sa-issues-type-filter" class="ept-select">' +
                '<option value="">All issue types</option>' +
                '<option value="EveryonePublish"' + (state.issueTypeFilter === 'EveryonePublish' ? ' selected' : '') + '>Everyone can Publish</option>' +
                '<option value="EveryoneEdit"' + (state.issueTypeFilter === 'EveryoneEdit' ? ' selected' : '') + '>Everyone can Edit</option>' +
                '<option value="ChildMorePermissive"' + (state.issueTypeFilter === 'ChildMorePermissive' ? ' selected' : '') + '>Inconsistent Inheritance</option>' +
                '<option value="NoRestrictions"' + (state.issueTypeFilter === 'NoRestrictions' ? ' selected' : '') + '>No Restrictions</option>' +
            '</select>' +
            '<select id="sa-issues-severity-filter" class="ept-select">' +
                '<option value="">All severities</option>' +
                '<option value="Critical"' + (state.issueSeverityFilter === 'Critical' ? ' selected' : '') + '>Critical</option>' +
                '<option value="Warning"' + (state.issueSeverityFilter === 'Warning' ? ' selected' : '') + '>Warning</option>' +
                '<option value="Info"' + (state.issueSeverityFilter === 'Info' ? ' selected' : '') + '>Info</option>' +
            '</select>';
        content.appendChild(toolbar);

        // Table container
        var tableContainer = document.createElement('div');
        tableContainer.id = 'sa-issues-table';
        tableContainer.className = 'ept-card';
        content.appendChild(tableContainer);

        // Bind filters
        document.getElementById('sa-issues-type-filter').addEventListener('change', function () {
            state.issueTypeFilter = this.value;
            state.issuesPage = 1;
            loadIssues();
            savePrefs();
        });

        document.getElementById('sa-issues-severity-filter').addEventListener('change', function () {
            state.issueSeverityFilter = this.value;
            state.issuesPage = 1;
            loadIssues();
            savePrefs();
        });

        loadIssues();
    }

    async function loadIssues() {
        var tableContainer = document.getElementById('sa-issues-table');
        if (!tableContainer) return;

        EPT.showLoading(tableContainer);

        try {
            var url = API + '/issues?page=' + state.issuesPage + '&pageSize=' + PAGE_SIZE;
            if (state.issueTypeFilter) url += '&type=' + encodeURIComponent(state.issueTypeFilter);
            if (state.issueSeverityFilter) url += '&severity=' + encodeURIComponent(state.issueSeverityFilter);

            var result = await EPT.fetchJson(url);
            state.issuesResult = result;

            tableContainer.innerHTML = '';

            if (!result.items || result.items.length === 0) {
                tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>No issues found with the current filters.</p></div></div>';
                return;
            }

            var body = document.createElement('div');
            body.className = 'ept-card__body ept-card__body--flush';

            var table = document.createElement('table');
            table.className = 'ept-table';
            table.innerHTML =
                '<thead><tr>' +
                    '<th>Severity</th>' +
                    '<th>Issue Type</th>' +
                    '<th>Content Name</th>' +
                    '<th>Location</th>' +
                    '<th>Actions</th>' +
                '</tr></thead>';

            var tbody = document.createElement('tbody');
            for (var i = 0; i < result.items.length; i++) {
                var issue = result.items[i];
                var tr = document.createElement('tr');

                var issueTypeLabel = formatIssueType(issue.issueType);

                tr.innerHTML =
                    '<td>' + severityLabel(issue.severity) + '</td>' +
                    '<td>' + escHtml(issueTypeLabel) + '</td>' +
                    '<td><strong>' + escHtml(issue.contentName) + '</strong>' +
                        (issue.description ? '<div class="ept-muted" style="font-size:11px">' + escHtml(issue.description) + '</div>' : '') + '</td>' +
                    '<td><span class="ept-truncate" title="' + escAttr(issue.breadcrumb) + '">' + escHtml(issue.breadcrumb || '') + '</span></td>' +
                    '<td class="sa-actions"></td>';

                var actionsCell = tr.querySelector('.sa-actions');

                if (issue.editUrl) {
                    var editLink = document.createElement('a');
                    editLink.className = 'ept-btn ept-btn--sm ept-btn--icon';
                    editLink.href = issue.editUrl;
                    editLink.target = '_blank';
                    editLink.title = 'Edit';
                    editLink.innerHTML = EPT.icons.edit;
                    actionsCell.appendChild(editLink);
                }

                var treeBtn = document.createElement('button');
                treeBtn.className = 'ept-btn ept-btn--sm ept-btn--icon';
                treeBtn.title = 'Show in tree';
                treeBtn.innerHTML = EPT.icons.tree;
                treeBtn.setAttribute('data-content-id', issue.contentId);
                treeBtn.addEventListener('click', function () {
                    showInTree(parseInt(this.getAttribute('data-content-id'), 10));
                });
                actionsCell.appendChild(treeBtn);

                tbody.appendChild(tr);
            }
            table.appendChild(tbody);
            body.appendChild(table);
            tableContainer.appendChild(body);

            // Pagination
            var totalPages = Math.ceil((result.totalCount || result.items.length) / PAGE_SIZE);
            if (totalPages > 1) {
                var pag = renderPagination(state.issuesPage, totalPages, function (newPage) {
                    state.issuesPage = newPage;
                    loadIssues();
                });
                tableContainer.appendChild(pag);
            }
        } catch (err) {
            tableContainer.innerHTML = '<div class="ept-card__body"><div class="ept-empty"><p>Error loading issues: ' + escHtml(err.message) + '</p></div></div>';
        }
    }

    function formatIssueType(type) {
        if (!type) return '';
        var map = {
            'EveryonePublish': 'Everyone can Publish',
            'EveryoneEdit': 'Everyone can Edit',
            'ChildMorePermissive': 'Inconsistent Inheritance',
            'NoRestrictions': 'No Restrictions'
        };
        return map[type] || type;
    }

    // ── Styles ─────────────────────────────────────────────────────
    function injectStyles() {
        var style = document.createElement('style');
        style.textContent =
            /* Tabs */
            '.sa-tabs { display:flex; gap:0; border-bottom:2px solid var(--ept-border, #e0e0e0); margin-bottom:16px; }' +
            '.sa-tab { padding:10px 20px; border:none; background:none; cursor:pointer; font-size:13px; font-weight:500; color:var(--ept-text-secondary, #666); border-bottom:2px solid transparent; margin-bottom:-2px; transition:all .15s; }' +
            '.sa-tab:hover { color:var(--ept-text, #333); }' +
            '.sa-tab--active { color:var(--ept-primary, #3b82f6); border-bottom-color:var(--ept-primary, #3b82f6); }' +
            '.sa-tab-badge { display:inline-block; background:#ef4444; color:#fff; font-size:10px; font-weight:700; padding:1px 6px; border-radius:8px; margin-left:6px; vertical-align:middle; }' +

            /* Tree */
            '.sa-tree-body { min-height:200px; }' +
            '.sa-tree { list-style:none; margin:0; padding:0; }' +
            '.sa-tree--root { padding:0; }' +
            '.sa-tree .sa-tree { padding-left:24px; }' +
            '.sa-tree__item { position:relative; }' +
            '.sa-tree__item--flagged { border-left:3px solid #f59e0b; }' +
            '.sa-tree__item--critical { border-left-color:#ef4444; }' +
            '.sa-tree__item--dimmed { opacity:0.35; }' +
            '.sa-tree__item--highlight { animation:sa-highlight 3s ease-out; }' +
            '@keyframes sa-highlight { 0% { background:#dbeafe; } 100% { background:transparent; } }' +

            '.sa-tree__row { display:flex; align-items:center; gap:6px; padding:6px 8px; cursor:pointer; border-radius:4px; transition:background .1s; }' +
            '.sa-tree__row:hover { background:var(--ept-hover, #f5f6f8); }' +

            '.sa-tree__toggle { display:flex; align-items:center; justify-content:center; width:20px; height:20px; border:none; background:none; cursor:pointer; padding:0; flex-shrink:0; color:var(--ept-text-secondary, #999); }' +
            '.sa-tree__toggle svg { width:14px; height:14px; }' +

            '.sa-tree__name { font-size:13px; font-weight:500; white-space:nowrap; overflow:hidden; text-overflow:ellipsis; max-width:250px; }' +
            '.sa-tree__name--nonpage { font-style:italic; color:var(--ept-text-secondary, #888); }' +

            '.sa-tree__inherit { font-size:10px; color:var(--ept-text-secondary, #aaa); background:var(--ept-bg, #f5f6f8); padding:1px 6px; border-radius:8px; flex-shrink:0; }' +

            '.sa-tree__badges { display:flex; gap:3px; flex-wrap:wrap; margin-left:auto; }' +
            '.sa-tree__badges .ept-badge { font-size:10px; padding:1px 6px; white-space:nowrap; }' +
            '.sa-badge--vg { border:1px dashed var(--ept-text-secondary, #999); }' +
            '.sa-badge--highlighted { outline:2px solid var(--ept-primary, #3b82f6); outline-offset:1px; }' +

            '.sa-tree__indicators { display:flex; gap:4px; align-items:center; margin-left:8px; flex-shrink:0; }' +

            '.sa-subtree-badge { font-size:10px; padding:1px 6px; background:#fef2f2; color:#991b1b; border-radius:8px; white-space:nowrap; }' +

            '.sa-tree__children { padding-left:0; }' +
            '.sa-tree__empty { font-size:12px; color:var(--ept-text-secondary, #999); padding:4px 8px 4px 48px; font-style:italic; }' +

            /* Severity dots */
            '.sa-severity { display:inline-block; width:10px; height:10px; border-radius:50%; flex-shrink:0; }' +
            '.sa-severity--critical { background:#ef4444; box-shadow:0 0 4px rgba(239,68,68,.4); }' +
            '.sa-severity--warning { background:#f59e0b; box-shadow:0 0 4px rgba(245,158,11,.4); }' +
            '.sa-severity--info { background:#6b7280; }' +

            /* Detail panel */
            '.sa-detail-panel { margin:4px 0 8px 44px; padding:12px 16px; background:#fff; border:1px solid var(--ept-border, #e0e0e0); border-radius:8px; box-shadow:0 2px 8px rgba(0,0,0,.06); animation:sa-slideDown .15s ease-out; }' +
            '@keyframes sa-slideDown { from { opacity:0; transform:translateY(-4px); } to { opacity:1; transform:translateY(0); } }' +
            '.sa-detail-header { margin-bottom:8px; }' +
            '.sa-detail-section { margin-bottom:8px; }' +
            '.sa-acl-table { font-size:12px; }' +
            '.sa-acl-table th { font-size:11px; text-transform:uppercase; letter-spacing:.3px; }' +
            '.sa-detail-issues { margin-top:12px; padding-top:8px; border-top:1px solid var(--ept-border, #e0e0e0); }' +
            '.sa-detail-issue { margin-top:4px; font-size:12px; display:flex; align-items:center; gap:6px; }' +

            /* Role explorer + Issues actions */
            '.sa-actions { display:flex; gap:4px; white-space:nowrap; }' +

            /* Pagination */
            '.sa-pagination { display:flex; align-items:center; justify-content:center; gap:12px; padding:12px 16px; border-top:1px solid var(--ept-border, #e0e0e0); }' +
            '.sa-pagination__info { font-size:12px; color:var(--ept-text-secondary, #666); }' +

            /* Stats coloring */
            '.sa-stat--critical { color:#ef4444; }' +
            '.sa-stat--warning { color:#f59e0b; }' +
            '.sa-stat--muted { font-size:14px; }';

        document.head.appendChild(style);
    }

    injectStyles();

    // Boot
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
