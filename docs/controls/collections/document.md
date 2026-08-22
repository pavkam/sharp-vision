# Document

## Overview

`Document` is declared
`public sealed class Document : CompositeControlBase, IStyled<DocumentStyle>`.
It displays a scrollable tree of rich text content: headings, paragraphs with
inline markup and activatable links, bulleted and numbered lists, block quotes,
preformatted code, and thematic breaks.

A document is a semantic content tree. Text, links, lists, tables, callouts, and
other structural nodes stay lightweight data. `DocumentInlineControl` and
`DocumentBlockControl` are the deliberate bridge for a form: each mounts one
real retained `ControlBase` into the shared flow, preserving its focus, routed
input, commands, events, styling, and disposal behavior. This hybrid keeps a
long Markdown document compact without faking interactive widgets as painted
text.

Ownership is single and explicit. Adding a node that already belongs to a tree
throws `ArgumentException` instead of silently reparenting it; removing a node
detaches it and hands back a value the caller may reuse. Mutating an attached
document is dispatcher-affine, exactly like every other control, and a detached
subtree invalidates nothing, so composing a whole document before adding it
costs one layout pass rather than one per node.

Presentation resolves from `ActualStyle` during the paint pass rather than being
cached onto nodes, so a theme swap or a local `Style` assignment restyles every
heading, marker, bar, and link on the next frame. Every glyph resolves against
the live ambiguous-width policy first and falls back to a code-owned ASCII
repair value when the primary glyph would not fit one cell, so a terminal
configured for wide ambiguous characters degrades to plain ASCII instead of
corrupting the columns beside each glyph.

Scrolling is vertical. `Document` stretches to fill its slot by default and is a
single focus stop; it never traps Tab, because link navigation releases focus at
either end of the document.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- CompositeControlBase
    CompositeControlBase <|-- Document
    DocumentNode <|-- DocumentBlock
    DocumentNode <|-- DocumentInline
    DocumentNode <|-- DocumentListItem
    DocumentBlock <|-- DocumentParagraph
    DocumentBlock <|-- DocumentHeading
    DocumentBlock <|-- DocumentList
    DocumentBlock <|-- DocumentBlockQuote
    DocumentBlock <|-- DocumentCodeBlock
    DocumentBlock <|-- DocumentSeparator
    DocumentBlock <|-- DocumentBlockControl
    DocumentBlock <|-- DocumentCallout
    DocumentBlock <|-- DocumentTable
    DocumentInline <|-- DocumentTextRun
    DocumentInline <|-- DocumentInlineContainer
    DocumentInlineContainer <|-- DocumentLink
    DocumentInlineContainer <|-- DocumentEmphasis
    DocumentInlineContainer <|-- DocumentStrong
    DocumentInlineContainer <|-- DocumentStrikethrough
    DocumentInline <|-- DocumentCodeSpan
    DocumentInline <|-- DocumentSoftBreak
    DocumentInline <|-- DocumentLineBreak
    DocumentInline <|-- DocumentInlineControl
    DocumentNodeCollection~TNode~ <|-- DocumentBlockCollection
    DocumentNodeCollection~TNode~ <|-- DocumentInlineCollection
    DocumentNodeCollection~TNode~ <|-- DocumentListItemCollection
