# TabControl

## TabControl contract

`TabControl` arranges typed [`TabItem`](#tabitem) pages and coordinates a header
strip, keyboard navigation, and content participation. It extends
[`ItemsControl`](../items-control.md) with one private retained presentation
host. The public `TabItem` objects are the semantic item controls owned by that
host; no page or header is rebuilt during selection, mutation, or layout.

The header strip occupies the first row. Each label has one cell of horizontal
padding, adjacent labels are separated by `│`, and a `─` rule occupies the
second row when height permits. Only selected content participates in measure,
arrangement, rendering, hit testing, and navigation below the rule. Unselected
content remains owned and attached with empty bounds.

## API

- `Items : TabItems` exposes typed `Add`/`Remove`/`Clear` overloads for
  `TabItem`, plus ordinary typed index, insertion, replacement, enumeration, and
  copy operations. Null, duplicate, attached, disposed, and cyclic candidates
  are rejected before ownership changes. Removal detaches without disposing.
- `SelectedIndex` tracks the selected eligible page; `-1` explicitly clears
  selection. The first effectively visible and enabled tab auto-selects. Invalid
  indexes and unavailable targets are rejected before mutation.
- `SelectedItem` returns the selected `TabItem`, or null.
- `HeaderOffset` reports the non-negative clipped header-strip origin in
  terminal cells. It updates during committed layout to reveal the selected
  label and returns toward zero when resize provides more room.
- `SelectionChanged` fires once after the selected page identity and retained
  header states commit. Identical assignment and index shifts that preserve the
  selected identity are not selection changes.

Insertion before the selected page preserves selected identity. Removing,
replacing, disabling, or collapsing the selected page chooses the nearest
eligible successor, then predecessor, or clears selection. Clearing the
collection clears selection. Re-enabling an unselected page does not steal
selection.

Primary pointer release on a header selects that page. Left/Right move and
select with wrapping; Home/End choose the first/last eligible page. Navigation
skips effectively hidden or disabled pages and brings the selected header fully
into the clipped strip when it fits. When one header is wider than the strip,
the leading label cell is revealed. Header focus follows user navigation. A
selected header resolves `State.Selected` in combination with hover, focus,
pressed, or disabled state.

## TabItem

`TabItem` extends
[`ContentControl`](../content-control.md#contentcontrol-contract). `Header` is a
non-null string without terminal controls, rendered in the tab strip by one
retained pressable framework part. `Content` is the single caller-replaceable
owned child arranged below the rule only while selected. `IsSelected` exposes
the committed owner-controlled state.

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
identity-preserving insertion, deterministic removal/replacement/availability
repair, retained header identity, exact header/divider/rule rendering,
selected-content exclusion and replacement, pointer and keyboard navigation,
focus, disabled skipping, Unicode cells, header overflow/reveal, zero/tiny
bounds, resize, stale-cell clearing, and final semantic cells.
