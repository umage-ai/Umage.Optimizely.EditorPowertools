define([
    "dojo/_base/declare",
    "dojo/dom-construct",
    "dojo/on",
    "epi/shell/command/_Command"
], function (
    declare,
    domConstruct,
    on,
    _Command
) {
    return declare([_Command], {
        label: "Manage Child Items",
        iconClass: "epi-iconTree",
        category: "context",
        canExecute: false,
        isAvailable: false,

        _onModelChange: function () {
            if (!this.model) {
                this.set("canExecute", false);
                this.set("isAvailable", false);
                return;
            }

            var available = !this.model.ownerContentLink && this.model.hasChildren;
            this.set("canExecute", !!available);
            this.set("isAvailable", !!available);
        },

        _execute: function () {
            if (!this.model || !this.model.contentLink) return;

            var contentId = String(this.model.contentLink).split("_")[0];
            var contentName = this.model.name || "Content";
            var self = this;

            // Load children and show management dialog inline
            fetch(window.EPT_API_URL + "/manage-children/" + contentId)
                .then(function (r) { return r.json(); })
                .then(function (items) {
                    self._showDialog(contentId, contentName, items);
                })
                .catch(function (err) {
                    alert("Failed to load children: " + err.message);
                });
        },

        _showDialog: function (parentId, parentName, items) {
            var self = this;

            // Create overlay
            var overlay = document.createElement("div");
            overlay.style.cssText = "position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:10000;display:flex;align-items:center;justify-content:center";

            var dialog = document.createElement("div");
            dialog.style.cssText = "background:#fff;border-radius:8px;box-shadow:0 10px 40px rgba(0,0,0,.25);width:90vw;max-width:900px;max-height:85vh;display:flex;flex-direction:column;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;font-size:13px";

            // Header
            var header = document.createElement("div");
            header.style.cssText = "padding:16px 20px;border-bottom:1px solid #e0e0e0;display:flex;align-items:center;justify-content:space-between";
            header.innerHTML = '<div style="font-size:16px;font-weight:600">Manage Children of ' + self._esc(parentName) + '</div>' +
                '<button style="background:none;border:none;font-size:22px;cursor:pointer;color:#999;padding:0 4px" id="ept-mc-close">&times;</button>';
            dialog.appendChild(header);

            // Body
            var body = document.createElement("div");
            body.style.cssText = "padding:16px 20px;overflow-y:auto;flex:1";
            dialog.appendChild(body);

            // Toolbar
            var toolbar = document.createElement("div");
            toolbar.style.cssText = "display:flex;gap:8px;margin-bottom:12px;align-items:center";
            toolbar.innerHTML = '<label style="font-size:12px;cursor:pointer"><input type="checkbox" id="ept-mc-selall"> Select all</label>' +
                '<div style="flex:1"></div>' +
                '<span id="ept-mc-count" style="font-size:12px;color:#999"></span>' +
                '<button class="ept-mc-action" data-action="publish" disabled style="padding:3px 10px;border:1px solid #2e7d32;border-radius:4px;font-size:11px;cursor:pointer;background:#fff;color:#2e7d32">Publish</button>' +
                '<button class="ept-mc-action" data-action="unpublish" disabled style="padding:3px 10px;border:1px solid #ef6c00;border-radius:4px;font-size:11px;cursor:pointer;background:#fff;color:#ef6c00">Unpublish</button>' +
                '<button class="ept-mc-action" data-action="delete" disabled style="padding:3px 10px;border:1px solid #c62828;border-radius:4px;font-size:11px;cursor:pointer;background:#fff;color:#c62828">Move to Trash</button>';
            body.appendChild(toolbar);

            // Table
            var table = document.createElement("div");
            table.style.cssText = "border:1px solid #e0e0e0;border-radius:4px;overflow:hidden";
            var html = '<table style="width:100%;border-collapse:collapse;font-size:12px">';
            html += '<thead><tr style="background:#f5f5f5">';
            html += '<th style="padding:8px;text-align:left;width:30px"></th>';
            html += '<th style="padding:8px;text-align:left">Name</th>';
            html += '<th style="padding:8px;text-align:left">Type</th>';
            html += '<th style="padding:8px;text-align:left">Status</th>';
            html += '</tr></thead><tbody>';

            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                var statusColor = item.status === "Published" ? "#2e7d32" : (item.status === "CheckedOut" || item.status === "Draft" ? "#1565c0" : "#666");
                html += '<tr style="border-top:1px solid #f0f0f0">';
                html += '<td style="padding:6px 8px"><input type="checkbox" class="ept-mc-check" data-id="' + item.contentId + '"></td>';
                html += '<td style="padding:6px 8px;font-weight:500">' + self._esc(item.name) + '</td>';
                html += '<td style="padding:6px 8px;color:#999">' + self._esc(item.contentTypeName) + '</td>';
                html += '<td style="padding:6px 8px"><span style="color:' + statusColor + ';font-weight:600;font-size:11px">' + self._esc(item.status) + '</span></td>';
                html += '</tr>';
            }
            if (items.length === 0) {
                html += '<tr><td colspan="4" style="padding:20px;text-align:center;color:#999">No children</td></tr>';
            }
            html += '</tbody></table>';
            table.innerHTML = html;
            body.appendChild(table);

            overlay.appendChild(dialog);
            document.body.appendChild(overlay);

            // Bindings
            var selected = new Set();

            function updateButtons() {
                var count = selected.size;
                var countEl = document.getElementById("ept-mc-count");
                if (countEl) countEl.textContent = count > 0 ? count + " selected" : "";
                var btns = overlay.querySelectorAll(".ept-mc-action");
                for (var b = 0; b < btns.length; b++) btns[b].disabled = count === 0;
            }

            document.getElementById("ept-mc-close").onclick = function () { document.body.removeChild(overlay); };
            overlay.onclick = function (e) { if (e.target === overlay) document.body.removeChild(overlay); };

            document.getElementById("ept-mc-selall").onclick = function () {
                var checks = overlay.querySelectorAll(".ept-mc-check");
                for (var c = 0; c < checks.length; c++) {
                    checks[c].checked = this.checked;
                    var id = parseInt(checks[c].getAttribute("data-id"));
                    if (this.checked) selected.add(id); else selected.delete(id);
                }
                updateButtons();
            };

            var checks = overlay.querySelectorAll(".ept-mc-check");
            for (var c = 0; c < checks.length; c++) {
                checks[c].onclick = function () {
                    var id = parseInt(this.getAttribute("data-id"));
                    if (this.checked) selected.add(id); else selected.delete(id);
                    updateButtons();
                };
            }

            var actionBtns = overlay.querySelectorAll(".ept-mc-action");
            for (var a = 0; a < actionBtns.length; a++) {
                actionBtns[a].onclick = function () {
                    var action = this.getAttribute("data-action");
                    if (selected.size === 0) return;
                    var msg = action === "delete" ? "Move " + selected.size + " items to trash?" :
                              action.charAt(0).toUpperCase() + action.slice(1) + " " + selected.size + " items?";
                    if (!confirm(msg)) return;

                    fetch(window.EPT_API_URL + "/manage-children/" + action, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ parentContentId: parseInt(parentId), contentIds: Array.from(selected) })
                    })
                    .then(function (r) { return r.json(); })
                    .then(function (result) {
                        alert(result.succeeded + " succeeded" + (result.failed > 0 ? ", " + result.failed + " failed" : ""));
                        document.body.removeChild(overlay);
                        // Refresh the tree
                        require(["dojo/topic"], function (topic) {
                            topic.publish("/epi/shell/context/request", { uri: "epi.cms.contentdata:///" + parentId }, { sender: null });
                        });
                    });
                };
            }
        },

        _esc: function (s) {
            if (!s) return "";
            var d = document.createElement("div");
            d.textContent = String(s);
            return d.innerHTML;
        }
    });
});
