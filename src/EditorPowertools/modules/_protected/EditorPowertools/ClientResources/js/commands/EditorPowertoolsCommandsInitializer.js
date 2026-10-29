define([
    "require",
    "dojo/_base/declare",
    "epi/_Module",
    "epi/locator",
    "editorpowertools/commands/ContentTimelineCommand",
    "editorpowertools/commands/ManageChildrenCommand"
], function (
    require,
    declare,
    _Module,
    locator,
    ContentTimelineCommand,
    ManageChildrenCommand
) {
    return declare([_Module], {
        initialize: function () {
            this.inherited(arguments);

            // Base URLs (the layout sets them for full-page tools). In the edit UI, derive them from where
            // the shell serves this module's scripts: the mount point differs between CMS 12 and 13 and
            // can be reconfigured, so it is never hard-coded.
            if (!window.EPT_BASE_URL) {
                window.EPT_BASE_URL = require.toUrl("editorpowertools/").split("?")[0]
                    .replace(/ClientResources\/js\/?$/, "");
            }
            if (!window.EPT_HUB_URL) window.EPT_HUB_URL = window.EPT_BASE_URL + "hubs";

            var self = this;
            try {
                var xhr = new XMLHttpRequest();
                xhr.open("GET", window.EPT_BASE_URL + "FeaturesApi/GetFeatures", false);
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
