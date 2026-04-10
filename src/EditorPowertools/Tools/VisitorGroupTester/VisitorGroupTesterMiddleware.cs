using System.Text;
using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using EPiServer.Core;
using EPiServer.Editor;
using EPiServer.Web.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace UmageAI.Optimizely.EditorPowerTools.Tools.VisitorGroupTester;

/// <summary>
/// Middleware that injects the Visitor Group Tester floating toolbar into HTML pages
/// for authenticated users with the UmageAI.Optimizely.EditorPowerTools policy.
/// Only runs on public-facing pages (not CMS shell or API requests).
/// </summary>
public class VisitorGroupTesterMiddleware
{
    private readonly RequestDelegate _next;

    public VisitorGroupTesterMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip non-GET requests
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip API, CMS shell, and static asset requests — only inject on public site
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/episerver", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/editorpowertools", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/util/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/globalassets", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/contentassets", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/siteassets", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/ClientResources/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/_content/", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/modules/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Skip full edit mode only — preview mode (epieditmode=false) is allowed through
        // because Optimizely adds data-epi-block-id attributes in preview mode that the inspector uses.
        var queryString = context.Request.QueryString.Value ?? "";
        if (IsEditMode(queryString))
        {
            await _next(context);
            return;
        }

        // Check authentication
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Check feature toggle
        var accessChecker = context.RequestServices.GetService(typeof(FeatureAccessChecker)) as FeatureAccessChecker;
        if (accessChecker == null || !accessChecker.HasAccess(
            context,
            nameof(FeatureToggles.VisitorGroupTester),
            EditorPowertoolsPermissions.VisitorGroupTester))
        {
            await _next(context);
            return;
        }

        // Check authorization policy
        var authService = context.RequestServices.GetService(typeof(IAuthorizationService)) as IAuthorizationService;
        if (authService != null)
        {
            var authResult = await authService.AuthorizeAsync(context.User, "codeart:editorpowertools");
            if (!authResult.Succeeded)
            {
                await _next(context);
                return;
            }
        }

        // Buffer the response to inject our toolbar
        var originalBody = context.Response.Body;
        using var bufferStream = new MemoryStream();
        context.Response.Body = bufferStream;

        await _next(context);

        bufferStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(bufferStream).ReadToEndAsync();

