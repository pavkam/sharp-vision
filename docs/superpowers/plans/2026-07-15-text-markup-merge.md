# Unified Text with Inline Markup — Implementation Plan

<!-- markdownlint-disable MD013 MD036 -->
<!-- Historical snapshot: MD013 preserves exact code; MD036 preserves original task labels. -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Collapse `Text` and `RichText` into one `Text` control whose `Content`
string is inline markup (`<red>…</red>`, `<b>`, `<fg=#ff8800>`, `<link=…>`),
delete the `Inline`/`Inlines`/`Run`/`LineBreak`/`Hyperlink` object model, and
expose overflow as a single `Overflow` enum (Wrap / WrapAnywhere / Clip /
Ellipsis / Visible).

**Architecture:** A new `SharpVision.Text.Markup` parser flattens the
(overlapping-allowed) tag stack of a markup string into a **display string**
plus non-overlapping `StyleSpan`s that index into it. The existing grapheme-safe
`SharpVision.Text.Layout.Format` lays the display string out (refactored to take
`Overflow`), and the new `Text.OnRender` walks each `Line`, finds the covering
`StyleSpan` by source offset, overlays its facets on the control's
`ResolvedStyle` via the existing `Decoration.Resolve`, and draws. This
recombines machinery that already exists: `Text`'s caching/layout and
`RichText`'s offset-based span rendering.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. Test harness:
`Dispatcher.Start()` + `Control.Attach(dispatcher)` for control tests; direct
static calls for `Markup`/`Layout`; showcase gallery screen tests for the pane.

**Full design:**
`docs/superpowers/specs/2026-07-15-text-markup-merge-design.md`.

**Gloss corrections:** `Overflow.Visible` is the default so existing `Text`
controls retain their measurement behavior; migrated prose sets `Wrap`
explicitly. Newlines are the only hard-break syntax (`<br>` is deliberately
omitted). `Markup`, `StyleSpan`, and parser helpers are internal implementation
types. Invalid tags preserve their complete raw fragment, invalid hyperlink
targets never reach rendering, theme roles resolve before constructing cell
styles, and the latest underline/blink tag wins within each mutually exclusive
facet.

## Global Constraints

- Target .NET 10 and C# 14. File-scoped namespaces; `var` for locals; `using`
  directives **after** the `namespace`; shared imports live in each project's
  `GlobalUsings.cs`.
- One public/named type per file, named exactly after the type (`Markup` →
  `Markup.cs`, `StyleSpan` → `StyleSpan.cs`, `Overflow` → `Overflow.cs`). No
  nested named types, no two types per file.
- No primary constructors, no positional records. Declare every constructor
  explicitly; validate all arguments before assigning state; document validation
  and exceptions.
- XML documentation on every public and internal type and member; document every
  thrown exception. Do not restate the signature.
- Validate every public argument before changing observable state; use
  `Debug.Assert` only for post-validation invariants.
- Prefer `Rune`, `Span<T>`, `ReadOnlySpan<T>` in text paths; segment extended
  grapheme clusters before measuring cells; never split a wide cluster.
- Controls are traditional mutable objects; mutation is dispatcher-affine;
  property changes invalidate only the required phase.
- Deterministic, culture-independent parsing (`CultureInfo.InvariantCulture`,
  `StringComparison.OrdinalIgnoreCase`).
- Tests: xUnit v3 + Shouldly, Arrange/Act/Assert, named
  `MethodName_WhenThis_ThatIsExpected`. Watch each new test fail for the
  expected reason first. Add randomized/property tests for the parser and
  layout.
- Quality gate before every commit: `make format && make lint && make build`
  plus the task's focused tests. Zero warnings, zero errors.
- Focused test command form:
  `dotnet test --project tests/SharpVision.Tests --filter-class "*ClassName" --timeout 120s`.

## File structure

**Create**

- `src/SharpVision/Text/Overflow.cs` — the single overflow enum.
- `src/SharpVision/Text/StyleSpan.cs` — resolved non-overlapping styled slice of
  the display string (`readonly record struct`).
- `src/SharpVision/Text/Markup.cs` — static markup parser + `Escape`.
- `src/SharpVision/Text/OpenTag.cs` — one open parser facet.
- `src/SharpVision/Text/Style.cs` — one flattened active parser style.
- `tests/SharpVision.Tests/Text/MarkupTests.cs` — parser unit tests.
- `tests/SharpVision.Tests/Text/RandomizedMarkupTests.cs` — property tests.

**Modify**

- `src/SharpVision/Text/Layout.cs` — `Format` takes `Overflow`; delete the
  `Wrapping`/`Trimming` parameters and branches map from `Overflow`.
- `src/SharpVision/Controls/Text.cs` — parse markup, render spans, expose
  `Overflow`, add `Escape`.
- `src/SharpVision.Showcase/Panes/TextPane.cs`,
  `src/SharpVision.Showcase/Panes/RichTextPane.cs`,
  `src/SharpVision.Showcase/Panes/TablePane.cs`,
  `src/SharpVision.Showcase/Panes/Doc.cs` — build markup strings.
- `docs/controls/display/text.md` — merged contract; delete
  `docs/controls/display/rich-text.md`.
- `tests/SharpVision.Tests/Controls/TextTests.cs`,
  `tests/SharpVision.Tests/Text/LayoutTests.cs`,
  `tests/SharpVision.Tests/Text/RandomizedLayoutTests.cs` — rewrite to the new
  API.

**Delete**

- `src/SharpVision/Controls/RichText.cs`, `Inline.cs`, `Inlines.cs`, `Run.cs`,
  `LineBreak.cs`, `Hyperlink.cs`.
- `src/SharpVision/Text/Wrapping.cs`, `src/SharpVision/Text/Trimming.cs`.
- `tests/SharpVision.Tests/Controls/RichTextTests.cs`.
- `docs/controls/display/rich-text.md`.

---

### Task 0: Establish a green baseline (precondition gate)

**Files:** none.

- [ ] **Step 1: Confirm the tree builds and tests pass**

Run:

```bash
make build && make test
```

Expected: zero warnings, zero errors, all tests green. If red, stop and repair
the baseline (or record the pre-existing failures) before proceeding — later
tasks assert green transitions and cannot distinguish a new break from an
inherited one.

