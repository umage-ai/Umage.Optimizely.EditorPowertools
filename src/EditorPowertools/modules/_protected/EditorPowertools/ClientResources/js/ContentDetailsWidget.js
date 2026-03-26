define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "dojo/dom-construct",
    "dijit/_WidgetBase",
    "dijit/_TemplatedMixin",
    "epi/dependency",
    "epi/shell/widget/_ValueRequiredMixin"
], function (
    declare, lang, when, domConstruct,
    _WidgetBase, _TemplatedMixin,
    dependency, _ValueRequiredMixin
) {
    return declare("editorpowertools.ContentDetailsWidget", [_WidgetBase, _TemplatedMixin, _ValueRequiredMixin], {
        templateString: '<div class="ept-content-details"><div data-dojo-attach-point="containerNode">Loading...</div></div>',

        // Called when the content context changes
        _setValueAttr: function (value) {
            this._set("value", value);
            if (value && value.contentLink) {
                this._loadDetails(value.contentLink);
            }
        },

        _loadDetails: function (contentLink) {
            var self = this;
            var container = this.containerNode;
            container.innerHTML = '<div style="text-align:center;padding:20px;color:#666"><div class="ept-cd-spinner"></div><div style="margin-top:8px">Loading...</div></div>';

            // Extract just the ID from contentLink (may be "5_103" format)
            var contentId = contentLink.toString().split('_')[0];

            fetch('/editorpowertools/api/content-details/' + contentId)
                .then(function (response) {
                    if (!response.ok) throw new Error('HTTP ' + response.status);
                    return response.json();
                })
                .then(function (data) {
                    self._renderDetails(data);
                })
                .catch(function (err) {
                    container.innerHTML = '<div style="padding:10px;color:#c00">Error loading details: ' + err.message + '</div>';
                });
        },

        _renderDetails: function (data) {
            var container = this.containerNode;
            var html = '';

            // Tabs
            html += '<div class="ept-cd-tabs">';
            html += '<button class="ept-cd-tab active" data-tab="info">Info</button>';
            html += '<button class="ept-cd-tab" data-tab="properties">Properties</button>';
            html += '<button class="ept-cd-tab" data-tab="references">References</button>';
            html += '<button class="ept-cd-tab" data-tab="versions">Versions</button>';
            html += '</div>';

            // Info tab
            html += '<div class="ept-cd-panel active" data-panel="info">';
            html += '<table class="ept-cd-info-table">';
            html += '<tr><td class="ept-cd-label">Name</td><td>' + this._esc(data.name) + '</td></tr>';
            html += '<tr><td class="ept-cd-label">Type</td><td>' + this._esc(data.contentTypeName) + '</td></tr>';
            html += '<tr><td class="ept-cd-label">ID</td><td>' + data.contentId + '</td></tr>';
            html += '<tr><td class="ept-cd-label">GUID</td><td class="ept-cd-guid">' + this._esc(data.contentGuid) + '</td></tr>';
            html += '<tr><td class="ept-cd-label">Status</td><td><span class="ept-cd-status ept-cd-status--' + this._statusClass(data.status) + '">' + this._esc(data.status) + '</span></td></tr>';
            html += '<tr><td class="ept-cd-label">Created</td><td>' + this._formatDate(data.created) + '<br><span class="ept-cd-by">by ' + this._esc(data.createdBy) + '</span></td></tr>';
            html += '<tr><td class="ept-cd-label">Changed</td><td>' + this._formatDate(data.changed) + '<br><span class="ept-cd-by">by ' + this._esc(data.changedBy) + '</span></td></tr>';
            if (data.published) {
                html += '<tr><td class="ept-cd-label">Published</td><td>' + this._formatDate(data.published) + '</td></tr>';
            }
            html += '<tr><td class="ept-cd-label">Language</td><td>' + this._esc(data.language || 'N/A') + '</td></tr>';
            html += '<tr><td class="ept-cd-label">Versions</td><td>' + (data.versionCount || 0) + '</td></tr>';
            if (data.parentName) {
                html += '<tr><td class="ept-cd-label">Parent</td><td>' + this._esc(data.parentName) + '</td></tr>';
            }
            html += '</table>';
            html += '</div>';

            // Properties tab
            html += '<div class="ept-cd-panel" data-panel="properties">';
            if (data.properties && data.properties.length > 0) {
                html += '<table class="ept-cd-info-table">';
                for (var i = 0; i < data.properties.length; i++) {
                    var prop = data.properties[i];
                    var val;
                    if (prop.isContentArea) {
                        val = '<span class="ept-cd-content-area">' + prop.itemCount + ' items</span>';
                    } else if (prop.value) {
                        val = this._esc(prop.value);
                    } else {
                        val = '<span class="ept-cd-empty">empty</span>';
                    }
                    html += '<tr><td class="ept-cd-label" title="' + this._esc(prop.name) + '">' + this._esc(prop.displayName) + '</td><td>' + val + '</td></tr>';
                }
                html += '</table>';
            } else {
                html += '<div class="ept-cd-empty-msg">No properties</div>';
            }
            html += '</div>';

            // References tab
            html += '<div class="ept-cd-panel" data-panel="references">';
            if (data.referencedBy && data.referencedBy.length > 0) {
                html += '<div class="ept-cd-ref-list">';
                for (var j = 0; j < data.referencedBy.length; j++) {
                    var ref = data.referencedBy[j];
                    html += '<div class="ept-cd-ref-item">';
                    html += '<strong>' + this._esc(ref.name) + '</strong>';
                    html += ' <span class="ept-cd-ref-type">' + this._esc(ref.contentTypeName) + '</span>';
                    if (ref.propertyName) {
                        html += ' <span class="ept-cd-ref-prop">via ' + this._esc(ref.propertyName) + '</span>';
                    }
                    html += '</div>';
                }
                html += '</div>';
            } else {
                html += '<div class="ept-cd-empty-msg">Not referenced by other content</div>';
            }
            html += '</div>';

            // Versions tab
            html += '<div class="ept-cd-panel" data-panel="versions">';
            if (data.versions && data.versions.length > 0) {
                for (var k = 0; k < data.versions.length; k++) {
                    var ver = data.versions[k];
                    html += '<div class="ept-cd-version">';
                    html += '<span class="ept-cd-status ept-cd-status--' + this._statusClass(ver.status) + '">' + this._esc(ver.status) + '</span>';
                    html += ' <span class="ept-cd-version-id">v' + ver.versionId + '</span>';
                    html += ' <span class="ept-cd-version-meta">' + this._formatDate(ver.saved) + ' by ' + this._esc(ver.savedBy) + '</span>';
                    html += '</div>';
                }
            } else {
                html += '<div class="ept-cd-empty-msg">No versions found</div>';
            }
            html += '</div>';

            container.innerHTML = html;

            // Bind tab clicks
            var tabs = container.querySelectorAll('.ept-cd-tab');
            var panels = container.querySelectorAll('.ept-cd-panel');
            for (var t = 0; t < tabs.length; t++) {
                tabs[t].addEventListener('click', function () {
                    for (var a = 0; a < tabs.length; a++) { tabs[a].classList.remove('active'); }
                    for (var b = 0; b < panels.length; b++) { panels[b].classList.remove('active'); }
                    this.classList.add('active');
                    container.querySelector('[data-panel="' + this.getAttribute('data-tab') + '"]').classList.add('active');
                });
            }
        },

        _esc: function (s) {
            if (!s) return '';
            var div = document.createElement('div');
            div.textContent = s;
            return div.innerHTML;
        },

        _formatDate: function (dateStr) {
            if (!dateStr) return 'N/A';
            var d = new Date(dateStr);
            if (isNaN(d.getTime())) return dateStr;
            return d.toLocaleDateString() + ' ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        },

        _statusClass: function (status) {
            if (!status) return 'default';
            var s = status.toLowerCase();
            if (s === 'published') return 'published';
            if (s === 'checkedout' || s === 'draft') return 'draft';
            if (s === 'previouslypublished') return 'previous';
            if (s === 'rejected') return 'rejected';
            if (s === 'delayedpublish') return 'delayed';
            return 'default';
        }
    });
});
