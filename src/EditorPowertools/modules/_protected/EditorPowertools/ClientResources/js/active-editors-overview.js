/**
 * Active Editors Overview - Full-page tool with editor list + chat
 */
(function () {
    var connection = null;
    var editors = [];
    var editorsToday = [];
    var chatMessages = [];
    var chatEnabled = false;
    var currentUser = '';

    function init() {
        EPT.showLoading(document.getElementById('ae-editors-panel'));

        EPT.fetchJson(window.EPT_API_URL + '/features').then(function (features) {
            chatEnabled = features.activeEditorsChat !== false;
            startConnection();
        }).catch(function () {
            chatEnabled = false;
            startConnection();
        });
    }

    function startConnection() {
        if (typeof signalR === 'undefined') {
            document.getElementById('ae-editors-panel').innerHTML =
                '<div class="ept-empty"><p>' + EPT.s('activeeditors.error_nosignalr', 'SignalR client not available. Check browser console for errors.') + '</p></div>';
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl(window.EPT_HUB_URL + '/active-editors')
            .withAutomaticReconnect()
            .build();

        connection.on('PresenceUpdate', function (data) {
            editors = data.editors || [];
            renderEditors();
            renderStats();
        });

        connection.on('ChatMessage', function (msg) {
            chatMessages.push(msg);
            renderChat();
        });

        connection.on('CurrentUser', function (username) {
            currentUser = username;
        });

        connection.start().then(function () {
            connection.invoke('GetEditorsToday').then(function (names) {
                editorsToday = names || [];
                renderEditors();
            });

            if (chatEnabled) {
                connection.invoke('GetChatHistory').then(function (messages) {
                    chatMessages = messages || [];
                    renderChatPanel();
                });
            } else {
                renderChatPanel();
            }

            renderStats();
            renderEditors();
        }).catch(function (err) {
            document.getElementById('ae-editors-panel').innerHTML =
                '<div class="ept-empty"><p>' + EPT.s('activeeditors.error_connect', 'Could not connect: {0}').replace('{0}', escHtml(err.message||err)) + '</p></div>';
        });
    }

    function renderStats() {
        var el = document.getElementById('ae-stats');
        var onlineCount = editors.length;
        var todayCount = Math.max(editorsToday.length, onlineCount);
        var editingCount = editors.filter(function (e) { return e.action === 'editing'; }).length;

        el.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + onlineCount + '</div><div class="ept-stat__label">' + EPT.s('activeeditors.stat_online', 'Online Now') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + editingCount + '</div><div class="ept-stat__label">' + EPT.s('activeeditors.stat_editing', 'Currently Editing') + '</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + todayCount + '</div><div class="ept-stat__label">' + EPT.s('activeeditors.stat_today', 'Active Today') + '</div></div>';
    }

    function renderEditors() {
        var panel = document.getElementById('ae-editors-panel');

        if (editors.length === 0) {
            panel.innerHTML = '<div class="ept-empty"><p>' + EPT.s('activeeditors.empty_noeditors', 'No editors currently online.') + '</p></div>';
            return;
        }

        var html = '<div class="ae-editor-list">';
        html += '<h3>' + EPT.s('activeeditors.section_online', 'Online Now') + '</h3>';

        for (var i = 0; i < editors.length; i++) {
            var e = editors[i];
            var isSelf = currentUser && e.username.toLowerCase() === currentUser.toLowerCase();
            var dotClass = 'ae-dot--' + (e.action || 'idle');
            html += '<div class="ae-editor-card' + (isSelf ? ' ae-editor-card--self' : '') + '">';
            html += '<div class="ae-editor-header">';
            html += '<span class="ae-dot ' + dotClass + '"></span>';
            html += '<strong>' + escHtml(e.displayName) + '</strong>';
            if (isSelf) html += '<span class="ae-you-badge">' + EPT.s('activeeditors.badge_you', 'you') + '</span>';
            html += '<span class="ae-action-badge ae-action-badge--' + (e.action || 'idle') + '">' + escHtml(e.action || 'idle') + '</span>';
            html += '</div>';
            if (e.contentName) {
                html += '<div class="ae-editor-detail">';
                html += '<svg class="ae-page-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>';
                html += '<span>' + escHtml(e.contentName) + '</span>';
                html += '</div>';
            }
            var connTime = formatRelativeTime(e.connectedAt);
            html += '<div class="ae-editor-meta">' + EPT.s('activeeditors.lbl_connected', 'Connected {0}').replace('{0}', connTime) + '</div>';
            if (!isSelf) {
                html += '<div class="ae-editor-actions">';
                html += '<button class="ept-btn ept-btn--sm ae-notify-btn" data-username="' + escHtml(e.username) + '" data-displayname="' + escHtml(e.displayName) + '">';
                html += '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:14px;height:14px;vertical-align:-2px;margin-right:4px"><path d="M22 2L11 13"/><path d="M22 2L15 22L11 13L2 9L22 2Z"/></svg>';
                html += EPT.s('activeeditors.btn_sendmessage', 'Send Message') + '</button>';
                html += '</div>';
            }
            html += '</div>';
        }
        html += '</div>';

        // Offline today
        var onlineNames = {};
        editors.forEach(function (e) { onlineNames[e.username.toLowerCase()] = true; });
        var offlineToday = editorsToday.filter(function (name) { return !onlineNames[name.toLowerCase()]; });

        if (offlineToday.length > 0) {
            html += '<div class="ae-editor-list ae-editor-list--today">';
            html += '<h3>' + EPT.s('activeeditors.section_today', 'Also Active Today') + '</h3>';
            for (var j = 0; j < offlineToday.length; j++) {
                html += '<div class="ae-editor-card ae-editor-card--offline">';
                html += '<span class="ae-dot ae-dot--offline"></span>';
                html += '<strong>' + escHtml(offlineToday[j]) + '</strong>';
                html += '<span class="ae-action-badge ae-action-badge--offline">' + EPT.s('activeeditors.badge_offline', 'offline') + '</span>';
                html += '</div>';
            }
            html += '</div>';
        }

        panel.innerHTML = html;
        bindNotifyButtons(panel);
    }

    function bindNotifyButtons(container) {
        var btns = container.querySelectorAll('.ae-notify-btn');
        for (var i = 0; i < btns.length; i++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    var username = btn.getAttribute('data-username');
                    var displayName = btn.getAttribute('data-displayname');
                    showNotifyDialog(username, displayName);
                });
            })(btns[i]);
        }
    }

    function showNotifyDialog(username, displayName) {
        var dialog = EPT.openDialog(EPT.s('activeeditors.dlg_sendmessage', 'Send Message to {0}').replace('{0}', displayName), { wide: false });
        dialog.body.innerHTML =
            '<div class="ae-notify-form">' +
                '<p class="ae-notify-desc">' + EPT.s('activeeditors.dlg_desc', 'This will send a CMS notification that {0} will see in their notification bell.').replace('{0}', escHtml(displayName)) + '</p>' +
                '<textarea id="ae-notify-msg" class="ae-notify-textarea" rows="4" placeholder="' + EPT.s('activeeditors.dlg_placeholder', 'Type your message...') + '" maxlength="500"></textarea>' +
                '<div class="ae-notify-actions">' +
                    '<button id="ae-notify-cancel" class="ept-btn">' + EPT.s('activeeditors.btn_cancel', 'Cancel') + '</button>' +
                    '<button id="ae-notify-send" class="ept-btn ept-btn--primary">' + EPT.s('activeeditors.btn_send', 'Send Notification') + '</button>' +
                '</div>' +
            '</div>';

        var textarea = document.getElementById('ae-notify-msg');
        textarea.focus();

        document.getElementById('ae-notify-cancel').addEventListener('click', function () {
            dialog.close();
        });

        document.getElementById('ae-notify-send').addEventListener('click', function () {
            var text = textarea.value.trim();
            if (!text) { textarea.focus(); return; }
            if (!connection) { dialog.close(); return; }

            var sendBtn = document.getElementById('ae-notify-send');
            sendBtn.disabled = true;
            sendBtn.textContent = EPT.s('activeeditors.btn_sending', 'Sending...');

            connection.invoke('SendNotification', username, text).then(function () {
                dialog.body.innerHTML =
                    '<div class="ae-notify-success">' +
                        '<svg viewBox="0 0 24 24" fill="none" stroke="#22c55e" stroke-width="2" style="width:48px;height:48px"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg>' +
                        '<p>' + EPT.s('activeeditors.msg_sent', 'Message sent to {0}').replace('{0}', escHtml(displayName)) + '</p>' +
                    '</div>';
                setTimeout(function () { dialog.close(); }, 1500);
            }).catch(function (err) {
                sendBtn.disabled = false;
                sendBtn.textContent = EPT.s('activeeditors.btn_send', 'Send Notification');
                dialog.body.querySelector('.ae-notify-desc').textContent = 'Error: ' + err.message;
                dialog.body.querySelector('.ae-notify-desc').style.color = '#ef4444';
            });
        });

        textarea.addEventListener('keydown', function (ev) {
            if (ev.key === 'Enter' && (ev.ctrlKey || ev.metaKey)) {
                document.getElementById('ae-notify-send').click();
            }
        });
    }

    // ── Chat ───────────────────────────────────────────────────────
    function renderChatPanel() {
        var panel = document.getElementById('ae-chat-panel');
        if (!chatEnabled) {
            panel.style.display = 'none';
            return;
        }

        panel.innerHTML =
            '<div class="ae-chat">' +
                '<h3>' + EPT.s('activeeditors.chat_title', 'Team Chat') + '</h3>' +
                '<div class="ae-chat-messages" id="ae-chat-messages">' +
                    '<div class="ae-chat-empty">' + EPT.s('activeeditors.chat_empty', 'No messages yet. Say hello!') + '</div>' +
                '</div>' +
                '<div class="ae-chat-input">' +
                    '<input type="text" id="ae-chat-text" class="ae-chat-textbox" placeholder="' + EPT.s('activeeditors.chat_placeholder', 'Type a message... (Enter to send)') + '" maxlength="500" />' +
                    '<button id="ae-chat-send" class="ae-chat-send-btn" title="Send">' +
                        '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style="width:18px;height:18px"><path d="M22 2L11 13"/><path d="M22 2L15 22L11 13L2 9L22 2Z"/></svg>' +
                    '</button>' +
                '</div>' +
            '</div>';

        document.getElementById('ae-chat-send').addEventListener('click', sendChat);
        document.getElementById('ae-chat-text').addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault();
                sendChat();
            }
        });

        renderChat();
    }

    function renderChat() {
        var el = document.getElementById('ae-chat-messages');
        if (!el) return;

        if (chatMessages.length === 0) {
            el.innerHTML = '<div class="ae-chat-empty">' + EPT.s('activeeditors.chat_empty', 'No messages yet. Say hello!') + '</div>';
            return;
        }

        var html = '';
        var lastUser = '';
        for (var i = 0; i < chatMessages.length; i++) {
            var msg = chatMessages[i];
            var isSelf = currentUser && msg.username.toLowerCase() === currentUser.toLowerCase();
            var time = new Date(msg.timestampUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            var showHeader = msg.username !== lastUser;
            lastUser = msg.username;

            html += '<div class="ae-chat-msg' + (isSelf ? ' ae-chat-msg--self' : '') + '">';
            if (showHeader) {
                html += '<div class="ae-chat-msg-header">';
                html += '<span class="ae-chat-user">' + escHtml(msg.displayName) + '</span>';
                html += '<span class="ae-chat-time">' + time + '</span>';
                html += '</div>';
            }
            html += '<div class="ae-chat-bubble">' + escHtml(msg.text) + '</div>';
            html += '</div>';
        }
        el.innerHTML = html;
        el.scrollTop = el.scrollHeight;
    }

    function sendChat() {
        var input = document.getElementById('ae-chat-text');
        var text = input.value.trim();
        if (!text || !connection) return;

        connection.invoke('SendChat', text).catch(function (err) {
            console.warn('Failed to send chat:', err);
        });
        input.value = '';
        input.focus();
    }

    // ── Helpers ────────────────────────────────────────────────────
    function formatRelativeTime(utcStr) {
        var date = new Date(utcStr);
        var now = new Date();
        var diffMin = Math.floor((now - date) / 60000);
        if (diffMin < 1) return EPT.s('activeeditors.time_justnow', 'just now');
        if (diffMin < 60) return EPT.s('activeeditors.time_mago', '{0}m ago').replace('{0}', diffMin);
        var diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return EPT.s('activeeditors.time_hago', '{0}h ago').replace('{0}', diffHour);
        return date.toLocaleDateString();
    }

    function escHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    // ── Styles ─────────────────────────────────────────────────────
    function injectStyles() {
        var style = document.createElement('style');
        style.textContent =
            '.ae-layout { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; min-height: 500px; }' +
            '@media (max-width: 900px) { .ae-layout { grid-template-columns: 1fr; } }' +

            '.ae-editor-list h3 { font-size: 13px; font-weight: 600; margin: 0 0 12px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; }' +

            '.ae-editor-card { display: flex; flex-direction: column; gap: 4px; padding: 14px 18px; background: #fff; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 10px; margin-bottom: 8px; transition: box-shadow 0.15s; }' +
            '.ae-editor-card:hover { box-shadow: 0 2px 8px rgba(0,0,0,0.06); }' +
            '.ae-editor-card--self { border-left: 3px solid #3b82f6; background: #f8faff; }' +
            '.ae-editor-card--offline { opacity: 0.5; }' +

            '.ae-editor-header { display: flex; align-items: center; gap: 8px; }' +
            '.ae-you-badge { font-size: 10px; padding: 1px 6px; border-radius: 8px; background: #dbeafe; color: #1e40af; font-weight: 600; }' +
            '.ae-editor-detail { display: flex; align-items: center; gap: 6px; font-size: 13px; color: var(--ept-text, #333); padding-left: 18px; }' +
            '.ae-page-icon { width: 14px; height: 14px; flex-shrink: 0; color: var(--ept-text-muted, #999); }' +
            '.ae-editor-meta { font-size: 11px; color: var(--ept-text-muted, #999); padding-left: 18px; }' +
            '.ae-editor-actions { padding-left: 18px; margin-top: 4px; }' +

            '.ae-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }' +
            '.ae-dot--editing { background: #22c55e; box-shadow: 0 0 6px rgba(34,197,94,0.4); animation: ae-pulse 2s infinite; }' +
            '.ae-dot--viewing { background: #3b82f6; }' +
            '.ae-dot--idle { background: #9ca3af; }' +
            '.ae-dot--offline { background: #d1d5db; }' +
            '@keyframes ae-pulse { 0%,100% { box-shadow: 0 0 6px rgba(34,197,94,0.4); } 50% { box-shadow: 0 0 12px rgba(34,197,94,0.6); } }' +

            '.ae-action-badge { font-size: 11px; padding: 2px 10px; border-radius: 10px; font-weight: 500; margin-left: auto; }' +
            '.ae-action-badge--editing { background: #dcfce7; color: #166534; }' +
            '.ae-action-badge--viewing { background: #dbeafe; color: #1e40af; }' +
            '.ae-action-badge--idle { background: #f3f4f6; color: #6b7280; }' +
            '.ae-action-badge--offline { background: #f3f4f6; color: #9ca3af; }' +

            /* Chat - modern messaging look */
            '.ae-chat { display: flex; flex-direction: column; background: #fff; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 10px; overflow: hidden; }' +
            '.ae-chat h3 { font-size: 13px; font-weight: 600; margin: 0; padding: 14px 18px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; border-bottom: 1px solid var(--ept-border, #e0e0e0); }' +
            '.ae-chat-messages { flex: 1; min-height: 350px; max-height: 500px; overflow-y: auto; padding: 16px; background: #f8f9fb; }' +
            '.ae-chat-empty { text-align: center; color: var(--ept-text-muted, #bbb); font-style: italic; padding: 40px 0; font-size: 13px; }' +
            '.ae-chat-msg { margin-bottom: 4px; }' +
            '.ae-chat-msg-header { display: flex; align-items: baseline; gap: 8px; margin-top: 12px; margin-bottom: 2px; }' +
            '.ae-chat-user { font-weight: 600; font-size: 12px; color: var(--ept-text, #333); }' +
            '.ae-chat-time { font-size: 10px; color: var(--ept-text-muted, #aaa); }' +
            '.ae-chat-bubble { display: inline-block; padding: 8px 14px; border-radius: 16px; font-size: 13px; line-height: 1.4; max-width: 85%; background: #e8eaed; color: var(--ept-text, #333); }' +
            '.ae-chat-msg--self .ae-chat-bubble { background: #3b82f6; color: #fff; }' +
            '.ae-chat-msg--self .ae-chat-msg-header { text-align: right; justify-content: flex-end; }' +
            '.ae-chat-msg--self { text-align: right; }' +

            '.ae-chat-input { display: flex; gap: 0; border-top: 1px solid var(--ept-border, #e0e0e0); }' +
            '.ae-chat-textbox { flex: 1; border: none; padding: 14px 18px; font-size: 13px; outline: none; background: #fff; }' +
            '.ae-chat-textbox::placeholder { color: #bbb; }' +
            '.ae-chat-send-btn { background: none; border: none; padding: 14px 18px; cursor: pointer; color: #3b82f6; transition: color 0.15s; }' +
            '.ae-chat-send-btn:hover { color: #1d4ed8; }' +

            /* Notification dialog */
            '.ae-notify-form { padding: 8px 0; }' +
            '.ae-notify-desc { font-size: 13px; color: var(--ept-text-muted, #666); margin: 0 0 12px; }' +
            '.ae-notify-textarea { width: 100%; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; padding: 12px; font-size: 13px; font-family: inherit; resize: vertical; outline: none; transition: border-color 0.15s; box-sizing: border-box; }' +
            '.ae-notify-textarea:focus { border-color: #3b82f6; }' +
            '.ae-notify-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 12px; }' +
            '.ae-notify-success { text-align: center; padding: 24px 0; }' +
            '.ae-notify-success p { font-size: 14px; color: #166534; margin-top: 12px; }' +

            /* Toast */
            '.ept-toast { position: fixed; bottom: 20px; right: 20px; background: #1e293b; color: #fff; padding: 12px 20px; border-radius: 10px; font-size: 13px; z-index: 99999; opacity: 0; transform: translateY(10px); transition: opacity 0.3s, transform 0.3s; box-shadow: 0 4px 16px rgba(0,0,0,0.25); }' +
            '.ept-toast--visible { opacity: 1; transform: translateY(0); }';

        document.head.appendChild(style);
    }

    injectStyles();
    init();
})();
