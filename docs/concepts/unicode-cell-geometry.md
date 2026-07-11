# Unicode cell geometry

## Unicode cell geometry contract

SharpVision pins
[Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/),
[UAX 29 revision 47](https://www.unicode.org/reports/tr29/tr29-47.html), and
[UAX 11 revision 44](https://www.unicode.org/reports/tr11/tr11-44.html) for its
first generated tables. Source data and attribution are checked in with the
generator output.

Text is decoded as Rune values and segmented into extended grapheme clusters.
Width is assigned to the cluster, never calculated by summing scalar East Asian
Width values. The default ambiguous-width policy is narrow; callers may choose
wide explicitly through `Capabilities`.

## Width rules

- Printable narrow clusters occupy one cell.
- Wide/fullwidth and recognized emoji-presentation clusters occupy two cells.
- Combining marks, variation selectors, emoji modifiers, tag characters, and ZWJ
  components do not contribute independent cells.
- Controls such as tab, CR, and LF are contextual operations, not printable
  widths.
- Invalid UTF-16 decodes to the documented replacement policy and occupies one
  cell per replacement.
- Private-use and unassigned scalars default to one cell unless an explicit
  profile tailors them.

Canonically equivalent precomposed/decomposed text yields equal width without
allocating normalized strings.

## Cell ownership

A wide cluster writes one lead cell plus a continuation cell referencing the
same owned grapheme. Clipping, clearing, or overwriting either cell damages and
repairs the full ownership range. Right-edge policy is explicit per drawing
operation: wrap, clip the whole cluster, or emit a replacement; half output is
forbidden.

## Shared consumers

Measurement, wrapping, horizontal scrolling, clipping, cursor movement, hit
testing, selection, `RichText`, canvas operations, damage tracking, and terminal
encoding use this single geometry service.

## Test contract

Use Unicode grapheme conformance data, emoji data, generated-table boundaries,
ASCII/CJK/ambiguous policies, decomposed text, selectors, keycaps, flags, ZWJ
families, lone surrogates, wrapping, clipping, selection, and wide-cell repair.
