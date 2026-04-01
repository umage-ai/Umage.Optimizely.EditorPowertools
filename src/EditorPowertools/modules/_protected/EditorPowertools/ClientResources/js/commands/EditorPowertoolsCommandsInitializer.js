define([
    "dojo/_base/declare",
    "epi/_Module",
    "epi/locator",
    "editorpowertools/commands/ContentTimelineCommand",
    "editorpowertools/commands/ManageChildrenCommand"
], function (
    declare,
    _Module,
    locator,
    ContentTimelineCommand,
    ManageChildrenCommand
) {
    return declare([_Module], {
        initialize: function () {
            this.inherited(arguments);

            var self = this;
            try {
                var xhr = new XMLHttpRequest();
                xhr.open("GET", "/editorpowertools/api/features", false);
                xhr.send();
                if (xhr.status === 200) {
                    var features = JSON.parse(xhr.responseText);
                    self._registerCommands(features);
                } else {
                    self._registerCommands({});
                }
            } catch (e) {
                self._registerCommands({});
            }
        },

        _registerCommands: function (features) {
            if (features.activityTimeline !== false) {
                locator.add("epi-cms/navigation-tree/commands[]", new ContentTimelineCommand({ order: 500 }));
            }

            if (features.manageChildren !== false) {
                locator.add("epi-cms/navigation-tree/commands[]", new ManageChildrenCommand({ order: 510 }));
            }

            // Dynamically load tracker + signalr only when feature is enabled
            if (features.activeEditors !== false) {
                require(["editorpowertools/active-editors-tracker"], function (ActiveEditorsTracker) {
                    ActiveEditorsTracker.start({
                        chatEnabled: features.activeEditorsChat !== false
                    });
                });
            }
        }
    });
});
