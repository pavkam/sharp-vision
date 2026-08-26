# Syntax highlighting

## Overview

`SharpVision.SyntaxHighlighting` is an independent C# reimplementation of the
[Kate](https://kate-editor.org)/KSyntaxHighlighting context-switching
highlighting engine against the public
[KDE syntax-definition XML format](https://docs.kde.org/?application=katepart&branch=stable5&path=highlight.html).
`SyntaxDefinitionReader` parses one definition file, `SyntaxGrammar.Compile`
resolves it (including any cross-definition reference) into a runtime grammar,
and `SyntaxTokenizer.Tokenize` runs that grammar's context-switching state
machine over a complete source string. KDE regular expressions execute through
PCRE2's UTF-16 engine, matching Qt's `QRegularExpression` dialect rather than
.NET's different regular-expression syntax. `CodeView` is the only consumer most
applications need; the engine types below exist as public API for anything that
needs raw tokens or fold ranges without a control attached.

Install the optional package alongside SharpVision:

```bash
dotnet add package SharpVision.SyntaxHighlighting
```

```csharp
var grammar = SyntaxDefinitionCatalog.Default.GetGrammar("Rust");
var result = SyntaxTokenizer.Tokenize(grammar, sourceText);

foreach (var token in result.Lines[0].Tokens)
{
    Console.WriteLine($"{token.Style}: {sourceText[token.Start..(token.Start + token.Length)]}");
}
```

## Engine architecture

| Type                      | Role                                                                                                   |
| ------------------------- | ------------------------------------------------------------------------------------------------------ |
| `SyntaxDefinition`        | The raw parsed shape of one XML file: metadata, keyword lists, item-data roles, and contexts.          |
| `SyntaxDefinitionReader`  | Parses one XML document into a `SyntaxDefinition`, failing fast on the first structural problem.       |
| `SyntaxGrammar`           | One definition after compilation: every `IncludeRules` spliced away, every context switch resolved.    |
| `SyntaxTokenizer`         | Runs the context-stack state machine over a complete document, once, top to bottom.                    |
| `SyntaxHighlightResult`   | One document's tokenized lines and detected fold ranges.                                               |
| `SyntaxDefinitionCatalog` | Lazy, hash-verified access to a named collection of definitions; resolves cross-definition references. |

Unlike an editor, which re-highlights only the lines a keystroke changed and
therefore needs a serializable per-line state to resume from, `CodeView`
displays immutable, already-complete text: the whole document is tokenized in
one pass whenever `Code` or `Language` changes, and the context stack lives as
an ordinary local list for that one pass rather than a reusable state object.
This is a deliberate simplification for a read-only display, not a limitation of
the underlying algorithm.

## Supported format surface

Every definition must declare its minimum engine format through the required
`kateversion` attribute. `SyntaxDefinition.KateVersion` preserves that value,
and the reader accepts format versions through 6.22; a missing, malformed, or
newer value fails before a directory catalog can publish the definition. This
keeps ignored future attributes from masquerading as compatible syntax.

The reader and grammar compiler support every rule element the schema defines:
`keyword`, `Float`, `HlCOct`, `HlCHex`, `Int`, `DetectChar`, `Detect2Chars`,
`AnyChar`, `StringDetect`, `WordDetect`, `RegExpr`, `LineContinue`,
`HlCStringChar`, `RangeDetect`, `HlCChar`, `IncludeRules`, `DetectSpaces`, and
`DetectIdentifier`; the full context-switch mini-language (`#stay`, `#pop`,
`#pop!Name`, multi-context `#pop!A!B`, and cross-definition
`Name##OtherLanguage` references); dynamic `%1`-`%9` context arguments captured
from a `RegExpr` match and consumed by a `dynamic="true"` rule in the context it
pushes (the mechanism heredocs and here-strings rely on); keyword lists with
per-rule case-sensitivity and delimiter overrides, including a cross-definition
`<include>ListName##OtherLanguage</include>`; and both `beginRegion`/`endRegion`
region folding and indentation-based folding. Named end markers close the most
recent open region with the same name, so differently named regions may
interleave without corrupting either fold range. Every rule's matching
algorithm - including the less obvious ones, such as `WordDetect`'s boundary
check or the escape-sequence grammar `HlCStringChar` and `HlCChar` share - was
verified line-for-line against the upstream KSyntaxHighlighting C++ source, not
inferred from the XML format documentation alone.

Indentation folding measures tabs as one cell, matching `CodeView` rendering. If
malformed look-ahead or fallthrough rules revisit one offset without consuming
text, tokenization follows at most 1,024 context transitions before styling the
remaining suffix with the active context; every non-empty output line therefore
remains completely tiled by tokens.

A catalog owns one compilation session, so a grammar reached through a
cross-definition reference is the same immutable instance returned by a direct
`GetGrammar` lookup. Concurrent first lookups also share one parse and one
compilation; failed lazy loads are removed so a later corrected resource can be
retried.

Parsed definitions, compiled grammars, and highlight results expose owned
read-only collection snapshots. Callers cannot cast those properties back to the
parser/compiler/tokenizer's mutable lists, dictionaries, or arrays. Public
syntax value structs define usable empty defaults; a resolved context target
entry remains a reference type because no valid target exists without a grammar.

A cross-definition reference (`IncludeRules`, a context switch, or a keyword
`<include>`) that names a definition or context the current
`SyntaxDefinitionCatalog` cannot resolve degrades to contributing nothing for
that one reference, the same graceful behavior upstream KSyntaxHighlighting
applies (a logged warning, not a load failure) - one missing embedded-language
definition never breaks highlighting of everything else in a document.

`RegExpr` supports PCRE2 constructs used by the shipped KDE corpus, including
possessive quantifiers, subroutine and back references, branch-reset groups,
quoted literals, and POSIX character classes. Its `minimal` attribute maps to
PCRE2's inverted-greediness option. Static expressions compile with their
grammar; invalid third-party patterns degrade to never matching.

Every match has bounded backtracking, nesting, and heap budgets. Exhausting a
budget suppresses that effective rule for the rest of the current line, so one
pathological expression cannot repay the same bound at every UTF-16 offset.
Capture-substituted dynamic expressions retain a 64-entry least-recently-used
working set per rule; distinct heredoc delimiters and similar user-controlled
captures therefore cannot create process-lifetime cache growth. Indentation
folding's `emptyLine` expressions use the same dialect and budgets.

Keyword candidates are matched directly from their source spans without
allocating temporary strings. A successful grouped regular expression only
materializes capture values when the resolved target context consumes them
through a dynamic rule, including a rule spliced through `IncludeRules`.

## The embedded catalog and licensing

`SyntaxDefinitionCatalog.Default` embeds 160 syntax definitions. 159 come from
[`KDE/syntax-highlighting`](https://github.com/KDE/syntax-highlighting), audited
to include only files whose own declared license is unambiguously permissive
(MIT, BSD-3-Clause, CC0-1.0, Zlib, or an explicit Public Domain dedication).
Roughly 250 upstream definitions - including C, C#, Python, PHP, Lua, MATLAB,
Objective-C, Pascal, and JSON - carry no stated license, an empty one, an
ambiguous bare `"BSD"` value, or a copyleft license, and are not redistributed
by this package. See the `SharpVision.SyntaxHighlighting` package's own
`THIRD-PARTY-NOTICES.md` for the complete per-file list and
`extern/kde-syntax-highlighting/README.md` for the full audit methodology.

The 160th, C#, is a first-party definition original to SharpVision itself rather
than redistributed from upstream: upstream's own C# definition carries no stated
license at all and cannot be redistributed, so SharpVision wrote its own from
scratch against the C# grammar directly, released under this project's own MIT
license. `SyntaxDefinitionInfo.SourceCommit` is empty for this one entry - every
other entry's is a real 40-character upstream commit hash - since there is no
external commit for a first-party definition to pin.

`SyntaxDefinitionCatalog.FromDirectory` loads any KDE-format XML file from an
application's own files, mirroring upstream Kate's own local-file pickup model -
including one of the excluded definitions above, which an application remains
free to add on its own without this package redistributing it. Directory files
are parsed once from bounded streams during catalog construction; the catalog
retains only their immutable definitions, not a second complete XML string. When
several extension globs match, the greatest KDE `priority` wins, with the
ordinal language name as a deterministic tie-break.

## Theming

Every token is colored purely by its Kate default-style role
(`SyntaxDefaultStyle`, matching the format's `dsNormal`, `dsKeyword`,
`dsString`, and so on) resolved against `CodeViewStyle`. A syntax definition's
own optional literal `color`/`bold`/`italic` hints are intentionally never read,
so a theme swap restyles every embedded and external definition consistently.
Each of the 31 roles defaults to one of SharpVision's existing `SemanticColor`
values rather than a new syntax-specific one: adding global theme roles just for
source-code tokens would ripple into every built-in theme's own required color
set well beyond this optional package's boundary. A theme, or a control
instance's own local `Style`, can still repaint any individual role directly.
