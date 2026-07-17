# NavigationView

## NavigationView contract

`NavigationView` provides a sidebar navigation control with typed items, groups,
separators, an optional header, and a pinned footer section. It extends
[`CompositeControl`](../composite-control.md#compositecontrol-contract) with an
internal `Dock` layout: header docked top, footer docked bottom, and a
scrollable items stack filling the remainder.

Items, groups, and separators are managed through typed collections. Selection
is flat across all `NavigationViewItem` entries in both the main and footer
sections. Group headers participate in the current keyboard order but never
become `SelectedItem`; separators are skipped entirely.

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

The view is the single sidebar tab stop (`TabNavigation.None`); item and group
faces never receive keyboard focus. Up/Down arrows move the current entry across
available group headers and items while skipping separators. Home/End move to
the first or last available entry. Current entries scroll into view
automatically. Enter and Space toggle a current group or invoke a current item
without transferring focus. When no entry is current, activation establishes the
first available entry before applying its action.

## NavigationViewItem

Extends [`Pressable`](../pressable.md#pressable-contract). `Header` (string) is
the label text. `Glyph` (string?) is an optional prefix shown before the header.
Renders as `› Header` when selected or hovered, `· Header` otherwise. Pointer or
programmatic selection updates the owning `NavigationView`; the item remains
non-focusable and non-tab-stop.

## NavigationViewGroup

A collapsible labeled section. `Header` (string) is the group label rendered
with the theme's expanded or collapsed disclosure glyph. `IsExpanded` (bool,
default true) controls sub-item visibility. Sub-items are `NavigationViewItem`
instances added via the group's internal `AddItem` method. Enter or Space on the
current group toggles its expansion while the owning `NavigationView` retains
focus. Collapsing a group whose descendant is current moves current state to the
group header and repairs any now-hidden selection.

## NavigationViewSeparator

A non-interactive horizontal divider line. Not focusable, not hit-testable.

## Theme glyphs

Idle/current item markers, group disclosure markers, and navigation separators
resolve from `Theme.Glyphs.Navigation`. `IdleMarker`, `CurrentMarker`,
`CollapsedGlyph`, `ExpandedGlyph`, and `NavigationViewSeparator.Glyph` provide
validated local overrides. `ResetMarkers()`, `ResetGlyphs()`, and `ResetGlyph()`
clear the corresponding item, group, and separator overrides.

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

Cover typed item addition, owned and foreign programmatic selection, retained
owner focus with group-and-item keyboard navigation, Enter/Space group
expand/collapse, selection repair for collapsed descendants, separator
non-interactivity, footer item separation, header rendering, item removal
clearing selection, and final cells.
