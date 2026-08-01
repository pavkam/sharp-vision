# Expander

## Overview

`Expander` displays a collapsible section with a focusable header toggle and
optional content. It extends [`ContentControl`](../content-control.md#overview).
Pressing Enter or Space, or clicking the header with the primary pointer
button, toggles the visibility of the content region below. The header always
renders the theme's expanded or collapsed disclosure glyph followed by the
header text.

The Expander itself owns focus, hover, and press for the header row. The
header is rendered directly by the control rather than exposed as a public
presentation child. Its pointer rectangle is the first non-empty row of
`ContentBounds`, after any caller-supplied border and padding are deflated.
The caller's content remains the one ordinary owned child.

The physical `IsPointerOver` state stays true while the pointer targets
retained content, because the Expander is in that child's routed ancestry. The
`PointerOver` appearance is narrower: it applies only while the pointer
directly targets the header row. Hovering or clicking the content region
therefore neither paints nor activates the header.

By default the control has no border and a transparent background; the
disclosure glyph, the header, and the two-cell content indentation supply the
structural signal. Hover and direct focus change only the foreground and
border semantics of the header — they do not invent a frame or a fill. Caller
content composes over the surrounding surface unless a descendant supplies an
explicit background. Callers can opt into inherited frame, face, or shadow
properties, in which case layout follows the shared
[chrome contract](../../concepts/styling.md#shared-chrome).

## API

| Member                            | Default        | Purpose                                                                           |
| --------------------------------- | -------------- | --------------------------------------------------------------------------------- |
| `Header`                          | Empty          | Supplies validated single-line text beside the disclosure glyph.                  |
| `IsExpanded`                      | `true`         | Includes or excludes owned content from layout, rendering, input, and navigation. |
| `Content`                         | `null`         | Owns the optional collapsible body.                                               |
| `ContentIndent`                   | `2` cells      | Insets expanded content from the leading edge.                                    |
| `CollapsedGlyph`, `ExpandedGlyph` | Code-owned     | Override one-cell disclosure marks; `ResetGlyphs()` restores code-owned defaults. |
| `ExpandedChanged`                 | No subscribers | Reports a committed expansion change.                                             |

## Behavior

- `Header` is a non-null string rendered beside the toggle glyph and defaults
  to empty. Text containing terminal control characters is rejected before any
  state changes.
- `IsExpanded` defaults to `true` and controls content visibility. When it is
  `false`, only the header row renders; the content is excluded from measure,
  arrangement, rendering, hit testing, and navigation, but it remains owned
  and attached.
- `ExpandedChanged` fires after `IsExpanded` commits a changed value.
- `Content` is the single owned child, arranged below the header row while
  expanded. Replacing the content while collapsed releases the previous child
  immediately without changing the expansion state.
- `ContentIndent` defaults to `2` and sets the leading cell inset for expanded
  content. The indent participates in intrinsic width and is clamped to the
  arranged content width when space is tight.

Inherited disabled state applies to the Expander as the semantic header owner:
a disabled header remains visible, but pointer, Space, and Enter input cannot
change expansion.

## Code-owned glyphs

`CollapsedGlyph` and `ExpandedGlyph` are validated one-cell local overrides.
`ResetGlyphs()` clears both; when no override is set, the header resolves the
code-owned disclosure glyph defaults at render time.

## Example

![The Expander control rendered in the live showcase](../../images/controls/expander.png)

```csharp
var details = new Expander
{
    Header = "Advanced settings",
    IsExpanded = false,
    Content = new Stack
    {
        Children =
        {
            new CheckBox { Content = new Text("Debug mode") },
            new CheckBox { Content = new Text("Verbose logging") },
        },
    },
};
```

An ampersand in `Header` declares an
[access key](../../concepts/access-keys.md#focus-and-semantic-actions). The
marker does not count toward visible width, and pressing Alt plus the key
focuses the Expander and toggles expansion.

## Expected behavior

Measurement is correct in both the expanded and collapsed states, and
collapsing excludes the content without giving up ownership. Toggling raises
`ExpandedChanged` in a deterministic order, and the borderless header renders
its glyph and text into exact cells. The header responds to keyboard and
pointer activation with hit geometry that accounts for any optional border
and padding, hovering a descendant does not light the header, and a disabled
Expander refuses to toggle. Focus, content replacement, Unicode cell
geometry, resize and reflow, clearing of stale cells, and the final committed
cells are all observable guarantees. The showcase includes expanded,
collapsed, nested, disabled, wide-header, and replaced-content specimens.
