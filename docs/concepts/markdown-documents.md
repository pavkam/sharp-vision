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

`Document.Load` and `LoadAsync` are the explicit ownership-transfer boundary.
They revalidate all mutable result roots together immediately before
replacement, including cross-root embedded-control uniqueness. A rejected result
leaves the current document unchanged. Success attaches those exact roots to the
destination; the returned result remains inspectable but is consumed and cannot
be loaded into another document.

`MaximumCharacters` defaults to 4 Mi UTF-16 code units and rejects non-positive
configuration or oversized input. This is a format-independent memory boundary,
not a Markdown rule.

Before block parsing, the Markdown reader normalizes CRLF and CR line endings to
LF and replaces every U+0000 NUL with U+FFFD REPLACEMENT CHARACTER. The latter
is CommonMark's insecure-character normalization: a NUL never reaches document
text, destinations, or other semantic nodes unchanged.

An inline link with an empty destination, such as `[label]()`, remains a
semantic `DocumentLink` with a null `Target`. It keeps document link navigation
and activation semantics without emitting an invalid OSC 8 hyperlink target.
Inline destinations follow the
[CommonMark 0.31.2 link grammar](https://spec.commonmark.org/0.31.2/#inline-link):
bare destinations exclude spaces and ASCII controls and require balanced
parentheses, while angle destinations remove their delimiters and may contain
spaces. Optional single-quoted, double-quoted, and parenthesized titles are
validated separately and are not folded into `DocumentLink.Target`; the document
model does not otherwise retain link titles. Invalid destination or title syntax
remains literal text.

Angle autolinks follow the CommonMark absolute-URI scheme grammar rather than a
fixed protocol allowlist, and the separate email grammar produces a visible
address with a `mailto:` target. Invalid schemes, prohibited ASCII controls and
spaces, incomplete authority forms, and malformed domain labels remain literal.
Code spans and angle autolinks bind more tightly than brackets while matching a
link label. If a completed label contains another active link at any inline
depth, the inner link wins and the outer bracket syntax remains literal.

## Baseline Markdown

The default reader recognizes ATX and Setext headings, paragraphs, soft and hard
breaks, emphasis, strong emphasis, code spans with arbitrary backtick
delimiters, links with balanced targets, bulleted and numbered lists, nested
list content, block quotes, fenced code blocks with language identifiers, and
thematic breaks. Backslash escapes preserve punctuation literally. A hard break
may use either two trailing spaces or a trailing backslash. Optional syntax
stays visible as literal text when its extension is disabled.

ATX headings follow the
[CommonMark 0.31.2 heading grammar](https://spec.commonmark.org/0.31.2/#atx-headings):
all spaces and tabs after the opening hash sequence and at the end of raw
heading content are structural. Interior whitespace remains authored content,
and a closing hash sequence is removed only when whitespace separates it.

Setext underlines convert every line in the immediately preceding paragraph into
one heading, preserving authored soft or hard line boundaries in its inline
content. A blank line closes the paragraph first, so a later `---` remains a
thematic break rather than retroactively creating a heading.

Paragraph raw content follows the
[CommonMark 0.31.2 paragraph rules](https://spec.commonmark.org/0.31.2/#paragraphs):
spaces and tabs at the paragraph edges and around soft line breaks are
structural and are removed before inline parsing. Two trailing spaces or an
unescaped trailing backslash still create an interior hard break, while trailing
markers at the end of a paragraph are stripped without creating a break. Only an
empty line or a line containing ASCII spaces and tabs is blank; non-breaking
spaces, form feeds, and other Unicode whitespace remain authored paragraph
content, including inside recursive blocks.

Each normalized paragraph is parsed as one inline stream rather than one stream
per physical source line. Emphasis, strong emphasis, strikethrough, and link
labels may therefore contain semantic soft or hard break nodes without losing
their container. A source line ending inside a code span instead normalizes to a
single literal space, as required by the code-span grammar. Escaped delimiters
remain literal across those boundaries.

Inline parsing builds code-span and balanced link-label indexes in bounded
passes before consuming candidates. Malformed repeated brackets and unmatched
backtick runs therefore do not rescan the remaining suffix at every opener;
extended-autolink delimiter balance is likewise counted once before trailing
punctuation is removed.

Asterisk and underscore emphasis runs use the
[CommonMark 0.31.2 left- and right-flanking rules](https://spec.commonmark.org/0.31.2/#emphasis-and-strong-emphasis)
for opening and closing at every supported run length. Whitespace cannot sit
inside a delimiter boundary; Unicode punctuation and symbol categories apply the
specified adjacency exceptions; and an invalid candidate closer is skipped so a
later valid run can close the container. Underscores additionally retain the
intraword restrictions defined by those rules.

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
follow those exact list boundaries. Task, radio, and plain content classify an
item, not its containing list, so transitions among them do not split an
otherwise continuous list.

A blank line makes a list loose only when it separates peer items or block
children within an item. Blank lines that merely separate the final item from a
following outdented paragraph, heading, quote, fence, or the end of the source
leave the list tight.

Fence block boundaries use the same full opener grammar as fence parsing. An
opener may be indented by at most three spaces, and a backtick fence's info
string cannot contain a backtick. A rejected fence-looking line remains part of
the surrounding paragraph instead of splitting it.

Every fenced-code body line is preserved literally after the opener's optional
indent removal, including leading blank lines and bodies made entirely of blank
lines. An empty body remains distinct from multiple empty body lines.

Block-quote recognition, paragraph interruption, and quote-line consumption use
the same marker grammar. A `>` marker may be preceded by zero through three
literal spaces; four or more spaces leave it as literal paragraph content. A
line that carries neither a block quote's `>` marker nor a list item's own
indentation still continues that container's open paragraph as a CommonMark lazy
continuation, provided the line does not itself look like the start of another
block; a blank line always closes that eligibility. Nested containers each track
their own open-paragraph state independently, so lazy continuation composes
correctly through arbitrary nesting.

Thematic breaks accept at least three matching `*`, `-`, or `_` markers with any
mixture of spaces and tabs between or after them, following the
[CommonMark 0.31.2 thematic-break grammar](https://spec.commonmark.org/0.31.2/#thematic-breaks).
The zero-to-three-space indentation prefix is evaluated before those interior
separators, so a leading tab is not silently discarded as marker spacing.

Recursive block parsing is interpreted to a shared maximum of 64 semantic levels
across block quotes and nested lists. Markers beyond that boundary remain
literal paragraph content and the result contains one deterministic diagnostic,
keeping hostile input bounded without discarding source text or leaking the
semantic tree's insertion exception.

Leading indentation for headings, block quotes, lists, and fenced code openers
expands a tab to the next 4-column stop, following
[CommonMark §2.2](https://spec.commonmark.org/0.31.2/#tabs), rather than
treating it as a single unmeasured character; a tab therefore reaches the
zero-to-three-column threshold - and beyond - on its own. A body or continuation
line's own leading tab is likewise consumed as one structural indentation
character, the same as a leading space, up to the container's indent budget. The
reader treats a tab atomically: it is never split across a threshold boundary,
so removing only part of a tab's four-column width - CommonMark's full
partial-tab splitting - is not implemented.

HTML character references are decoded against a curated table rather
than the full HTML5 named-reference list: the five XML entities (`&amp;`,
`&lt;`, `&gt;`, `&quot;`, `&apos;`), a small set of common typographic and arrow
entities (`&nbsp;`, `&copy;`, `&reg;`, `&trade;`, `&hellip;`, `&mdash;`,
`&ndash;`, `&larr;`, `&rarr;`, `&uarr;`, `&darr;`), a handful of accented Latin
letters (`&eacute;`, `&egrave;`, `&agrave;`, `&auml;`, `&ouml;`, `&uuml;`,
`&ntilde;`, `&ccedil;`), and decimal (`&#65;`) or hexadecimal (`&#x41;`) numeric
references. A named reference outside this curated set is left completely
literal - including the `&` and `;` - rather than guessed or partially decoded.
A numeric reference for the null code point, a code point past U+10FFFF, or a
surrogate-range code point decodes to the replacement character (U+FFFD) instead
of being rejected.

## Optional extensions

| `MarkdownExtension` | Syntax                               | Document result                                                  |
| ------------------- | ------------------------------------ | ---------------------------------------------------------------- |
| `Strikethrough`     | `~text~` / `~~text~~`                | `DocumentStrikethrough`                                          |
| `Tables`            | GFM pipe table                       | `DocumentTable` with header and cell alignment                   |
| `TaskLists`         | `- [ ]` / `- [x]`                    | Inline `CheckBox` followed by the parsed semantic label          |
| `Autolinks`         | Extended URL                         | Activatable `DocumentLink`                                       |
| `WikiLinks`         | `[[target]]` / `[[target \| label]]` | `DocumentLink` preserving target, heading, or block suffix       |
| `Callouts`          | `> [!NOTE] Title`                    | Typed, semantically colored `DocumentCallout` with nested blocks |
| `RadioLists`        | `- ( )` / `- (x)`                    | Genuine `RadioButton`; consecutive items share one stable group  |

`GitHubFlavored` combines strikethrough, tables, task lists, and extended
autolinks. `All` enables every extension. Enabling one individual flag never
implicitly enables another.

Strikethrough accepts matching runs of exactly one or two tildes. Whitespace
cannot sit immediately inside either delimiter, and runs of three or more tildes
remain literal rather than being partially consumed.

Extended autolinks recognize `http://` and `https://` URLs, `www.` domains, and
email addresses. A `www.` link receives an `http://` target and an email address
receives a `mailto:` target. URL hosts require valid dotted domain labels;
unfinished prefixes, intraword URL prefixes, and incomplete email domains remain
literal. Closing punctuation and emphasis or strikethrough delimiters are not
absorbed into the target, while balanced URL parentheses remain part of it.

Tables follow the
[GFM 0.29 delimiter-row grammar](https://github.github.com/gfm/#tables-extension-):
each delimiter cell contains one or more hyphens, with at most one optional
colon on either edge for alignment. One-, two-, and longer-hyphen cells are
equivalent structurally, and the header and delimiter rows must contain the same
number of cells. The Showcase uses longer delimiters for readability, not as a
parser requirement.

GFM table bodies accept rows without pipes and fill missing trailing cells with
empty content. A blank line or any recognized block start ends the table, even
when that block's source line contains a pipe.

A pipe separates cells even when it appears between backticks; only `\|`
protects it as cell content. The table splitter removes that escape before
inline parsing, so an escaped pipe inside a code span becomes literal `|`, while
an unmatched backtick cannot swallow later column boundaries.

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

Task markers follow GFM's ASCII-whitespace rule: an unchecked marker may contain
an ASCII whitespace character between its brackets, and at least one ASCII
whitespace character must separate the closing bracket from the label. All such
separator whitespace is structural and omitted from the generated `CheckBox`
text. The empty-caption checkbox is the first inline in the item paragraph,
followed by a visual gap and the label parsed through the ordinary Markdown
inline grammar. Emphasis, code spans, links, and escapes therefore keep the same
semantics as a non-task list item; Unicode whitespace such as a non-breaking
space remains literal.

Those generated labels participate in the owning `Document`'s continuous
semantic selection like directly embedded controls. Copy output preserves their
displayed text but omits checkbox, radio, table-border, quote-bar, and callout
chrome. The complete stream normalization, pointer/keyboard selection, and
clipboard rules belong to the
[Document control](../controls/collections/document.md#selection-and-copying).

## Expected behavior

| Scope              | Observable evidence                                                               |
| ------------------ | --------------------------------------------------------------------------------- |
| Format abstraction | All detached roots transfer atomically through `Document.Load`.                   |
| Baseline parsing   | Representative blocks and nested semantic inline nodes preserve source order.     |
| Extensions         | Each flag changes only its syntax family; disabled syntax remains literal.        |
| Forms              | Parsed task and radio items are retained controls with ordinary input behavior.   |
| Bounds             | Oversized input is rejected before parsing beyond the configured character limit. |
