# ComboBox

## ComboBox contract

`ComboBox` displays one selected value in a compact field and owns a
[Popup](../windows/popup.md#popup-contract) containing a
[List](../collections/list.md#list-contract) immediately below the field when
open. The Popup clears and frames its surface before the list renders, so
choices never show through content behind the drop-down. The list uses the same
keyboard, pointer, selection, and scrolling semantics as a standalone list. When
the active style supplies a `State.Checked` background, the selected choice
fills the complete interior row, including trailing blank cells, under the
[List row rendering contract](../collections/list.md#list-contract).

## API

- `Items` copies non-null choices into the owned single-selection list.
- `SelectedIndex` is `-1` or an index within `Items`; a committed selection
  raises `SelectionChanged`.
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
