---
name: unicode-cell-geometry
description:
  Use when changing SharpVision grapheme segmentation, Rune decoding, Unicode
  width, emoji, combining marks, wide cells, wrapping, clipping, cursor
  placement, selection, or cell-coordinate behavior.
---

# Unicode Cell Geometry

## Overview

Measure terminal cells by extended grapheme cluster, not `char` and not a sum of
Rune widths. Keep every consumer on one versioned geometry implementation.

## Workflow

1. Read `docs/concepts/unicode-cell-geometry.md`,
   `docs/architecture/memory-ownership.md`, and
   `docs/testing/unicode-rendering.md`.
2. Confirm the pinned Unicode version and source data before changing tables or
   tailoring. Generated tables must record their input version.
3. Write failing cluster, table-boundary, and integration tests before changing
   measurement.
4. Decode with `Rune`, segment extended grapheme clusters, then apply the
   documented terminal-width and ambiguous-width policy to the whole cluster.
5. Route measurement, wrapping, clipping, cursor movement, hit testing,
   selection, and rendering through the same API.
6. Update the concept spec, fixtures, source attribution, and performance proof
   with behavior.

## Invariants

- Never split surrogate pairs, grapheme clusters, or wide glyphs.
- Combining marks, variation selectors, modifiers, tags, and ZWJ components do
  not contribute independent cell widths.
- Width is deterministic and culture-independent. Ambiguous width changes only
  through an explicit policy.
- Controls such as tab, CR, and LF are contextual layout/input operations, not
  ordinary printable width.
- A wide cluster owns a lead cell and continuation cells. Clearing or
  overwriting any owned cell repairs the complete cluster.
- Right-edge behavior is explicit: wrap, clip, or replace without orphaned
  continuation cells.
- Canonically equivalent precomposed and decomposed text has equal cell width
  without allocating normalized strings.
- Hot measurement and cell operations allocate no object per Rune or cell.
- Keep one named type per file, including generated files, name the file exactly
  after the type, and never declare nested named types.
- Make immutable value types readonly. Leave a struct mutable only when its role
  intrinsically advances or accumulates state, and keep that mutability narrow.
- Prefer readonly structs for small immutable wrappers with valid defaults and
  cheap copies; preserve classes for identity, ownership, polymorphism, or
  shared mutable state.
- Never use primary or positional constructors. Define constructors explicitly,
  validate before assignment, and document every rejected argument.
- Use named regions at genuine responsibility boundaries in substantial source
  files; avoid trivial nesting and split unrelated responsibilities instead.

## Example review

For a family emoji followed by CJK text at the right edge, verify one emoji
cluster of width two, a valid wide-cell continuation, the configured wrapping
policy, cursor placement, hit testing, and repair when either emoji cell is
overwritten.

## Verification

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*CellGeometry*Tests" --timeout 60s
make lint
make build
make test
```

## Common mistakes

- Treating East Asian Width as a complete terminal-width algorithm.
- Summing Rune widths inside emoji, keycap, flag, or ZWJ clusters.
- Fixing drawing without updating selection, hit testing, or cursor movement.
- Updating Unicode tables without conformance fixtures and version attribution.
