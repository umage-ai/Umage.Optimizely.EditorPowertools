define([
    "dojo/_base/declare",
    "epi/_Module",
    "epi/dependency",
    "epi/routes",
    "editorpowertools/commands/ContentTimelineCommand",
    "editorpowertools/commands/ManageChildrenCommand"
], function (
    declare,
    _Module,
    dependency,
    routes,
    ContentTimelineCommand,
    ManageChildrenCommand
) {
    return declare([_Module], {
        initialize: function () {
            this.inherited(arguments);

            // Store the module base path for command URL construction
            try {
                window.__eptModuleBasePath = routes.getActionPath({
                    moduleArea: "EditorPowertools",
                    controller: "",
                    action: ""
                });
            } catch (e) {
                // Fallback will be used in commands
            }

            // Check which features are enabled before adding commands
            var self = this;
            try {
                var xhr = new XMLHttpRequest();
                xhr.open("GET", "/editorpowertools/api/features", false); // sync to block init
                xhr.send();
                if (xhr.status === 200) {
                    var features = JSON.parse(xhr.responseText);
                    self._registerCommands(features);
                } else {
                    // If the API fails, register all commands (let server-side auth handle it)
                    self._registerCommands({});
                }
            } catch (e) {
                self._registerCommands({});
            }
        },

        _registerCommands: function (features) {
            var commands = [];

            if (features.activityTimeline !== false) {
                commands.push(new ContentTimelineCommand({ order: 500 }));
            }

            if (features.bulkPropertyEditor !== false) {
                commands.push(new ManageChildrenCommand({ order: 510 }));
            }

            if (commands.length === 0) return;

            // Add to the navigation tree plugin area
            try {
                var locator = dependency.resolve("epi.storeregistry");
            } catch (e) { /* ignore */ }

            // Register via the epi locator directly
            require(["epi/locator"], function (locator) {
                commands.forEach(function (cmd) {
                    locator.add("epi-cms/navigation-tree/commands[]", cmd);
                });
            });
        }
    });
});
