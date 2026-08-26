# SharpVision.SyntaxHighlighting

`SharpVision.SyntaxHighlighting` is the optional syntax-highlighted code display
for [SharpVision](https://github.com/pavkam/sharp-vision). It is an independent
C# reimplementation of the [Kate](https://kate-editor.org)/KSyntaxHighlighting
context-switching highlighting engine against the public
[KDE syntax-definition XML format](https://docs.kde.org/?application=katepart&branch=stable5&path=highlight.html),
verified line-for-line against the upstream C++ engine's own source rather than
against the format documentation alone.

```csharp
var view = new CodeView
{
    Language = "Rust",
    Code = """
        fn main() {
            println!("Hello, SharpVision!");
        }
        """,
};
```

`CodeView` is read-only - there is no editing API at all - but its content is
fully selectable and copyable by mouse or keyboard, exactly like a terminal
pager:

```csharp
var view = new CodeView { Code = "let x = 1;" };
view.SelectAll();
var clipboardText = view.CopySelection(); // a pure read; the host wires the real clipboard
```

Regions delimited by a language's `beginRegion`/`endRegion` rules (braces,
blocks, multi-line comments, and similar) can be collapsed:

```csharp
var view = new CodeView { Code = "fn f() {\n    g();\n}", Language = "Rust" };
var firstFoldableLine = Enumerable.Range(0, 3).First(line => view.IsFoldStart(line));
view.ToggleFold(firstFoldableLine);
```

Every token is colored purely by its Kate default-style role (`dsKeyword`,
`dsString`, `dsComment`, and so on) resolved against `CodeViewStyle`, the same
semantic-color-first theming every other SharpVision control uses - a syntax
definition's own optional literal color hints are never read, so a theme swap
restyles every language consistently. See `docs/concepts/syntax-highlighting.md`
in the main repository for the full architecture, and
`docs/controls/display/code-view.md` for the complete control reference.

## Loading additional languages

The embedded `SyntaxDefinitionCatalog.Default` ships 160 permissively licensed
syntax definitions - see [Attribution](#attribution) below for exactly which
ones and why. Any other KDE-format XML file, including one of the definitions
this package does not embed, can be loaded from an application's own files:

```csharp
var catalog = SyntaxDefinitionCatalog.FromDirectory("/path/to/syntax/definitions");
var view = new CodeView { Catalog = catalog, Language = "Python" };
```

This mirrors upstream Kate's own local-file pickup model for syntax definitions.
Definitions must declare `kateversion`; SharpVision accepts format versions
through 6.22 and rejects newer definitions before adding them to the catalog.

## Attribution

159 of the 160 embedded syntax-definition XML files under `Resources/Syntax/`
are copied byte-for-byte from
[`KDE/syntax-highlighting`](https://github.com/KDE/syntax-highlighting), each
one included only because its own declared license is unambiguously permissive
(MIT, BSD-3-Clause, CC0-1.0, Zlib, or an explicit Public Domain dedication). See
`THIRD-PARTY-NOTICES.md` for the complete per-file list grouped by license, and
[`extern/kde-syntax-highlighting/README.md`](https://github.com/pavkam/sharp-vision/blob/main/extern/kde-syntax-highlighting/README.md)
in the main repository for the full audit methodology, including why roughly 250
upstream definitions - among them C, Python, PHP, Lua, MATLAB, Objective-C,
Pascal, and JSON - are not redistributed by this package. Full license texts
ship in the package's `licenses/` directory.

The 160th, `csharp.xml` (C#), is first-party SharpVision source code, not
third-party: upstream's own C# definition carries no stated license and cannot
be redistributed, so this one was written from scratch against the C# grammar
directly and is licensed under this repository's own root `LICENSE`, the same as
every other file in this project.

No C++ source code from `KDE/syntax-highlighting` is compiled into or
redistributed by this package; only the third-party data files identified above
are.
