# TabControl

## TabControl contract

`TabControl` arranges typed [`TabItem`](#tabitem) pages and coordinates a header
strip, keyboard navigation, and content participation. It extends
[`ItemsControl`](../items-control.md) with one private retained item host. The
`TabControl` itself owns header focus and input; public `TabItem` objects own
semantic page content without exposing presentation header controls.

The header strip occupies the first row. Each label has one cell of horizontal
padding, adjacent labels use the theme's tab-divider glyph, and the theme's
selected-underline glyph occupies the second row when height permits. Only
selected content participates in measure, arrangement, rendering, hit testing,
and navigation below the rule. Unselected content remains owned and attached
with empty bounds.

## API

- `Items : TabItems` exposes typed `Add`/`Remove`/`Clear` operations for
  `TabItem`, plus read-only typed indexing and enumeration. Null, duplicate,
  attached, disposed, and cyclic candidates are rejected before ownership
  changes. Removal detaches without disposing.
- `SelectedIndex` tracks the selected eligible page; `-1` explicitly clears
  selection. The first effectively visible and enabled tab auto-selects. Invalid
  indexes and unavailable targets are rejected before mutation.
- `SelectionChanged` fires once after the selected page identity and retained
  content participation commit. Identical assignment is not a selection change.

Removing, disabling, or collapsing the selected page chooses the nearest
eligible successor, then predecessor, or clears selection. Clearing the
collection clears selection. Re-enabling an unselected page does not steal
selection.

Primary pointer release on a header selects that page. Left/Right move and
select with wrapping; Home/End choose the first/last eligible page. Navigation
skips effectively hidden or disabled pages. Focus remains on `TabControl`; its
selected header appearance combines `VisualState.Selected` with hover, focus,
pressed, or disabled state.

## TabItem

`TabItem` extends
[`ContentControl`](../content-control.md#contentcontrol-contract). `Header` is a
non-null string without terminal controls, rendered in the owning tab strip.
`Content` is the single caller-replaceable owned child arranged below the rule
only while selected.

## Theme glyphs

`DividerGlyph` and `UnderlineGlyph` are validated one-cell local overrides for
the header row. Their defaults resolve from `Theme.Glyphs.Separators` on every
render. `ResetGlyphs()` clears both overrides.

## Example

```csharp
var tabs = new TabControl();
tabs.Items.Add(new TabItem
{
    Header = "General",
    Content = new Stack
    {
        Children = { new Text("General settings") },
    },
});
tabs.Items.Add(new TabItem
{
    Header = "Advanced",
    Content = new Stack
    {
        Children = { new CheckBox { Content = new Text("Debug mode") } },
    },
});
```

## Test obligations

Cover typed ownership and validation, default/cleared selection, event order,
deterministic removal and availability repair, exact header/divider/rule
rendering, selected-content exclusion and replacement, pointer and keyboard
navigation, owner focus, disabled skipping, Unicode cells, clipping, zero/tiny
bounds, resize, stale-cell clearing, and final semantic cells.