```

## API

| Member                                                      | Type                                   | Default          | Description                                                                            |
| ----------------------------------------------------------- | -------------------------------------- | ---------------- | -------------------------------------------------------------------------------------- |
| `Blocks`                                                    | `DocumentBlockCollection`              | Empty            | Owned ordered root block content; accepts only `DocumentBlock` nodes.                  |
| `Style`                                                     | `DocumentStyle?`                       | `null`           | Complete local presentation, or null for theme ownership.                              |
| `ActualStyle`                                               | `DocumentStyle`                        | Resolved         | Read-only; the complete local, theme-owned, or code-owned presentation.                |
| `ScrollBarStyle`                                            | `ScrollBarStyle?`                      | `null`           | Local generated-bar style; null leaves it to the theme.                                |
| `ActualScrollBarStyle`                                      | `ScrollBarStyle`                       | Resolved         | Read-only resolved generated-bar style.                                                |
| `Extent`                                                    | `Size`                                 | Layout-dependent | Read-only committed non-negative content extent, in cells.                             |
| `Viewport`                                                  | `Size`                                 | Layout-dependent | Read-only committed non-negative visible extent, in cells.                             |
| `VerticalOffset`                                            | `int`                                  | `0`              | Valid vertical content offset in lines; rejects a value outside the current extent.    |
| `LineSize`                                                  | `int`                                  | `1`              | Non-negative lines one arrow key or wheel notch scrolls; rejects a negative value.     |
| `PageOverlap`                                               | `int`                                  | `0`              | Non-negative lines a page command keeps in view; rejects a negative value.             |
| `ShowScrollBars`                                            | `ShowScrollBars`                       | `WhenNeeded`     | When the generated vertical scrollbar is shown; rejects an undefined value.            |
| `ActiveLink`                                                | `DocumentLink?`                        | `null`           | The focused link; assigning a foreign or disabled link clears the selection instead.   |
| `Load(string, IDocumentFormatReader, DocumentReadOptions?)` | `DocumentReadResult`                   | —                | Parses detached content through a format reader, then replaces the block tree.         |
| `ScrollBy(int lines, ScrollCause cause)`                    | `bool`                                 | —                | Adds a signed line delta with saturation and endpoint clamping; rejects unknown cause. |
| `ScrollToTop()`                                             | `bool`                                 | —                | Scrolls to the first line; reports whether the offset changed.                         |
| `ScrollToEnd()`                                             | `bool`                                 | —                | Scrolls to the last line; reports whether the offset changed.                          |
| `ScrollChanged`                                             | `EventHandler<ScrollChangedEventArgs>` | —                | Raised after the vertical offset commits.                                              |
| `LinkClicked`                                               | `EventHandler<DocumentLinkEventArgs>`  | —                | Raised after any link is activated, following that link's own `Clicked`.               |

`ScrollBy` defaults its `cause` to `ScrollCause.Programmatic`; the other causes
describe keyboard, pointer, wheel, and content-driven changes and reach
subscribers through `ScrollChanged` (see
[scrolling.md](../../concepts/scrolling.md#overview)). `Extent` and `Viewport`
report the committed values from the most recent layout pass, so both are zero
before the document has been measured.

`DocumentLinkEventArgs` carries one property, `Link`, holding the activated
`DocumentLink`. It exists so an application can handle every link centrally
instead of subscribing to each one.

> [!NOTE]
>
> `ActiveLink` resolves against the links the most recent layout pass found.
> Assigning it on a document that has not been measured yet leaves it `null`,
> because there is no projected link sequence to match the value against.

## Content tree

Every node derives from `DocumentNode`, most of them through one of two abstract
roles: `DocumentBlock` for content that occupies whole lines and stacks
vertically, and `DocumentInline` for content that flows within a line.
`DocumentNode`, `DocumentBlock`, and `DocumentInline` each declare a
`private protected` constructor, so the hierarchy is closed to the library and
cannot be extended from outside it. The sealed types below are the only nodes
that can ever exist, which is what lets consuming code pattern-match a node
exhaustively and lets the layout pass be total over the tree.

| Node                    | Role   | Content it owns                                        |
| ----------------------- | ------ | ------------------------------------------------------ |
| `DocumentParagraph`     | Block  | `Inlines` — flowing inline content.                    |
| `DocumentHeading`       | Block  | `Inlines` — flowing inline content, at a level 1 to 6. |
| `DocumentList`          | Block  | `Items` — marked list items.                           |
| `DocumentBlockQuote`    | Block  | `Blocks` — nested block content.                       |
| `DocumentCodeBlock`     | Block  | `Text` — literal preformatted text.                    |
| `DocumentSeparator`     | Block  | Nothing; it draws a thematic break.                    |
| `DocumentBlockControl`  | Block  | `Control` — one genuine retained control.              |
| `DocumentCallout`       | Block  | `Kind`, `Title`, and nested `Blocks`.                  |
| `DocumentTable`         | Block  | `Rows` containing aligned semantic cells.              |
| `DocumentListItem`      | Item   | `Blocks` — nested block content.                       |
| `DocumentTextRun`       | Inline | `Text` — inline-markup text.                           |
| `DocumentLink`          | Inline | `Inlines` — semantic activatable label content.        |
| `DocumentEmphasis`      | Inline | `Inlines` — italic semantic content.                   |
| `DocumentStrong`        | Inline | `Inlines` — bold semantic content.                     |
| `DocumentStrikethrough` | Inline | `Inlines` — struck semantic content.                   |
| `DocumentCodeSpan`      | Inline | `Text` — literal inline code.                          |
| `DocumentSoftBreak`     | Inline | Collapsible whitespace between source lines.           |
| `DocumentLineBreak`     | Inline | Nothing; it ends the current line.                     |
| `DocumentInlineControl` | Inline | `Control` — one atomic, single-line retained control.  |

`DocumentListItem` derives from `DocumentNode` directly rather than from
`DocumentBlock`, and that is deliberate: an item is only meaningful inside a
`DocumentList`, which supplies its marker and its gutter. Because it is not a
block, the type system alone prevents an item from being dropped into a
document, a block quote, or another item's `Blocks`, where it would render with
no marker at all.

### Node collections

`DocumentBlockCollection`, `DocumentInlineCollection`, and
`DocumentListItemCollection` are the three sealed collections a node or a
document exposes. Each derives from `DocumentNodeCollection<TNode>` and offers
exactly this surface:

| Member                          | Type                     | Default | Description                                                             |
| ------------------------------- | ------------------------ | ------- | ----------------------------------------------------------------------- |
| `Count`                         | `int`                    | `0`     | Read-only; the number of owned nodes.                                   |
| `Add(TNode node)`               | `void`                   | —       | Appends one detached non-null node.                                     |
| `Insert(int index, TNode node)` | `void`                   | —       | Inserts one detached non-null node at a position from zero to `Count`.  |
| `Remove(TNode node)`            | `bool`                   | —       | Removes the first reference match, leaving it detached and reusable.    |
| `RemoveAt(int index)`           | `void`                   | —       | Removes the node at a valid position, leaving it detached and reusable. |
| `Clear()`                       | `void`                   | —       | Removes every node, leaving each one detached and reusable.             |
| `GetEnumerator()`               | `List<TNode>.Enumerator` | —       | Allocation-free value enumerator used by direct iteration.              |

An indexer returns the node at a valid zero-based position. The collection
implements `IReadOnlyList<TNode>`.

Validation happens before the sequence changes, so a rejected insertion leaves
both the node and the collection exactly as the caller found them:

1. A null node throws `ArgumentNullException`.
2. An index outside the insertion or removal range throws
   `ArgumentOutOfRangeException`.
3. A node that already belongs to any document tree — this one included — throws
   `ArgumentException`. Remove it from its current owner first.

Every successful mutation invalidates the owning document's layout exactly once.
A collection inside a detached subtree invalidates nothing.

## Blocks

Sibling blocks are separated by exactly one blank line, both at the document
root and inside a block quote. A list item's own blocks are the one exception:
they are tight, so an item's paragraph sits directly above its nested list.

Emptying a block does not remove its line. An empty paragraph still occupies one
line, which makes it a deliberate way to add vertical space, and an empty list
item still occupies one marked line.

### DocumentParagraph

| Member                           | Type                       | Default | Description                                                            |
| -------------------------------- | -------------------------- | ------- | ---------------------------------------------------------------------- |
| `DocumentParagraph(string text)` | —                          | —       | Initializes a paragraph with one markup text run; rejects a null text. |
| `Inlines`                        | `DocumentInlineCollection` | Empty   | Owned ordered inline content.                                          |

`DocumentParagraph()` initializes an empty paragraph. A paragraph wraps its
inline content to the width available at its nesting level.

### DocumentHeading

| Member                                    | Type                       | Default | Description                                                             |
| ----------------------------------------- | -------------------------- | ------- | ----------------------------------------------------------------------- |
| `DocumentHeading(int level)`              | —                          | —       | Initializes an empty heading; rejects a level outside 1 through 6.      |
| `DocumentHeading(int level, string text)` | —                          | —       | Adds one markup text run; rejects an invalid level or a null text.      |
| `Level`                                   | `int`                      | —       | The heading level from 1 through 6; rejects a value outside that range. |
| `Inlines`                                 | `DocumentInlineCollection` | Empty   | Owned ordered inline content.                                           |

`Level` has no default because a heading cannot be constructed without one. The
public constants `MinimumLevel` and `MaximumLevel` are `1` and `6`.

A terminal has no font sizes, so levels differentiate through weight, color, and
underline rather than scale. Levels 1 and 2 paint with `HeadingFace`; levels 3
through 6 paint in the body face with bold weight added.

### DocumentList

| Member                                | Type                         | Default    | Description                                                               |
| ------------------------------------- | ---------------------------- | ---------- | ------------------------------------------------------------------------- |
| `DocumentList(DocumentListKind kind)` | —                            | —          | Initializes an empty list with a marker style; rejects an undefined kind. |
| `Kind`                                | `DocumentListKind`           | `Bulleted` | The marker style applied to every item; rejects an undefined value.       |
| `IsLoose`                             | `bool`                       | `false`    | Whether a blank line separates one item from the next.                    |
| `Start`                               | `int`                        | `1`        | Non-negative first ordinal for a numbered list.                           |
| `Items`                               | `DocumentListItemCollection` | Empty      | Owned ordered items.                                                      |

`DocumentList()` initializes an empty bulleted list. `DocumentListKind` is
`Bulleted`, which marks each item with a depth-rotating bullet glyph, or
`Numbered`, which marks each item with its one-based position formatted as
`"N."`.

The marker gutter is measured from the widest marker the list will actually
draw, plus one cell of gap. A list that reaches `"10."` or `"100."` therefore
keeps every item's content aligned behind the same column instead of letting a
wider marker overwrite its own text.

A bulleted item's glyph rotates by nesting depth modulo three — `FirstBullet`,
`SecondBullet`, `ThirdBullet`, then back to `FirstBullet` at the fourth level.
Depth is derived from the tree during layout and never cached, so moving a
nested list out to the document's own `Blocks` renders it at depth zero on the
next frame, with no stale marker.

### DocumentListItem

| Member                                               | Type                      | Default | Description                                                                    |
| ---------------------------------------------------- | ------------------------- | ------- | ------------------------------------------------------------------------------ |
| `DocumentListItem(DocumentBlock block)`              | —                         | —       | Initializes an item owning one detached block; rejects null or an owned block. |
| `DocumentListItem(string text)`                      | —                         | —       | Initializes an item with one markup text paragraph; rejects a null text.       |
| `DocumentListItem(string text, DocumentList nested)` | —                         | —       | Adds one markup text paragraph followed by one detached nested list.           |
| `Blocks`                                             | `DocumentBlockCollection` | Empty   | Owned ordered block content.                                                   |

`DocumentListItem()` initializes an empty item, which still occupies one marked
line so that emptying an item never silently drops its marker.

An item's own blocks are tight: its paragraph sits directly above its nested
list with no blank line between them, matching CommonMark. `IsLoose` adds a
blank line _between_ items only and leaves each item's own blocks tight.

### DocumentBlockQuote

| Member                                    | Type                      | Default | Description                                                                    |
| ----------------------------------------- | ------------------------- | ------- | ------------------------------------------------------------------------------ |
| `DocumentBlockQuote(DocumentBlock block)` | —                         | —       | Initializes a quote owning one detached block; rejects null or an owned block. |
| `DocumentBlockQuote(string text)`         | —                         | —       | Initializes a quote with one markup text paragraph; rejects a null text.       |
| `Blocks`                                  | `DocumentBlockCollection` | Empty   | Owned ordered block content.                                                   |

`DocumentBlockQuote()` initializes an empty quote. A quote indents its content
by two cells and draws `QuoteBar` in the first of them on every line it spans,
including wrapped continuations. Quotes nest freely: a quote inside a quote
indents twice and draws two bars.

### DocumentCodeBlock

| Member                           | Type     | Default | Description                                                      |
| -------------------------------- | -------- | ------- | ---------------------------------------------------------------- |
| `DocumentCodeBlock(string text)` | —        | —       | Initializes a code block with literal text; rejects a null text. |
| `Text`                           | `string` | `""`    | Non-null literal text; rejects null.                             |
| `Language`                       | `string` | `""`    | Optional language identifier retained from a source format.      |

`DocumentCodeBlock()` initializes an empty code block. `Text` is literal: markup
is never parsed, so source containing angle brackets needs no escaping. The
block splits on CRLF, CR, and LF — a CRLF pair is one break, not two — and each
source line becomes exactly one rendered line. Tabs expand to the next four-cell
stop.

A code line never wraps, because re-flowing code changes its meaning. A line
longer than the available width is clipped at the content edge.

### DocumentSeparator

`DocumentSeparator` has no members beyond its default constructor. It draws
`Rule` repeated across the full width available at its nesting level and
occupies a single line, so a separator inside a block quote stops at the quote's
own indent rather than at the document's edge.

### DocumentBlockControl

| Member                                      | Type          | Default | Description                                                          |
| ------------------------------------------- | ------------- | ------- | -------------------------------------------------------------------- |
| `DocumentBlockControl(ControlBase control)` | —             | —       | Owns one detached control; rejects null or an already-owned control. |
| `Control`                                   | `ControlBase` | —       | The retained control mounted at its measured block size.             |

The control participates in the ordinary retained tree, routed input, focus,
measurement, and disposal contracts. A desired-size change reflows later
document content. The same control instance cannot appear in two document nodes.

### DocumentCallout

| Member   | Type                      | Default  | Description                                       |
| -------- | ------------------------- | -------- | ------------------------------------------------- |
| `Kind`   | `string`                  | `"NOTE"` | Non-null source-defined callout kind.             |
| `Title`  | `string`                  | `""`     | Non-null title displayed above the nested blocks. |
| `Blocks` | `DocumentBlockCollection` | Empty    | Owned callout body blocks.                        |

A callout uses `CalloutFace` for its quote-like vertical bar and body, and
`CalloutTitleFace` for its bold title. Both faces use the same semantic
foreground by default so the callout reads as one visual region. `Kind` is
retained so applications can interpret format-specific kinds without the control
inventing a closed taxonomy.

Five standard kinds replace that fallback foreground with a theme semantic color
while preserving the two faces' other channels: `NOTE` uses `Info`, `TIP` uses
`Success`, `IMPORTANT` uses `Accent`, `WARNING` uses `Warning`, and `CAUTION`
uses `Error`. Kind matching is case-insensitive; any other value keeps the
fallback foreground.

That foreground cascades through headings, links, code, lists, nested quotes,
rules, and tables inside the callout. Descendants retain their own attributes,
backgrounds, and interaction states, but cannot break the callout into unrelated
foreground colors. Long generated titles wrap below the two-cell callout indent,
with the vertical bar continuing beside every title line.

### DocumentTable

`DocumentTable.Rows` owns `DocumentTableRow` values. Each row owns
`DocumentTableCell` values through `Cells`; `IsHeader` selects the header face.
A cell owns flowing `Inlines`, and its `Alignment` is `Left`, `Center`, or
`Right`. Column widths are measured once across all rows, then shared by every
cell so headings and body values stay aligned. Rich inline attributes, links,
and one-line retained controls remain active inside cells; table layout never
flattens them to decorative text. Content still clips to the document viewport
when the complete table is wider than the available cells.

## Inlines

Inline content flows inside a `DocumentParagraph` or a `DocumentHeading`.
Wrapping is greedy and prefers whitespace boundaries, and it can break between
two adjacent inlines as readily as within one, so neighboring runs read as one
continuous stretch of text. A token wider than the content width is placed alone
on its own line and overflows it rather than being split mid-word.

Whitespace an author actually typed survives — at the start of a paragraph and
immediately after a hard break — while whitespace that a wrap pushed to the
front of a continuation line is dropped.

### DocumentTextRun

| Member                         | Type     | Default | Description                                              |
| ------------------------------ | -------- | ------- | -------------------------------------------------------- |
| `DocumentTextRun(string text)` | —        | —       | Initializes a run with markup text; rejects a null text. |
| `Text`                         | `string` | `""`    | Non-null inline-markup text; rejects null.               |

`DocumentTextRun()` initializes an empty run. `Text` uses the same inline-markup
syntax as [`Text.Content`](../display/text.md#markup-grammar) — bold, dim,
italic, underline, strikethrough, reverse, and color tags. Call
[`Text.Escape`](../display/text.md#api) on dynamic content that must render
literally.

Markup applies at exact character boundaries rather than at token boundaries, so
a tag may open or close in the middle of a word: `"pre<b>post</b>"` renders
`prepost` with only the last four cells bold, and `"go <b>fast</b>, ok"` leaves
the comma unstyled.

Two characters inside a run are structural rather than text. A newline — `\n`,
`\r`, or `\r\n` — is a hard break that ends the current line wherever it
appears. A tab advances a fixed four cells; it is a blank advance rather than a
tab stop, so a break in a different place cannot change its width.

### DocumentLink

| Member                                     | Type                      | Default    | Description                                                                    |
| ------------------------------------------ | ------------------------- | ---------- | ------------------------------------------------------------------------------ |
| `DocumentLink(string text)`                | —                         | —          | Initializes a link with literal text; rejects a null text.                     |
| `DocumentLink(string text, string target)` | —                         | —          | Adds an OSC 8 target; rejects a null text or target.                           |
| `Text`                                     | `string`                  | `""`       | Non-null literal link text; rejects null.                                      |
| `Target`                                   | `string?`                 | `null`     | OSC 8 hyperlink target emitted around the link's cells, or null.               |
| `IsEnabled`                                | `bool`                    | `true`     | Whether the link can be focused and activated.                                 |
| `Emphasis`                                 | `DocumentLinkEmphasis`    | `Standard` | Which `DocumentStyle` face family paints the link; rejects an undefined value. |
| `Clicked`                                  | `EventHandler<EventArgs>` | —          | Raised after the link is activated by Enter, Space, or a primary click.        |

`DocumentLink()` initializes an empty link. The owning document — not the data
node — owns link focus and activation. Embedded controls retain their own input
behavior independently.

`Text` is literal, not markup: a link's face is its identity and is never
reinterpreted by inline tags. A link that wraps across lines stays one logical
link and remains activatable on every line it occupies.

`Target` and `Clicked` are independent. `Target` emits an OSC 8 terminal
hyperlink around the link's cells so a capable terminal can offer its own open
or copy affordance, and a terminal without OSC 8 support renders the text
unchanged; `Clicked` is what makes the link do something inside the application.
Either, both, or neither is valid.

`Emphasis` chooses the presentation, never the behavior: an `Action`-emphasis
link is exactly as focusable and activatable as a `Standard` one. `Standard`
paints with `LinkFace`/`ActiveLinkFace`, reading as an ordinary part of the
flowing text. `Action` paints with `ActionLinkFace`/`ActiveActionLinkFace`, a
solid, high-contrast chip that reads as a compact call to action. Use
`DocumentBlockControl` or `DocumentInlineControl` when the content needs a real
`Button` rather than link semantics.

A disabled link paints with `DisabledLinkFace` regardless of `Emphasis`, is
skipped by link navigation, and never raises `Clicked`. Disabling the currently
active link clears `ActiveLink` on the next layout pass, as does removing it
from the tree.

A `DocumentLink` cannot contain another `DocumentLink`. Inserting a subtree that
would create nested link semantics throws `ArgumentException` before the tree
changes.

### DocumentLineBreak

`DocumentLineBreak` has no members beyond its default constructor. It ends the
current line independently of word wrapping, so two consecutive breaks leave one
blank line and a trailing break leaves a blank final line.

## Input

`Document` is one framework tab stop with `TabNavigation.None`, then performs a
source-ordered walk across its semantic links and retained controls. Links keep
focus on the document; controls embedded through `DocumentInlineControl` and
`DocumentBlockControl` receive focus directly and retain their ordinary input
behavior. At either end, the unhandled Tab leaves the whole document subtree.

| Input                            | Result                                                                        |
| -------------------------------- | ----------------------------------------------------------------------------- |
| Tab                              | Moves to the next enabled link and scrolls it into view.                      |
| Shift+Tab                        | Moves to the previous enabled link and scrolls it into view.                  |
| Enter / Space                    | Activates the focused link for an activation-eligible modifier state.         |
| Up / Down                        | Scrolls by `LineSize` lines.                                                  |
| Page Up / Page Down              | Scrolls by the viewport height minus `PageOverlap`, and by at least one line. |
| Home / End                       | Scrolls to the first or last line.                                            |
| Wheel up / down                  | Scrolls by `LineSize` lines per notch.                                        |
| Primary press on an enabled link | Focuses the document, makes that link active, and activates it.               |
| Primary press elsewhere          | Leaves the event unhandled.                                                   |

Tab and Shift+Tab consume the keystroke only while it actually reaches another
link. At either end of the document the keystroke is left unhandled, so the
framework's own Tab default moves focus out — the same browser-like convention
that stops a page of links from trapping the caret. Disabled links are skipped
during the walk, and a document with no enabled links never consumes Tab at all.

Activating a link raises that link's own `Clicked` first, then the document's
`LinkClicked` with the same link. A keyboard activation and a pointer activation
produce the identical pair of events.

A scroll command is reported handled whenever the document has anything to
scroll, even at a boundary, so the keystroke cannot escape and page an enclosing
scrollable container out from under the still-focused document. A document whose
content fits its viewport leaves every scroll key unhandled instead, which keeps
those keys available to an ancestor.

## Presentation

### DocumentStyle

`DocumentStyle : ControlStyle` is a complete immutable presentation. Its own
`"sharpVision.document"` theme key falls back to `control` for anything it does
not author itself, and it adds fourteen faces and one glyph family to the
inherited `Face`/`Border`/`Shadow`:

| Member                 | Type             | Default                                                              | Description                                                                |
| ---------------------- | ---------------- | -------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `HeadingFace`          | `Face`           | Bold, straight-underlined `SemanticColor.Accent`                     | Level 1 and 2 headings.                                                    |
| `MarkerFace`           | `Face`           | `SemanticColor.Accent`                                               | A list item's bullet or number.                                            |
| `QuoteFace`            | `Face`           | Inherited foreground, italic                                         | A block quote's bar and its quoted content.                                |
| `CodeFace`             | `Face`           | `SemanticColor.SurfaceText` on `SemanticColor.Surface`               | A preformatted code block.                                                 |
| `RuleFace`             | `Face`           | `SemanticColor.Muted`                                                | A thematic break.                                                          |
| `CalloutFace`          | `Face`           | `SemanticColor.Warning`                                              | A callout body and its vertical bar.                                       |
| `CalloutTitleFace`     | `Face`           | Bold `SemanticColor.Warning`                                         | A callout title.                                                           |
| `TableFace`            | `Face`           | `SemanticColor.SurfaceText` on `SemanticColor.Surface`               | Ordinary table cells.                                                      |
| `TableHeaderFace`      | `Face`           | Bold `SemanticColor.Accent` on `SemanticColor.Surface`               | Header table cells.                                                        |
| `LinkFace`             | `Face`           | Straight-underlined `SemanticColor.Info`                             | A `Standard`-emphasis link that is not focused.                            |
| `ActiveLinkFace`       | `Face`           | `SemanticColor.SelectedText` on `SemanticColor.SelectedControl`      | The `Standard`-emphasis link at `ActiveLink` while the document has focus. |
| `DisabledLinkFace`     | `Face`           | `SemanticColor.DisabledText`                                         | A link whose `IsEnabled` is false, regardless of emphasis.                 |
| `ActionLinkFace`       | `Face`           | Bold `SemanticColor.SelectedText` on `SemanticColor.SelectedControl` | An `Action`-emphasis link that is not focused.                             |
| `ActiveActionLinkFace` | `Face`           | Bold `SemanticColor.PressedText` on `SemanticColor.PressedControl`   | The `Action`-emphasis link at `ActiveLink` while the document has focus.   |
| `Glyphs`               | `DocumentGlyphs` | `DocumentGlyphs.Default`                                             | The bullet, quote-bar, and rule glyph family.                              |

Every member is required. A `with` expression creates a validated member-wise
copy of `DocumentStyle.Default` or of any resolved style; assigning `null` to
`Style` restores the theme-owned presentation, and `ActualStyle` never returns
null. A theme may author a `styles.sharpVision.document` section ahead of the
code-owned defaults. Every bundled theme authors this namespaced
optional-package section (see
[themes.md](../../concepts/themes.md#style-types)).

The code-owned `QuoteFace` default uses `Color.Default`, while bundled themes
author the same semantic foreground as their document body. A quotation is set
apart through the italic attribute, never through a separately colored,
lower-contrast tone that a theme's palette might not keep legible.

`ActionLinkFace` and `ActiveActionLinkFace` deliberately reuse the same
selection and press color pairs every theme must already keep legible for
ordinary selection highlighting and pressed-button feedback, rather than an
arbitrary color invented only for this one face — the guarantee a solid button
chip needs and a general-purpose accent color does not carry on its own.

Replacing a face costs a repaint, because faces resolve during painting.
Replacing `Glyphs` costs a remeasure, because a different glyph can occupy a
different number of cells and move the text beside it.

A disabled document dims uniformly: every face resolves to the disabled body
style, so a heading, marker, bar, or link cannot stay bright while the
paragraphs around it fade.

### DocumentGlyphs

`DocumentGlyphs` is a complete immutable `readonly record struct`. Each member
is the theme-customizable primary glyph; the portable ASCII repair value beside
it is permanently code-owned and is not themeable.

| Member         | Type             | Default | Description                                                         |
| -------------- | ---------------- | ------- | ------------------------------------------------------------------- |
| `FirstBullet`  | `Rune`           | `'•'`   | The bullet marking a top-level list item; repairs to `'*'`.         |
| `SecondBullet` | `Rune`           | `'◦'`   | The bullet marking a once-nested list item; repairs to `'o'`.       |
| `ThirdBullet`  | `Rune`           | `'▪'`   | The bullet marking a twice-nested list item; repairs to `'+'`.      |
| `QuoteBar`     | `Rune`           | `'│'`   | The vertical bar down a block quote's left edge; repairs to `'\|'`. |
| `Rule`         | `Rune`           | `'─'`   | The horizontal rule drawn for a thematic break; repairs to `'-'`.   |
| `Default`      | `DocumentGlyphs` | —       | Static; the established code-owned glyph family.                    |

Every glyph is validated on construction and on `init`: a control character, or
a rune that does not measure exactly one cell, throws `ArgumentException`.

Every default above is an East Asian Ambiguous character, which a terminal
configured for wide ambiguous width renders as two cells. The document resolves
each glyph against the live cell policy before measuring and substitutes the
repair value when the primary would not fit, so under that configuration a
nested list, a quote, and a separator render as `*`, `o`, `+`, `|`, and `-`
rather than shifting every column beside them (see
[unicode-cell-geometry.md](../../concepts/unicode-cell-geometry.md#width-rules)).

## Example

![The Document control rendered in the live showcase](../../images/controls/document.png)

```csharp
var document = new Document();
document.Blocks.Add(new DocumentHeading(1, "SharpVision"));
document.Blocks.Add(new DocumentParagraph(
    "Build rich terminal apps without giving up <b>Unicode</b>."));

