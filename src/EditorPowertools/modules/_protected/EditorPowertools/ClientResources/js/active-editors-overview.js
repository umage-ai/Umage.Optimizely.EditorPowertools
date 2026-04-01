/**
 * Active Editors Overview - Full-page tool with editor list + chat
 */
(function () {
    var connection = null;
    var editors = [];
    var editorsToday = [];
    var chatMessages = [];
    var chatEnabled = false;

    function init() {
        EPT.showLoading(document.getElementById('ae-editors-panel'));

        EPT.fetchJson('/editorpowertools/api/features').then(function (features) {
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
                '<div class="ept-empty"><p>SignalR client not available.</p></div>';
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/editorpowertools/hubs/active-editors')
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
                '<div class="ept-empty"><p>Could not connect: ' + escHtml(err.message) + '</p></div>';
        });
    }

    function renderStats() {
        var el = document.getElementById('ae-stats');
        var onlineCount = editors.length;
        var todayCount = Math.max(editorsToday.length, onlineCount);
        var editingCount = editors.filter(function (e) { return e.action === 'editing'; }).length;

        el.innerHTML =
            '<div class="ept-stat"><div class="ept-stat__value">' + onlineCount + '</div><div class="ept-stat__label">Online Now</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + editingCount + '</div><div class="ept-stat__label">Currently Editing</div></div>' +
            '<div class="ept-stat"><div class="ept-stat__value">' + todayCount + '</div><div class="ept-stat__label">Active Today</div></div>';
    }

    function renderEditors() {
        var panel = document.getElementById('ae-editors-panel');

        if (editors.length === 0) {
            panel.innerHTML = '<div class="ept-empty"><p>No editors currently online.</p></div>';
            return;
        }

        var html = '<div class="ae-editor-list">';
        html += '<h3>Online Now</h3>';

        for (var i = 0; i < editors.length; i++) {
            var e = editors[i];
            var dotClass = 'ae-dot--' + (e.action || 'idle');
            html += '<div class="ae-editor-card">';
            html += '<div class="ae-editor-header">';
            html += '<span class="ae-dot ' + dotClass + '"></span>';
            html += '<strong>' + escHtml(e.displayName) + '</strong>';
            html += '<span class="ae-action-badge ae-action-badge--' + (e.action || 'idle') + '">' + escHtml(e.action || 'idle') + '</span>';
            html += '</div>';
            if (e.contentName) {
                html += '<div class="ae-editor-detail">';
                html += 'Working on: <strong>' + escHtml(e.contentName) + '</strong>';
                if (e.contentId) html += ' <span class="ae-content-id">(ID: ' + e.contentId + ')</span>';
                html += '</div>';
            }
            var connTime = formatRelativeTime(e.connectedAt);
            html += '<div class="ae-editor-meta">Connected ' + connTime + '</div>';
            html += '<div class="ae-editor-actions">';
            html += '<button class="ept-btn ept-btn--sm ae-notify-btn" data-username="' + escHtml(e.username) + '" data-displayname="' + escHtml(e.displayName) + '">Send Notification</button>';
            html += '</div>';
            html += '</div>';
        }
        html += '</div>';

        // Editors today (offline ones)
        var onlineNames = {};
        editors.forEach(function (e) { onlineNames[e.username.toLowerCase()] = true; });
        var offlineToday = editorsToday.filter(function (name) { return !onlineNames[name.toLowerCase()]; });

        if (offlineToday.length > 0) {
            html += '<div class="ae-editor-list ae-editor-list--today">';
            html += '<h3>Also Active Today</h3>';
            for (var j = 0; j < offlineToday.length; j++) {
                html += '<div class="ae-editor-card ae-editor-card--offline">';
                html += '<span class="ae-dot ae-dot--offline"></span>';
                html += '<strong>' + escHtml(offlineToday[j]) + '</strong>';
                html += '<span class="ae-action-badge ae-action-badge--offline">offline</span>';
                html += '</div>';
            }
            html += '</div>';
        }

        panel.innerHTML = html;

        // Bind notification buttons
        var notifyBtns = panel.querySelectorAll('.ae-notify-btn');
        for (var n = 0; n < notifyBtns.length; n++) {
            (function (btn) {
                btn.addEventListener('click', function () {
                    var username = btn.getAttribute('data-username');
                    var displayName = btn.getAttribute('data-displayname');
                    var message = prompt('Send notification to ' + displayName + ':');
                    if (message && connection) {
                        connection.invoke('SendNotification', username, message).catch(function (err) {
                            alert('Failed to send: ' + err.message);
                        });
                    }
                });
            })(notifyBtns[n]);
        }
    }

    function renderChatPanel() {
        var panel = document.getElementById('ae-chat-panel');
        if (!chatEnabled) {
            panel.style.display = 'none';
            return;
        }

        panel.innerHTML =
            '<div class="ae-chat">' +
                '<h3>Team Chat</h3>' +
                '<div class="ae-chat-messages" id="ae-chat-messages"></div>' +
                '<div class="ae-chat-input">' +
                    '<input type="text" id="ae-chat-text" class="ept-input" placeholder="Type a message..." maxlength="500" />' +
                    '<button id="ae-chat-send" class="ept-btn ept-btn--primary">Send</button>' +
                '</div>' +
            '</div>';

        document.getElementById('ae-chat-send').addEventListener('click', sendChat);
        document.getElementById('ae-chat-text').addEventListener('keydown', function (e) {
            if (e.key === 'Enter') sendChat();
        });

        renderChat();
    }

    function renderChat() {
        var el = document.getElementById('ae-chat-messages');
        if (!el) return;

        var html = '';
        for (var i = 0; i < chatMessages.length; i++) {
            var msg = chatMessages[i];
            var time = new Date(msg.timestampUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
            html += '<div class="ae-chat-msg">';
            html += '<span class="ae-chat-user">' + escHtml(msg.displayName) + '</span>';
            html += '<span class="ae-chat-time">' + time + '</span>';
            html += '<div class="ae-chat-text">' + escHtml(msg.text) + '</div>';
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
    }

    function formatRelativeTime(utcStr) {
        var date = new Date(utcStr);
        var now = new Date();
        var diffMin = Math.floor((now - date) / 60000);
        if (diffMin < 1) return 'just now';
        if (diffMin < 60) return diffMin + 'm ago';
        var diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return diffHour + 'h ago';
        return date.toLocaleDateString();
    }

    function escHtml(str) {
        if (!str) return '';
        var div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function injectStyles() {
        var style = document.createElement('style');
        style.textContent =
            '.ae-layout { display: grid; grid-template-columns: 1fr 1fr; gap: 24px; }' +
            '@media (max-width: 900px) { .ae-layout { grid-template-columns: 1fr; } }' +

            '.ae-editor-list h3 { font-size: 14px; font-weight: 600; margin: 0 0 12px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; }' +

            '.ae-editor-card { display: flex; flex-direction: column; gap: 4px; padding: 12px 16px; background: #fff; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; margin-bottom: 8px; }' +
            '.ae-editor-card--offline { opacity: 0.6; }' +

            '.ae-editor-header { display: flex; align-items: center; gap: 8px; }' +
            '.ae-editor-detail { font-size: 13px; color: var(--ept-text, #333); padding-left: 20px; }' +
            '.ae-editor-meta { font-size: 11px; color: var(--ept-text-muted, #999); padding-left: 20px; }' +
            '.ae-editor-actions { padding-left: 20px; margin-top: 4px; }' +
            '.ae-content-id { font-size: 11px; color: var(--ept-text-muted, #999); }' +

            '.ae-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }' +
            '.ae-dot--editing { background: #22c55e; box-shadow: 0 0 6px rgba(34,197,94,0.4); }' +
            '.ae-dot--viewing { background: #3b82f6; }' +
            '.ae-dot--idle { background: #9ca3af; }' +
            '.ae-dot--offline { background: #d1d5db; }' +

            '.ae-action-badge { font-size: 11px; padding: 1px 8px; border-radius: 10px; font-weight: 500; margin-left: auto; }' +
            '.ae-action-badge--editing { background: #dcfce7; color: #166534; }' +
            '.ae-action-badge--viewing { background: #dbeafe; color: #1e40af; }' +
            '.ae-action-badge--idle { background: #f3f4f6; color: #6b7280; }' +
            '.ae-action-badge--offline { background: #f3f4f6; color: #9ca3af; }' +

            '.ae-chat { display: flex; flex-direction: column; height: 100%; }' +
            '.ae-chat h3 { font-size: 14px; font-weight: 600; margin: 0 0 12px; color: var(--ept-text-muted, #666); text-transform: uppercase; letter-spacing: 0.5px; }' +
            '.ae-chat-messages { flex: 1; min-height: 300px; max-height: 500px; overflow-y: auto; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 8px; padding: 12px; margin-bottom: 12px; background: #fafafa; }' +
            '.ae-chat-msg { margin-bottom: 10px; }' +
            '.ae-chat-user { font-weight: 600; font-size: 13px; margin-right: 6px; }' +
            '.ae-chat-time { font-size: 11px; color: var(--ept-text-muted, #999); }' +
            '.ae-chat-text { font-size: 13px; margin-top: 2px; color: var(--ept-text, #333); }' +
            '.ae-chat-input { display: flex; gap: 8px; }' +
            '.ae-chat-input .ept-input { flex: 1; }' +

            '.ept-toast { position: fixed; bottom: 20px; right: 20px; background: #1e293b; color: #fff; padding: 12px 20px; border-radius: 8px; font-size: 13px; z-index: 99999; opacity: 0; transform: translateY(10px); transition: opacity 0.3s, transform 0.3s; box-shadow: 0 4px 12px rgba(0,0,0,0.2); }' +
            '.ept-toast--visible { opacity: 1; transform: translateY(0); }' +

            '.ept-ae-root { font-size: 12px; }' +
            '.ept-ae-container { padding: 8px; }' +
            '.ept-ae-empty { color: var(--ept-text-muted, #999); font-style: italic; padding: 12px 0; }' +
            '.ept-ae-section { margin-bottom: 12px; }' +
            '.ept-ae-section-title { font-size: 10px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; color: var(--ept-text-muted, #999); margin-bottom: 6px; }' +
            '.ept-ae-editor { display: flex; align-items: center; gap: 6px; padding: 4px 0; }' +
            '.ept-ae-dot { width: 8px; height: 8px; border-radius: 50%; flex-shrink: 0; }' +
            '.ept-ae-dot--editing { background: #22c55e; }' +
            '.ept-ae-dot--viewing { background: #3b82f6; }' +
            '.ept-ae-dot--idle { background: #9ca3af; }' +
            '.ept-ae-name { font-weight: 600; }' +
            '.ept-ae-action { font-size: 10px; color: var(--ept-text-muted, #999); }' +
            '.ept-ae-content { font-size: 10px; color: var(--ept-text-muted, #999); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 120px; }' +
            '.ept-ae-editor-info { flex: 1; min-width: 0; }' +
            '.ept-ae-content-detail { font-size: 10px; color: var(--ept-text-muted, #999); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }' +
            '.ept-ae-notify-btn { background: none; border: 1px solid var(--ept-border, #e0e0e0); border-radius: 4px; cursor: pointer; font-size: 14px; padding: 2px 6px; opacity: 0.5; transition: opacity 0.2s; }' +
            '.ept-ae-notify-btn:hover { opacity: 1; background: #f0f0f0; }';

        document.head.appendChild(style);
    }

    injectStyles();
    init();
})();
