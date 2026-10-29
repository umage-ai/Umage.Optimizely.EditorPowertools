define([
    "dojo/_base/declare",
    "dojo/_base/lang",
    "dojo/dom-construct",
    "dojo/dom-geometry",
    "dojo/dom-style",

    "dijit/_TemplatedMixin",
    "dijit/_WidgetsInTemplateMixin",
    "dijit/_Container",
    "dijit/layout/_LayoutWidget",
    "dijit/form/Select",
    "dijit/form/TextBox",

    "epi-cms/component/ContentQueryGrid",

    "dojo/text!./templates/PowerSliceWidget.html"
], function (
    declare,
    lang,
    domConstruct,
    domGeometry,
    domStyle,

    _TemplatedMixin,
    _WidgetsInTemplateMixin,
    _Container,
    _LayoutWidget,
    Select,
    TextBox,

    ContentQueryGrid,

    template
) {
    // summary:
    //      Assets-pane widget listing a chosen slice of content immediately.
    //
    //      A templated container with a toolbar above a ContentQueryGrid, like the platform's Tasks
    //      component. The toolbar must be a sibling of the grid: the grid is a dgrid that re-renders
    //      its own domNode on every layout pass, so anything placed inside it is wiped out. The grid
    //      is told how much room is left once the toolbar has taken its share.

    return declare("editorpowertools-graph.PowerSliceWidget", [_Container, _LayoutWidget, _TemplatedMixin, _WidgetsInTemplateMixin], {

        templateString: template,

        // slices: Array
        //      Injected by PowerSliceComponent: [{name, displayName, sortOptions: [{key, label}]}].
        slices: null,

        // strings: Object
        //      Localized strings: {filterPlaceholder, reload, notAvailable, empty}.
        strings: null,

        // enabled: Boolean
        //      False when the current user has no access to the Power Slice feature.
        enabled: true,

        // minHeight: Number
        //      Read by the shell's ComponentContainer for the gadget's minimum height (default 200),
        //      which also raises saved layouts below it. At 200 the two-row toolbar leaves the list
        //      no room, so the slice looked empty.
        minHeight: 400,

        // _filterPhrase: [private] String
        //      Contents of the filter box, passed to the slice as its "q" parameter.
        _filterPhrase: "",

        buildRendering: function () {
            this.inherited(arguments);

            this.strings = this.strings || {};

            if (this.enabled === false || !this.slices || this.slices.length === 0) {
                this._unavailable = true;
                domStyle.set(this.toolbar, "display", "none");
                domStyle.set(this.contentQuery.domNode, "display", "none");

                var message = domConstruct.create("div", { "class": "ept-slices__message" }, this.domNode);
                message.textContent = this.strings.notAvailable || "";
                return;
            }

            this._sliceSelect = new Select({
                name: "EptSliceSelection",
                "class": "ept-slices__select",
                options: this.slices.map(function (slice, index) {
                    return {
                        label: slice.displayName,
                        value: slice.name,
                        selected: index === 0
                    };
                })
            });

            domConstruct.place(this._sliceSelect.domNode, this.reloadButton.domNode, "before");

            this.reloadButton.set("title", this.strings.reload || "");

            this._filterBox = new TextBox({
                "class": "ept-slices__filter",
                placeHolder: this.strings.filterPlaceholder || "",
                intermediateChanges: true
            });

            this._sortSelect = new Select({
                name: "EptSliceSort",
                "class": "ept-slices__sort",
                options: []
            });

            domConstruct.place(this._sortSelect.domNode, this.filterRow);
            domConstruct.place(this._filterBox.domNode, this.filterRow);

            this._applySortOptions();

            this.own(
                this._sliceSelect,
                this._filterBox,
                this._sortSelect,
                this._sliceSelect.on("change", lang.hitch(this, this._onSliceChanged)),
                this._sortSelect.on("change", lang.hitch(this, this._reloadQuery)),
                this._filterBox.on("change", lang.hitch(this, this._onFilterChanged))
            );
        },

        startup: function () {
            this.inherited(arguments);

            if (this._unavailable) { return; }

            // Load the first slice once the grid exists so the widget has content on screen when it opens.
            this._reloadQuery();
        },

        resize: function (changeSize, resultSize) {
            this.inherited(arguments);

            if (this._unavailable) { return; }

            // _LayoutWidget.startup() calls resize() with no arguments; _contentBox is the fallback.
            var box = resultSize || changeSize || this._contentBox;

            if (box && this.contentQuery) {
                this.contentQuery.resize(this._calculateContentQuerySize(box));
            }
        },

        _calculateContentQuerySize: function (box) {
            // The grid is a sibling of the toolbar, so whatever the toolbar occupies comes off explicitly.
            var toolbarSize = domGeometry.getMarginBox(this.toolbar);

            return { w: box.w, h: box.h - toolbarSize.h };
        },

        _applySortOptions: function () {
            // The options belong to the slice, so rebuild them on every switch. Without the explicit
            // removal dijit keeps the previous slice's orderings alongside the new ones.
            var current = this._sliceSelect.get("value");
            var slice = (this.slices || []).filter(function (s) {
                return s.name === current;
            })[0];

            var options = (slice && slice.sortOptions) || [];

            this._sortSelect.removeOption(this._sortSelect.getOptions());
            this._sortSelect.addOption(options.map(function (option, index) {
                return { label: option.label, value: option.key, selected: index === 0 };
            }));
        },

        _onSliceChanged: function () {
            clearTimeout(this._filterHandle);
            this._applySortOptions();

            // The filter belonged to the list just left; carrying it over makes the new slice open empty.
            this._filterPhrase = "";
            this._filterBox.set("value", "", false);
            this._reloadQuery();
        },

        _onFilterChanged: function (value) {
            // intermediateChanges fires per keystroke and each reload is a round trip: debounce 300ms.
            clearTimeout(this._filterHandle);

            this._filterHandle = setTimeout(lang.hitch(this, function () {
                this._filterPhrase = value || "";
                this._reloadQuery();
            }), 300);
        },

        _reloadQuery: function () {
            // Parameters before the name: setting queryName fetches straight away, so anything set
            // afterwards would miss the request already in flight.
            var parameters = {};
            if (this._filterPhrase) { parameters.q = this._filterPhrase; }

            var sortBy = this._sortSelect && this._sortSelect.get("value");
            if (sortBy) { parameters.sortBy = sortBy; }

            this.contentQuery.set("queryParameters", parameters);
            this.contentQuery.set("queryName", this._sliceSelect.get("value"));
        },

        _reloadClicked: function () {
            if (this._unavailable) { return; }
            this._reloadQuery();
        },

        destroy: function () {
            clearTimeout(this._filterHandle);
            this.inherited(arguments);
        }
    });
});
