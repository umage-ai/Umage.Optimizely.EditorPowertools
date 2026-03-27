define([
    "dojo/_base/declare",
    "epi/shell/command/_Command"
], function (
    declare,
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

            // Only available for content that has children and is not an asset folder
            var available = !this.model.ownerContentLink && this.model.hasChildren;
            this.set("canExecute", !!available);
            this.set("isAvailable", !!available);
        },

        _execute: function () {
            if (!this.model || !this.model.contentLink) return;

            var contentId = String(this.model.contentLink).split("_")[0];

            // Open the Manage Children tool for this content
            var url = this._getToolUrl("EditorPowertools/ManageChildren") +
                "?parentId=" + encodeURIComponent(contentId);
            window.open(url, "_blank");
        },

        _getToolUrl: function (path) {
            if (window.__eptModuleBasePath) {
                return window.__eptModuleBasePath + path;
            }
            return "/EPiServer/" + path;
        }
    });
});
