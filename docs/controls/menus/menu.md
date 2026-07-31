# Menu

## Menu contract

`Menu` owns a constrained mixture of
[`MenuItem` and `MenuSeparator`](menu-item.md#menuitem-contract) controls and
coordinates layout, selected visual state, keyboard navigation, check/radio
activation, and menu-level invocation notifications.

## API

| Member          | Default                | Purpose                                                          |
| --------------- | ---------------------- | ---------------------------------------------------------------- |
| `Items`         | Empty typed collection | Owns only `MenuItem` and `MenuSeparator` values.                 |
| `Orientation`   | `Horizontal`           | Chooses menu-bar or vertical-flyout geometry and arrow behavior. |
| `Spacing`       | `0` cells              | Inserts gaps between semantic entries.                           |
| `MinWidth`      | `10` cells             | Sets the inherited minimum Menu border-box width.                |
| `MaxWidth`      | `int.MaxValue`         | Sets the inherited maximum Menu border-box width.                |
| `SelectedIndex` | `-1`                   | Retains the active non-separator navigation cursor.              |
| `ItemInvoked`   | No subscribers         | Reports an item after its own `Invoked` subscribers complete.    |

## Behavior

- `Items : MenuEntryCollection` exposes `IReadOnlyList<Control>` inspection plus
  typed `Add`/`Remove` overloads for `MenuItem` and `MenuSeparator`; arbitrary
  controls cannot enter through the semantic collection.
- `Orientation` and `Spacing` control horizontal or vertical geometry. `Spacing`
  defaults to zero so vertical flyout entries occupy adjacent rows; horizontal
  bars can opt into additional separation.
- Inherited `MinWidth` defaults to 10 cells, while `MaxWidth` retains its
  unbounded `int.MaxValue` default. Both constrain the Menu border box through
  the ordinary [layout contract](../../concepts/layout.md#lengths). Set
  `MinWidth = 0` for label-tight sizing. A retained submenu Popup adds its
  one-cell frame outside both horizontal Menu edges, so the default produces a
  12-cell framed surface when space permits. A smaller root clamps the complete
  framed surface without drawing outside the viewport.
- `SelectedIndex` retains the active `MenuItem` navigation cursor; `-1` clears
  it and a separator index is rejected. The cursor paints with selection colors
  only while focus remains within the menu or one of its retained submenus.
- `ItemInvoked` reports the item and activation cause after the item's own
  `Invoked` subscribers complete.

## Interaction

Arrow keys follow `Orientation`, while Tab and Shift+Tab move forward and
backward regardless of orientation. Navigation wraps, skips separators and
unavailable items, updates `SelectedIndex`, and retains focus on the menu. Enter
and a completed Space activate the selected private item with a keyboard cause.
A primary pointer click invokes through the shared
[`Pressable`](../pressable.md#pressable-contract) contract.

An ampersand
[access key](../../concepts/access-keys.md#focus-and-semantic-actions) on an
item selects and focuses its owning menu, then uses the same activation path to
open a submenu or invoke a command. A top-level Alt mnemonic therefore arms the
same modal menu plane as Enter or pointer activation.

Pointer motion over an available item selects it and changes the row foreground
without replacing the containing menu background. Physical hover styling is
independent of the focus-scoped selected appearance. An unfocused, inactive menu
therefore does not paint its retained navigation cursor. Hover does not open a
dormant menu. Once one sibling submenu is open, moving or keyboard-navigating to
another item closes the previous sibling and opens the new item's submenu.
Moving to an item without a submenu closes the previous submenu without invoking
the command.

Opening the first submenu arms one top-menu-rooted
[modal plane](../../concepts/modality.md#menu-planes) with
`OutsideInteraction.Dismiss`. Sibling switches, command rows, retained popup
surfaces, and arbitrarily deep submenus reuse that exact scope. Escape closes
the deepest branch before ending the root session; leaf invocation and outside
dismissal close the complete chain. A top menu inside a modal Window becomes a
temporary younger scope and restores the Window plane when it closes.

A horizontal menu opens item submenus below the anchor. A vertical menu opens
nested submenus to the right. Popup edge fallback may flip those preferred
directions to keep the framed surface inside the terminal. Closing a submenu
restores focus to its owning menu before hiding submenu content.

Vertical menu width uses independent label and shortcut measurements: the widest
label plus a two-cell gutter plus the widest shortcut, clamped by inherited
`MinWidth` and `MaxWidth`. Shortcut text is right-aligned to the shared trailing
edge, and label content cannot draw into the gutter. Changing either width
constraint while attached invalidates measure; an open retained submenu is
remeasured and reframed on the next layout pass.

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

![The Menu control rendered in the live showcase](../../images/controls/menu.png)

![The Menu control with its popup open in the live showcase](../../images/controls/menu-open.png)

```csharp
var menu = new Menu
{
    Orientation = Orientation.Vertical,
    MinWidth = 14,
    MaxWidth = 30,
};
menu.Items.Add(new MenuItem { Content = new Text("Open") });
menu.Items.Add(new MenuSeparator());
menu.Items.Add(new MenuItem
{
    Content = new Text("Auto save"),
    Kind = MenuItemKind.Check,
});
```

## Expected behavior

Cover constrained mixed ownership, zero-spacing compact layout, shared row
width, default and configured minimum and maximum widths, live width
remeasurement, tiny-root popup clamping, full-width separators, horizontal and
vertical submenu placement, keyboard focus retention, Tab/Shift+Tab and arrow
wrapping, Enter/Space and pointer invocation, physical hover selection and
style, armed submenu switching, one-scope identity through sibling and nested
transitions, outside consumption without replay, focus restoration, atomic
check/radio publication, disabled items, item/menu event order, tiny bounds,
Unicode shortcuts, and final cells.
