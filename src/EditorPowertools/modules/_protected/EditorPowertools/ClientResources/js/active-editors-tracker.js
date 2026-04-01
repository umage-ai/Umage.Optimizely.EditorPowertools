define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/when",
    "epi/shell/_ContextMixin"
], function (declare, lang, when, _ContextMixin) {

    var Tracker = declare([_ContextMixin], {
        _connection: null,
        _currentContentId: null,
        _heartbeatTimer: null,
        _chatEnabled: false,

        constructor: function (options) {
            this._chatEnabled = options && options.chatEnabled !== false;
        },

        start: function () {
            var self = this;

            if (typeof signalR === "undefined") {
                console.warn("[EditorPowertools] SignalR client not loaded, active editors tracking disabled.");
                return;
            }

            this._connection = new signalR.HubConnectionBuilder()
                .withUrl("/editorpowertools/hubs/active-editors")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();

            this._connection.on("PresenceUpdate", function (data) {
                window.__eptActiveEditors = data.editors || [];
                document.dispatchEvent(new CustomEvent("ept-presence-update", { detail: data }));
            });

            this._connection.on("EditorsOnContent", function (editors) {
                if (editors && editors.length > 0) {
                    var names = editors.map(function (e) { return e.displayName; }).join(", ");
                    self._showToast(names + (editors.length === 1 ? " is" : " are") + " also editing this content");
                }
            });

            this._connection.on("ChatMessage", function (msg) {
                document.dispatchEvent(new CustomEvent("ept-chat-message", { detail: msg }));
            });

            this._connection.start().then(function () {
                when(self.getCurrentContext(), function (context) {
                    self._sendContext(context);
                });

                self._heartbeatTimer = setInterval(function () {
                    if (self._connection.state === "Connected") {
                        self._connection.invoke("Heartbeat").catch(function () {});
                    }
                }, 30000);
            }).catch(function (err) {
                console.warn("[EditorPowertools] Could not connect to Active Editors hub:", err);
            });

            window.addEventListener("beforeunload", function () {
                if (self._heartbeatTimer) clearInterval(self._heartbeatTimer);
                if (self._connection) self._connection.stop();
            });

            window.__eptHubConnection = this._connection;
        },

        contextChanged: function (context, callerData) {
            this.inherited(arguments);
            this._sendContext(context);
        },

        _sendContext: function (ctx) {
            if (!ctx || !ctx.id || !this._connection || this._connection.state !== "Connected") return;

            var contentId = ctx.id;
            if (typeof contentId === "string") {
                contentId = parseInt(contentId.split("_")[0].replace(/[^0-9]/g, ""), 10);
            }
            if (!contentId || contentId === this._currentContentId) return;

            this._currentContentId = contentId;
            var contentName = ctx.name || "";
            var action = "editing";

            this._connection.invoke("UpdateContext", contentId, contentName, action).catch(function (err) {
                console.warn("[EditorPowertools] Failed to update context:", err);
            });
        },

        _showToast: function (message) {
            var toast = document.createElement("div");
            toast.className = "ept-toast";
            toast.textContent = message;
            document.body.appendChild(toast);
            requestAnimationFrame(function () {
                toast.classList.add("ept-toast--visible");
            });
            setTimeout(function () {
                toast.classList.remove("ept-toast--visible");
                setTimeout(function () { toast.remove(); }, 300);
            }, 5000);
        }
    });

    var instance = null;

    return {
        start: function (options) {
            if (!instance) {
                instance = new Tracker(options);
                instance.start();
            }
        }
    };
});
