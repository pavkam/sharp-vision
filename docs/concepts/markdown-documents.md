# Markdown documents

## Overview

`SharpVision.Document` provides `IDocumentFormatReader`, a format-independent
contract that produces detached `DocumentBlock` trees and diagnostics.
`MarkdownDocumentReader` is the built-in native reader. `Document.Load` parses
first and replaces the current tree only after the reader returns successfully.

Install the optional package alongside SharpVision:

```bash
dotnet add package SharpVision.Document
```

```csharp
var reader = new MarkdownDocumentReader();
var document = new Document();
document.Load(await File.ReadAllTextAsync("README.md"), reader);
```

## Format-reader contract

`IDocumentFormatReader.Read` accepts a non-null source string and optional
`DocumentReadOptions`. A reader returns `DocumentReadResult`: root blocks in
source order plus deterministic `DocumentDiagnostic` entries carrying zero-based
UTF-16 `DocumentSourceSpan` ranges. Readers construct detached nodes; they never
mutate a mounted control.

`MaximumCharacters` defaults to 4 Mi UTF-16 code units and rejects non-positive
configuration or oversized input. This is a format-independent memory boundary,
not a Markdown rule.

An inline link with an empty destination, such as `[label]()`, remains a
semantic `DocumentLink` with a null `Target`. It keeps document link navigation
and activation semantics without emitting an invalid OSC 8 hyperlink target.

## Baseline Markdown

The default reader recognizes ATX and Setext headings, paragraphs, soft and hard
breaks, emphasis, strong emphasis, code spans with arbitrary backtick
delimiters, links with balanced targets, bulleted and numbered lists, nested
list content, block quotes, fenced code blocks with language identifiers, and
thematic breaks. Backslash escapes preserve punctuation literally. A hard break
may use either two trailing spaces or a trailing backslash. Optional syntax
stays visible as literal text when its extension is disabled.

Ordered-list markers follow the
[CommonMark 0.31.2 list-item grammar](https://spec.commonmark.org/0.31.2/#list-items):
one to nine ASCII digits followed by `.` or `)`. Any valid start value forms a
list after a block boundary, but only a list starting at `1` may interrupt an
open paragraph. A ten-digit prefix and a non-one marker inside paragraph
continuation remain literal paragraph text. A bullet or ordered marker with no
content creates an empty list item when it starts a block or continues a list;
optional trailing spaces or tabs do not change that result. An empty marker
cannot interrupt an open paragraph. For a non-empty item, one through four
literal spaces after the marker establish that item's content column and are not
included in semantic text. Continuation and nested blocks use the same
item-specific column, including ordered markers whose digit widths differ from
their peers. Changing between `-`, `+`, and `*`, or between the `.` and `)`
ordered delimiters, starts a distinct list block. Parser-generated radio groups
follow those exact list boundaries.

Fence block boundaries use the same full opener grammar as fence parsing. An
opener may be indented by at most three spaces, and a backtick fence's info
string cannot contain a backtick. A rejected fence-looking line remains part of
the surrounding paragraph instead of splitting it.

Block-quote nesting is interpreted to a maximum of 64 semantic levels. Any
deeper quote markers remain literal paragraph content, keeping hostile input
bounded without discarding source text.

**Known limitations**, tracked as intentional scope boundaries rather than
silent gaps:

- **Tab indentation.** CommonMark expands a tab to the next 4-column stop when
  measuring a list, block-quote, or code-fence indent. This reader's own
  block-start detectors count only literal space characters toward that
  measurement, so a tab-indented marker is not recognized as indentation at all;
  the tab itself survives as a literal character in the resulting text. This
  applies only to structural indentation - a tab inside an already-parsed code
  block or inline flow expands correctly.
- **Lazy continuation.** A line that fails to repeat a block quote's `>` marker,
  or a list item's own indentation, but is otherwise ordinary text ends that
  quote or list item immediately instead of continuing its enclosing paragraph,
  the way CommonMark's lazy-continuation rule requires.
- **HTML entities.** `&amp;`, `&#65;`, and similar entity references pass
  through completely literally; this reader does not decode them.

## Optional extensions

| `MarkdownExtension` | Syntax                               | Document result                                                  |
| ------------------- | ------------------------------------ | ---------------------------------------------------------------- |
| `Strikethrough`     | `~~text~~`                           | `DocumentStrikethrough`                                          |
| `Tables`            | GFM pipe table                       | `DocumentTable` with header and cell alignment                   |
| `TaskLists`         | `- [ ]` / `- [x]`                    | Genuine `CheckBox` inside `DocumentBlockControl`                 |
| `Autolinks`         | Extended URL                         | Activatable `DocumentLink`                                       |
| `WikiLinks`         | `[[target]]` / `[[target \| label]]` | `DocumentLink` preserving target, heading, or block suffix       |
| `Callouts`          | `> [!NOTE] Title`                    | Typed, semantically colored `DocumentCallout` with nested blocks |
| `RadioLists`        | `- ( )` / `- (x)`                    | Genuine `RadioButton`; consecutive items share one stable group  |

`GitHubFlavored` combines strikethrough, tables, task lists, and extended
autolinks. `All` enables every extension. Enabling one individual flag never
implicitly enables another.

The standard callout kinds use distinct theme semantic colors: `NOTE` uses
`Info`, `TIP` uses `Success`, `IMPORTANT` uses `Accent`, `WARNING` uses
`Warning`, and `CAUTION` uses `Error`. Matching is case-insensitive. Other kinds
remain valid and use the authored `CalloutFace` and `CalloutTitleFace` fallback.

```csharp
var reader = new MarkdownDocumentReader(new MarkdownOptions
{
    Extensions = MarkdownExtension.GitHubFlavored |
                 MarkdownExtension.WikiLinks |
                 MarkdownExtension.Callouts |
                 MarkdownExtension.RadioLists
});

document.Load(markdown, reader);
```

## Interactive content

Task and radio markers are not decorative glyphs. The reader creates ordinary
`CheckBox` and `RadioButton` controls, so keyboard and pointer activation,
commands, state events, focus, disabled state, and theme resolution behave the
same as controls authored directly by the application. Each parsed radio list
receives its own deterministic `GroupName`; a separate or nested list does not
join that group. When mounted, the presenter scopes generated group names to the
owning `Document`, so radio lists in sibling documents cannot deselect each
other. If malformed source marks several radios in one list as selected, the
last selected marker wins.

Those generated labels participate in the owning `Document`'s continuous
semantic selection like directly embedded controls. Copy output preserves their
displayed text but omits checkbox, radio, table-border, quote-bar, and callout
chrome. The complete stream normalization, pointer/keyboard selection, and
clipboard rules belong to the
[Document control](../controls/collections/document.md#selection-and-copying).

## Expected behavior

| Scope              | Observable evidence                                                               |
| ------------------ | --------------------------------------------------------------------------------- |
| Format abstraction | A custom reader can return a detached tree consumed by `Document.Load`.           |
| Baseline parsing   | Representative blocks and nested semantic inline nodes preserve source order.     |
| Extensions         | Each flag changes only its syntax family; disabled syntax remains literal.        |
| Forms              | Parsed task and radio items are retained controls with ordinary input behavior.   |
| Bounds             | Oversized input is rejected before parsing beyond the configured character limit. |
