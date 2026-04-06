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
    function ensureStrings(callback) {
        if (window.EPT_STRINGS) {
            callback();
            return;
        }
        var base = window.EPT_BASE_URL || '';
        fetch(base + 'EditorPowertools/WidgetStrings', { credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (data) {
                if (data) window.EPT_STRINGS = data;
                callback();
            })
            .catch(function () { callback(); });
    }

    return declare("editorpowertools.ContentDetailsWidget", [_LayoutWidget, _TemplatedMixin, _ContextMixin], {
        templateString: '<div class="ept-cd-root">' +
            '<div data-dojo-attach-point="containerNode" class="ept-cd-container"></div>' +
            '<button data-dojo-attach-point="helpBtn" class="ept-help-btn ept-cd-help-btn" title="Help">?</button>' +
            '</div>',

        _currentContentId: null,

        postCreate: function () {
            this.inherited(arguments);
            var self = this;
            ensureStrings(function () {
                self.containerNode.innerHTML = '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_selectcontent', 'Select content to see details.') + '</div>';
                // Load initial context
                when(self.getCurrentContext(), function (context) {
                    self._onContextChanged(context);
                });
                // Set help button title from strings
                if (self.helpBtn) {
                    self.helpBtn.title = EPT.s('help.helpbtn', 'Help');
                    self.helpBtn.addEventListener('click', function(e) {
                        e.stopPropagation();
                        var existing = self.domNode.querySelector('.ept-help-popover');
                        if (existing) { existing.remove(); return; }
                        var popover = document.createElement('div');
                        popover.className = 'ept-help-popover';
                        popover.textContent = EPT.s('help.contentdetails', '');
                        self.domNode.style.position = 'relative';
                        self.domNode.appendChild(popover);
                        var close = function(ev) {
                            if (!popover.contains(ev.target) && ev.target !== self.helpBtn) {
                                popover.remove();
                                document.removeEventListener('click', close);
                            }
                        };
                        setTimeout(function() { document.addEventListener('click', close); }, 0);
                    });
                }
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
            container.innerHTML = '<div class="ept-cd-loading">' + EPT.s('shared.loading', 'Loading...') + '</div>';
            var self = this;
            fetch(window.EPT_API_URL + "/content-details/" + contentId)
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
            var treeChildren = data.contentTree && data.contentTree.properties ? data.contentTree.properties.length : 0;
            var verCount = data.versions ? data.versions.length : 0;
            var persCount = data.personalizations ? data.personalizations.length : 0;
            var langCount = data.languageSync ? data.languageSync.length : 0;
            var langBehind = data.languageSync ? data.languageSync.filter(function(l) { return l.isBehindMaster; }).length : 0;

            html += '<div class="ept-cd-tabs">';
            html += '<button class="ept-cd-tab ept-cd-tab--active" data-tab="info">' + EPT.s('contentdetails.tab_info', 'Info') + '</button>';
            html += '<button class="ept-cd-tab" data-tab="uses">' + EPT.s('contentdetails.tab_uses', 'Uses') + ' <span class="ept-cd-count">(' + usesCount + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="usedby">' + EPT.s('contentdetails.tab_usedby', 'Used By') + ' <span class="ept-cd-count">(' + usedByCount + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="tree">' + EPT.s('contentdetails.tab_tree', 'Tree') + ' <span class="ept-cd-count">(' + treeChildren + ')</span></button>';
            html += '<button class="ept-cd-tab" data-tab="vers">' + EPT.s('contentdetails.tab_versions', 'Versions') + ' <span class="ept-cd-count">(' + verCount + ')</span></button>';
            if (persCount > 0) html += '<button class="ept-cd-tab" data-tab="pers">' + EPT.s('contentdetails.tab_pers', 'Pers.') + ' <span class="ept-cd-count">(' + persCount + ')</span></button>';
            if (langCount > 1) html += '<button class="ept-cd-tab" data-tab="lang">' + EPT.s('contentdetails.tab_lang', 'Lang') + (langBehind > 0 ? ' <span class="ept-cd-count ept-cd-warn">(' + langBehind + ' ' + EPT.s('contentdetails.lbl_behind', 'behind') + ')</span>' : '') + '</button>';
            html += '</div>';

            // Info panel
            html += '<div class="ept-cd-panel ept-cd-panel--active" data-panel="info">';
            html += this._row(EPT.s('contentdetails.lbl_name', 'Name'), data.name);
            html += this._row(EPT.s('contentdetails.lbl_type', 'Type'), data.contentTypeName);
            html += this._row(EPT.s('contentdetails.lbl_id', 'ID'), data.contentId);
            html += this._row(EPT.s('contentdetails.lbl_status', 'Status'), this._statusBadge(data.status));
            html += this._row(EPT.s('contentdetails.lbl_created', 'Created'), this._fmtDate(data.created) + (data.createdBy ? " " + EPT.s('shared.lbl_by', 'by') + " " + this._esc(data.createdBy) : ""));
            html += this._row(EPT.s('contentdetails.lbl_changed', 'Changed'), this._fmtDate(data.changed) + (data.changedBy ? " " + EPT.s('shared.lbl_by', 'by') + " " + this._esc(data.changedBy) : ""));
            if (data.published) html += this._row(EPT.s('contentdetails.lbl_published', 'Published'), this._fmtDate(data.published));
            html += this._row(EPT.s('contentdetails.lbl_language', 'Language'), data.language || EPT.s('shared.lbl_na', 'N/A'));
            html += this._row(EPT.s('contentdetails.lbl_versions', 'Versions'), data.versionCount || 0);
            if (data.parentName) html += this._row(EPT.s('contentdetails.lbl_parent', 'Parent'), data.parentName);
            html += this._row(EPT.s('contentdetails.lbl_guid', 'GUID'), '<span style="font-size:10px;word-break:break-all">' + this._esc(data.contentGuid) + '</span>');
            html += '</div>';

            // Uses panel (what this content references)
            html += '<div class="ept-cd-panel" data-panel="uses">';
            if (data.uses && data.uses.length > 0) {
                for (var i = 0; i < data.uses.length; i++) {
                    var u = data.uses[i];
                    html += '<div class="ept-cd-ref">';
                    html += '<a class="ept-cd-ref-name ept-cd-link" href="' + this._editUrl(u.contentId) + '">' + this._esc(u.name) + '</a>';
                    html += ' <span class="ept-cd-ref-type">' + this._esc(u.contentTypeName) + '</span>';
                    if (u.propertyName) html += ' <span class="ept-cd-ref-prop">via ' + this._esc(u.propertyName) + '</span>';
                    html += '<span class="ept-cd-ref-kind">' + this._esc(u.referenceType) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_uses', 'This content does not reference other content.') + '</div>';
            }
            html += '</div>';

            // Used By panel (what references this content)
            html += '<div class="ept-cd-panel" data-panel="usedby">';
            if (data.usedBy && data.usedBy.length > 0) {
                for (var j = 0; j < data.usedBy.length; j++) {
                    var ref = data.usedBy[j];
                    html += '<div class="ept-cd-ref">';
                    html += '<a class="ept-cd-ref-name ept-cd-link" href="' + this._editUrl(ref.contentId) + '">' + this._esc(ref.name) + '</a>';
                    html += ' <span class="ept-cd-ref-type">' + this._esc(ref.contentTypeName) + '</span>';
                    if (ref.propertyName) html += ' <span class="ept-cd-ref-prop">via ' + this._esc(ref.propertyName) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_usedby', 'No content references this item.') + '</div>';
            }
            html += '</div>';

            // Tree panel (property-grouped, scrollable)
            html += '<div class="ept-cd-panel ept-cd-panel--scroll" data-panel="tree">';
            if (data.contentTree && data.contentTree.properties && data.contentTree.properties.length > 0) {
                html += this._renderTreeNode(data.contentTree, true);
            } else {
                html += '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_tree', 'No nested content found.') + '</div>';
            }
            html += '</div>';

            // Versions panel
            html += '<div class="ept-cd-panel" data-panel="vers">';
            if (data.versions && data.versions.length > 0) {
                html += '<button class="ept-cd-expand" data-action="timeline-versions">' + EPT.s('contentdetails.btn_fulltimeline', 'Full Timeline') + '</button>';
                for (var k = 0; k < data.versions.length; k++) {
                    html += this._renderVersion(data.versions[k]);
                }
            } else {
                html += '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_versions', 'No versions found.') + '</div>';
            }
            html += '</div>';

            // Personalizations panel
            if (persCount > 0) {
                html += '<div class="ept-cd-panel" data-panel="pers">';
                for (var p = 0; p < data.personalizations.length; p++) {
                    var pr = data.personalizations[p];
                    html += '<div class="ept-cd-ref">';
                    html += '<span class="ept-cd-ref-name">' + this._esc(pr.visitorGroupName) + '</span>';
                    html += ' <span class="ept-cd-ref-type">' + EPT.s('contentdetails.lbl_on', 'on') + ' ' + this._esc(pr.contentName) + '</span>';
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
                    if (ls.isMaster) html += '<span class="ept-cd-ref-kind">' + EPT.s('contentdetails.lbl_master', 'master') + '</span>';
                    html += '<span class="ept-cd-ver-status ept-cd-ver-status--' + (ls.status || '').toLowerCase() + '">' + this._esc(ls.status) + '</span>';
                    html += '<span class="ept-cd-muted">' + this._fmtDate(ls.lastChanged) + '</span>';
                    if (ls.isBehindMaster) html += '<span style="color:#ef6c00;font-size:10px;font-weight:600">' + EPT.s('contentdetails.lbl_behindmaster', 'behind master') + '</span>';
                    html += '</div>';
                }
                html += '</div>';
            }

            container.innerHTML = html;
            this._bindTabs(container);
            this._bindExpand(container);
            this._bindTreeToggles(container);
        },

        _renderTreeNode: function (node, isRoot) {
            var hasProps = node.properties && node.properties.length > 0;
            var html = '<ul class="ept-cd-tree">';
            html += '<li class="ept-cd-tree-item">';

            // Content node with toggle
            if (hasProps) {
                html += '<button class="ept-cd-tree-toggle" data-expanded="true">&#9660;</button>';
            }
            if (isRoot) {
                html += '<span class="ept-cd-tree-name">' + this._esc(node.name) + '</span>';
            } else {
                html += '<a class="ept-cd-tree-name ept-cd-link" href="' + this._editUrl(node.contentId) + '">' + this._esc(node.name) + '</a>';
            }
            html += ' <span class="ept-cd-tree-type">' + this._esc(node.contentTypeName) + '</span>';

            // Property nodes under this content
            if (hasProps) {
                html += '<ul class="ept-cd-tree ept-cd-tree-children">';
                for (var p = 0; p < node.properties.length; p++) {
                    var prop = node.properties[p];
                    var hasChildren = prop.children && prop.children.length > 0;
                    html += '<li class="ept-cd-tree-item">';
                    if (hasChildren) {
                        html += '<button class="ept-cd-tree-toggle" data-expanded="true">&#9660;</button>';
                    }
                    html += '<span class="ept-cd-tree-prop">' + this._esc(prop.propertyName) + '</span>';
                    html += ' <span class="ept-cd-ref-kind">' + this._esc(prop.propertyType) + '</span>';
                    if (hasChildren) {
                        html += '<div class="ept-cd-tree-children">';
                        for (var c = 0; c < prop.children.length; c++) {
                            html += this._renderTreeNode(prop.children[c], false);
                        }
                        html += '</div>';
                    }
                    html += '</li>';
                }
                html += '</ul>';
            }
            html += '</li>';
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
            html += '</div>';
            html += '<div style="font-size:10px;color:#555;margin-top:2px">';
            if (v.savedBy) html += '<strong>' + this._esc(v.savedBy) + '</strong> &middot; ';
            html += this._fmtDate(v.saved);
            if (v.compareUrl) html += ' &middot; <a class="ept-cd-ver-compare" href="' + this._esc(v.compareUrl) + '">' + EPT.s('contentdetails.lbl_compare', 'compare') + '</a>';
            html += '</div>';

            if (v.changedProperties && v.changedProperties.length > 0) {
                html += '<div class="ept-cd-ver-changes">';
                for (var c = 0; c < v.changedProperties.length; c++) {
                    var ch = v.changedProperties[c];
                    html += '<div class="ept-cd-ver-change">';
                    html += '<span class="ept-cd-ver-change-prop">' + this._esc(ch.propertyName) + '</span> ' + EPT.s('contentdetails.lbl_changed', 'changed');
                    html += '</div>';
                }
                html += '</div>';
            }

            html += '</div>';
            return html;
        },

        _statusBadge: function (status) {
            if (!status) return EPT.s('shared.lbl_na', 'N/A');
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

        _bindTreeToggles: function (container) {
            var toggles = container.querySelectorAll(".ept-cd-tree-toggle");
            for (var i = 0; i < toggles.length; i++) {
                (function (btn) {
                    on(btn, "click", function (e) {
                        e.stopPropagation();
                        var expanded = btn.getAttribute("data-expanded") === "true";
                        var sibling = btn.parentNode.querySelector(".ept-cd-tree-children");
                        if (sibling) {
                            sibling.style.display = expanded ? "none" : "";
                            btn.setAttribute("data-expanded", expanded ? "false" : "true");
                            btn.innerHTML = expanded ? "&#9654;" : "&#9660;";
                        }
                    });
                })(toggles[i]);
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
            html += '<span class="ept-cd-popup-title">' + EPT.s('contentdetails.lbl_timeline', 'Timeline') + ': ' + this._esc(data.name) + '</span>';
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
                    if (v.savedBy) html += ' ' + EPT.s('shared.lbl_by', 'by') + ' ' + this._esc(v.savedBy);
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
                        html += '<a class="ept-cd-tl-compare" href="' + this._esc(v.compareUrl) + '">' + EPT.s('contentdetails.lbl_comparewithprevious', 'Compare with previous') + '</a>';
                    }

                    html += '</div>';
                }
                html += '</div>';
            } else {
                html += '<div class="ept-cd-empty">' + EPT.s('contentdetails.empty_history', 'No version history available.') + '</div>';
            }

            // Content tree section
            if (data.contentTree && data.contentTree.properties && data.contentTree.properties.length > 0) {
                html += '<h3 style="margin:20px 0 10px;font-size:14px;font-weight:600;">' + EPT.s('contentdetails.lbl_contentstructure', 'Content Structure') + '</h3>';
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
            if (!s) return EPT.s('shared.lbl_na', 'N/A');
            var d = new Date(s);
            if (isNaN(d.getTime())) return s;
            return d.toLocaleDateString() + " " + d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        },

        _editUrl: function (contentId) {
            // Build edit URL relative to the current CMS shell path
            var shellPath = location.pathname.replace(/\/[^\/]*$/, "/");
            return shellPath + "#context=epi.cms.contentdata:///" + contentId;
        },

        resize: function () {
            this.inherited(arguments);
        }
    });
});
