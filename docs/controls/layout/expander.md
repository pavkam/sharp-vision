# Expander

## Expander contract

`Expander` displays a collapsible section with a focusable header toggle and
optional content. It extends
[`ContentControl`](../content-control.md#contentcontrol-contract). Enter, Space,
or a primary pointer click on the header toggles visibility of the content
region below. The header always renders the theme's expanded or collapsed
disclosure glyph followed by the header text.

`Expander` itself is the focus, hover, and press owner for the header row. The
header is rendered directly by the control and is not exposed as a public
presentation child. Its pointer rectangle is the first non-empty row of
`ContentBounds`, after optional caller border and padding deflation. Caller
content remains the one ordinary owned child.

Physical `IsPointerOver` remains true while the pointer targets retained content
because the Expander is in that child's routed ancestry. The `PointerOver`
appearance is narrower: it applies only while the pointer directly targets the
header row. Hovering or clicking the content region therefore does not paint or
activate the header.

The control defaults to no border and a transparent background. Its disclosure
glyph, header, and two-cell content indentation provide the structural signal.
Hover and direct focus replace only foreground and border semantics on the
header; they do not invent a frame or fill. Caller content composes over the
surrounding surface unless a descendant supplies an explicit background. Callers
may opt into inherited frame, face, or shadow properties; layout then follows
the shared [chrome contract](../../concepts/styling.md#shared-chrome).

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

- `Header` is a non-null string rendered beside the toggle glyph. Terminal
  control characters are rejected before state changes. Default is empty.
- `IsExpanded` controls content visibility. When `false`, only the header row
  renders and content is excluded from measure, arrangement, rendering, hit
  testing, and navigation. The content remains owned and attached. Default is
  `true`.
- `ExpandedChanged` fires after `IsExpanded` commits a changed value.
- `Content` is the single owned child, arranged below the header row when
  expanded. Replacing content while collapsed immediately releases the previous
  child without changing expansion state.
- `ContentIndent` controls the leading cell inset for expanded content. It
  defaults to `2`; the indent participates in intrinsic width and is clamped to
  the arranged content width when space is tight.

Inherited disabled state applies to the semantic header owner. An unavailable
header remains visible but pointer, Space, and Enter input cannot change
expansion.

## Code-owned glyphs

`CollapsedGlyph` and `ExpandedGlyph` are validated one-cell local overrides.
`ResetGlyphs()` clears both; otherwise the header resolves
`the code-owned disclosure glyph defaults` at render time.

## Example

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
[access key](../../concepts/access-keys.md#focus-and-semantic-actions). Visible
width excludes the marker, and Alt plus the key focuses and toggles expansion.

## Expected behavior

Cover expanded and collapsed measurement, ownership-preserving exclusion, toggle
event order, exact borderless header glyph/text cells, keyboard and pointer
activation, optional-border-and-padding-aware header hit geometry, descendant
hover isolation, disabled refusal, focus, content replacement, Unicode cell
geometry, resize/reflow, stale-cell clearing, and final cells. The showcase must
include expanded, collapsed, nested, disabled, wide-header, and replaced-content
specimens.
