# Menu

## Menu contract

`Menu` owns managed [MenuItem](menu-item.md#menuitem-contract) children and
coordinates keyboard/pointer navigation, submenus, commands, mnemonics, and
popup focus/capture.

## API

- `Items` accepts only unparented menu items and enforces ownership.
- `Orientation` controls top-level navigation.
- `IsOpen`, `SelectedIndex`, `OpenDelay`, and `CloseDelay` expose menu state;
  delays are non-negative and use the dispatcher clock.
- `Opened`, `Closed`, and `ItemInvoked` report committed state and cause.

## Interaction

Alt/mnemonic or explicit command opens the menu; arrows navigate according to
orientation and submenu direction; Enter/Space invokes; Escape closes one scope
and restores focus; pointer hover delay opens submenus; outside click follows
popup dismissal policy.

Only one submenu path is open. Closing releases capture, cancels timers, clears
pressed/hover selection, and restores the recorded valid focus owner.

## Example

```csharp
var file = new MenuItem { Header = new Text("File") };
file.Items.Add(new MenuItem { Header = new Text("Open") });
var menu = new Menu();
menu.Items.Add(file);
```

## Test obligations

Cover empty/nested menus, ownership, all navigation/invocation paths, mnemonic,
delays with fake clock, disabled/checked items, outside click, capture/focus
restoration, resize/reposition, tiny viewport scrolling, events, and cells.
