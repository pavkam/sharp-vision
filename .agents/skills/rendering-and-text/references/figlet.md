# FIGlet

## Load this reference when

Changing FIGfont parsing, fitting, smushing, FigletFont, FigletCatalog,
FigletText, `.flf` or `.tlf` resources, the audited archive, or font provenance.

## Normative documentation

- [FigletText contract](../../../../docs/controls/display/figlet-text.md#overview)
- [Rendering pipeline](../../../../docs/architecture/rendering-pipeline.md#control-rendering)
- [Control evidence](../../../../docs/testing/controls-integration.md#mounted-component-surfaces)
- [Pinned collection provenance](../../../../extern/figlet/README.md)

## Code map

- Parser, renderer, limits, catalog: `src/SharpVision/Fonts/`
- Embedded resources: `src/SharpVision/Fonts/Resources/`
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
5. Rebuild only from the pinned commit and require deterministic archive bytes.

## Project-specific traps

- The upstream collection has no collection-wide license; unverified entries are
  release blockers, not permission grants.
- Preserve hardblanks until composition completes.
- Normalize CRLF and CR explicitly; legacy bytes may map to Unicode NEL.

## Focused verification

```bash
node --test scripts/audit-figlet-fonts.test.mjs
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Fonts*" \
  --minimum-expected-tests 1 --timeout 60s
```
