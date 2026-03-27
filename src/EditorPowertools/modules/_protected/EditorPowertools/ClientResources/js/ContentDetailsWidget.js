define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "dojo/on",
    "dojo/dom-construct",
    "dojo/dom-class",
    "dijit/_TemplatedMixin",
    "dijit/layout/_LayoutWidget",
    "epi/shell/_ContextMixin"
], function (
    declare, lang, when, on, domConstruct, domClass,
    _TemplatedMixin, _LayoutWidget,
    _ContextMixin
) {
    return declare("editorpowertools.ContentDetailsWidget", [_LayoutWidget, _TemplatedMixin, _ContextMixin], {
        templateString: '<div class="ept-cd-root">' +
            '<div data-dojo-attach-point="containerNode" class="ept-cd-container">Select content to see details.</div>' +
            '</div>',

        _currentContentId: null,

        postCreate: function () {
            this.inherited(arguments);
            // Load initial context
            var self = this;
            when(this.getCurrentContext(), function (context) {
                self._onContextChanged(context);
            });
        },

        // Called by _ContextMixin when CMS context changes
        contextChanged: function (context, callerData) {
            this.inherited(arguments);
            this._onContextChanged(context);
        },

        _onContextChanged: function (ctx) {
            if (!ctx || !ctx.id) return;
            var contentId = ctx.id;
            if (typeof contentId === "string") {
                contentId = contentId.split("_")[0].replace(/[^0-9]/g, "");
            }
            if (!contentId || contentId === this._currentContentId) return;
            this._currentContentId = contentId;
            this._loadDetails(contentId);
        },

        _loadDetails: function (contentId) {
            var container = this.containerNode;
            container.innerHTML = '<div class="ept-cd-loading">Loading...</div>';
            var self = this;
            fetch("/editorpowertools/api/content-details/" + contentId)
                .then(function (r) {
                    if (!r.ok) throw new Error("HTTP " + r.status);
                    return r.json();
                })
                .then(function (data) {
                    self._data = data;
                    self._renderDetails(data);
                })
                .catch(function (err) {
                    container.innerHTML = '<div class="ept-cd-error">Error: ' + self._esc(err.message) + '</div>';
                });
        },

        _renderDetails: function (data) {
            var container = this.containerNode;
            var self = this;
            var html = '';

            // Tabs
            var usesCount = data.uses ? data.uses.length : 0;
            var usedByCount = data.usedBy ? data.usedBy.length : 0;
            var treeChildren = data.contentTree && data.contentTree.children ? data.contentTree.children.length : 0;
            var verCount = data.versions ? data.versions.length : 0;
            var persCount = data.personalizations ? data.personalizations.length : 0;
            var langCount = data.languageSync ? data.languageSync.length : 0;
            var langBehind = data.languageSync ? data.languageSync.filter(function(l) { return l.isBehindMaster; }).length : 0;

            html += '<div class="ept-cd-tabs">';
            html += '<button class="ept-cd-tab ept-cd-tab--active" data-tab="info">Info</button>';
            html += '<button class="ept-cd-tab" data-tab="uses">Uses <span class="ept-cd-count">(' + usesCount + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="usedby">Used By <span class="ept-cd-count">(' + usedByCount + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="tree">Tree <span class="ept-cd-count">(' + treeChildren + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="vers">Versions <span class="ept-cd-count">(' + verCount + ')</span></button>';
            if (persCount > 0) html += '<button class="ept-cd-tab" data-tab="pers">Pers. <span class="ept-cd-count">(' + persCount + ')</span></button>';
            if (langCount > 1) html += '<button class="ept-cd-tab" data-tab="lang">Lang' + (langBehind > 0 ? ' <span class="ept-cd-count ept-cd-warn">(' + langBehind + ' behind)</span>' : '') + '</button>';
            html += '</div>';

            // Info panel
            html += '<div class="ept-cd-panel ept-cd-panel--active" data-panel="info">';
            html += this._row("Name", data.name);
            html += this._row("Type", data.contentTypeName);
            html += this._row("ID", data.contentId);
            html += this._row("Status", this._statusBadge(data.status));
            html += this._row("Created", this._fmtDate(data.created) + (data.createdBy ? " by " + this._esc(data.createdBy) : ""));
            html += this._row("Changed", this._fmtDate(data.changed) + (data.changedBy ? " by " + this._esc(data.changedBy) : ""));
            if (data.published) html += this._row("Published", this._fmtDate(data.published));
            html += this._row("Language", data.language || "N/A");
            html += this._row("Versions", data.versionCount || 0);
            if (data.parentName) html += this._row("Parent", data.parentName);
            html += this._row("GUID", '<span style="font-size:10px;word-break:break-all">' + this._esc(data.contentGuid) + '</span>');
            html += '</div>';

            // Uses panel (what this content references)
            html += '<div class="ept-cd-panel" data-panel="uses">';
            if (data.uses && data.uses.length > 0) {
                for (var i = 0; i < data.uses.length; i++) {
                    var u = data.uses[i];
                    html += '<div class="ept-cd-ref">';
                    html += '<span class="ept-cd-ref-name">' + this._esc(u.name) + '</span>';
                    html += ' <span class="ept-cd-ref-type">' + this._esc(u.contentTypeName) + '</span>';
                    if (u.propertyName) html += ' <span class="ept-cd-ref-prop">via ' + this._esc(u.propertyName) + '</span>';
                    html += '<span class="ept-cd-ref-kind">' + this._esc(u.referenceType) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">This content does not reference other content.</div>';
            }
            html += '</div>';

            // Used By panel (what references this content)
            html += '<div class="ept-cd-panel" data-panel="usedby">';
            if (data.usedBy && data.usedBy.length > 0) {
                for (var j = 0; j < data.usedBy.length; j++) {
                    var ref = data.usedBy[j];
                    html += '<div class="ept-cd-ref">';
                    html += '<span class="ept-cd-ref-name">' + this._esc(ref.name) + '</span>';
                    html += ' <span class="ept-cd-ref-type">' + this._esc(ref.contentTypeName) + '</span>';
                    if (ref.propertyName) html += ' <span class="ept-cd-ref-prop">via ' + this._esc(ref.propertyName) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">Not referenced by other content.</div>';
            }
            html += '</div>';

            // Tree panel
            html += '<div class="ept-cd-panel" data-panel="tree">';
            if (data.contentTree && data.contentTree.children && data.contentTree.children.length > 0) {
                html += '<button class="ept-cd-expand" data-action="timeline">Timeline</button>';
                html += this._renderTreeNode(data.contentTree, true);
            } else {
                html += '<div class="ept-cd-empty">No nested content found.</div>';
            }
            html += '</div>';

            // Versions panel
            html += '<div class="ept-cd-panel" data-panel="vers">';
            if (data.versions && data.versions.length > 0) {
                html += '<button class="ept-cd-expand" data-action="timeline-versions">Full Timeline</button>';
                for (var k = 0; k < data.versions.length; k++) {
                    html += this._renderVersion(data.versions[k]);
                }
            } else {
                html += '<div class="ept-cd-empty">No versions found.</div>';
            }
            html += '</div>';

            // Personalizations panel
            if (persCount > 0) {
                html += '<div class="ept-cd-panel" data-panel="pers">';
                for (var p = 0; p < data.personalizations.length; p++) {
                    var pr = data.personalizations[p];
                    html += '<div class="ept-cd-ref">';
                    html += '<span class="ept-cd-ref-name">' + this._esc(pr.visitorGroupName) + '</span>';
                    html += ' <span class="ept-cd-ref-type">on ' + this._esc(pr.contentName) + '</span>';
                    html += ' <span class="ept-cd-ref-prop">' + this._esc(pr.propertyName) + '</span>';
                    html += '</div>';
                }
                html += '</div>';
            }

            // Language sync panel
            if (langCount > 1) {
                html += '<div class="ept-cd-panel" data-panel="lang">';
                for (var l = 0; l < data.languageSync.length; l++) {
                    var ls = data.languageSync[l];
                    html += '<div class="ept-cd-ref" style="display:flex;align-items:center;gap:6px">';
                    html += '<span class="ept-cd-ver-lang">' + this._esc(ls.language) + '</span>';
                    if (ls.isMaster) html += '<span class="ept-cd-ref-kind">master</span>';
                    html += '<span class="ept-cd-ver-status ept-cd-ver-status--' + (ls.status || '').toLowerCase() + '">' + this._esc(ls.status) + '</span>';
                    html += '<span class="ept-cd-muted">' + this._fmtDate(ls.lastChanged) + '</span>';
                    if (ls.isBehindMaster) html += '<span style="color:#ef6c00;font-size:10px;font-weight:600">behind master</span>';
                    html += '</div>';
                }
                html += '</div>';
            }

            container.innerHTML = html;
            this._bindTabs(container);
            this._bindExpand(container);
        },

        _renderTreeNode: function (node, isRoot) {
            var html = '<ul class="ept-cd-tree">';
            if (!isRoot) {
                html += '<li class="ept-cd-tree-item">';
                html += '<span class="ept-cd-tree-name">' + this._esc(node.name) + '</span>';
                html += '<span class="ept-cd-tree-type">' + this._esc(node.contentTypeName) + '</span>';
                if (node.propertyName) html += ' <span class="ept-cd-tree-prop">' + this._esc(node.propertyName) + '</span>';
            }
            if (node.children && node.children.length > 0) {
                for (var i = 0; i < node.children.length; i++) {
                    html += this._renderTreeNode(node.children[i], false);
                }
            }
            if (!isRoot) html += '</li>';
            html += '</ul>';
            return html;
        },

        _renderVersion: function (v) {
            var statusClass = "ept-cd-ver-status--" + (v.status || "").toLowerCase().replace(/\s/g, "");
            var html = '<div class="ept-cd-ver">';
            html += '<div class="ept-cd-ver-header">';
            html += '<span class="ept-cd-ver-status ' + statusClass + '">' + this._esc(v.status) + '</span>';
            html += ' <span class="ept-cd-muted">v' + v.versionId + '</span>';
            if (v.language) html += ' <span class="ept-cd-ver-lang">' + this._esc(v.language) + '</span>';
            html += ' <span class="ept-cd-muted">' + this._fmtDate(v.saved) + '</span>';
            if (v.savedBy) html += ' <span class="ept-cd-muted">' + this._esc(v.savedBy) + '</span>';
            if (v.compareUrl) html += ' <a class="ept-cd-ver-compare" href="' + this._esc(v.compareUrl) + '">compare</a>';
            html += '</div>';

            if (v.changedProperties && v.changedProperties.length > 0) {
                html += '<div class="ept-cd-ver-changes">';
                for (var c = 0; c < v.changedProperties.length; c++) {
                    var ch = v.changedProperties[c];
                    html += '<div class="ept-cd-ver-change">';
                    html += '<span class="ept-cd-ver-change-prop">' + this._esc(ch.propertyName) + '</span> changed';
                    html += '</div>';
                }
                html += '</div>';
            }

            html += '</div>';
            return html;
        },

        _statusBadge: function (status) {
            if (!status) return "N/A";
            var cls = "ept-cd-ver-status--" + status.toLowerCase().replace(/\s/g, "");
            return '<span class="ept-cd-ver-status ' + cls + '">' + this._esc(status) + '</span>';
        },

        _bindTabs: function (container) {
            var tabs = container.querySelectorAll(".ept-cd-tab");
            var panels = container.querySelectorAll(".ept-cd-panel");
            for (var t = 0; t < tabs.length; t++) {
                (function (tab) {
                    on(tab, "click", function () {
                        for (var x = 0; x < tabs.length; x++) {
                            domClass.remove(tabs[x], "ept-cd-tab--active");
                            domClass.remove(panels[x], "ept-cd-panel--active");
                        }
                        domClass.add(tab, "ept-cd-tab--active");
                        var panel = container.querySelector('[data-panel="' + tab.getAttribute("data-tab") + '"]');
                        if (panel) domClass.add(panel, "ept-cd-panel--active");
                    });
                })(tabs[t]);
            }
        },

        _bindExpand: function (container) {
            var self = this;
            var btns = container.querySelectorAll(".ept-cd-expand");
            for (var i = 0; i < btns.length; i++) {
                (function (btn) {
                    on(btn, "click", function () {
                        var action = btn.getAttribute("data-action");
                        if (action === "timeline" || action === "timeline-versions") {
                            self._showTimelinePopup();
                        }
                    });
                })(btns[i]);
            }
        },

        _showTimelinePopup: function () {
            var self = this;
            var data = this._data;
            if (!data) return;

            var overlay = document.createElement("div");
            overlay.className = "ept-cd-overlay";

            var html = '<div class="ept-cd-popup">';
            html += '<div class="ept-cd-popup-header">';
            html += '<span class="ept-cd-popup-title">Timeline: ' + this._esc(data.name) + '</span>';
            html += '<button class="ept-cd-popup-close">&times;</button>';
            html += '</div>';
            html += '<div class="ept-cd-popup-body">';

            // Version timeline
            if (data.versions && data.versions.length > 0) {
                html += '<div class="ept-cd-timeline">';
                for (var i = 0; i < data.versions.length; i++) {
                    var v = data.versions[i];
                    var dotClass = "ept-cd-tl-dot--default";
                    if (v.status === "Published") dotClass = "ept-cd-tl-dot--published";
                    else if (v.status === "Draft" || v.status === "CheckedOut") dotClass = "ept-cd-tl-dot--draft";
                    else if (v.status === "Rejected") dotClass = "ept-cd-tl-dot--rejected";

                    html += '<div class="ept-cd-tl-item">';
                    html += '<div class="ept-cd-tl-dot ' + dotClass + '"></div>';
                    html += '<div class="ept-cd-tl-header">';
                    html += '<span class="ept-cd-ver-status ept-cd-ver-status--' + (v.status || "").toLowerCase().replace(/\s/g, "") + '">' + this._esc(v.status) + '</span>';
                    html += ' v' + v.versionId;
                    if (v.language) html += ' <span class="ept-cd-ver-lang">' + this._esc(v.language) + '</span>';
                    html += '</div>';
                    html += '<div class="ept-cd-tl-meta">' + this._fmtDate(v.saved);
                    if (v.savedBy) html += ' by ' + this._esc(v.savedBy);
                    html += '</div>';

                    if (v.changedProperties && v.changedProperties.length > 0) {
                        html += '<div class="ept-cd-tl-changes">';
                        for (var c = 0; c < v.changedProperties.length; c++) {
                            var ch = v.changedProperties[c];
                            html += '<div class="ept-cd-tl-change">';
                            html += '<span class="ept-cd-tl-change-prop">' + this._esc(ch.propertyName) + '</span>';
                            if (ch.newValue) html += ' &rarr; ' + this._esc(ch.newValue);
                            html += '</div>';
                        }
                        html += '</div>';
                    }

                    if (v.compareUrl) {
                        html += '<a class="ept-cd-tl-compare" href="' + this._esc(v.compareUrl) + '">Compare with previous</a>';
                    }

                    html += '</div>';
                }
                html += '</div>';
            } else {
                html += '<div class="ept-cd-empty">No version history available.</div>';
            }

            // Content tree section
            if (data.contentTree && data.contentTree.children && data.contentTree.children.length > 0) {
                html += '<h3 style="margin:20px 0 10px;font-size:14px;font-weight:600;">Content Structure</h3>';
                html += this._renderTreeNode(data.contentTree, true);
            }

            html += '</div></div>';
            overlay.innerHTML = html;
            document.body.appendChild(overlay);

            // Close handlers
            var closeBtn = overlay.querySelector(".ept-cd-popup-close");
            on(closeBtn, "click", function () { document.body.removeChild(overlay); });
            on(overlay, "click", function (e) {
                if (e.target === overlay) document.body.removeChild(overlay);
            });
        },

        _row: function (label, value) {
            return '<div class="ept-cd-row"><span class="ept-cd-label">' + this._esc(label) + '</span><span class="ept-cd-value">' + value + '</span></div>';
        },

        _esc: function (s) {
            if (!s && s !== 0) return "";
            var d = document.createElement("div");
            d.textContent = String(s);
            return d.innerHTML;
        },

        _fmtDate: function (s) {
            if (!s) return "N/A";
            var d = new Date(s);
            if (isNaN(d.getTime())) return s;
            return d.toLocaleDateString() + " " + d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        },

        resize: function () {
            this.inherited(arguments);
        }
    });
});