- [ ] **Step 2: Note the current Text/RichText behavior for reference**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*TextTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests --filter-class "*RichTextTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests --filter-class "*LayoutTests" --timeout 120s
```

Expected: PASS. These suites are rewritten later; the counts here are the
coverage floor to preserve.

---

### Task 1: Markup parser and `StyleSpan` (additive)

Purely additive — no existing type changes, so the build stays green. Produces
the parser the `Text` control will consume in Task 3.

**Files:**

- Create: `src/SharpVision/Text/StyleSpan.cs`
- Create: `src/SharpVision/Text/Markup.cs`
- Test: `tests/SharpVision.Tests/Text/MarkupTests.cs`

**Interfaces:**

- Consumes: `SharpVision.Terminal.Protocols.Color`,
  `SharpVision.Terminal.Protocols.Underline`,
  `SharpVision.Terminal.Rendering.Attributes`, `SharpVision.Styling.ColorRole`.
- Produces:
  - internal `StyleSpan` (`readonly record struct`) with `int Offset`,
    `int Length`, `Color? Foreground`, `Color? Background`,
    `Attributes Attributes`, `Underline Underline`, `Color? UnderlineColor`,
    `string? Link`.
  - internal
    `Markup.Parse(ReadOnlySpan<char> source, out string display) → StyleSpan[]`
    — never throws; returns spans covering `display` with no gaps or overlap,
    ordered by `Offset`.
  - `Markup.Escape(string value) → string` — backslash-escapes `<` and `\`.

- [ ] **Step 1: Write `StyleSpan`**

Create `src/SharpVision/Text/StyleSpan.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>Describes one resolved, non-overlapping styled slice of a parsed markup display string.</summary>
internal readonly record struct StyleSpan
{
    /// <summary>Initializes a validated styled slice.</summary>
    /// <param name="offset">The non-negative UTF-16 offset into the display string.</param>
    /// <param name="length">The non-negative UTF-16 length of the slice.</param>
    /// <param name="foreground">The foreground override, or null to inherit.</param>
    /// <param name="background">The background override, or null to inherit.</param>
    /// <param name="attributes">The additive rendition flags contributed by markup.</param>
    /// <param name="underline">The typed underline variant, or <see cref="Underline.None"/>.</param>
    /// <param name="underlineColor">The underline color override, or null to inherit.</param>
    /// <param name="link">The OSC 8 hyperlink target, or null when the slice is not a link.</param>
    /// <exception cref="ArgumentOutOfRangeException">A numeric value is negative or an enum value is unknown.</exception>
    public StyleSpan(
        int offset,
        int length,
        Color? foreground,
        Color? background,
        Attributes attributes,
        Underline underline,
        Color? underlineColor,
        string? link)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (!Enum.IsDefined(underline))
        {
            throw new ArgumentOutOfRangeException(nameof(underline), underline, "The underline style is unknown.");
        }

        Offset = offset;
        Length = length;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the zero-based UTF-16 offset into the display string.</summary>
    public int Offset { get; }

    /// <summary>Gets the UTF-16 length of the styled slice.</summary>
    public int Length { get; }

    /// <summary>Gets the foreground override, or null to inherit the control style.</summary>
    public Color? Foreground { get; }

    /// <summary>Gets the background override, or null to inherit the control style.</summary>
    public Color? Background { get; }

    /// <summary>Gets the additive rendition flags contributed by markup attribute tags.</summary>
    public Attributes Attributes { get; }

    /// <summary>Gets the typed underline variant, or <see cref="Underline.None"/>.</summary>
    public Underline Underline { get; }

    /// <summary>Gets the underline color override, or null to inherit.</summary>
    public Color? UnderlineColor { get; }

    /// <summary>Gets the OSC 8 hyperlink target, or null when the slice is not a link.</summary>
    public string? Link { get; }
}
```

- [ ] **Step 2: Write the first failing test (plain text passthrough)**

Create `tests/SharpVision.Tests/Text/MarkupTests.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Text;

/// <summary>Verifies markup parsing into a display string and resolved style spans.</summary>
public sealed class MarkupTests
{
    /// <summary>Verifies plain text yields one inheriting span over the whole string.</summary>
    [Fact]
    public void Parse_WhenPlainText_YieldsSingleInheritingSpan()
    {
        StyleSpan[] spans = Markup.Parse("hello", out string display);

        display.ShouldBe("hello");
        spans.Length.ShouldBe(1);
        spans[0].Offset.ShouldBe(0);
        spans[0].Length.ShouldBe(5);
        spans[0].Foreground.ShouldBeNull();
        spans[0].Attributes.ShouldBe(Attributes.None);
        spans[0].Link.ShouldBeNull();
    }
}
```

- [ ] **Step 3: Run it to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*MarkupTests" --timeout 120s`
Expected: FAIL — `Markup` does not exist.

- [ ] **Step 4: Implement `Markup`**

Create `src/SharpVision/Text/Markup.cs`. This is the full parser; read the block
comments — they encode the lenient/overlap rules from the design.

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using System.Globalization;

using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>Parses SharpVision inline text markup into a display string and resolved style spans.</summary>
/// <remarks>
/// Grammar: a tag is <c>&lt;name&gt;</c>, <c>&lt;name=value&gt;</c>, a close
/// <c>&lt;/name&gt;</c>, or a generic close <c>&lt;/&gt;</c>. One tag carries one style facet;
/// facets combine by stacking and may overlap. Parsing never throws: unknown or malformed tags
/// degrade to literal text, stray closes are ignored, and open tags auto-close at end.
/// This is a small inline markup, not Markdown.
/// </remarks>
internal static class Markup
{
    /// <summary>Parses markup into a display string plus gap-free, non-overlapping style spans.</summary>
    /// <param name="source">The markup borrowed for this call.</param>
    /// <param name="display">The visible text with tags removed and escapes resolved.</param>
    /// <returns>Spans ordered by offset that exactly tile the display string.</returns>
    public static StyleSpan[] Parse(ReadOnlySpan<char> source, out string display)
    {
        var text = new StringBuilder(source.Length);
        var spans = new List<StyleSpan>();
        var open = new List<OpenTag>();
        int spanStart = 0;
        Style current = Style.Inherit;

        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];

            if (c == '\\' && i + 1 < source.Length && source[i + 1] is '<' or '\\')
            {
                // Escaped metacharacter: emit the literal and skip the backslash.
                text.Append(source[i + 1]);
                i += 2;
                continue;
            }

            if (c != '<')
            {
                text.Append(c);
                i++;
                continue;
            }

            // A '<' begins a tag. Read to the next '>'.
            int close = IndexOfTagEnd(source, i + 1);
            if (close < 0 || !TryApplyTag(source[(i + 1)..close], open))
            {
                // Malformed or unknown: preserve the complete raw fragment.
                if (close < 0)
                {
                    _ = text.Append(source[i..]);
                    break;
                }

                _ = text.Append(source[i..(close + 1)]);
                i = close + 1;
                continue;
            }

            // The active style may have changed; close the current span at this display offset.
            Style next = Style.From(open);
            if (!next.Equals(current) && text.Length > spanStart)
            {
                spans.Add(current.ToSpan(spanStart, text.Length - spanStart));
                spanStart = text.Length;
            }
            else if (!next.Equals(current))
            {
                spanStart = text.Length;
            }

