# NavigationView

## NavigationView contract

`NavigationView` provides a sidebar navigation control with typed items, groups,
separators, an optional header, and a pinned footer section. It extends
[`CompositeControl`](../composite-control.md#compositecontrol-contract) with an
internal `Dock` layout: header docked top, footer docked bottom, and a
scrollable items stack filling the remainder.

Items, groups, and separators are managed through typed collections. Selection
is flat across all `NavigationViewItem` entries in both the main and footer
sections. Groups and separators are not selectable.

## API

- `Header` (string?) — optional title rendered bold at the top. Hidden when null
  or empty.
- `Items` (NavigationViewItems) — typed main item collection accepting
  `NavigationViewItem`, `NavigationViewGroup`, and `NavigationViewSeparator`.
- `FooterItems` (NavigationViewItems) — typed footer items pinned to the bottom,
  same typed overloads.
- `SelectedItem` (NavigationViewItem?) — the currently selected item, or null.
- `SelectItem(NavigationViewItem)` — selects an owned item without moving
  keyboard focus; rejects null and items owned by another navigation view.
- `SelectionChanged` — fires after a committed selection change.

The view is the single sidebar tab stop (`TabNavigation.None`); items are
private presentation faces and never receive keyboard focus. Up/Down arrows
navigate between selectable items, skipping groups and separators. Items scroll
into view automatically. Enter and Space are consumed by the selected view
without transferring focus to an item face.

## NavigationViewItem

Extends [`Pressable`](../pressable.md#pressable-contract). `Header` (string) is
the label text. `Glyph` (string?) is an optional prefix shown before the header.
Renders as `› Header` when selected or hovered, `· Header` otherwise. Pointer or
programmatic selection updates the owning `NavigationView`; the item remains
non-focusable and non-tab-stop.

## NavigationViewGroup

A collapsible labeled section. `Header` (string) is the group label rendered
with a toggle glyph (`▼` expanded, `▶` collapsed). `IsExpanded` (bool, default
true) controls sub-item visibility. Sub-items are `NavigationViewItem` instances
added via the group's internal `AddItem` method. Pressing Enter on a focused
group toggles its expansion.

## NavigationViewSeparator

A non-interactive horizontal divider line. Not focusable, not hit-testable.

## Example

```csharp
var nav = new NavigationView { Header = "MY APP" };
nav.Items.Add(new NavigationViewItem { Header = "Dashboard", Glyph = "📊" });

var settings = new NavigationViewGroup { Header = "Settings" };
settings.AddItem(new NavigationViewItem { Header = "General" });
settings.AddItem(new NavigationViewItem { Header = "Advanced" });
nav.Items.Add(settings);

nav.Items.Add(new NavigationViewSeparator());
nav.FooterItems.Add(new NavigationViewItem { Header = "Quit", Glyph = "🚪" });
nav.SelectItem((NavigationViewItem)nav.Items[0]);

nav.SelectionChanged += (_, _) =>
    Console.WriteLine($"Selected: {nav.SelectedItem?.Header}");
```

## Test obligations

Cover typed item addition, owned and foreign programmatic selection, owner focus
with Up/Down keyboard navigation, group expand/collapse with sub-items,
separator non-interactivity, footer item separation, header rendering, item
removal clearing selection, and final cells.
