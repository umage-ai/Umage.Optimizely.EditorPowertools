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
        _presenceHandler: null,
        _chatHandler: null,
        _activeTab: "editors",
        _chatMessages: [],
        _unreadCount: 0,

        postCreate: function () {
            this.inherited(arguments);
            this._injectStyles();
            var self = this;

            this._presenceHandler = function (e) {
                self._onPresenceUpdate(e.detail);
            };
            this._chatHandler = function (e) {
                self._chatMessages.push(e.detail);
                if (self._activeTab !== "chat") {
                    self._unreadCount++;
                    self._updateTabBadge();
                }
                if (self._activeTab === "chat") {
                    self._renderChat();
                }
            };
            document.addEventListener("ept-presence-update", this._presenceHandler);
            document.addEventListener("ept-chat-message", this._chatHandler);

            // Load chat history if hub is connected
            this._loadChatHistory();

            when(this.getCurrentContext(), function (context) {
                self._onContextChanged(context);
            });

            if (window.__eptActiveEditors) {
                this._renderFull(window.__eptActiveEditors);
            }
        },

        destroy: function () {
            if (this._presenceHandler) document.removeEventListener("ept-presence-update", this._presenceHandler);
            if (this._chatHandler) document.removeEventListener("ept-chat-message", this._chatHandler);
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
            this._renderFull(window.__eptActiveEditors || []);
        },

        _onPresenceUpdate: function (data) {
            if (this._activeTab === "editors") {
                this._renderFull(data.editors || []);
            }
        },

        _loadChatHistory: function () {
            var self = this;
            var conn = window.__eptHubConnection;
            if (conn && conn.state === "Connected") {
                conn.invoke("GetChatHistory").then(function (messages) {
                    self._chatMessages = messages || [];
                }).catch(function () {});
            } else {
                // Retry after tracker connects
                setTimeout(function () { self._loadChatHistory(); }, 2000);
            }
        },

        // ── Full render with tabs ──────────────────────────────────
        _renderFull: function (allEditors) {
            var container = this.containerNode;
            var editorCount = allEditors ? allEditors.length : 0;
            var currentUser = (window.__eptCurrentUser || "").toLowerCase();
            if (currentUser) editorCount = allEditors.filter(function (e) { return e.username.toLowerCase() !== currentUser; }).length;

            var html = '<div class="ept-ae-tabs">';
            html += '<button class="ept-ae-tab' + (this._activeTab === "editors" ? ' ept-ae-tab--active' : '') + '" data-tab="editors">Editors';
            if (editorCount > 0) html += ' <span class="ept-ae-badge">' + editorCount + '</span>';
            html += '</button>';
            html += '<button class="ept-ae-tab' + (this._activeTab === "chat" ? ' ept-ae-tab--active' : '') + '" data-tab="chat">Chat';
            if (this._unreadCount > 0) html += ' <span class="ept-ae-badge ept-ae-badge--chat">' + this._unreadCount + '</span>';
            html += '</button>';
            html += '</div>';

            html += '<div class="ept-ae-tab-content" id="ept-ae-editors-tab"' + (this._activeTab !== "editors" ? ' style="display:none"' : '') + '></div>';
            html += '<div class="ept-ae-tab-content" id="ept-ae-chat-tab"' + (this._activeTab !== "chat" ? ' style="display:none"' : '') + '></div>';

            container.innerHTML = html;
            this._bindTabs(container);
            this._renderEditors(allEditors);
            this._renderChat();
        },

        _bindTabs: function (container) {
            var self = this;
            var tabs = container.querySelectorAll(".ept-ae-tab");
            for (var i = 0; i < tabs.length; i++) {
                (function (tab) {
                    tab.addEventListener("click", function () {
                        self._activeTab = tab.getAttribute("data-tab");
                        if (self._activeTab === "chat") {
                            self._unreadCount = 0;
                        }
                        self._renderFull(window.__eptActiveEditors || []);
                    });
                })(tabs[i]);
            }
        },

        _updateTabBadge: function () {
            var chatTab = this.containerNode.querySelector('.ept-ae-tab[data-tab="chat"]');
            if (!chatTab) return;
            var badge = chatTab.querySelector(".ept-ae-badge--chat");
            if (this._unreadCount > 0) {
                if (badge) {
                    badge.textContent = this._unreadCount;
                } else {
                    chatTab.insertAdjacentHTML("beforeend", ' <span class="ept-ae-badge ept-ae-badge--chat">' + this._unreadCount + '</span>');
                }
            } else if (badge) {
                badge.remove();
            }
        },

        // ── Editors tab ────────────────────────────────────────────
        _renderEditors: function (allEditors) {
            var panel = document.getElementById("ept-ae-editors-tab");
            if (!panel) return;

            if (!this._currentContentId) {
                panel.innerHTML = '<div class="ept-ae-empty">Select content to see active editors.</div>';
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

            panel.innerHTML = html;
            this._bindNotifyButtons(panel);
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
                        self._showNotifyDialog(username, displayName);
                    });
                })(btns[i]);
            }
        },

        _showNotifyDialog: function (username, displayName) {
            var self = this;
            var conn = window.__eptHubConnection;
            if (!conn || conn.state !== "Connected") return;

            var dialog = EPT.openDialog("Message " + displayName, { wide: false });
            dialog.body.innerHTML =
                '<div style="padding:4px 0">' +
                    '<textarea id="ept-ae-msg" rows="3" placeholder="Type your message..." maxlength="500" style="width:100%;border:1px solid #e0e0e0;border-radius:8px;padding:10px;font-size:13px;font-family:inherit;resize:vertical;outline:none;box-sizing:border-box"></textarea>' +
                    '<div style="display:flex;justify-content:flex-end;gap:8px;margin-top:10px">' +
                        '<button id="ept-ae-cancel" class="ept-btn">Cancel</button>' +
                        '<button id="ept-ae-send" class="ept-btn ept-btn--primary">Send</button>' +
                    '</div>' +
                '</div>';

            document.getElementById("ept-ae-msg").focus();
            document.getElementById("ept-ae-cancel").addEventListener("click", function () { dialog.close(); });
            document.getElementById("ept-ae-send").addEventListener("click", function () {
                var text = document.getElementById("ept-ae-msg").value.trim();
                if (!text) return;
                conn.invoke("SendNotification", username, text).then(function () {
                    dialog.body.innerHTML = '<p style="text-align:center;color:#166534;padding:16px 0">Message sent!</p>';
                    setTimeout(function () { dialog.close(); }, 1200);
                }).catch(function (err) {
                    dialog.body.innerHTML = '<p style="text-align:center;color:#ef4444;padding:16px 0">Error: ' + self._esc(err.message) + '</p>';
                });
            });
        },

        // ── Chat tab ───────────────────────────────────────────────
        _renderChat: function () {
            var panel = document.getElementById("ept-ae-chat-tab");
            if (!panel) return;

            var currentUser = (window.__eptCurrentUser || "").toLowerCase();
            var msgs = this._chatMessages;

            var html = '<div class="ept-ae-chat-msgs" id="ept-ae-chat-scroll">';
            if (msgs.length === 0) {
                html += '<div class="ept-ae-empty" style="text-align:center;padding:20px 0">No messages yet</div>';
            } else {
                var lastUser = '';
                for (var i = 0; i < msgs.length; i++) {
                    var msg = msgs[i];
                    var isSelf = currentUser && msg.username && msg.username.toLowerCase() === currentUser;
                    var showHeader = msg.username !== lastUser;
                    lastUser = msg.username;
                    var time = new Date(msg.timestampUtc).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

                    html += '<div class="ept-ae-chat-msg' + (isSelf ? ' ept-ae-chat-msg--self' : '') + '">';
                    if (showHeader) {
                        html += '<div class="ept-ae-chat-header">';
                        html += '<span class="ept-ae-chat-user">' + this._esc(msg.displayName) + '</span>';
                        html += '<span class="ept-ae-chat-time">' + time + '</span>';
                        html += '</div>';
                    }
                    html += '<div class="ept-ae-chat-bubble">' + this._esc(msg.text) + '</div>';
                    html += '</div>';
                }
            }
            html += '</div>';

            html += '<div class="ept-ae-chat-input">';
            html += '<input type="text" id="ept-ae-chat-text" class="ept-ae-chat-textbox" placeholder="Message..." maxlength="500" />';
            html += '<button id="ept-ae-chat-send" class="ept-ae-chat-send" title="Send">&#9654;</button>';
            html += '</div>';

            panel.innerHTML = html;

            // Scroll to bottom
            var scroll = document.getElementById("ept-ae-chat-scroll");
            if (scroll) scroll.scrollTop = scroll.scrollHeight;

            // Bind send
            var self = this;
            var input = document.getElementById("ept-ae-chat-text");
            var sendBtn = document.getElementById("ept-ae-chat-send");

            if (sendBtn) {
                sendBtn.addEventListener("click", function () { self._sendChat(); });
            }
            if (input) {
                input.addEventListener("keydown", function (ev) {
                    if (ev.key === "Enter" && !ev.shiftKey) {
                        ev.preventDefault();
                        self._sendChat();
                    }
                });
                input.focus();
            }
        },

        _sendChat: function () {
            var input = document.getElementById("ept-ae-chat-text");
            if (!input) return;
            var text = input.value.trim();
            if (!text) return;
            var conn = window.__eptHubConnection;
            if (!conn || conn.state !== "Connected") return;

            conn.invoke("SendChat", text).catch(function (err) {
                console.warn("[EditorPowertools] Chat send failed:", err);
            });
            input.value = "";
            input.focus();
        },

        // ── Styles ─────────────────────────────────────────────────
        _injectStyles: function () {
            if (document.getElementById("ept-ae-widget-styles")) return;
            var style = document.createElement("style");
            style.id = "ept-ae-widget-styles";
            style.textContent =
                '.ept-ae-root { font-size: 12px; line-height: 1.4; display: flex; flex-direction: column; height: 100%; }' +
                '.ept-ae-container { padding: 0; display: flex; flex-direction: column; height: 100%; }' +
                '.ept-ae-empty { color: #999; font-style: italic; padding: 8px 10px; font-size: 11px; }' +

                /* Tabs */
                '.ept-ae-tabs { display: flex; border-bottom: 1px solid #e0e0e0; flex-shrink: 0; }' +
                '.ept-ae-tab { flex: 1; padding: 8px 0; font-size: 11px; font-weight: 600; text-align: center; background: none; border: none; border-bottom: 2px solid transparent; cursor: pointer; color: #888; transition: all 0.15s; }' +
                '.ept-ae-tab:hover { color: #555; }' +
                '.ept-ae-tab--active { color: #3b82f6; border-bottom-color: #3b82f6; }' +
                '.ept-ae-badge { display: inline-block; min-width: 16px; padding: 0 5px; font-size: 10px; font-weight: 700; border-radius: 8px; background: #e5e7eb; color: #555; text-align: center; margin-left: 3px; }' +
                '.ept-ae-badge--chat { background: #3b82f6; color: #fff; }' +
                '.ept-ae-tab-content { flex: 1; overflow-y: auto; padding: 6px 10px; }' +

                /* Editor list */
                '.ept-ae-section { margin-bottom: 10px; }' +
                '.ept-ae-section-title { font-size: 10px; font-weight: 700; text-transform: uppercase; letter-spacing: 0.5px; color: #888; margin-bottom: 6px; padding-bottom: 3px; border-bottom: 1px solid #eee; }' +
                '.ept-ae-editor { display: flex; align-items: center; gap: 8px; padding: 5px 4px; border-radius: 6px; transition: background 0.15s; }' +
                '.ept-ae-editor:hover { background: #f5f7fa; }' +
                '.ept-ae-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }' +
                '.ept-ae-dot--editing { background: #22c55e; box-shadow: 0 0 5px rgba(34,197,94,0.4); }' +
                '.ept-ae-dot--viewing { background: #3b82f6; }' +
                '.ept-ae-dot--idle { background: #d1d5db; }' +
                '.ept-ae-editor-info { flex: 1; min-width: 0; }' +
                '.ept-ae-name { font-weight: 600; font-size: 12px; color: #333; }' +
                '.ept-ae-action { font-size: 10px; color: #999; margin-left: 4px; }' +
                '.ept-ae-content-detail { font-size: 10px; color: #888; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; margin-top: 1px; }' +
                '.ept-ae-notify-btn { background: none; border: 1px solid #ddd; border-radius: 4px; cursor: pointer; font-size: 12px; padding: 2px 6px; color: #666; opacity: 0; transition: opacity 0.15s; }' +
                '.ept-ae-editor:hover .ept-ae-notify-btn { opacity: 1; }' +
                '.ept-ae-notify-btn:hover { background: #f0f0f0; color: #333; }' +

                /* Chat */
                '#ept-ae-chat-tab { padding: 0; display: flex; flex-direction: column; }' +
                '.ept-ae-chat-msgs { flex: 1; overflow-y: auto; padding: 8px 10px; min-height: 120px; max-height: 300px; background: #fafbfc; }' +
                '.ept-ae-chat-msg { margin-bottom: 2px; }' +
                '.ept-ae-chat-msg--self { text-align: right; }' +
                '.ept-ae-chat-header { display: flex; align-items: baseline; gap: 6px; margin-top: 8px; margin-bottom: 1px; }' +
                '.ept-ae-chat-msg--self .ept-ae-chat-header { justify-content: flex-end; }' +
                '.ept-ae-chat-user { font-weight: 600; font-size: 10px; color: #555; }' +
                '.ept-ae-chat-time { font-size: 9px; color: #bbb; }' +
                '.ept-ae-chat-bubble { display: inline-block; padding: 5px 10px; border-radius: 12px; font-size: 12px; line-height: 1.35; max-width: 85%; background: #e8eaed; color: #333; word-break: break-word; }' +
                '.ept-ae-chat-msg--self .ept-ae-chat-bubble { background: #3b82f6; color: #fff; }' +
                '.ept-ae-chat-input { display: flex; border-top: 1px solid #e0e0e0; flex-shrink: 0; }' +
                '.ept-ae-chat-textbox { flex: 1; border: none; padding: 8px 10px; font-size: 12px; outline: none; background: #fff; min-width: 0; }' +
                '.ept-ae-chat-textbox::placeholder { color: #bbb; }' +
                '.ept-ae-chat-send { background: none; border: none; padding: 8px 10px; cursor: pointer; color: #3b82f6; font-size: 14px; }' +
                '.ept-ae-chat-send:hover { color: #1d4ed8; }';
            document.head.appendChild(style);
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
