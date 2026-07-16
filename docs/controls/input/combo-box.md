# ComboBox

## ComboBox contract

`ComboBox` derives directly from `Control`. It displays one selected value in a
compact field and owns a private [Popup](../windows/popup.md#popup-contract)
containing a [List](../collections/list.md#list-contract) immediately below the
field when open. The Popup clears and frames its surface before the list
renders, so choices never show through content behind the drop-down. The list
uses the same keyboard, pointer, selection, and scrolling semantics as a
standalone list. When the active style supplies a `State.Selected` background,
the selected choice fills the complete interior row, including trailing blank
cells, under the
[List row rendering contract](../collections/list.md#list-contract).

The selected value is the field's face; `ComboBox` therefore exposes neither
`Content` nor `Children`. It owns exactly one popup-layer framework part, and
that `Popup.Content` owns the private List. Keyboard and pointer press mechanics
are composed from the same internal behavior used by `Pressable`, without
claiming its single-content inheritance role.

## API

- `Items` copies non-null choices into the owned single-selection list.
- `SelectedIndex` is `-1` or an index within `Items`; a committed selection
  publishes `PropertyChanged(SelectedIndex)` before `SelectionChanged`.
- `DropDownHeight` is a positive terminal-cell maximum for the visible list.
- `ScrollBars`, `ShowScrollBars`, `ScrollBarChrome`, and `ScrollBarFill` forward
  the common overflow policy to the owned List, so long choice popups use the
  same rails as standalone lists and viewports.
- `IsOpen` controls Popup layout, rendering, hit testing, and focus transfer to
  the list. The popup width is at least the field width, while `DropDownHeight`
  limits only the list interior; the Popup adds its physical frame outside that
  limit and keeps the open list above later page content as defined by the
  [Popup contract](../windows/popup.md#popup-contract).

## Interaction

Enter, Space, or a primary pointer click toggles the list. When opening, focus
moves to the owned list: arrows navigate it, Enter chooses the active item and
closes the list, and Escape closes without changing the selection. Pointer
clicks route through the Popup frame to the realized List item, using the same
semantic invocation path as Enter. Closed list cells neither render nor hit
test. A close that began in the list restores keyboard focus to the visible
ComboBox field during [Popup closing](../windows/popup.md#api), before the list
becomes unavailable.

### Keyboard navigation inside the popup

The popup sets
[`TabNavigation.Contained`](../../concepts/focus.md#navigation-scopes) to trap
Tab within its scope. The inner List sets `IsTabStop = false` so that Tab cycles
through the realized `ListItem` controls and cannot escape to controls outside
the open drop-down. Arrow keys (Up/Down/Left/Right), Home, End, Page Up, and
Page Down navigate between items through the List's own keyboard handler.

## Example

```csharp
var density = new ComboBox
{
    Items = ["Compact", "Comfortable", "Spacious"],
    SelectedIndex = 1,
    DropDownHeight = 5,
};
```

## Test obligations

Cover item copying, index validation, framed popup rendering/hit testing, popup
arrangement, Escape, mouse selection through the Popup, keyboard
focus/navigation/activation, scrollable long lists, resize, style states, and
exact cells.
