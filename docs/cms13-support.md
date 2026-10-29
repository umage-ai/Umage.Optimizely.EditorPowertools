# CMS 13 support in EditorPowertools

This page lists every tool's CMS 13-specific behavior and how it's gated.

## Packaging

Every library builds as `net8.0` against CMS 12 and `net10.0` against CMS 13, and the main packages ship both. NuGet picks a build by target framework alone, so CMS 12 sites on .NET 10 cannot use them. `-p:EptCmsFlavor=Cms12` builds the same libraries as `net8.0`-only packages with a `.Cms12` id suffix (`src/Directory.Build.props`). `scripts/pack-all.sh` packs both flavors and `scripts/pack-smoke.sh` installs them into throwaway sites for each supported combination; CI and the release workflow run both. Dependency ranges for CMS, Forms and Graph live in `src/Directory.Build.props`, and the libraries compile against their lower bounds.

## Foundation — content-type metadata

All CMS 13 content-type reads go through `IContentTypeMetadataProvider`:

- `IsContract` — true when the type is declared as a .NET interface inheriting `IContentData` and flagged with `[ContentType]`.
- `CompositionBehaviors` — array of `"SectionEnabled"` / `"ElementEnabled"`.
- `Contracts` — references to contract types that the content type implements.

Under CMS 12, all three are empty. Shared services never branch on CMS version themselves.

## Tool-by-tool

### Content Type Audit

- Contract / Section / Element badges per type (both table and tree view).
- Two new filters:
  - **Kind** — All kinds / Contract types only / Non-contract types.
  - **Composition** — Any / Section-enabled / Element-enabled / Both / Plain.
- **Contracts** stat card in the summary strip.
- **Applied contracts** panel in the type-detail dialog; each contract chip is clickable and opens the contract's own detail view.

### Content Statistics

- **Contracts** summary card counting content whose type is a contract.
- Contracts slice in the type-distribution chart.
- **Block breakdown** panel showing counts for Section-enabled / Element-enabled / Plain blocks. A block that enables both Section and Element is counted in both — the panel shows role-availability, not a disjoint partition.

### Bulk Property Editor

- *Content types* / *Contracts* tabs in the type picker (native `<select>`, tabs toggle which options are visible).
- Composition behaviors appear as text suffixes on block options (e.g. `Promo (Section, Element)`).
- Selecting a Contract expands at execution time to every non-contract type whose `Contracts` collection references it; the preview shows "Contract expands to: X, Y, Z".

### Content Audit

- Filter drawer gains a "CMS 13" section with two selects:
  - **Contracts** — Any / Include / Exclude / Contracts only.
  - **Composition** — Any / Section / Element.
- Filters are applied post-page; if a page ends up smaller than the page size because rows were dropped, `TotalCount` is nullified and the UI falls back to the "prev/next with unknown total" mode used by the default data provider.

## Terminology

Content types that exist in the database but have no matching .NET class are labeled **"Code-less"** (formerly "Orphaned"). The rename applies to:

- C# symbols: `PropertyOrigin.Codeless`, `ContentTypeDto.IsCodeless`, `ContentTypeTreeNodeDto.IsCodeless`.
- JS property names: `isCodeless`.
- All 11 UI languages (en, da, sv, no, de, fi, fr, es, nl, ja, zh-CN).

The CSS class `ept-row--orphaned` is preserved because it's purely styling reused by `link-checker.js` for an unrelated concept.

## Scoped-out: CmsDoctor

CmsDoctor's `OrphanedPropertyCheck` retains its original name because it detects a different concern — property definitions present in the database but missing from the content-type model. That check is unchanged.