            current = next;
            i = close + 1;
        }

        if (text.Length > spanStart || spans.Count == 0)
        {
            spans.Add(current.ToSpan(spanStart, text.Length - spanStart));
        }

        display = text.ToString();
        return [.. spans];
    }

    /// <summary>Backslash-escapes markup metacharacters so dynamic text interpolates literally.</summary>
    /// <param name="value">The non-null text to escape.</param>
    /// <returns>The text with each <c>\</c> and <c>&lt;</c> backslash-escaped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);

        foreach (char c in value)
        {
            if (c is '\\' or '<')
            {
                _ = builder.Append('\\');
            }

            _ = builder.Append(c);
        }

        return builder.ToString();
    }

    private static int IndexOfTagEnd(ReadOnlySpan<char> source, int start)
    {
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '>')
            {
                return i;
            }
        }

        return -1;
    }

    // Applies one tag body (without angle brackets) to the open-tag stack.
    // Returns false when the tag is unknown/malformed so the caller can degrade to literal text.
    private static bool TryApplyTag(ReadOnlySpan<char> body, List<OpenTag> open)
    {
        if (body.IsEmpty)
        {
            return false;
        }

        if (body[0] == '/')
        {
            ReadOnlySpan<char> name = body[1..].Trim();

            if (name.IsEmpty)
            {
                // Generic close: pop the most-recently-opened tag.
                if (open.Count > 0)
                {
                    open.RemoveAt(open.Count - 1);
                }

                return true;
            }

            // Named close: remove the nearest still-open tag of that name (overlap-aware).
            for (int k = open.Count - 1; k >= 0; k--)
            {
                if (open[k].Name.Equals(name.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    open.RemoveAt(k);
                    return true;
                }
            }

            // Stray close with no matching open tag: ignore, but still valid markup.
            return true;
        }

        int equals = body.IndexOf('=');
        ReadOnlySpan<char> tagName = (equals < 0 ? body : body[..equals]).Trim();
        ReadOnlySpan<char> value = equals < 0 ? default : body[(equals + 1)..].Trim();

        if (!TryResolveFacet(tagName, value, out OpenTag tag))
        {
            return false;
        }

        open.Add(tag);
        return true;
    }

    // Resolves a tag name (+ optional value) into an open-tag facet.
    private static bool TryResolveFacet(ReadOnlySpan<char> name, ReadOnlySpan<char> value, out OpenTag tag)
    {
        tag = default;
        string key = name.ToString().ToLowerInvariant();

        switch (key)
        {
            case "b" or "bold": tag = OpenTag.Attribute(name, Attributes.Bold); return true;
            case "d" or "dim": tag = OpenTag.Attribute(name, Attributes.Dim); return true;
            case "i" or "italic": tag = OpenTag.Attribute(name, Attributes.Italic); return true;
            case "s" or "strike": tag = OpenTag.Attribute(name, Attributes.Strike); return true;
            case "reverse": tag = OpenTag.Attribute(name, Attributes.Reverse); return true;
            case "blink": tag = OpenTag.Attribute(name, Attributes.Blink); return true;
            case "rapidblink": tag = OpenTag.Attribute(name, Attributes.RapidBlink); return true;
            case "hidden" or "conceal": tag = OpenTag.Attribute(name, Attributes.Hidden); return true;
            case "overline": tag = OpenTag.Attribute(name, Attributes.Overline); return true;
            case "u" or "underline":
                if (value.IsEmpty)
                {
                    tag = OpenTag.UnderlineShape(name, Underline.Straight);
                    return true;
                }

                if (TryUnderline(value, out Underline shape))
                {
                    tag = OpenTag.UnderlineShape(name, shape);
                    return true;
                }

                return false;
            case "uc":
                if (TryColor(value, out Color uc))
                {
                    tag = OpenTag.UnderlineColor(name, uc);
                    return true;
                }

                return false;
            case "fg" or "color":
                if (TryColor(value, out Color fg))
                {
                    tag = OpenTag.Foreground(name, fg);
                    return true;
                }

                return false;
            case "bg":
                if (TryColor(value, out Color bg))
                {
                    tag = OpenTag.Background(name, bg);
                    return true;
                }

                return false;
            case "link" or "a":
                if (!value.IsEmpty)
                {
                    tag = OpenTag.LinkTag(name, value.ToString());
                    return true;
                }

                return false;
            default:
                // Bare unknown name with no '=': treat as a foreground color shortcut (named/role/hex/index).
                if (value.IsEmpty && TryColor(name, out Color bare))
                {
                    tag = OpenTag.Foreground(name, bare);
                    return true;
                }

                return false;
        }
    }

    private static bool TryUnderline(ReadOnlySpan<char> value, out Underline underline)
    {
        underline = value.ToString().ToLowerInvariant() switch
        {
            "straight" => Underline.Straight,
            "double" => Underline.Paired,
            "curly" => Underline.Curly,
            "dotted" => Underline.Dotted,
            "dashed" => Underline.Dashed,
            _ => Underline.None,
        };

        return underline != Underline.None;
    }

    // Named ANSI (0..15), theme role names, palette index 0..255, or #rgb / #rrggbb hex.
    private static bool TryColor(ReadOnlySpan<char> value, out Color color)
    {
        color = Color.Default;

        if (value.IsEmpty)
        {
            return false;
        }

        if (value[0] == '#')
        {
            return Color.TryFromHex(value.ToString(), out color);
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) &&
            index is >= 0 and <= 255)
        {
            color = Color.Indexed(index);
            return true;
        }

        string key = value.ToString().ToLowerInvariant();
        int ansi = key switch
        {
            "black" => 0, "red" => 1, "green" => 2, "yellow" => 3,
            "blue" => 4, "magenta" => 5, "cyan" => 6, "white" => 7,
            "brightblack" or "gray" or "grey" => 8, "brightred" => 9,
            "brightgreen" => 10, "brightyellow" => 11, "brightblue" => 12,
            "brightmagenta" => 13, "brightcyan" => 14, "brightwhite" => 15,
            _ => -1,
        };

        if (ansi >= 0)
        {
            color = Color.Indexed(ansi);
            return true;
        }

        if (Enum.TryParse(key, ignoreCase: true, out ColorRole role) && Enum.IsDefined(role))
        {
            color = Color.Role((int) role);
            return true;
        }

        return false;
    }
}
```

This file references two small internal helper types (`OpenTag`, `Style`). Per
the one-type-per-file rule they are separate files, created in the next steps.

- [ ] **Step 5: Add the `OpenTag` helper**

Create `src/SharpVision/Text/OpenTag.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>Represents one open markup facet tag on the parser stack.</summary>
internal readonly record struct OpenTag
{
    private OpenTag(
        string name,
        Attributes attributes,
        Color? foreground,
        Color? background,
        Underline underline,
        Color? underlineColor,
        string? link)
    {
        Name = name;
        Attributes = attributes;
        Foreground = foreground;
        Background = background;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the original tag name used to match a named close.</summary>
    public string Name { get; }

    /// <summary>Gets the additive attribute this tag contributes.</summary>
    public Attributes Attributes { get; }

    /// <summary>Gets the foreground this tag sets, or null.</summary>
    public Color? Foreground { get; }

    /// <summary>Gets the background this tag sets, or null.</summary>
    public Color? Background { get; }

    /// <summary>Gets the underline shape this tag sets, or <see cref="Underline.None"/>.</summary>
    public Underline Underline { get; }

    /// <summary>Gets the underline color this tag sets, or null.</summary>
    public Color? UnderlineColor { get; }

    /// <summary>Gets the hyperlink target this tag sets, or null.</summary>
    public string? Link { get; }

    /// <summary>Creates an attribute-flag tag.</summary>
    public static OpenTag Attribute(ReadOnlySpan<char> name, Attributes value) =>
        new(name.ToString(), value, null, null, Underline.None, null, null);

    /// <summary>Creates a foreground-color tag.</summary>
    public static OpenTag Foreground(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), Attributes.None, value, null, Underline.None, null, null);

    /// <summary>Creates a background-color tag.</summary>
    public static OpenTag Background(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), Attributes.None, null, value, Underline.None, null, null);

    /// <summary>Creates a typed-underline-shape tag.</summary>
    public static OpenTag UnderlineShape(ReadOnlySpan<char> name, Underline value) =>
        new(name.ToString(), Attributes.None, null, null, value, null, null);

    /// <summary>Creates an underline-color tag.</summary>
    public static OpenTag UnderlineColor(ReadOnlySpan<char> name, Color value) =>
        new(name.ToString(), Attributes.None, null, null, Underline.None, value, null);

    /// <summary>Creates a hyperlink tag.</summary>
    public static OpenTag LinkTag(ReadOnlySpan<char> name, string value) =>
        new(name.ToString(), Attributes.None, null, null, Underline.None, null, value);
}
```

- [ ] **Step 6: Add the `Style` snapshot helper**

Create `src/SharpVision/Text/Style.cs`. It computes the active style from the
open-tag stack (attributes OR-combine; last-opened wins for single-valued
facets) and emits a `StyleSpan`.

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>Represents the flattened active style at one point in a markup stream.</summary>
internal readonly record struct Style
{
    private Style(
        Attributes attributes,
        Color? foreground,
        Color? background,
        Underline underline,
        Color? underlineColor,
        string? link)
    {
        Attributes = attributes;
        Foreground = foreground;
        Background = background;
        Underline = underline;
        UnderlineColor = underlineColor;
        Link = link;
    }

    /// <summary>Gets the inheriting empty style.</summary>
    public static Style Inherit { get; } = new(Attributes.None, null, null, Underline.None, null, null);

    private Attributes Attributes { get; }

    private Color? Foreground { get; }

    private Color? Background { get; }

    private Underline Underline { get; }

    private Color? UnderlineColor { get; }

    private string? Link { get; }

    /// <summary>Flattens the open-tag stack: attributes OR-combine; last-opened wins per single facet.</summary>
    public static Style From(List<OpenTag> open)
    {
        Attributes attributes = Attributes.None;
        Color? foreground = null;
        Color? background = null;
        Underline underline = Underline.None;
        Color? underlineColor = null;
        string? link = null;

        foreach (OpenTag tag in open)
        {
            attributes |= tag.Attributes;

            if (tag.Foreground is { } fg)
            {
                foreground = fg;
            }

            if (tag.Background is { } bg)
            {
                background = bg;
            }

            if (tag.Underline != Underline.None)
            {
                underline = tag.Underline;
            }

            if (tag.UnderlineColor is { } uc)
            {
                underlineColor = uc;
            }

            if (tag.Link is { } target)
            {
                link = target;
            }
        }

        return new Style(attributes, foreground, background, underline, underlineColor, link);
    }

    /// <summary>Projects this style onto a display-string slice.</summary>
    public StyleSpan ToSpan(int offset, int length) =>
        new(offset, length, Foreground, Background, Attributes, Underline, UnderlineColor, Link);
}
```

