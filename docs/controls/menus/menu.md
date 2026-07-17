# Menu

## Menu contract

`Menu` owns a constrained mixture of
[`MenuItem` and `MenuSeparator`](menu-item.md#menuitem-contract) controls and
coordinates layout, selected visual state, keyboard navigation, check/radio
activation, and menu-level invocation notifications.

## API

- `Items : MenuItems` exposes `IReadOnlyList<Control>` inspection plus typed
  `Add`/`Remove` overloads for `MenuItem` and `MenuSeparator`; arbitrary
  controls cannot enter through the semantic collection.
- `Orientation` and `Spacing` control horizontal or vertical geometry. `Spacing`
  defaults to zero so vertical flyout entries occupy adjacent rows; horizontal
  bars can opt into additional separation.
- `SelectedIndex` tracks the active `MenuItem`; `-1` clears selection and a
  separator index is rejected.
- `ItemInvoked` reports the item and activation cause after the item's own
  `Invoked` subscribers complete.

## Interaction

Arrow keys follow `Orientation`, while Tab and Shift+Tab move forward and
backward regardless of orientation. Navigation wraps, skips separators and
unavailable items, updates `SelectedIndex`, and retains focus on the menu. Enter
and a completed Space activate the selected private item with a keyboard cause.
A primary pointer click invokes through the shared
[`Pressable`](../pressable.md#pressable-contract) contract.

Pointer motion over an available item selects it and paints its complete row.
Hover does not open a dormant menu. Once one sibling submenu is open, moving or
keyboard-navigating to another item closes the previous sibling and opens the
new item's submenu. Moving to an item without a submenu closes the previous
submenu without invoking the command.

A horizontal menu opens item submenus below the anchor. A vertical menu opens
nested submenus to the right. Popup edge fallback may flip those preferred
directions to keep the framed surface inside the terminal. Closing a submenu
restores focus to its owning menu before hiding submenu content.

Vertical menu width uses independent label and shortcut measurements: the widest
label plus a two-cell gutter plus the widest shortcut. Shortcut text is
right-aligned to the shared trailing edge, and label content cannot draw into
the gutter.

### Keyboard navigation

The menu sets
[`TabNavigation.None`](../../concepts/focus.md#hierarchical-tab-navigation) and
owns one focus stop. Its private `MenuItem` faces never enter global traversal;
the menu handles Tab and Shift+Tab before the global focus default and uses them
to update selection. Separators and unavailable items are skipped.

```text
  Menu focus ──► MenuItem "File"       (selected private face)
                 MenuItem "Edit"
                 MenuSeparator         (skipped)
                 MenuItem "Help"
                       ▲
                       │ Tab/Shift+Tab: next/previous
                       │ Arrow keys: follow Orientation
```

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

Cover constrained mixed ownership, zero-spacing compact layout, shared row
width, full-width separators, horizontal and vertical submenu placement,
keyboard focus retention, Tab/Shift+Tab and arrow wrapping, Enter/Space and
pointer invocation, physical hover selection and style, armed submenu switching,
focus restoration, atomic check/radio publication, disabled items, item/menu
event order, tiny bounds, Unicode shortcuts, and final cells.