var steps = new DocumentList(DocumentListKind.Numbered);
steps.Items.Add(new DocumentListItem("Install the CLI"));

var details = new DocumentList(DocumentListKind.Bulleted);
details.Items.Add(new DocumentListItem("Bullets rotate with nesting depth"));
steps.Items.Add(new DocumentListItem("Run <b>sharpvision init</b>:", details));

document.Blocks.Add(steps);
document.Blocks.Add(new DocumentBlockQuote("Correctness outranks shortcuts."));
document.Blocks.Add(new DocumentCodeBlock("dotnet add package SharpVision"));
document.Blocks.Add(new DocumentSeparator());

var footer = new DocumentParagraph();
footer.Inlines.Add(new DocumentTextRun("Read the "));
var guide = new DocumentLink("guide", "https://example.com/guide");
guide.Clicked += (_, _) => OpenGuide();
footer.Inlines.Add(guide);
footer.Inlines.Add(new DocumentTextRun(" to continue."));
document.Blocks.Add(footer);

document.LinkClicked += (_, eventArgs) => RecordLinkClick(eventArgs.Link.Text);

document.Blocks.Add(new DocumentBlockControl(new CheckBox("Send updates")));

var markdown = File.ReadAllText("README.md");
document.Load(markdown, new MarkdownDocumentReader());
```

## Expected behavior

| Scope               | Observable evidence                                                                                                    |
| ------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| Public API          | Constructor and property validation, single ownership, defaults, detach-and-reuse, and level/kind range rejection.     |
| Surface             | Exact rendered lines: block spacing, wrapping, markers and gutters, quote bars, literal code, rules, and face styling. |
| Integrated behavior | Link navigation, activation, event order, and scrolling through mounted routed keyboard and pointer input.             |

- Embedded controls are explicit retained descendants. Inline controls are one
  atomic one-line token; block controls preserve their natural height.
- Adding an already-owned node throws `ArgumentException` before any state
  changes, and removing a node leaves it detached and immediately reusable in
  another tree.
- A semantic path is limited to 256 nodes. An insertion that would exceed that
  bound throws `ArgumentException` before ownership or collection state changes,
  keeping recursive layout bounded for programmatically authored trees.
- Sibling blocks are separated by exactly one blank line at the document root
  and inside a block quote, while a list item's own blocks stay tight; `IsLoose`
  adds a blank line between items only.
- A list reserves a gutter measured from its widest marker plus one cell, so
  numbering past 9 or 99 keeps every item's content on the same column.
- Bullet glyphs rotate by nesting depth modulo three, and moving a subtree
  re-derives its depth on the next layout instead of reusing a cached one.
- A code block never wraps: it splits on CRLF, CR, and LF, expands tabs to the
  next four-cell stop, renders markup characters literally, and clips a line
  that exceeds the width.
- Inline markup applies at exact character boundaries, so a tag that opens or
  closes mid-word styles only the characters it covers.
- Tab and Shift+Tab walk enabled links, skip disabled ones, and release focus at
  either end rather than trapping it inside the document.
- Activating a link raises the link's own `Clicked` before the document's
  `LinkClicked`, identically for Enter, Space, and a primary click.
- Swapping the theme restyles every heading, marker, bar, and link on the next
  frame, because presentation resolves at paint time and no node caches it.
- Under a wide ambiguous-width policy every default glyph degrades to its
  code-owned one-cell ASCII repair value instead of shifting the columns beside
  it.
- A disabled document renders every face in the disabled body style.
