# MenuItem and MenuSeparator

## MenuItem contract

`MenuItem` is a sealed [`Pressable`](../pressable.md#pressable-contract)
command, check, or radio entry inside a [Menu](menu.md#menu-contract). It uses
inherited `Content` as its sole visible face; there is no competing text-only
`Header` property.

## API

- `Content` is the atomic zero-or-one owned face and may contain any Control.
- `Kind` is command, check, or radio.
- `IsChecked` is valid only for check/radio kinds; `GroupName` scopes radio
  selection within its containing menu.
- `Invoked` reports the committed activation after check/radio state updates.

Check entries reserve `[ ]` or `[x]` plus one separator cell before content.
Radio entries reserve `○` or `◉` plus one separator cell. Content is measured
through the remaining constraint and arranged through the common inherited
content slot, so state changes do not move it and collapsed content contributes
no margin. Matching radio fields are all staged before any
`PropertyChanged(IsChecked)` callback; reentrant selection suppresses stale
outer notifications.

The item's own `Invoked` subscribers complete before `Menu.ItemInvoked` is
forwarded. Both callbacks observe committed check/radio state.

## MenuSeparator contract

`MenuSeparator : Control` is the distinct non-interactive entry role. It is
never a `Pressable` or a `MenuItemKind`: it cannot focus, hit test, select, or
invoke. It measures three cells by one cell and draws a clipped horizontal rule
across its arranged width.

`MenuItems` exposes typed `Add` and `Remove` overloads for `MenuItem` and
`MenuSeparator`; it has no arbitrary `Add(Control)` entry point.

## Example

```csharp
menu.Items.Add(new MenuItem { Content = new Text("Open") });
menu.Items.Add(new MenuSeparator());
menu.Items.Add(new MenuItem
{
    Content = new Text("Auto save"),
    Kind = MenuItemKind.Check,
});
```

## Test obligations

Cover each item kind, invalid checked-state assignment, atomic radio observers,
item-before-menu invocation, inherited content ownership and Unicode layout,
keyboard and pointer activation, separator focus/hit/invoke suppression, narrow
clipping, styles, and exact cells.
