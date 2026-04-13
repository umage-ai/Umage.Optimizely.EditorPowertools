using System.Text;
using UmageAI.Optimizely.EditorPowerTools.Configuration;
using UmageAI.Optimizely.EditorPowerTools.Menu;
using UmageAI.Optimizely.EditorPowerTools.Permissions;
using EPiServer.Core;
using EPiServer.Editor;
using EPiServer.Shell;
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

    // Derived once from EPT's own module path: takes the first segment of the virtual path
    // (e.g. "/EPiServer" on CMS 12, "/Optimizely" on CMS 13) — no hardcoded strings needed.
    private static readonly Lazy<string> _shellRoot = new(() =>
    {
        var eptPath = Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "");
        return "/" + eptPath.TrimStart('/').Split('/')[0];
    });

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

        // Skip CMS shell/module paths and static asset requests — only inject on public site.
        // _shellRoot covers all Optimizely module paths (EPT included) regardless of CMS version.
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith(_shellRoot.Value, StringComparison.OrdinalIgnoreCase) ||
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

        // Resolve the visitor groups API URL using the module path (avoids hardcoding)
        var groupsUrl = Paths.ToResource(typeof(EditorPowertoolsMenuProvider), "VisitorGroupTesterApi/GetGroups");

        // Buffer the response to inject our toolbar
        var originalBody = context.Response.Body;
        using var bufferStream = new MemoryStream();
        context.Response.Body = bufferStream;

        await _next(context);

        bufferStream.Seek(0, SeekOrigin.Begin);

        // If the response is compressed, don't try to read/modify it as text — just pass it through
        var encoding = context.Response.Headers["Content-Encoding"].ToString();
        if (!string.IsNullOrEmpty(encoding))
        {
            context.Response.Body = originalBody;
            bufferStream.Seek(0, SeekOrigin.Begin);
            await bufferStream.CopyToAsync(context.Response.Body);
            return;
        }

        var responseBody = await new StreamReader(bufferStream).ReadToEndAsync();

        // Only inject into HTML responses
        var contentType = context.Response.ContentType ?? "";
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            responseBody.Contains("</body>", StringComparison.OrdinalIgnoreCase))
        {
            // After _next(), Optimizely's routing pipeline has run and IContentRouteHelper
            // has the content reference for the current page — use it to build an edit URL.
            var pageEditUrl = ResolvePageEditUrl(context);
            var toolbar = GetToolbarHtml(pageEditUrl, groupsUrl);
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
    /// Returns true only for full edit mode (epieditmode=true or epieditmode=1).
    /// Preview mode (epieditmode=false) is allowed through.
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

    private static string GetToolbarHtml(string? pageEditUrl = null, string? groupsUrl = null)
    {
        var safeEditUrl = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(pageEditUrl ?? "");
        var safeGroupsUrl = System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(groupsUrl ?? "/episerver/EditorPowertools/VisitorGroupTesterApi/GetGroups");
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
    flex-direction: column;
    gap: 8px;
    flex-shrink: 0;
}
.ept-vgt-footer-actions {
    display: flex;
    gap: 8px;
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
.ept-vgt-edit-link {
    display: block;
    padding: 6px 12px;
    background: #1e3a5f;
    border-radius: 6px;
    color: #93c5fd;
    text-decoration: none;
    font-size: 11px;
    font-weight: 500;
    text-align: center;
}
.ept-vgt-edit-link:hover { background: #1e40af; color: #fff; }

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
</style>

<button class="ept-vgt-toggle" id="ept-vgt-toggle" title="Editor Powertools: Visitor Group Tester">&#9881;</button>

<div class="ept-vgt-panel" id="ept-vgt-panel">
    <div class="ept-vgt-header">
        <span class="ept-vgt-header-title">&#9881; Visitor Group Tester</span>
    </div>
    <div class="ept-vgt-body" id="ept-vgt-body">
        <div class="ept-vgt-loading">Loading visitor groups...</div>
    </div>
    <div class="ept-vgt-footer" id="ept-vgt-footer">
        <div class="ept-vgt-footer-actions">
            <button class="ept-vgt-btn ept-vgt-btn-secondary" id="ept-vgt-clear">Clear all</button>
            <button class="ept-vgt-btn ept-vgt-btn-primary" id="ept-vgt-apply">Apply</button>
        </div>
        ###EDIT_LINK###
    </div>
</div>

<script>
(function() {
    'use strict';
    var panel = document.getElementById('ept-vgt-panel');
    var toggle = document.getElementById('ept-vgt-toggle');
    var body = document.getElementById('ept-vgt-body');
    var groups = [];

    // Parse current visitor groups from query param or cookie
    function getActiveGroupIds() {
        var params = new URLSearchParams(window.location.search);
        var val = params.get('visitorgroupsByID');
        if (val) return val.split('|').filter(function(s) { return s.trim(); });
        var match = document.cookie.match(/ImpersonatedVisitorGroupsById=([^;]*)/);
        if (match && match[1]) return match[1].split('|').filter(function(s) { return s.trim(); });
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

    // Fetch visitor groups
    fetch('###GROUPS_URL###', { credentials: 'same-origin' })
        .then(function(r) { return r.ok ? r.json() : []; })
        .then(function(data) {
            groups = data || [];
            renderGroups('');
        })
        .catch(function() {
            groups = [];
            renderGroups('');
        });

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

        var searchInput = document.getElementById('ept-vgt-search');
        if (searchInput) {
            searchInput.addEventListener('input', function() { renderGroups(this.value); });
            if (filter) searchInput.focus();
        }

        body.querySelectorAll('input[type="checkbox"]').forEach(function(cb) {
            cb.addEventListener('change', function() {
                if (this.checked) activeGroupIds.add(this.value);
                else activeGroupIds.delete(this.value);
                var countEl = body.querySelector('.ept-vgt-count');
                var c = 0;
                activeGroupIds.forEach(function() { c++; });
                if (countEl) {
                    countEl.textContent = c + ' active';
                    countEl.parentElement.style.display = c > 0 ? '' : 'none';
                }
            });
        });
    }

    function setVgCookie(ids) {
        if (ids.length > 0) {
            document.cookie = 'ImpersonatedVisitorGroupsById=' + ids.join('|') + ';path=/';
        } else {
            document.cookie = 'ImpersonatedVisitorGroupsById=;path=/;expires=Thu, 01 Jan 1970 00:00:00 GMT';
        }
    }

    function buildUrlWithGroups(ids) {
        var href = window.location.href;
        href = href.replace(/([?&])visitorgroupsByID=[^&#]*/g, '$1');
        href = href.replace(/[?&]$/, '').replace(/[?]&/, '?').replace(/&&+/g, '&');
        if (ids.length > 0) {
            var sep = href.indexOf('?') >= 0 ? '&' : '?';
            return href + sep + 'visitorgroupsByID=' + ids.join('|');
        }
        return href;
    }

    function patchHref(href, ids) {
        if (!href) return href;
        if (/^https?:\/\//.test(href) && !href.startsWith(window.location.origin)) return href;
        if (/^(\/\/|mailto:|tel:|javascript:|#)/.test(href)) return href;
        if (/\/(episerver|editorpowertools)\//i.test(href)) return href;
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

    document.getElementById('ept-vgt-clear').addEventListener('click', function() {
        activeGroupIds.clear();
        setVgCookie([]);
        window.location.href = buildUrlWithGroups([]);
    });

    function escapeHtml(str) {
        var d = document.createElement('div');
        d.textContent = str;
        return d.innerHTML;
    }
})();
</script>
""".Replace("###GROUPS_URL###", safeGroupsUrl)
       .Replace("###EDIT_LINK###", string.IsNullOrEmpty(safeEditUrl)
            ? ""
            : $"<a href=\"{safeEditUrl}\" target=\"_blank\" class=\"ept-vgt-edit-link\">&#9998; Edit this page in CMS</a>");
    }
}
