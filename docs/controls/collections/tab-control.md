# TabControl

## TabControl contract

`TabControl` arranges typed [`TabItem`](#tabitem) pages and coordinates a header
bar, keyboard navigation, and content visibility. It extends
[`ItemsControl`](../items-control.md) with a private vertical `Stack` as the
presentation host. Only the selected tab's content is visible; other pages are
collapsed during arrangement.

The header bar renders in the first row using tab labels separated by vertical
dividers. A horizontal rule separates headers from content. Keyboard Left/Right
arrow keys switch the selected tab.

## API

- `Items : TabItems` exposes typed `Add`/`Remove`/`Clear` overloads for
  `TabItem`. Arbitrary controls cannot enter through the semantic collection.
- `SelectedIndex` tracks the active page; `-1` clears selection. The first added
  tab auto-selects.
- `SelectionChanged` fires after a committed index change.

## TabItem

`TabItem` extends
[`ContentControl`](../content-control.md#contentcontrol-contract). `Header` is a
non-null string rendered in the tab bar. `Content` is the single owned child
arranged below the header when this page is selected.

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

Cover typed ownership, default selection, selection change event, header
rendering, content visibility toggle, keyboard Left/Right navigation, tab
removal index adjustment, zero bounds, and final cells.
