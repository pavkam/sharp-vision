# Rendering equivalence testing

## Correctness oracle

For a committed frame A and target frame B, the production `Encoder` emits an
incremental update from A to B. `VirtualScreen`, an independent test terminal
model, applies a full render of A and then that update. A second model applies a
clean full render of B. Both models must equal B and each other in grapheme
text, lead/continuation ownership, style, hyperlink, cursor position, and
visibility.

The model parses emitted ECMA-48 bytes and implements only terminal semantics,
including colon-form underline variants, underline color, overline, slow/rapid
blink, and resets; it does not call `Damage` or `Encoder`. Exact-byte tests
remain separate so two implementations cannot agree on the same unnecessary or
malformed sequence.

## Damage proof

`Damage.Enumerate` is tested for no-op, sparse/adjacent runs, style-only
changes, deletion, narrow-to-wide, wide-to-narrow, and changed wide graphemes.
Every run is half-open, row-major, and expanded through complete ownership in
both frames. Dimension changes and explicit invalidation return every target
row.

Canvas primitive tests also paint a non-default surface, then draw lines, single
glyphs, and transparent style overlays over it. Foreground and attributes may
change, but the destination background must remain identical; this catches
isolated table dividers, borders, shadows, and indicator glyphs that
accidentally fall back to the terminal default.

Foreground-transformation tests require row-major selector callbacks at absolute
lead-cell coordinates, exactly once per complete stored owner. They cover stored
spaces versus untouched blank cells, preservation of every non-foreground
semantic field, clipped wide owners with no callback, and equal
lead/continuation styles. Callback-failure tests prove unchanged exception
identity, a valid transformed prefix, an unchanged failing owner, and intact
wide-cell ownership links.

Cell hashes may reject unequal graphemes quickly, but hash equality never proves
semantic equality: complete UTF-8 bytes and renderer metadata are compared. This
keeps collision behavior correct.

## Randomized transitions

Fixed seed `0xD1FF` generates 128 frame pairs containing ASCII, CJK, combining
clusters, emoji ZWJ sequences, spaces, indexed colors, attributes, hyperlinks,
typed underline variants/colors, overline, slow/rapid blink, edge policies, and
cursor states. Every pair runs through the full/incremental oracle. A failure
reports the seed and case before it becomes a named regression.
