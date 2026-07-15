# Menu

## Menu contract

`Menu` owns a constrained mixture of
[`MenuItem` and `MenuSeparator`](menu-item.md#menuitem-contract) controls and
coordinates layout, selected visual state, keyboard navigation, check/radio
activation, and menu-level invocation notifications.

## API

- `Items : MenuItems` exposes `IReadOnlyList<Control>` inspection plus typed
  `Add`/`Remove` overloads for `MenuItem` and `MenuSeparator`; arbitrary controls
  cannot enter through the semantic collection.
- `Orientation` and `Spacing` control horizontal or vertical geometry.
- `SelectedIndex` tracks the active `MenuItem`; `-1` clears selection and a
  separator index is rejected.
- `ItemInvoked` reports the item and activation cause after the item's own
  `Invoked` subscribers complete.

## Interaction

Arrow keys follow `Orientation`, skip separators and unavailable items, update
`SelectedIndex`, and move focus to the new active item. Enter, Space, and a
primary pointer click invoke through the shared
[`Pressable`](../pressable.md#pressable-contract) contract. A `Menu` is
composable inside `Popup` when an anchored flyout is needed.

Selecting a radio item stages every matching sibling's checked field before the
first property callback. Changed properties publish in item order. A reentrant
selection invalidates the older versions and suppresses their remaining stale
notifications; the first callback failure is rethrown only after current
publication has been attempted.

## Example

```csharp
var menu = new Menu { Orientation = Orientation.Vertical };
menu.Items.Add(new MenuItem { Content = new Text("Open") });
menu.Items.Add(new MenuSeparator());
menu.Items.Add(new MenuItem
{
    Content = new Text("Auto save"),
    Kind = MenuItemKind.Check,
});
```

## Test obligations

Cover constrained mixed ownership, separator selection rejection, horizontal
and vertical layout, keyboard focus movement, pointer and keyboard invocation,
atomic check/radio publication, disabled items, item/menu event order, tiny
bounds, and final cells.
