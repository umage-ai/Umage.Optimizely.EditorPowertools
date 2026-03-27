define([
    "dojo/_base/declare",
    "epi/shell/command/_Command"
], function (
    declare,
    _Command
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

            // Available for any content item (not asset folders)
            var available = !this.model.ownerContentLink;
            this.set("canExecute", !!available);
            this.set("isAvailable", !!available);
        },

        _execute: function () {
            if (!this.model || !this.model.contentLink) return;

            // Extract numeric content ID from contentLink (may be "5_123" format)
            var contentId = String(this.model.contentLink).split("_")[0];

            // Open the Activity Timeline filtered to this content
            var url = this._getToolUrl("EditorPowertools/ActivityTimeline") +
                "?contentId=" + encodeURIComponent(contentId);
            window.open(url, "_blank");
        },

        _getToolUrl: function (path) {
            // Use Paths.ToResource equivalent — look for the module path
            // The module base path is set on the global scope by module initialization
            if (window.__eptModuleBasePath) {
                return window.__eptModuleBasePath + path;
            }
            // Fallback: construct from current shell path
            return "/EPiServer/" + path;
        }
    });
});
