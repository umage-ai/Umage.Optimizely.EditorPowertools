define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/on",
    "dojo/topic",
    "dojo/dom-construct",
    "dojo/dom-class",
    "dijit/_TemplatedMixin",
    "dijit/layout/_LayoutWidget",
    "epi/dependency"
], function (
    declare, lang, on, topic, domConstruct, domClass,
    _TemplatedMixin, _LayoutWidget,
    dependency
) {
    return declare("editorpowertools.ContentDetailsWidget", [_LayoutWidget, _TemplatedMixin], {
        templateString: '<div class="ept-cd-root">' +
            '<div data-dojo-attach-point="containerNode" class="ept-cd-container">Select content to see details.</div>' +
            '</div>',

        _currentContentId: null,

        postCreate: function () {
            this.inherited(arguments);
            this._initContextListener();
        },

        _initContextListener: function () {
            var self = this;

            // The CMS shell sets currentContext on asset panel components automatically.
            // Also subscribe to the context change topic as a fallback.
            topic.subscribe("/epi/shell/context/changed", function (sender, ctx) {
                self._onContextChanged(ctx || sender);
            });
        },

        _onContextChanged: function (ctx) {
            if (!ctx || !ctx.id) return;

            // Extract content ID from context
            var contentId = ctx.id;
            if (typeof contentId === "string") {
                // Remove version info and provider prefix
                contentId = contentId.split("_")[0].replace(/[^0-9]/g, "");
            }
            if (!contentId || contentId === this._currentContentId) return;

            this._currentContentId = contentId;
            this._loadDetails(contentId);
        },

        // Also handle the component model being set by the framework
        _setCurrentContextAttr: function (value) {
            if (value && value.id) {
                this._onContextChanged(value);
            }
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
            html += '<div class="ept-cd-tabs">';
            html += '<button class="ept-cd-tab ept-cd-tab--active" data-tab="info">Info</button>';
            html += '<button class="ept-cd-tab" data-tab="props">Props</button>';
            html += '<button class="ept-cd-tab" data-tab="refs">Refs</button>';
            html += '<button class="ept-cd-tab" data-tab="vers">Versions</button>';
            html += '</div>';

            // Info panel
            html += '<div class="ept-cd-panel ept-cd-panel--active" data-panel="info">';
            html += this._row("Name", data.name);
            html += this._row("Type", data.contentTypeName);
            html += this._row("ID", data.contentId);
            html += this._row("Status", data.status);
            html += this._row("Created", this._fmtDate(data.created) + (data.createdBy ? " by " + this._esc(data.createdBy) : ""));
            html += this._row("Changed", this._fmtDate(data.changed) + (data.changedBy ? " by " + this._esc(data.changedBy) : ""));
            if (data.published) html += this._row("Published", this._fmtDate(data.published));
            html += this._row("Language", data.language || "N/A");
            html += this._row("Versions", data.versionCount || 0);
            if (data.parentName) html += this._row("Parent", data.parentName);
            html += this._row("GUID", '<span style="font-size:10px;word-break:break-all">' + this._esc(data.contentGuid) + '</span>');
            html += '</div>';

            // Properties panel
            html += '<div class="ept-cd-panel" data-panel="props">';
            if (data.properties && data.properties.length > 0) {
                for (var i = 0; i < data.properties.length; i++) {
                    var p = data.properties[i];
                    var val = p.value || '<span class="ept-cd-empty">empty</span>';
                    if (p.isContentArea) val = '<span class="ept-cd-ca">' + p.itemCount + ' items</span>';
                    html += this._row(p.displayName, val);
                }
            } else {
                html += '<div class="ept-cd-empty">No properties</div>';
            }
            html += '</div>';

            // References panel
            html += '<div class="ept-cd-panel" data-panel="refs">';
            if (data.referencedBy && data.referencedBy.length > 0) {
                for (var j = 0; j < data.referencedBy.length; j++) {
                    var ref = data.referencedBy[j];
                    html += '<div class="ept-cd-ref">';
                    html += '<strong>' + this._esc(ref.name) + '</strong>';
                    html += ' <span class="ept-cd-muted">' + this._esc(ref.contentTypeName) + '</span>';
                    if (ref.propertyName) html += ' <span class="ept-cd-muted">via ' + this._esc(ref.propertyName) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">Not referenced</div>';
            }
            html += '</div>';

            // Versions panel
            html += '<div class="ept-cd-panel" data-panel="vers">';
            if (data.versions && data.versions.length > 0) {
                for (var k = 0; k < data.versions.length; k++) {
                    var v = data.versions[k];
                    var sc = v.status === "Published" ? "color:#16a34a" : (v.status === "Draft" ? "color:#3b82f6" : "color:#666");
                    html += '<div class="ept-cd-ver">';
                    html += '<span style="' + sc + ';font-weight:600">' + this._esc(v.status) + '</span>';
                    html += ' v' + v.versionId;
                    html += ' <span class="ept-cd-muted">' + this._fmtDate(v.saved) + ' ' + this._esc(v.savedBy || "") + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty">No versions</div>';
            }
            html += '</div>';

            container.innerHTML = html;

            // Bind tabs
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