        // Only inject into HTML responses
        var contentType = context.Response.ContentType ?? "";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            responseBody.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            // After _next(), Optimizely's routing pipeline has run and IContentRouteHelper
            // has the content reference for the current page — use it to build an edit URL.
            var pageEditUrl = ResolvePageEditUrl(context);
            var toolbar = GetToolbarHtml(pageEditUrl);
            responseBody = responseBody.Replace("</body>", toolbar + "\n</body>", StringComparison.OrdinalIgnoreCase);
        }

        var bytes = Encoding.UTF8.GetBytes(responseBody);
        context.Response.Body = originalBody;
        context.Response.ContentLength = bytes.Length;
        await context.Response.Body.WriteAsync(bytes);
    }

    private static string? ResolvePageEditUrl(HttpContext context)
    {
        try
        {
            var routeHelper = context.RequestServices.GetService(typeof(IContentRouteHelper)) as IContentRouteHelper;
            var contentLink = routeHelper?.ContentLink;
            if (ContentReference.IsNullOrEmpty(contentLink)) return null;
            return PageEditing.GetEditUrl(contentLink);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Returns true only for full edit mode (epieditmode=true).
    /// Preview mode (epieditmode=false) is allowed through so the inspector can use
    /// Optimizely's data-epi-block-id attributes which are only present in preview mode.
    /// </summary>
    private static bool IsEditMode(string queryString)
    {
        if (string.IsNullOrEmpty(queryString)) return false;
        var qs = queryString.ToLowerInvariant();
        // Match epieditmode=true or epieditmode=1
        if (qs.Contains("epieditmode=true") || qs.Contains("epieditmode=1")) return true;
        // epi.editmode variant used by some CMS versions
        if (qs.Contains("epi.editmode=true") || qs.Contains("epi.editmode=1")) return true;
        return false;
    }

    private static string GetToolbarHtml(string? pageEditUrl = null)
    {
        var safeEditUrl = (pageEditUrl ?? "").Replace("'", "\\'");
        return """
<style>
.ept-vgt-toggle {
    position: fixed;
    bottom: 20px;
    right: 20px;
    z-index: 99999;
    width: 48px;
    height: 48px;
    border-radius: 50%;
    background: #1e293b;
    color: #fff;
    border: 2px solid #475569;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    box-shadow: 0 4px 12px rgba(0,0,0,0.3);
    transition: background 0.2s;
    font-size: 20px;
}
.ept-vgt-toggle:hover { background: #334155; }

.ept-vgt-panel {
    position: fixed;
    bottom: 80px;
    right: 20px;
    z-index: 99998;
    width: 340px;
    max-height: 70vh;
    background: #1e293bee;
    backdrop-filter: blur(8px);
    color: #e2e8f0;
    border-radius: 12px;
    border: 1px solid #475569;
    box-shadow: 0 8px 32px rgba(0,0,0,0.4);
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    font-size: 13px;
    display: none;
    flex-direction: column;
    overflow: hidden;
}
.ept-vgt-panel.ept-vgt-open { display: flex; }

.ept-vgt-header {
    padding: 12px 16px;
    font-weight: 600;
    font-size: 14px;
    border-bottom: 1px solid #334155;
    display: flex;
    align-items: center;
    justify-content: space-between;
    flex-shrink: 0;
}
.ept-vgt-header-title { display: flex; align-items: center; gap: 8px; }

.ept-vgt-tabs {
    display: flex;
    border-bottom: 1px solid #334155;
    flex-shrink: 0;
}
.ept-vgt-tab {
    flex: 1;
    padding: 8px 12px;
    text-align: center;
    cursor: pointer;
    border: none;
    background: transparent;
    color: #94a3b8;
    font-size: 12px;
    font-weight: 500;
    transition: all 0.2s;
}
.ept-vgt-tab:hover { color: #e2e8f0; background: #334155; }
.ept-vgt-tab.ept-vgt-active { color: #60a5fa; border-bottom: 2px solid #60a5fa; }

.ept-vgt-body {
    padding: 8px 0;
    overflow-y: auto;
    flex: 1;
    min-height: 0;
}

.ept-vgt-search {
    margin: 4px 12px 8px;
    padding: 6px 10px;
    border: 1px solid #475569;
    border-radius: 6px;
    background: #0f172a;
    color: #e2e8f0;
    font-size: 12px;
    width: calc(100% - 24px);
    box-sizing: border-box;
}
.ept-vgt-search::placeholder { color: #64748b; }

.ept-vgt-group {
    display: flex;
    align-items: center;
    padding: 6px 16px;
    cursor: pointer;
    transition: background 0.15s;
}
.ept-vgt-group:hover { background: #334155; }
.ept-vgt-group input[type="checkbox"] {
    margin-right: 10px;
    accent-color: #60a5fa;
    flex-shrink: 0;
}
.ept-vgt-group-name {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

.ept-vgt-footer {
    padding: 10px 16px;
    border-top: 1px solid #334155;
    display: flex;
    gap: 8px;
    flex-shrink: 0;
}
.ept-vgt-btn {
    flex: 1;
    padding: 8px 12px;
    border: none;
    border-radius: 6px;
    cursor: pointer;
    font-size: 12px;
    font-weight: 600;
    transition: background 0.2s;
}
.ept-vgt-btn-primary { background: #3b82f6; color: #fff; }
.ept-vgt-btn-primary:hover { background: #2563eb; }
.ept-vgt-btn-secondary { background: #475569; color: #e2e8f0; }
.ept-vgt-btn-secondary:hover { background: #64748b; }

.ept-vgt-empty {
    text-align: center;
    padding: 24px 16px;
    color: #64748b;
}
.ept-vgt-loading { text-align: center; padding: 24px 16px; color: #94a3b8; }
.ept-vgt-count {
    font-size: 11px;
    color: #60a5fa;
    background: #1e3a5f;
    padding: 2px 8px;
    border-radius: 10px;
}

/* Inspector mode styles */
.ept-inspect-highlight {
    outline: 2px dashed #60a5fa !important;
    outline-offset: 2px;
    position: relative;
}
.ept-inspect-tooltip {
    position: fixed;
    z-index: 100000;
    background: #1e293bee;
    color: #e2e8f0;
    padding: 8px 12px;
    border-radius: 6px;
    font-size: 12px;
    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
    pointer-events: none;
    box-shadow: 0 4px 12px rgba(0,0,0,0.3);
    border: 1px solid #475569;
    max-width: 300px;
}
.ept-inspect-tooltip a {
    color: #60a5fa;
    text-decoration: underline;
    pointer-events: auto;
    cursor: pointer;
}
.ept-inspect-active [data-epi-block-id],
.ept-inspect-active [data-epi-content-link],
.ept-inspect-active [data-epi-property-name] {
    outline: 1px dashed #60a5fa44;
    outline-offset: 1px;
}
</style>

<button class="ept-vgt-toggle" id="ept-vgt-toggle" title="Editor Powertools: Visitor Group Tester">&#9881;</button>

<div class="ept-vgt-panel" id="ept-vgt-panel">
    <div class="ept-vgt-header">
        <span class="ept-vgt-header-title">&#9881; Editor Powertools</span>
    </div>
    <div class="ept-vgt-tabs">
        <button class="ept-vgt-tab ept-vgt-active" data-tab="groups">Visitor Groups</button>
        <button class="ept-vgt-tab" data-tab="inspect">Inspector</button>
    </div>
    <div class="ept-vgt-body" id="ept-vgt-body">
        <div class="ept-vgt-loading">Loading visitor groups...</div>
    </div>
    <div class="ept-vgt-footer" id="ept-vgt-footer">
        <button class="ept-vgt-btn ept-vgt-btn-secondary" id="ept-vgt-clear">Clear all</button>
        <button class="ept-vgt-btn ept-vgt-btn-primary" id="ept-vgt-apply">Apply</button>
    </div>
</div>

<script>
(function() {
    'use strict';
    var panel = document.getElementById('ept-vgt-panel');
    var toggle = document.getElementById('ept-vgt-toggle');
    var body = document.getElementById('ept-vgt-body');
    var footer = document.getElementById('ept-vgt-footer');
    var groups = [];
    var activeTab = 'groups';
    var inspectActive = false;
    var tooltip = null;
    var PAGE_EDIT_URL = '###PAGE_EDIT_URL###';

    // Parse current visitor groups from query param or cookie
    function getActiveGroupIds() {
        // Check query param first (takes precedence) — Optimizely uses | as separator
        var params = new URLSearchParams(window.location.search);
        var val = params.get('visitorgroupsByID');
        if (val) return val.split('|').filter(function(s) { return s.trim(); });
        // Fall back to cookie (Optimizely uses comma-separated GUIDs in cookie)
        var match = document.cookie.match(/ImpersonatedVisitorGroupsById=([^;]*)/);
        if (match && match[1]) return match[1].split(',').filter(function(s) { return s.trim(); });
        return [];
    }

    var activeGroupIds = new Set(getActiveGroupIds());

    // Rewrite internal links to carry visitorgroupsByID so navigation persists the selection
    if (activeGroupIds.size > 0) {
        patchAllLinks();
        startLinkObserver();
    }

    // Toggle panel
    toggle.addEventListener('click', function() {
        panel.classList.toggle('ept-vgt-open');
    });

    // Tab switching
    panel.querySelectorAll('.ept-vgt-tab').forEach(function(tab) {
        tab.addEventListener('click', function() {
            panel.querySelectorAll('.ept-vgt-tab').forEach(function(t) { t.classList.remove('ept-vgt-active'); });
            tab.classList.add('ept-vgt-active');
            activeTab = tab.getAttribute('data-tab');
            renderContent();
        });
    });

    // Fetch visitor groups
    fetch('/editorpowertools/api/visitor-group-tester/groups', { credentials: 'same-origin' })
        .then(function(r) { return r.ok ? r.json() : []; })
        .then(function(data) {
            groups = data || [];
            renderContent();
        })
        .catch(function() {
            groups = [];
            renderContent();
        });

    function renderContent() {
        if (activeTab === 'groups') {
            renderGroups('');
            footer.style.display = 'flex';
        } else {
            renderInspector();
            footer.style.display = 'none';
        }
    }

    function renderGroups(filter) {
        if (groups.length === 0) {
            body.innerHTML = '<div class="ept-vgt-empty">No visitor groups found.</div>';
            return;
        }

        var filtered = groups;
        if (filter) {
            var lf = filter.toLowerCase();
            filtered = groups.filter(function(g) { return g.name.toLowerCase().indexOf(lf) >= 0; });
        }

        var html = '<input type="text" class="ept-vgt-search" placeholder="Search groups..." id="ept-vgt-search" value="' +
            (filter || '').replace(/"/g, '&quot;') + '">';

        var count = 0;
        activeGroupIds.forEach(function(id) {
            if (groups.some(function(g) { return g.id === id; })) count++;
        });
        if (count > 0) {
            html += '<div style="padding: 2px 16px 6px; text-align: right;"><span class="ept-vgt-count">' + count + ' active</span></div>';
        }

        filtered.forEach(function(g) {
            var checked = activeGroupIds.has(g.id) ? ' checked' : '';
            html += '<label class="ept-vgt-group"><input type="checkbox" value="' + g.id + '"' + checked + '>' +
                '<span class="ept-vgt-group-name">' + escapeHtml(g.name) + '</span></label>';
        });

        body.innerHTML = html;

        // Bind search
        var searchInput = document.getElementById('ept-vgt-search');
        if (searchInput) {
            searchInput.addEventListener('input', function() { renderGroups(this.value); });
            if (filter) searchInput.focus();
        }

        // Bind checkboxes
        body.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
            cb.addEventListener('change', function() {
                if (this.checked) {
                    activeGroupIds.add(this.value);
                } else {
                    activeGroupIds.delete(this.value);
                }
                // Update count display
                var countEl = body.querySelector('.ept-vgt-count');
                var c = 0;
                activeGroupIds.forEach(function() { c++; });
                if (countEl) {
                    if (c > 0) {
                        countEl.textContent = c + ' active';
                        countEl.parentElement.style.display = '';
                    } else {
                        countEl.parentElement.style.display = 'none';
                    }
                }
            });
        });
    }

    // Apply button
    function setVgCookie(ids) {
        // Optimizely reads impersonated visitor groups from cookie
        // Cookie name: ImpersonatedVisitorGroupsById
        // Value: pipe-separated GUIDs (same format as visitorgroupsByID query param)
        if (ids.length > 0) {
            document.cookie = 'ImpersonatedVisitorGroupsById=' + ids.join('|') + ';path=/';
        } else {
            document.cookie = 'ImpersonatedVisitorGroupsById=;path=/;expires=Thu, 01 Jan 1970 00:00:00 GMT';
        }
    }

    function buildUrlWithGroups(ids) {
        // Pure string manipulation — avoid URLSearchParams which would percent-encode | characters
        var href = window.location.href;
        // Remove existing visitorgroupsByID param
        href = href.replace(/([?&])visitorgroupsByID=[^&#]*/g, '$1');
        // Clean up leftover ? or &&
        href = href.replace(/[?&]$/, '').replace(/[?]&/, '?').replace(/&&+/g, '&');
        if (ids.length > 0) {
            var sep = href.indexOf('?') >= 0 ? '&' : '?';
            // Optimizely expects pipe-separated GUIDs: visitorgroupsByID=guid1|guid2
            return href + sep + 'visitorgroupsByID=' + ids.join('|');
        }
        return href;
    }

    // ── Link Rewriting ────────────────────────────────────────────────
    // Patch a single href: strip existing visitorgroupsByID and add current ids.
    function patchHref(href, ids) {
        if (!href) return href;
        // Skip external origins
        if (/^https?:\/\//.test(href) && !href.startsWith(window.location.origin)) return href;
        // Skip non-navigating links
        if (/^(\/\/|mailto:|tel:|javascript:|#)/.test(href)) return href;
        // Skip CMS and EPT paths
        if (/\/(episerver|editorpowertools)\//i.test(href)) return href;
        // Strip existing param then re-add
        var clean = href.replace(/([?&])visitorgroupsByID=[^&#]*/g, '$1')
                        .replace(/[?&]$/, '').replace(/\?&/, '?').replace(/&&+/g, '&');
        if (ids.length === 0) return clean;
        var sep = clean.indexOf('?') >= 0 ? '&' : '?';
        return clean + sep + 'visitorgroupsByID=' + ids.join('|');
    }

    function patchAllLinks() {
        var ids = Array.from(activeGroupIds);
        document.querySelectorAll('a[href]').forEach(function(a) {
            var orig = a.getAttribute('href');
            var next = patchHref(orig, ids);
            if (next !== orig) a.setAttribute('href', next);
        });
    }

    var linkObserver = null;
    function startLinkObserver() {
        if (linkObserver) return;
        var ids = Array.from(activeGroupIds);
        linkObserver = new MutationObserver(function(mutations) {
            mutations.forEach(function(m) {
                m.addedNodes.forEach(function(node) {
                    if (node.nodeType !== 1) return;
                    var links = node.tagName === 'A' ? [node] : [];
                    node.querySelectorAll && node.querySelectorAll('a[href]').forEach(function(a) { links.push(a); });
                    links.forEach(function(a) {
                        if (!a.getAttribute('href')) return;
                        var orig = a.getAttribute('href');
                        var next = patchHref(orig, ids);
                        if (next !== orig) a.setAttribute('href', next);
                    });
                });
            });
        });
        linkObserver.observe(document.documentElement, { childList: true, subtree: true });
    }

    document.getElementById('ept-vgt-apply').addEventListener('click', function() {
        var ids = [];
        activeGroupIds.forEach(function(id) { ids.push(id); });
        setVgCookie(ids);
        window.location.href = buildUrlWithGroups(ids);
    });

    // Clear button
    document.getElementById('ept-vgt-clear').addEventListener('click', function() {
        activeGroupIds.clear();
        setVgCookie([]);
        window.location.href = buildUrlWithGroups([]);
    });

    // Inspector tab
    function renderInspector() {
        var blockCount = document.querySelectorAll('[data-epi-block-id],[data-epi-content-link]').length;
        var html = '<div style="padding: 16px;">';

        // Always show "Edit this page" link — URL resolved server-side by the middleware
        var editHref = PAGE_EDIT_URL || '/episerver/CMS/';
        html += '<div style="margin-bottom:12px;">' +
            '<a href="' + editHref + '" target="_blank" style="display:block;padding:7px 12px;background:#334155;border-radius:6px;color:#e2e8f0;text-decoration:none;font-size:12px;font-weight:600;text-align:center;">✏ Edit this page in CMS</a>' +
            '</div>';

        // Block inspector toggle
        html += '<label class="ept-vgt-group" style="padding: 8px 0;">' +
            '<input type="checkbox" id="ept-vgt-inspect-toggle"' + (inspectActive ? ' checked' : '') + '>' +
            '<span class="ept-vgt-group-name" style="font-weight: 600;">Block Inspector</span>' +
            '</label>';

        if (blockCount > 0) {
            html += '<p style="margin:6px 0 0;font-size:11px;color:#4ade80;">✓ ' + blockCount + ' inspectable block' + (blockCount === 1 ? '' : 's') + ' found — hover to highlight</p>';
        } else {
            html += '<div style="margin-top:8px;padding:10px;background:#0f172a;border-radius:6px;border:1px solid #334155;">' +
                '<p style="margin:0;font-size:11px;color:#94a3b8;line-height:1.5;">' +
                'Block-level inspection requires <code style="color:#60a5fa">data-epi-block-id</code> attributes in the HTML.<br><br>' +
                'Add the EPT TagHelper to your block views: <code style="color:#60a5fa">ept-block="@Model"</code>' +
                '</p></div>';
        }

        html += '</div>';
        body.innerHTML = html;

        var toggleCb = document.getElementById('ept-vgt-inspect-toggle');
        if (toggleCb) {
            toggleCb.addEventListener('change', function() {
                inspectActive = this.checked;
                if (inspectActive) enableInspect();
                else disableInspect();
            });
        }
    }

    // Currently highlighted element
    var currentHighlight = null;

    function enableInspect() {
        document.body.classList.add('ept-inspect-active');
        toggle.style.outline = '2px solid #60a5fa';

        tooltip = document.createElement('div');
        tooltip.className = 'ept-inspect-tooltip';
        tooltip.style.display = 'none';
        document.body.appendChild(tooltip);

        // Event delegation — works regardless of when elements were rendered
        document.addEventListener('mouseover', onInspectOver, true);
        document.addEventListener('mouseout', onInspectOut, true);
        document.addEventListener('mousemove', onInspectMove, true);
    }

    function disableInspect() {
        document.body.classList.remove('ept-inspect-active');
        toggle.style.outline = '';

        if (currentHighlight) {
            currentHighlight.classList.remove('ept-inspect-highlight');
            currentHighlight = null;
        }
        if (tooltip) { tooltip.remove(); tooltip = null; }

        document.removeEventListener('mouseover', onInspectOver, true);
        document.removeEventListener('mouseout', onInspectOut, true);
        document.removeEventListener('mousemove', onInspectMove, true);
    }

    // Walk up from target to find nearest element with epi data attributes
    function findInspectable(el) {
        var current = el;
        while (current && current !== document.documentElement) {
            if (current === panel || current === toggle) return null; // don't highlight our own UI
            if (current.hasAttribute && (
                current.hasAttribute('data-epi-block-id') ||
                current.hasAttribute('data-epi-content-link') ||
                current.hasAttribute('data-contentlink')
            )) return current;
            current = current.parentElement;
        }
        return null;
    }

    function onInspectOver(e) {
        var target = findInspectable(e.target);
        if (target === currentHighlight) return;

        if (currentHighlight) {
            currentHighlight.classList.remove('ept-inspect-highlight');
        }
        currentHighlight = target;

        if (!target) {
            if (tooltip) tooltip.style.display = 'none';
            return;
        }

        target.classList.add('ept-inspect-highlight');

        var blockId    = target.getAttribute('data-epi-block-id');
        var contentRef = target.getAttribute('data-epi-content-link') || target.getAttribute('data-contentlink');
        var rawId      = blockId || contentRef || '';
        // Optimizely content refs are like "42_0" or "42" — extract numeric part for the edit URL
        var numericId  = rawId.split('_')[0].split(',')[0];

        var lines = [];
        if (numericId) {
            lines.push('<span style="color:#94a3b8;font-size:11px;">ID: ' + escapeHtml(rawId) + '</span>');
            var editUrl = '/episerver/CMS/#context=epi.cms.contentdata:///' + numericId;
            lines.push('<a href="' + editUrl + '" target="_blank" style="display:inline-block;margin-top:4px;padding:3px 8px;background:#3b82f6;color:#fff;border-radius:4px;text-decoration:none;font-size:11px;font-weight:600;">✏ Edit in CMS</a>');
        } else {
            lines.push('<span style="color:#94a3b8;font-size:11px;">' + escapeHtml(target.tagName.toLowerCase()) + '</span>');
        }

        if (tooltip) {
            tooltip.innerHTML = lines.join('<br>');
            tooltip.style.display = 'block';
        }
    }

    function onInspectOut(e) {
        var target = findInspectable(e.target);
        if (target && target === currentHighlight) {
            // Only clear if relatedTarget is outside this element
            if (!target.contains(e.relatedTarget)) {
                target.classList.remove('ept-inspect-highlight');
                currentHighlight = null;
                if (tooltip) tooltip.style.display = 'none';
            }
        }
    }

    function onInspectMove(e) {
        if (tooltip && tooltip.style.display === 'block') {
            var x = e.clientX + 14;
            var y = e.clientY + 14;
            // Keep tooltip on screen
            if (x + 200 > window.innerWidth) x = e.clientX - 214;
            if (y + 80 > window.innerHeight) y = e.clientY - 84;
            tooltip.style.left = x + 'px';
            tooltip.style.top  = y + 'px';
        }
    }

    function escapeHtml(str) {
        var d = document.createElement('div');
        d.textContent = str;
        return d.innerHTML;
    }
})();
</script>
""".Replace("###PAGE_EDIT_URL###", safeEditUrl);
    }
}
