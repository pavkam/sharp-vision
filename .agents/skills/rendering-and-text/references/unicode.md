# Unicode Cell Geometry

## Load this reference when

Changing Rune decoding, grapheme segmentation, Unicode data, terminal width,
emoji, combining marks, variation selectors, ZWJ, wide cells, clipping,
wrapping, selection, hit testing, or cursor coordinates.

## Normative documentation

- [Unicode geometry](../../../../docs/concepts/unicode-cell-geometry.md#unicode-cell-geometry-contract)
- [Width rules](../../../../docs/concepts/unicode-cell-geometry.md#width-rules)
- [Cell ownership](../../../../docs/concepts/unicode-cell-geometry.md#cell-ownership)
- [Shared consumers](../../../../docs/concepts/unicode-cell-geometry.md#shared-consumers)
- [Unicode evidence](../../../../docs/testing/unicode-rendering.md#unicode-and-rendering-testing-contract)

## Code map

- Unicode implementation and generated tables:
  `src/SharpVision.Terminal/Unicode/`
- Data generator: `scripts/generate-unicode-data.mjs`
- Source provenance: `extern/unicode/README.md`
- Primitive tests: `tests/SharpVision.Terminal.Tests/Unicode/`
- UI text integration: `tests/SharpVision.Tests/Text/` and control surface tests

## Workflow

1. Confirm the pinned Unicode version and source data.
2. Add cluster, table-boundary, width-policy, and consumer-integration failures.
3. Decode with `Rune`, segment clusters, then measure the complete cluster.
4. Route layout, wrapping, clipping, cursor movement, hit testing, selection,
   and rendering through the shared geometry API.
5. Regenerate tables only through the repository generator and retain source
   attribution.

## Project-specific traps

- Combining marks, modifiers, tags, variation selectors, and ZWJ components do
  not contribute independent widths.
- Canonical equivalents must have equal cell width without allocating normalized
  strings.
- Named emoji transition evidence is required when randomized coverage would
  otherwise hide the exact regression.

## Focused verification

```bash
node scripts/generate-unicode-data.mjs --check
dotnet test --project tests/SharpVision.Terminal.Tests \
  --filter-namespace "SharpVision.Terminal.Tests.Unicode*" \
  --minimum-expected-tests 1 --timeout 60s
dotnet test --project tests/SharpVision.Tests \
  --filter-namespace "SharpVision.Tests.Text*" \
  --minimum-expected-tests 1 --timeout 60s
```
