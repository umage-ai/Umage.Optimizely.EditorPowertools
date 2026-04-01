define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "dojo/on",
    "dijit/_TemplatedMixin",
    "dijit/layout/_LayoutWidget",
    "epi/shell/_ContextMixin"
], function (
    declare, lang, when, on,
    _TemplatedMixin, _LayoutWidget,
    _ContextMixin
) {
    return declare("editorpowertools.ActiveEditorsWidget", [_LayoutWidget, _TemplatedMixin, _ContextMixin], {
        templateString: '<div class="ept-ae-root">' +
            '<div data-dojo-attach-point="containerNode" class="ept-ae-container">' +
            '<div class="ept-ae-empty">Connecting...</div>' +
            '</div>' +
            '</div>',

        _currentContentId: null,
        _eventHandler: null,

        postCreate: function () {
            this.inherited(arguments);
            var self = this;

            this._eventHandler = function (e) {
                self._onPresenceUpdate(e.detail);
            };
            document.addEventListener("ept-presence-update", this._eventHandler);

            when(this.getCurrentContext(), function (context) {
                self._onContextChanged(context);
            });

            if (window.__eptActiveEditors) {
                this._render(window.__eptActiveEditors);
            }
        },

        destroy: function () {
            if (this._eventHandler) {
                document.removeEventListener("ept-presence-update", this._eventHandler);
            }
            this.inherited(arguments);
        },

        contextChanged: function (context, callerData) {
            this.inherited(arguments);
            this._onContextChanged(context);
        },

        _onContextChanged: function (ctx) {
            if (!ctx || !ctx.id) return;
            var contentId = ctx.id;
            if (typeof contentId === "string") {
                contentId = parseInt(contentId.split("_")[0].replace(/[^0-9]/g, ""), 10);
            }
            this._currentContentId = contentId;
            this._render(window.__eptActiveEditors || []);
        },

        _onPresenceUpdate: function (data) {
            this._render(data.editors || []);
        },

        _render: function (allEditors) {
            var container = this.containerNode;
            if (!this._currentContentId) {
                container.innerHTML = '<div class="ept-ae-empty">Select content to see active editors.</div>';
                return;
            }

            var currentUser = (window.__eptCurrentUser || "").toLowerCase();
            var onContent = [];
            var otherOnline = [];

            for (var i = 0; i < allEditors.length; i++) {
                var e = allEditors[i];
                if (currentUser && e.username.toLowerCase() === currentUser) continue;
                if (e.contentId === this._currentContentId) {
                    onContent.push(e);
                } else {
                    otherOnline.push(e);
                }
            }

            var html = '';

            if (onContent.length > 0) {
                html += '<div class="ept-ae-section">';
                html += '<div class="ept-ae-section-title">On this content</div>';
                for (var j = 0; j < onContent.length; j++) {
                    html += this._renderEditor(onContent[j], true);
                }
                html += '</div>';
            }

            if (otherOnline.length > 0) {
                html += '<div class="ept-ae-section">';
                html += '<div class="ept-ae-section-title">Online now</div>';
                for (var k = 0; k < otherOnline.length; k++) {
                    html += this._renderEditor(otherOnline[k], false);
                }
                html += '</div>';
            }

            if (!html) {
                html = '<div class="ept-ae-empty">No other editors online.</div>';
            }

            container.innerHTML = html;
            this._bindNotifyButtons(container);
        },

        _renderEditor: function (editor, showAction) {
            var actionClass = "ept-ae-dot--" + (editor.action || "idle");
            var html = '<div class="ept-ae-editor">';
            html += '<span class="ept-ae-dot ' + actionClass + '"></span>';
            html += '<div class="ept-ae-editor-info">';
            html += '<span class="ept-ae-name">' + this._esc(editor.displayName) + '</span>';
            if (showAction) {
                html += ' <span class="ept-ae-action">' + this._esc(editor.action) + '</span>';
            }
            if (editor.contentName) {
                html += '<div class="ept-ae-content-detail">' + this._esc(editor.contentName) + '</div>';
            }
            html += '</div>';
            html += '<button class="ept-ae-notify-btn" data-username="' + this._esc(editor.username) + '" data-displayname="' + this._esc(editor.displayName) + '" title="Send notification">&#9993;</button>';
            html += '</div>';
            return html;
        },

        _bindNotifyButtons: function (container) {
            var self = this;
            var btns = container.querySelectorAll(".ept-ae-notify-btn");
            for (var i = 0; i < btns.length; i++) {
                (function (btn) {
                    btn.addEventListener("click", function () {
                        var username = btn.getAttribute("data-username");
                        var displayName = btn.getAttribute("data-displayname");
                        var message = prompt("Send notification to " + displayName + ":");
                        if (message && window.__eptHubConnection && window.__eptHubConnection.state === "Connected") {
                            window.__eptHubConnection.invoke("SendNotification", username, message).catch(function (err) {
                                alert("Failed to send: " + err.message);
                            });
                        }
                    });
                })(btns[i]);
            }
        },

        _esc: function (s) {
            if (!s) return "";
            var d = document.createElement("div");
            d.textContent = String(s);
            return d.innerHTML;
        },

        resize: function () {
            this.inherited(arguments);
        }
    });
});