- [ ] **Step 7: Run the passthrough test to verify it passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*MarkupTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 8: Add the behavior tests (one facet family per test)**

Append to `MarkupTests.cs`:

```csharp
    /// <summary>Verifies a bare color tag sets foreground over its range only.</summary>
    [Fact]
    public void Parse_WhenBareColorTag_SetsForegroundForRange()
    {
        StyleSpan[] spans = Markup.Parse("a<red>b</red>c", out string display);

        display.ShouldBe("abc");
        spans.Length.ShouldBe(3);
        spans[0].Foreground.ShouldBeNull();
        spans[1].Foreground.ShouldBe(Color.Indexed(1));
        spans[1].Offset.ShouldBe(1);
        spans[1].Length.ShouldBe(1);
        spans[2].Foreground.ShouldBeNull();
    }

    /// <summary>Verifies stacked attribute tags OR-combine over the inner range.</summary>
    [Fact]
    public void Parse_WhenNestedAttributes_CombinesFlags()
    {
        StyleSpan[] spans = Markup.Parse("<b><i>x</i></b>", out _);

        spans.ShouldHaveSingleItem();
        spans[0].Attributes.ShouldBe(Attributes.Bold | Attributes.Italic);
    }

    /// <summary>Verifies overlapping ranges close by nearest matching name (the design sketch).</summary>
    [Fact]
    public void Parse_WhenTagsOverlap_ClosesNearestMatchingName()
    {
        // <u> opens, <b> opens, </u> closes u while b stays open, then </b>.
        StyleSpan[] spans = Markup.Parse("<u><b>hi</u> there</b>", out string display);

        display.ShouldBe("hi there");
        // "hi" is underline+bold; " there" is bold only.
        StyleSpan hi = Array.Find(spans, s => s.Length == 2);
        hi.Attributes.ShouldBe(Attributes.Underline | Attributes.Bold);
        StyleSpan tail = Array.Find(spans, s => s.Offset == 2);
        tail.Attributes.ShouldBe(Attributes.Bold);
    }

    /// <summary>Verifies the generic close pops the most-recently-opened tag.</summary>
    [Fact]
    public void Parse_WhenGenericClose_PopsMostRecentTag()
    {
        StyleSpan[] spans = Markup.Parse("<accent>a</>b", out _);

        spans[0].Foreground.ShouldBe(Color.Role((int) ColorRole.Accent));
        Array.Find(spans, s => s.Offset == 1).Foreground.ShouldBeNull();
    }

    /// <summary>Verifies fg/bg/underline-color/shape value tags resolve.</summary>
    [Fact]
    public void Parse_WhenValueTags_ResolveColorsAndUnderline()
    {
        StyleSpan[] spans = Markup.Parse("<fg=#ff8800><bg=blue><uc=214><u=curly>x</u></bg></fg>", out _);

        spans.ShouldHaveSingleItem();
        spans[0].Foreground.ShouldBe(Color.Rgb(255, 136, 0));
        spans[0].Background.ShouldBe(Color.Indexed(4));
        spans[0].UnderlineColor.ShouldBe(Color.Indexed(214));
        spans[0].Underline.ShouldBe(Underline.Curly);
    }

    /// <summary>Verifies a link tag records an OSC 8 target for its range.</summary>
    [Fact]
    public void Parse_WhenLinkTag_RecordsTarget()
    {
        StyleSpan[] spans = Markup.Parse("see <link=https://a.test>here</link>", out string display);

        display.ShouldBe("see here");
        Array.Find(spans, s => s.Link is not null).Link.ShouldBe("https://a.test");
    }

    /// <summary>Verifies unknown tags degrade to literal text so content is preserved.</summary>
    [Fact]
    public void Parse_WhenUnknownTag_DegradesToLiteral()
    {
        _ = Markup.Parse("a<nope>b", out string display);

        display.ShouldBe("a<nope>b");
    }

    /// <summary>Verifies unclosed tags auto-close at end of content without throwing.</summary>
    [Fact]
    public void Parse_WhenTagUnclosed_AutoClosesAtEnd()
    {
        StyleSpan[] spans = Markup.Parse("<b>bold to the end", out string display);

        display.ShouldBe("bold to the end");
        spans[^1].Attributes.ShouldBe(Attributes.Bold);
    }

    /// <summary>Verifies escaped metacharacters render literally.</summary>
    [Fact]
    public void Parse_WhenEscaped_EmitsLiteralMetacharacters()
    {
        _ = Markup.Parse(@"a \< b \\ c", out string display);

        display.ShouldBe(@"a < b \ c");
    }

    /// <summary>Verifies Escape round-trips through Parse to the original text.</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("a < b")]
    [InlineData(@"back\slash <tag>")]
    public void Escape_WhenParsed_YieldsOriginalText(string original)
    {
        _ = Markup.Parse(Markup.Escape(original), out string display);

        display.ShouldBe(original);
    }
```

