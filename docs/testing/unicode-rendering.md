# Unicode and rendering testing

## Unicode and rendering testing

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

## Allocation

Hot measurement, canvas, damage, and encoding cases assert no object per
Rune/grapheme/cell after warm-up and record total allocation for representative
ASCII, CJK, combining, emoji, sparse, and dense frames.
