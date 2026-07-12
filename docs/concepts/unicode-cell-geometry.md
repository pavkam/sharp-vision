# Unicode cell geometry

## Unicode cell geometry contract

SharpVision pins
[Unicode 17.0.0](https://www.unicode.org/versions/Unicode17.0.0/),
[UAX 29 revision 47](https://www.unicode.org/reports/tr29/tr29-47.html), and
[UAX 11 revision 44](https://www.unicode.org/reports/tr11/tr11-44.html) for its
first generated tables. Source data and attribution are checked in with the
generator output.

The exact Unicode property and conformance inputs live under
`extern/unicode/17.0.0`. `npm run generate:unicode` reads those local files and
deterministically emits the runtime lookup table; `npm run check:unicode`
requires no network. Maintainers use the explicit `npm run refresh:unicode`
command to redownload the pinned official files and verify their recorded
SHA-256 values.

Text is decoded as Rune values and segmented into extended grapheme clusters.
Width is assigned to the cluster, never calculated by summing scalar East Asian
Width values. The default ambiguous-width policy is narrow; callers may choose
wide explicitly through `Capabilities`.

`Graphemes.Enumerate(ReadOnlySpan<char>)` returns an allocation-free
`GraphemeEnumerable`. Each `Grapheme` is a borrowed `(Offset, Length)` into the
original UTF-16 span and is invalid as soon as that source is no longer valid.
`HasInvalidData` identifies a segment whose single ill-formed UTF-16 code unit
is interpreted as U+FFFD. The enumerator applies UAX 29 GB3 through GB13 and
GB999 directly, including regional-indicator parity, Indic conjunct state, and
extended-pictographic ZWJ state; it does not allocate a normalized string.

`Width.Measure(ReadOnlySpan<char>, Ambiguous)` returns printable cells,
graphemes, and contextual-control counts in one allocation-free pass.
`Ambiguous.Narrow` is the default; `Ambiguous.Wide` is applied only when
selected explicitly in `Capabilities`. Generated canonical-decomposition bases
preserve equal width for composed and decomposed text without allocating
normalized storage. Invalid UTF-16, combining-only clusters, private-use
scalars, and unassigned Unicode 17 scalars occupy one conservative repairable
cell.

`SharpVision.Text.Layout` is the shipped UI consumer of this geometry. It emits
only grapheme-boundary source slices, expands tabs at four-cell stops, and
reserves ellipsis width under the same explicit ambiguous-width policy. Text
caches those lines, and Border rejects control or wide glyph Runes before
mutation.

## Source and terminal presentation

Source text and terminal presentation have distinct ownership. Editing,
selection, clipboard, and accessibility retain the original grapheme-aligned
UTF-16. A frame stores the safe UTF-8 presentation that owns terminal cells.

A base-less cluster made only from combining or spacing marks, prepend
characters, variation selectors, emoji modifiers, tag characters, or joiners has
no portable independent glyph position. It advances one repairable logical cell
but presents as U+FFFD. Raw orphan components are never emitted alone, because a
terminal could apply them to the preceding cell and violate frame ownership.
Valid decomposed text retains its original base plus components.

Cluster analysis returns width and presentation classification in one
allocation-free pass. Measurement, layout, canvas preflight, canvas mutation,
selection, and cursor placement consume that classification; none may invent a
second orphan or emoji-width rule.

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

Ownership generalizes to a positive rectangular cell span. Ordinary text spans
one row and one or two columns. Every continuation references one lead cell.
Repair, clearing, styling, clipping, selection, semantic comparison, damage, and
frame copying operate on the complete owner rectangle.

## Cell and pixel grids

When total cell and pixel dimensions are known, pixel boundaries use exact
rational mapping rather than truncated per-cell division:

`cell = floor(pixel * cellCount / pixelCount)`

The coordinate must be inside the known pixel rectangle and arithmetic uses a
checked 64-bit intermediate. Every valid pixel maps monotonically to one valid
cell even when the final columns or rows have different pixel extents. Missing
or out-of-domain geometry produces no cell coordinate; `(0, 0)` is never used as
a placeholder.

## Shared consumers

Measurement, wrapping, horizontal scrolling, clipping, cursor movement, hit
testing, selection, `RichText`, canvas operations, damage tracking, and terminal
encoding use this single geometry service.

## Test contract

Use Unicode grapheme conformance data, emoji data, generated-table boundaries,
ASCII/CJK/ambiguous policies, decomposed text, selectors, keycaps, flags, ZWJ
families, lone surrogates, wrapping, clipping, selection, and wide-cell repair.