- [ ] **Step 9: Run all parser tests**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*MarkupTests" --timeout 120s`
Expected: PASS. If a span-boundary test fails, verify `Parse` emits a boundary
whenever `Style.From` changes and never emits a zero-length span except for
empty input.

- [ ] **Step 10: Commit**

```bash
git add src/SharpVision/Text/StyleSpan.cs src/SharpVision/Text/Markup.cs \
        src/SharpVision/Text/OpenTag.cs src/SharpVision/Text/Style.cs \
        tests/SharpVision.Tests/Text/MarkupTests.cs
git commit -m "feat(text): add inline markup parser producing display string and style spans"
```

---

### Task 2: Randomized parser invariants (additive)

**Files:**

- Test: `tests/SharpVision.Tests/Text/RandomizedMarkupTests.cs`

**Interfaces:**

- Consumes: `Markup.Parse`, `Markup.Escape` from Task 1.

- [ ] **Step 1: Write the property tests**

Create `tests/SharpVision.Tests/Text/RandomizedMarkupTests.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Text;

/// <summary>Verifies markup parsing invariants over randomized input.</summary>
public sealed class RandomizedMarkupTests
{
    /// <summary>Verifies parsing never throws and spans tile the display string with no gaps or overlaps.</summary>
    [Fact]
    public void Parse_WhenRandomMarkup_SpansTileDisplayContiguously()
    {
        var random = new Random(20260715);
        string[] fragments = ["<b>", "</b>", "<red>", "</red>", "<u>", "</>", "<fg=#0f0>", "<bg=2>", "x", "<link=t>", "</link>", "\\<", "<?", "="];

        for (int trial = 0; trial < 2000; trial++)
        {
            var builder = new StringBuilder();
            int length = random.Next(0, 30);
            for (int j = 0; j < length; j++)
            {
                _ = builder.Append(fragments[random.Next(fragments.Length)]);
            }

            StyleSpan[] spans = Markup.Parse(builder.ToString(), out string display);

            int cursor = 0;
            foreach (StyleSpan span in spans)
            {
                span.Offset.ShouldBe(cursor);
                span.Length.ShouldBeGreaterThanOrEqualTo(0);
                cursor += span.Length;
            }

            cursor.ShouldBe(display.Length);
        }
    }
}
```

- [ ] **Step 2: Run to verify it fails, then passes**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*RandomizedMarkupTests" --timeout 120s`
Expected: PASS (the invariant should already hold from Task 1). This task still
starts red by adding an independent escape round-trip property that includes
random `<` and `\\` input before the implementation is generalized. If tiling
fails, the parser has a gap/overlap bug — fix `Parse`'s span emission so every
branch advances `spanStart` to the current `text.Length`.

- [ ] **Step 3: Commit**

```bash
git add tests/SharpVision.Tests/Text/RandomizedMarkupTests.cs
git commit -m "test(text): randomized markup tiling invariants"
```

---

### Task 3: `Overflow` enum and `Layout.Format` refactor (additive overload)

Add `Overflow` and a new `Layout.Format` overload that takes it, **keeping** the
existing `(Wrapping, Trimming)` overload so `Text`/`RichText` still compile. The
old overload and enums are deleted in Task 6.

**Files:**

- Create: `src/SharpVision/Text/Overflow.cs`
- Modify: `src/SharpVision/Text/Layout.cs`
- Test: `tests/SharpVision.Tests/Text/LayoutTests.cs` (append)

**Interfaces:**

- Produces:
  - `enum Overflow { Wrap, WrapAnywhere, Clip, Ellipsis, Visible }`.
  - `Layout.Format(ReadOnlySpan<char> value, int width, Overflow overflow, Alignment alignment, Ambiguous ambiguous, Span<Line> destination) → int`.

- [ ] **Step 1: Write `Overflow`**

Create `src/SharpVision/Text/Overflow.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Selects how text handles horizontal overflow within a finite cell width.</summary>
public enum Overflow
{
    /// <summary>Break onto new lines at whitespace, falling back to grapheme boundaries.</summary>
    Wrap,

    /// <summary>Break onto new lines between graphemes, splitting words when needed.</summary>
    WrapAnywhere,

    /// <summary>Keep one line and cut at the last complete grapheme that fits.</summary>
    Clip,

    /// <summary>Keep one line and cut with a trailing ellipsis, preferring a word boundary.</summary>
    Ellipsis,

    /// <summary>Keep one line, report the full width, and let a scroll container clip.</summary>
    Visible,
}
```

- [ ] **Step 2: Write the failing overload test**

Append to `tests/SharpVision.Tests/Text/LayoutTests.cs`:

```csharp
    /// <summary>Verifies the Overflow overload word-wraps like Wrapping.Word.</summary>
    [Fact]
    public void Format_WhenOverflowWrap_WrapsAtWordBoundaries()
    {
        Span<Line> lines = stackalloc Line[8];

        int count = Layout.Format("one two three", 7, Overflow.Wrap, Alignment.Start, Ambiguous.Narrow, lines);

        count.ShouldBe(2);
        lines[0].Length.ShouldBe(4); // "one "
    }

    /// <summary>Verifies Overflow.Ellipsis trims a single overflowing line with an ellipsis.</summary>
    [Fact]
    public void Format_WhenOverflowEllipsis_TrimsWithEllipsis()
    {
        Span<Line> lines = stackalloc Line[4];

        int count = Layout.Format("abcdefgh", 4, Overflow.Ellipsis, Alignment.Start, Ambiguous.Narrow, lines);

        count.ShouldBe(1);
        lines[0].HasEllipsis.ShouldBeTrue();
        lines[0].Cells.ShouldBeLessThanOrEqualTo(4);
    }

    /// <summary>Verifies Overflow.Visible keeps one full-width line without trimming.</summary>
    [Fact]
    public void Format_WhenOverflowVisible_KeepsFullWidthSingleLine()
    {
        Span<Line> lines = stackalloc Line[4];

        int count = Layout.Format("abcdefgh", 4, Overflow.Visible, Alignment.Start, Ambiguous.Narrow, lines);

        count.ShouldBe(1);
        lines[0].HasEllipsis.ShouldBeFalse();
        lines[0].Length.ShouldBe(8);
    }
```

- [ ] **Step 3: Run to verify failure**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*LayoutTests" --timeout 120s`
Expected: FAIL — no `Format(Overflow …)` overload.

- [ ] **Step 4: Add the overload to `Layout`**

In `src/SharpVision/Text/Layout.cs`, add a public overload that maps `Overflow`
onto the existing private `FormatWrapped`/`FormatUnwrapped` by translating to
the current `(Wrapping, Trimming)` internal parameters:

