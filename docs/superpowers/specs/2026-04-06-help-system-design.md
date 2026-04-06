# Help System Design
_Date: 2026-04-06_

## Overview

Two changes:
1. **Bug fix**: "About" button in the shared layout was hardcoded in English — already fixed.
2. **Feature**: Translated in-context help for every tool, surfaced as a right-side drawer on full-page tools and a lightweight popover in CMS shell widgets.

---

## 1. About Button Fix (done)

`_PowertoolsLayout.cshtml` now injects `LocalizationService Loc` and uses `@Loc.GetString("/editorpowertools/menu/about")`. The translation key `/editorpowertools/menu/about` already existed in all 11 lang files.

---

## 2. Widget Base URL Derivation

**Problem:** CMS shell widgets (`ContentDetailsWidget`, `ActiveEditorsWidget`) load in Optimizely's edit mode without EPT's layout, so `window.EPT_BASE_URL` and `window.EPT_STRINGS` are not set. Previously `ensureStrings()` fell back to `/editorpowertools/api/ui-strings` — an endpoint outside the module's protected path, which is not acceptable.

**Solution:** `editorpowertools.js` is a `requiredResource` that loads on every CMS page. At the top of this file, derive and set `window.EPT_BASE_URL` if it is not already present (it is set by EPT's layout when visiting an EPT page):

```js
if (!window.EPT_BASE_URL && typeof require !== 'undefined') {
    try {
        window.EPT_BASE_URL = require.toUrl('editorpowertools/')
            .replace(/ClientResources\/js\/?$/, '');
    } catch(e) {}
}
```

`require.toUrl('editorpowertools/')` resolves to the Dojo module path (e.g. `/_protected/EditorPowertools/ClientResources/js/`), stripping `ClientResources/js/` yields the module root.

---

## 3. Widget Strings Endpoint

A new action `WidgetStrings()` is added to `EditorPowertoolsController`, returning `UiStringsProvider.GetAll()` as JSON. Route: `EditorPowertools/WidgetStrings` (under the module's protected path, consistent with all other EPT page routes).

The `ensureStrings()` function in both widget files changes its fallback URL from `/editorpowertools/api/ui-strings` to `window.EPT_BASE_URL + 'EditorPowertools/WidgetStrings'`.

The existing `/editorpowertools/api/ui-strings` endpoint (`UiStringsController`) is retained — it is referenced by the Razor layout's inline `<script>` block and must not be removed.

---

## 4. Help Strings in Lang Files

New strings are added under `/editorpowertools/ui/help/` in all 11 lang files (`en`, `da`, `sv`, `no`, `de`, `fi`, `fr`, `es`, `nl`, `ja`, `zh-CN`):

```xml
<help>
  <helpbtn>Help</helpbtn>
  <contenttypeaudit>1–2 paragraphs...</contenttypeaudit>
  <personalizationaudit>...</personalizationaudit>
  <audiencemanager>...</audiencemanager>
  <contenttyperecommendations>...</contenttyperecommendations>
  <bulkpropertyeditor>...</bulkpropertyeditor>
  <scheduledjobsgantt>...</scheduledjobsgantt>
  <activitytimeline>...</activitytimeline>
  <contentimporter>...</contentimporter>
  <cmsdoctor>...</cmsdoctor>
  <contentaudit>...</contentaudit>
  <linkchecker>...</linkchecker>
  <activeeditors>...</activeeditors>
  <managechildren>...</managechildren>
  <securityaudit>...</securityaudit>
  <contentstatistics>...</contentstatistics>
  <languageaudit>...</languageaudit>
  <contentdetails>...</contentdetails>
</help>
```

`UiStringsProvider.GetAll()` gains a `help` property with all 18 keys, making them available as `EPT.s('help.{toolkey}')` in JS.

English text is written first; the 10 other languages are machine-translated with the same style as existing strings.

---

## 5. Full-Page Tool Drawer

### `?` Button
Each tool's Razor view (`Views/{Tool}/Index.cshtml`) gets a `?` button inside the `ept-page-header`:

```html
<div class="ept-page-header">
    <h1>
        @Loc.GetString("/editorpowertools/tools/{tool}/title")
        <button class="ept-help-btn" data-ept-help="contenttypeaudit"
                aria-label="@Loc.GetString("/editorpowertools/ui/help/helpbtn")">?</button>
    </h1>
    <p>@Loc.GetString("/editorpowertools/tools/{tool}/description")</p>
</div>
```

All 16 full-page tool views get this button with the appropriate `data-ept-help` key.

### Drawer JS (`editorpowertools.js`)
- `EPT.openHelp(toolKey)` — reads the page `<h1>` text for the drawer title, reads `EPT.s('help.' + toolKey)` for the body, dynamically creates and appends a `<div class="ept-help-drawer">` to `document.body`
- `EPT.closeHelp()` — removes the drawer and overlay
- Event delegation on `document` handles all `[data-ept-help]` clicks — zero per-tool wiring
- ESC key and click on overlay both call `EPT.closeHelp()`

### CSS (`editorpowertools.css`)
```
.ept-help-btn          — small round/square button, sits inline with h1
.ept-help-drawer       — fixed right panel, full height, slides in from right
.ept-help-drawer--open — active/visible state (CSS transition)
.ept-help-drawer__overlay — semi-transparent full-page backdrop
.ept-help-drawer__header  — title + close button row
.ept-help-drawer__body    — scrollable help text area
```

---

## 6. Widget Popovers

### `?` Button
`ContentDetailsWidget.js` and `ActiveEditorsWidget.js` each get a small `?` button appended to their `templateString`.

### Popover JS (per widget, self-contained)
- Click the `?` button → toggle a `<div class="ept-help-popover">` positioned near the button
- Text from `EPT.s('help.contentdetails')` / `EPT.s('help.activeeditors')`
- `ensureStrings()` guarantees strings are loaded before the button is active
- Click anywhere outside the popover dismisses it (`document` click listener, removed after dismiss)
- No shared drawer infrastructure — fully self-contained in each widget file

### CSS (in `editorpowertools.css`, used by widgets via the `editorpowertools.css` requiredResource)
```
.ept-help-popover       — small card, absolute/fixed positioned near the ? button
.ept-help-popover__text — text content area
```

---

## Files Changed

| File | Change |
|------|--------|
| `Views/Shared/_PowertoolsLayout.cshtml` | Inject `LocalizationService`, translate About button — **already done** |
| `editorpowertools.js` | Add EPT_BASE_URL derivation; add `EPT.openHelp()`, `EPT.closeHelp()`; add `help` block to `EPT_STRINGS`; event delegation |
| `Views/{Tool}/Index.cshtml` (×16) | Add `?` button in `ept-page-header` |
| `ContentDetailsWidget.js` | Update `ensureStrings()` URL; add `?` button + popover |
| `ActiveEditorsWidget.js` | Update `ensureStrings()` URL; add `?` button + popover |
| `EditorPowertoolsController.cs` | Add `WidgetStrings()` action |
| `UiStringsProvider.cs` | Add `help` block with 18 keys |
| `lang/en.xml` + 10 others | Add `<help>` section with 18 keys |
| `editorpowertools.css` | Add drawer + popover + help button styles |
