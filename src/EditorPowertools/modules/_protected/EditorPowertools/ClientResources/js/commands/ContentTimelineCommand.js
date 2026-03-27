define([
    "dojo/_base/declare",
    "epi/shell/command/_Command",
    "epi/routes"
], function (
    declare,
    _Command,
    routes
) {
    return declare([_Command], {
        label: "Content Timeline",
        iconClass: "epi-iconClock",
        category: "context",
        canExecute: false,
        isAvailable: false,

        _onModelChange: function () {
            if (!this.model) {
                this.set("canExecute", false);
                this.set("isAvailable", false);
                return;
            }

            var available = !this.model.ownerContentLink;
            this.set("canExecute", !!available);
            this.set("isAvailable", !!available);
        },

        _execute: function () {
            if (!this.model || !this.model.contentLink) return;

            var contentId = String(this.model.contentLink).split("_")[0];
            var url = routes.getActionPath({
                moduleArea: "EditorPowertools",
                controller: "EditorPowertools",
                action: "ActivityTimeline"
            });
            window.open(url + "?contentId=" + encodeURIComponent(contentId), "_blank");
        }
    });
});