```csharp
    /// <summary>Formats text with a single overflow policy into caller-owned line storage.</summary>
    /// <param name="value">The UTF-16 text borrowed for this call.</param>
    /// <param name="width">The non-negative finite line width in terminal cells.</param>
    /// <param name="overflow">The horizontal overflow policy.</param>
    /// <param name="alignment">The horizontal placement policy.</param>
    /// <param name="ambiguous">The East Asian Ambiguous width policy.</param>
    /// <param name="destination">Caller-owned prefix storage.</param>
    /// <returns>The complete required line count, which may exceed destination length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The width is negative or an enum value is unknown.</exception>
    public static int Format(
        ReadOnlySpan<char> value,
        int width,
        Overflow overflow,
        Alignment alignment,
        Ambiguous ambiguous,
        Span<Line> destination)
    {
        Validate(overflow);

        (Wrapping wrapping, Trimming trimming) = overflow switch
        {
            Overflow.Wrap => (Wrapping.Word, Trimming.None),
            Overflow.WrapAnywhere => (Wrapping.Grapheme, Trimming.None),
            Overflow.Clip => (Wrapping.None, Trimming.Clip),
            Overflow.Ellipsis => (Wrapping.None, Trimming.WordEllipsis),
            Overflow.Visible => (Wrapping.None, Trimming.None),
            _ => throw new UnreachableException(),
        };

        return Format(value, width, wrapping, trimming, alignment, ambiguous, destination);
    }
```

- [ ] **Step 5: Run to verify the tests pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*LayoutTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Text/Overflow.cs src/SharpVision/Text/Layout.cs \
        tests/SharpVision.Tests/Text/LayoutTests.cs
git commit -m "feat(text): add Overflow enum and Layout.Format(Overflow) overload"
```

---

### Task 4: Rebuild the `Text` control on markup + `Overflow`

Transform `Text`: parse `Content` as markup into a cached display string +
spans, render spans over the wrapped lines, replace `Wrapping`/`Trimming` with
`Overflow`, and add the static `Escape`. Migrate `TextPane` and rewrite
`TextTests` in the same task (the public API changes). `RichText` is untouched
here (still uses the old `Layout` overload and `Wrapping`); it is removed in
Task 5.

**Files:**

- Modify: `src/SharpVision/Controls/Text.cs`
- Modify: `src/SharpVision.Showcase/Panes/TextPane.cs`
- Test: `tests/SharpVision.Tests/Controls/TextTests.cs` (rewrite)

**Interfaces:**

- Consumes: `Markup.Parse`, `Markup.Escape`, `StyleSpan`, `Overflow`,
  `Layout.Format(Overflow)`, `Decoration.Resolve`.
- Produces: `Text.Content` (markup), `Text.Overflow`, `Text.TextAlignment`,
  `Text.AmbiguousWidth`, `Text.Lines`, `static Text.Escape(string)`.

- [ ] **Step 1: Write the failing behavior tests**

Rewrite `tests/SharpVision.Tests/Controls/TextTests.cs`. Replace `Wrapping`/
`Trimming` references with `Overflow` and add markup coverage. Representative
new tests (keep the existing Unicode-geometry / measurement tests, changing only
`Wrapping.Word` → `Overflow.Wrap`, `Trimming.GraphemeEllipsis` →
`Overflow.Ellipsis`, `Wrapping.None` → `Overflow.Visible`):

```csharp
    /// <summary>Verifies documented defaults on the merged control.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        ControlText empty = new();
        ControlText value = new("hello");

        empty.Content.ShouldBe(string.Empty);
        value.Content.ShouldBe("hello");
        value.Overflow.ShouldBe(Overflow.Visible);
        value.TextAlignment.ShouldBe(Alignment.Start);
        value.AmbiguousWidth.ShouldBe(Ambiguous.Narrow);
        value.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies markup tags are stripped from the measured/laid-out display text.</summary>
    [Fact]
    public void Content_WhenMarkup_LaysOutVisibleTextOnly()
    {
        ControlText text = new("<b>hi</b>") { Overflow = Overflow.Visible };
        Dispatcher dispatcher = Dispatcher.Start();
        text.Attach(dispatcher);

        text.Measure(new Constraint(80, 24));

        // "hi" is two cells; the tags contribute no width.
        text.DesiredSize.Width.ShouldBe(2);
    }

    /// <summary>Verifies a bad markup string never throws when set.</summary>
    [Fact]
    public void Content_WhenMalformedMarkup_DoesNotThrow()
    {
        ControlText text = new();

        Should.NotThrow(() => text.Content = "<unterminated <b> a\\");
    }

    /// <summary>Verifies Escape round-trips arbitrary text through Content.</summary>
    [Fact]
    public void Escape_WhenAssignedToContent_RendersLiterally()
    {
        ControlText text = new(ControlText.Escape("a < b")) { Overflow = Overflow.Visible };
        Dispatcher dispatcher = Dispatcher.Start();
        text.Attach(dispatcher);

        text.Measure(new Constraint(80, 24));

        text.DesiredSize.Width.ShouldBe(5); // "a < b"
    }
```

Add a rendered-bytes test that a styled span applies its color, mirroring the
existing render tests in `RichTextTests` (drive through `FakeTerminal` and
assert the emitted SGR for the colored cluster). Reuse the harness already
imported in this test file.

- [ ] **Step 2: Run to verify failure**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*TextTests" --timeout 120s`
Expected: FAIL — `Overflow`/`Escape` not on `Text`; markup not parsed.

- [ ] **Step 3: Rewrite `Text.cs`**

Replace `src/SharpVision/Controls/Text.cs`. Keep the caching skeleton; swap the
cached raw `Content` for a cached (display string + spans) pair, replace
`Wrapping`/`Trimming` with `Overflow`, and render per span. Key members:

