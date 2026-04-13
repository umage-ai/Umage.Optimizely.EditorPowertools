# Watermark Branding Design

## Goal

Add a fixed-position umage.ai wordmark badge to every tool page that acts as subtle brand attribution — visible to curious users, invisible to focused ones.

## Design decisions

**Style:** Ghost text (Option A) — the umage.ai SVG wordmark at ~28% opacity. No background, no border, no pill. Fades in slightly on hover.

**Size:** 88px wide SVG. At this width the wordmark is legible when you look for it but does not register as a focal element.

**Position:** `position: fixed; bottom: 14px; right: 16px` — always in the lower-right corner regardless of scroll position. `z-index: 10` so it stays above normal content but below dialogs.

**Link:** `https://umage.ai/?utm_source=powertools` — opens in a new tab.

**Tooltip:** `title="Powered by umage.ai"` for accessibility.

## Placement

Injected once in `_PowertoolsLayout.cshtml`, directly before `</body>`. Every tool that uses this layout (all of them) gets the badge automatically. No per-tool changes needed.

## CSS

New rule in `editorpowertools.css`:

```css
.ept-watermark {
  position: fixed;
  bottom: 14px;
  right: 16px;
  z-index: 10;
  opacity: 0.28;
  transition: opacity 0.2s;
  text-decoration: none;
  display: block;
  line-height: 0;
}
.ept-watermark:hover {
  opacity: 0.6;
}
.ept-watermark svg {
  width: 88px;
  height: auto;
  display: block;
}
```

## SVG

The SVG from `About/Index.cshtml` (lines 196–201): `viewBox="0 0 160.47713 57.079746"`, two `<path>` elements, `fill="currentColor"`. The `fill` attribute on the `<g>` element becomes `currentColor` so the colour inherits from the link/page context rather than being hardcoded.

## Constraints

- Must not cover interactive elements. Fixed bottom-right is safe: all tool content scrolls inside `.ept-main`, no tool places controls in the viewport corner.
- No JavaScript required — pure HTML + CSS.
- No new server-side code required.
