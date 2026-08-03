# FIGlet

## Load this reference when

Changing FIGfont parsing, fitting, smushing, FigletFont, FigletCatalog,
FigletText, `.flf` or `.tlf` resources, the optional font package, or font
provenance.

## Normative documentation

- [FigletText contract](../../../../docs/controls/display/figlet-text.md#overview)
- [Rendering pipeline](../../../../docs/architecture/rendering-pipeline.md#control-rendering)
- [Control evidence](../../../../docs/testing/controls-integration.md#mounted-component-surfaces)
- [Pinned collection provenance](../../../../extern/figlet/README.md)

## Code map

- Parser, renderer, and limits: `src/SharpVision/Fonts/`
- Catalog and provenance types: `src/SharpVision.FigletFonts/Fonts/`
- Embedded resources: `src/SharpVision.FigletFonts/Resources/`
- Audit and package scripts: `scripts/audit-figlet-fonts.mjs` and
  `scripts/package-figlet-fonts.mjs`
- Tests: `tests/SharpVision.Tests/Fonts/` and `Controls/Display/FigletText*`

## Workflow

1. Preserve source bytes and verify hashes before parsing.
2. Keep input, glyph counts, row widths, comments, nesting, and output bounded
   by `FigletLimits`.
3. Compare composition with the official `figlet` executable using exact bytes
   and whitespace.
4. Run every catalog entry; a representative font is insufficient.
5. Rebuild only from both pinned commits, preserve one resource per font, and
   require deterministic manifest bytes.

## Project-specific traps

- Only the 18 official BSD-3-Clause fonts and MIT `Classy` font are allowed in
  the optional package; every other entry is a release blocker.
- Preserve hardblanks until composition completes.
- Normalize CRLF and CR explicitly; legacy bytes may map to Unicode NEL.
- Keep `SharpVision` free of catalogs, manifests, and embedded font resources.

## Focused verification

```bash
node --test scripts/audit-figlet-fonts.test.mjs
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Fonts*" \
  --minimum-expected-tests 1 --timeout 60s
```