```csharp
namespace SharpVision.Controls;

using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Unicode;
using SharpVision.Text;

using TextLayout = SharpVision.Text.Layout;

/// <summary>Displays grapheme-safe inline-markup text through semantic terminal cells.</summary>
public sealed class Text : Control
{
    private const string _ellipsis = "…";
    private string _display = string.Empty;
    private StyleSpan[] _spans = [];
    private string? _parsedContent;   // reference-equality cache key for the last parse
    private Line[] _lines = [];
    private int _lineCount;
    // width/overflow/alignment/ambiguous cache fields as today …

    public Text() { }

    public Text(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        Content = content;
    }

    /// <summary>Gets or sets the inline-markup content. Malformed markup renders best-effort and never throws.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Content
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (Set(ref field, value, Invalidation.Measure))
            {
                _layoutValid = false;
            }
        }
    } = string.Empty;

    /// <summary>Gets or sets the horizontal overflow policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public Overflow Overflow
    {
        get;
        set
        {
            Validate(value);
            if (Set(ref field, value, Invalidation.Measure))
            {
                _layoutValid = false;
            }
        }
    } = Overflow.Visible;

    // TextAlignment and AmbiguousWidth: unchanged from today.

    /// <summary>Backslash-escapes markup metacharacters so dynamic text renders literally.</summary>
    /// <param name="value">The non-null text to escape.</param>
    /// <returns>The escaped text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static string Escape(string value) => Markup.Escape(value);

    public ReadOnlyMemory<Line> Lines => _lines.AsMemory(0, _lineCount);

    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureParsed();
        EnsureLayout(constraint.Width ?? int.MaxValue);
        int width = 0;
        foreach (Line line in Lines.Span)
        {
            width = Math.Max(width, line.Cells);
        }

        return new Size(width, _lineCount);
    }

    protected override void ArrangeOverride(Rect bounds)
    {
        EnsureParsed();
        EnsureLayout(bounds.Width);
    }

    protected override void OnRender(TerminalCanvas canvas)
    {
        Rect bounds = ContentBounds;
        EnsureParsed();
        EnsureLayout(bounds.Width);
        ReadOnlySpan<Line> lines = Lines.Span;

        for (int index = 0; index < lines.Length && index < bounds.Height; index++)
        {
            RenderLine(canvas, bounds, lines[index], index);
        }
    }

    private void EnsureParsed()
    {
        // Reference-equality guard: re-parse only when Content is a different instance.
        if (ReferenceEquals(_parsedContent, Content))
        {
            return;
        }

        _spans = Markup.Parse(Content, out _display);
        _parsedContent = Content;
        _layoutValid = false;
    }

    // EnsureLayout/Format/Align: identical to today but format _display (not Content)
    // with the Overflow overload:
    //   TextLayout.Format(_display, width, Overflow, TextAlignment, AmbiguousWidth, _lines);
    // and cache on Overflow instead of Wrapping/Trimming.

    private void RenderLine(TerminalCanvas canvas, Rect bounds, Line line, int row)
    {
        // Walk the line's graphemes; for each, find the covering StyleSpan by source
        // offset and draw. This is RichText's offset-based render path over _display.
        int cells = 0;
        int position = line.Offset;
        int endExclusive = line.Offset + line.Length;

        foreach (Grapheme segment in Graphemes.Enumerate(_display.AsSpan(line.Offset, line.Length)))
        {
            int offset = line.Offset + segment.Offset;
            ReadOnlySpan<char> cluster = _display.AsSpan(offset, segment.Length);
            StyleSpan span = SpanAt(offset);
            TerminalStyle style = ResolveSpanStyle(span);
            DrawResult result = canvas.Draw(
                cluster,
                new Point(bounds.X + line.Leading + cells, bounds.Y + row),
                style,
                background: ResolveBackgroundMode(span));
            cells += Terminal.Unicode.Width.Measure(cluster, AmbiguousWidth).Cells;
            position = offset + segment.Length;
        }

        if (line.HasEllipsis)
        {
            _ = canvas.Draw(
                _ellipsis,
                new Point(bounds.X + line.Leading + cells, bounds.Y + row),
                ResolveSpanStyle(SpanAt(Math.Max(line.Offset, endExclusive - 1))),
                background: ResolveBackgroundMode(default));
        }
    }

    private StyleSpan SpanAt(int offset)
    {
        // Spans tile the display string in order; binary or linear scan is fine.
        foreach (StyleSpan span in _spans)
        {
            if (offset >= span.Offset && offset < span.Offset + span.Length)
            {
                return span;
            }
        }

        return _spans.Length > 0 ? _spans[^1] : default;
    }

    private TerminalStyle ResolveSpanStyle(StyleSpan span)
    {
        TerminalStyle inherited = ResolvedStyle;
        (TerminalAttributes attributes, Underline underline, Color underlineColor) = Decoration.Resolve(
            inherited,
            inherited.Attributes | span.Attributes,
            span.Underline == Underline.None ? null : span.Underline,
            span.UnderlineColor);
        return new TerminalStyle(
            span.Foreground ?? inherited.Foreground,
            span.Background ?? inherited.Background,
            attributes,
            span.Link ?? inherited.Hyperlink,
            underline,
            underlineColor);
    }

    private BackgroundMode ResolveBackgroundMode(StyleSpan span) =>
        span.Background.HasValue || ControlAppearance.HasOpaqueFill(this, GetVisualState())
            ? BackgroundMode.Opaque
            : BackgroundMode.Transparent;

    private static void Validate<T>(T value) where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The enum value is unknown.");
        }
    }
}
```

Notes for the implementer:

- `Decoration.Resolve`'s second parameter is the complete attribute set; pass
  `inherited.Attributes | span.Attributes` so control-level attributes and
  markup attributes both apply.
- `EnsureLayout`/`Format`/`Align` keep today's structure — only the source
  string (`_display`), the cache keys (`Overflow` replacing
  `Wrapping`/`Trimming`), and the `Format` overload change.
- `ContentBounds`, `ResolvedStyle`, `GetVisualState`, `Set`, `Invalidation`,
  `Constraint`, `Rect`, `Point`, `Size`, `DrawResult`, `TerminalCanvas`,
  `TerminalStyle` (= `CellStyle`), `TerminalAttributes` (= `Attributes`) are all
  already available in the control's global usings/base (see the current
  `Text.cs` and `RichText.cs`).

- [ ] **Step 4: Migrate `TextPane`**

In `src/SharpVision.Showcase/Panes/TextPane.cs`, replace the removed properties:

- `Wrapping = Wrapping.Word` → `Overflow = Overflow.Wrap`
- `Trimming = Trimming.GraphemeEllipsis` → `Overflow = Overflow.Ellipsis`
- the bold centered label: keep `Attributes = TerminalAttributes.Bold` (control
  style) **or** switch its content to markup `"<b>Centered status</b>"` — prefer
  the markup form to exercise the new path.

Remove the now-unused `using SharpVision.Text;` alias only if nothing else in
the file needs it (it still provides `Overflow`, so keep it).

- [ ] **Step 5: Run the Text tests and the showcase build**

Run:

```bash
dotnet test --project tests/SharpVision.Tests --filter-class "*TextTests" --timeout 120s
make build
```

Expected: PASS; build green (RichText still compiles against the old `Layout`
overload and `Wrapping`).

- [ ] **Step 6: Commit**

```bash
git add src/SharpVision/Controls/Text.cs src/SharpVision.Showcase/Panes/TextPane.cs \
        tests/SharpVision.Tests/Controls/TextTests.cs
git commit -m "feat(text): render inline markup and Overflow on the Text control"
```

---

### Task 5: Migrate RichText consumers to markup and delete the object model

