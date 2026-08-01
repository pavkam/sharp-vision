# Unicode and rendering testing

## Overview

Geometry fixtures pin Unicode 17 grapheme, East Asian Width, and emoji source
versions. Generated tables have sorted/non-overlap and first/last boundary
tests. Conformance failures print the exact code points and rule context.

The checked-in Unicode 17 `GraphemeBreakTest.txt` is executed line by line.
Expected boundaries are decoded independently from its division/multiplication
markers and compared with `Graphemes.Enumerate`; curated cases separately name
CR/LF, Hangul, Indic, flags, modifiers, keycaps, ZWJ families, and invalid
UTF-16. A warmed mixed-text loop must allocate zero managed bytes.

Width tests independently cover every East Asian width family, both ambiguous
policies, canonical composed/decomposed pairs, text/emoji variation selectors,
keycaps, flags, tags, modifiers, ZWJ sequences, private/unassigned values,
orphan marks, controls, and invalid UTF-16. The warmed measurement loop must
also allocate zero managed bytes.

Orphan-presentation tests separately cover combining and spacing marks,
prepend-only clusters, VS15/VS16, emoji modifiers, tag characters, ZWJ, and
mixed base-less components. They prove source preservation at editing
boundaries, U+FFFD ownership in frames, exact replacement UTF-8 in full and
incremental output, and no mutation of a neighboring preceding cell. The
independent terminal model classifies test input without production width code.

## Curated cases

Cover ASCII, CJK, supplementary ideographs, full/half/ambiguous widths under
both policies, precomposed/decomposed text, spacing/nonspacing/orphan marks,
VS15/VS16, modifiers, keycaps, flags, tag flags, ZWJ families/professions,
private/unassigned values, C0/C1 controls, tabs/newlines, and invalid UTF-16.

## Frame oracle

For frames A and B, render A fully into a virtual terminal, apply the production
incremental diff to B, then compare screen cells, grapheme ownership, cursor,
style, hyperlink, and modes with a second virtual terminal that fully rendered
B. Run this for targeted transitions and seeded random frame pairs.

Targeted transitions include no-op, sparse/dense/style-only damage, deletion,
narrow↔wide, combining changes, edge clipping/wrapping, bottom-right behavior,
resize, failed/partial writes, and full invalidation.

The concrete byte-application oracle and seeded transition matrix are specified
in [rendering equivalence testing](rendering.md#overview); snapshots do not
replace semantic screen comparison.

Before diff encoding exists, frame/canvas tests prove lead/continuation
metadata, complete UTF-8 copying, two-pass arena-limit validation, clip
intersection, right-edge policy, overwrite and clear repair from either occupied
cell, and idempotent disposal. A fixed-seed mutation suite checks every cell
after each random draw/clear operation and prints the seed plus operation on
failure.

`Text` consumes only committed grapheme-aligned `Line` slices and draws them
through the semantic canvas. Control coverage verifies combining sequences, CJK,
emoji ZWJ, invalid UTF-16 replacement, multiline wrapping, clipping, and
ellipsis under both narrow and wide ambiguous-width policies. U+2026 reserves
the same one or two cells that the target frame owns; half-wide ellipsis output
is forbidden. A warmed unchanged Unicode layout/render loop must allocate zero
managed bytes across five measured windows.

## Allocation

Hot measurement, canvas, damage, and encoding cases assert no object per
Rune/grapheme/cell after warm-up and record total allocation for representative
ASCII, CJK, combining, emoji, sparse, and dense frames. The cross-domain
renderer and protocol gate repeats ASCII/mixed/emoji segmentation plus
unchanged/sparse/dense 80×24 encodes in five warmed windows; at least one
measured window must remain literally allocation-free so one-time tiered-runtime
bookkeeping is not mistaken for a per-operation allocation.

The interactive host proof additionally enters CJK followed by decomposed text
through distinct UTF-8 and bracketed-paste paths, navigates across their shared
grapheme boundaries, deletes the complete preceding wide cluster, and verifies
the remaining combining cluster in a fresh semantic frame and terminal output.
The nested automatic-scroll proof renders wide CJK content through two clipped
viewports while offsets and pixel-derived pointer cells change, then asserts no
orphan continuation survives at the exposed frame edge.

Exact grid tests enumerate every pixel in representative uneven totals and
compare against an independent 64-bit rational formula. Seeded positive grids
prove monotonic bounded mapping, first/last boundaries, overflow safety,
nullable cells without metrics, no fabricated top-left hit, resize replacement,
and captured pixel-only release behavior.

## Required evidence

| Domain       | Required observation                                                                 |
| ------------ | ------------------------------------------------------------------------------------ |
| Segmentation | Combining, variation, modifier, tag, ZWJ, invalid UTF-16, and line boundaries.       |
| Width        | Ambiguous policy, emoji, CJK, controls, and pinned Unicode-version behavior.         |
| Cells        | Lead/continuation ownership across draw, clip, clear, overwrite, scroll, and resize. |
| Rendering    | Final semantic frame and output never contain half a grapheme owner.                 |
| Allocation   | Warmed measurement, mutation, damage, and encoding meet their contract.              |
