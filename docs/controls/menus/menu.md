# Menu

## Menu contract

`Menu` owns typed [MenuItem](menu-item.md#menuitem-contract) children and
coordinates their layout, selected visual state, keyboard navigation, check and
radio activation, and menu-level invocation notifications.

## API

- `Items` accepts only detached menu items and enforces managed ownership.
- `Orientation` and `Spacing` control horizontal or vertical geometry.
- `SelectedIndex` tracks the active non-separator item and applies its checked
  visual state; `-1` clears selection.
- `ItemInvoked` reports the item and activation cause after a completed item
  state transition.

## Interaction

Arrow keys follow `Orientation`, skip separators and unavailable items, update
`SelectedIndex`, and move focus to the new active item. Enter, Space, and a
primary pointer click invoke the selected item through the shared `Pressable`
contract. A `Menu` is composable inside `Popup` when an anchored flyout is
needed.

## Example

```csharp
var menu = new Menu { Orientation = Orientation.Vertical };
menu.Items.Add(new MenuItem { Header = "Open" });
menu.Items.Add(new MenuItem { Header = "Auto save", Kind = MenuItemKind.Check });
```

## Test obligations

Cover typed ownership, separator rejection, horizontal and vertical layout,
keyboard focus movement, pointer and keyboard invocation, check/radio commits,
disabled items, events, tiny bounds, and final cells.