Rewrite the three showcase files that build `Inlines`, then delete `RichText`
and its inline model and tests. After this task no code references `RichText`,
`Inline`, `Inlines`, `Run`, `LineBreak`, or the `Hyperlink` inline.

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/RichTextPane.cs`,
  `src/SharpVision.Showcase/Panes/TablePane.cs`,
  `src/SharpVision.Showcase/Panes/Doc.cs`
- Delete: `src/SharpVision/Controls/RichText.cs`, `Inline.cs`, `Inlines.cs`,
  `Run.cs`, `LineBreak.cs`, `Hyperlink.cs`,
  `tests/SharpVision.Tests/Controls/RichTextTests.cs`
- Modify every remaining source/test consumer found by
  `rg "RichText|Inlines|new Run|new Hyperlink|LineBreak|Wrapping|Trimming" src tests`.

**Interfaces:**

- Consumes: `Text` markup from Task 4.

- [ ] **Step 1: Rewrite `Doc.cs` heading/description helpers to markup**

The two helpers build a `RichText` from `Run`+`LineBreak`. Replace each with a
`Text` whose `Content` is a markup string. Example transformation for the
`heading` builder (`name` bold, `overview` plain, separated by a break):

```csharp
// Before: RichText with Run(name){Bold} + LineBreak + Run("Overview"){Bold} + LineBreak + Run(overview)
// After:
var heading = new Text($"<b>{Text.Escape(name)}</b>\n<b>Overview</b>\n{Text.Escape(overview)}")
{
    Overflow = Overflow.Wrap,
};
```

Apply the same pattern to the `description` builder
(`<b>{heading}</b>\n<d>{description}</d>` — `Dim` maps to `<d>`). Always wrap
interpolated arguments in `Text.Escape`.

- [ ] **Step 2: Rewrite `TablePane.cs` linked cell**

Replace:

```csharp
linked.Inlines.Add(new Run("Open "));
linked.Inlines.Add(new Hyperlink("protocol guide", "https://invisible-island.net/xterm/ctlseqs/ctlseqs.html"));
```

with:

```csharp
var linked = new Text("Open <link=https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>protocol guide</link>");
```

- [ ] **Step 3: Rewrite `RichTextPane.cs` as a `Text` markup showcase**

Rebuild each specimen as a markup string on `Text`. Mapping reference:

- `Run("x"){Attributes = Bold}` → `<b>x</b>`; `Italic` → `<i>`; `Dim` → `<d>`;
  `Underline` → `<u>`; `Strike` → `<s>`; `Reverse` → `<reverse>`; `Blink` →
  `<blink>`; `RapidBlink` → `<rapidblink>`; `Hidden` → `<hidden>`; `Overline` →
  `<overline>`.
- `Foreground = Color.Indexed(n)` → `<fg=n>` (or a named/role tag).
- `Underline = Underline.Curly, UnderlineColor = c` →
  `<u=curly><uc=…>…</uc></u>`.
- `Hyperlink(text, target){Underline}` → `<u><link=target>text</link></u>`.
- `LineBreak` → `\n`.

Example for the introductory specimen:

```csharp
var introductory = new Text(
    "<b>Rich </b><i>terminal text</i>\n" +
    "Unicode: café · 你好 · 👩‍💻 · " +
    "<u><link=https://github.com/pavkam>project source</link></u>")
{
    Overflow = Overflow.Wrap,
};
```

Rename the page title/text as desired, or keep "RichText" if the gallery entry
name must stay stable (check `GalleryTests`). The pane no longer needs the
`append`/mutation button that mutated `Inlines`; replace it with a button that
reassigns `wrapped.Content` to a new markup string to preserve the "responsive
reading column" interactive example.

- [ ] **Step 4: Delete the object model and its tests**

```bash
git rm src/SharpVision/Controls/RichText.cs src/SharpVision/Controls/Inline.cs \
       src/SharpVision/Controls/Inlines.cs src/SharpVision/Controls/Run.cs \
       src/SharpVision/Controls/LineBreak.cs src/SharpVision/Controls/Hyperlink.cs \
       tests/SharpVision.Tests/Controls/RichTextTests.cs
```

- [ ] **Step 5: Build and run the full suite**

Run:

```bash
make build
make test
```

Expected: green. If `GalleryTests` fails on a missing "RichText" entry, update
the gallery registration/screen test to the renamed page (or keep the name). Fix
any remaining `Inlines`/`Run` references the compiler flags.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "refactor(text): migrate RichText specimens to markup and delete the inline model"
```

---

### Task 6: Delete the old `Layout` overload and `Wrapping`/`Trimming`; docs; final gate

Now that nothing consumes `Wrapping`/`Trimming`, remove them and the old
`Layout.Format` overload, rewrite the two layout test suites to `Overflow`, and
finish documentation.

**Files:**

- Modify: `src/SharpVision/Text/Layout.cs`
- Delete: `src/SharpVision/Text/Wrapping.cs`, `src/SharpVision/Text/Trimming.cs`
- Modify: `tests/SharpVision.Tests/Text/LayoutTests.cs`,
  `tests/SharpVision.Tests/Text/RandomizedLayoutTests.cs`
- Modify: `docs/controls/display/text.md`; Delete:
  `docs/controls/display/rich-text.md`

**Interfaces:**

- Produces: `Layout.Format(Overflow …)` as the only formatting entry point.

- [ ] **Step 1: Rewrite the layout tests to the `Overflow` API**

In `LayoutTests.cs` and `RandomizedLayoutTests.cs`, replace every
`Layout.Format(…, Wrapping.X, Trimming.Y, …)` call with the `Overflow` overload
using this mapping: `(Word,None)`→`Wrap`, `(Grapheme,None)`→`WrapAnywhere`,
`(None,Clip)`→`Clip`, `(None,WordEllipsis)` and `(None,GraphemeEllipsis)`→
`Ellipsis`, `(None,None)`→`Visible`. Keep every assertion; only the policy
argument changes. Delete any test that asserted the specific
`WordEllipsis`-vs-`GraphemeEllipsis` distinction (folded into `Ellipsis`) or
convert it to assert the `Ellipsis` behavior.

- [ ] **Step 2: Remove the old overload and mapping**

In `src/SharpVision/Text/Layout.cs`, inline the `Overflow`→behavior mapping
directly into `FormatWrapped`/`FormatUnwrapped` dispatch and delete the
`(Wrapping, Trimming)` public overload and their `Validate` calls. The private
`FormatWrapped` takes a `bool wordBoundaries` (true for `Overflow.Wrap`); the
private `FormatUnwrapped` takes the trim behavior derived from `Overflow`.

- [ ] **Step 3: Delete the enums**

```bash
git rm src/SharpVision/Text/Wrapping.cs src/SharpVision/Text/Trimming.cs
```

- [ ] **Step 4: Build and run**

Run:

```bash
make build
dotnet test --project tests/SharpVision.Tests --filter-class "*LayoutTests" --timeout 120s
dotnet test --project tests/SharpVision.Tests --filter-class "*RandomizedLayoutTests" --timeout 120s
```

Expected: PASS; no references to `Wrapping`/`Trimming` remain (`make build`
would fail otherwise).

- [ ] **Step 5: Merge the control docs**

Rewrite `docs/controls/display/text.md` to document the merged control: the
markup grammar (tags, value forms, escaping, lenient rules), the `Overflow`
enum, `TextAlignment`, `AmbiguousWidth`, and OSC 8 links. Fold in the
still-relevant test-obligations from `rich-text.md`. Then:

```bash
git rm docs/controls/display/rich-text.md
```

Update `docs/controls/index.md` (and any control coverage matrix) to drop the
RichText row and point its content at Text. Grep for stale links:

```bash
grep -rn "rich-text\|RichText\|Inlines\|\bRun\b" docs/ | grep -v superpowers/plans
```

Fix every hit.

- [ ] **Step 6: Full quality gate**

Run:

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, all tests green, no Markdown/link
failures.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor(text): make Overflow the only layout policy and merge Text docs"
```

---

## Self-review notes

- **Spec coverage:** content-always-markup + escape (Tasks 1, 4); grammar shape
  named-tags-plus-value (Task 1 `TryResolveFacet`); lenient/overlap/never-throw
  (Task 1 tests + Task 4 `Content` setter test); single `Overflow` enum (Tasks
  3, 4); hard delete (Tasks 5, 6); parser + randomized + overflow + render +
  showcase tests (Tasks 1–6). All design sections map to a task after the gloss
  corrections above.
- **Type consistency:** `Markup.Parse(source, out display) → StyleSpan[]`,
  `StyleSpan`'s eight-field shape, `Overflow`'s five members, and
  `Layout.Format(…, Overflow, …)` are used identically wherever referenced.
- **Green-at-boundary:** Tasks 1–3 are additive; Task 4 changes `Text` and its
  one showcase pane + tests together; Task 5 migrates the remaining consumers
  before deleting the model; Task 6 removes the enums only after the last
  consumer is gone.
- **Gloss resolved:** `Visible` is the default and `<br>` is not part of the
  grammar. The implementation must also migrate non-showcase consumers and
  resolve role colors before constructing `TerminalStyle`.
