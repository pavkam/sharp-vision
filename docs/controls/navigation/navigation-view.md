# NavigationView

## Overview

`NavigationView` provides a sidebar navigation control with typed items, groups,
separators, an optional header, and a pinned footer section. It extends
[`CompositeControl`](../composite-control.md#overview) with an internal `Dock`
layout: header docked top, footer docked bottom, and a scrollable items stack
filling the remainder.

The view defaults to no border and the active theme's `NavigationView.normal`
fill. That single continuous plane is the sidebar boundary instead of a frame
around every navigation region. The inherited chrome properties remain
caller-overridable. Item rows render with transparent normal and hover
backgrounds so the view owns one continuous surface; pointer-over and selected
rows use their matching theme states. Optional caller chrome follows the shared
[chrome contract](../../concepts/styling.md#shared-chrome).

Items, groups, and separators are managed through typed collections. Selection
is flat across all `NavigationViewItem` entries in both the main and footer
sections. Group headers participate in the current keyboard order but never
become `SelectedItem`; separators are skipped entirely.

## API

| Member                 | Default                | Purpose                                                            |
| ---------------------- | ---------------------- | ------------------------------------------------------------------ |
| `Header`               | `null`                 | Shows an optional bold title above the main section.               |
| `Items`                | Empty typed collection | Owns main items, groups, and separators in a scrollable section.   |
| `FooterItems`          | Empty typed collection | Owns equivalent entries pinned below the main section.             |
| `SelectedItem`         | `null`, read-only      | Reports the selected `NavigationViewItem` across both sections.    |
| `SelectItem(...)`      | —                      | Selects one item owned by this view without moving keyboard focus. |
| `SelectionChanged`     | No subscribers         | Reports committed selection.                                       |
| `ScrollBarStyle`       | `null`                 | Local style for the generated bar; null resolves to `ThinLine`.    |
| `ActualScrollBarStyle` | `ThinLine`, read-only  | Reports the style applied to the generated bar.                    |

## Behavior

- `Header` (string?) — optional title rendered bold at the top. Hidden when null
  or empty.
- `Items` (NavigationViewEntryCollection) — typed main item collection accepting
  `NavigationViewItem`, `NavigationViewGroup`, and `NavigationViewSeparator`.
- `FooterItems` (NavigationViewEntryCollection) — typed footer items pinned to
  the bottom, same typed overloads.
- `SelectedItem` (NavigationViewItem?) — the currently selected item, or null.
- `SelectItem(NavigationViewItem)` — selects an owned item without moving
  keyboard focus; rejects null and items owned by another navigation view.
- `SelectionChanged` — fires after a committed selection change.

The view is the single sidebar tab stop (`TabNavigation.None`); item and group
faces never receive keyboard focus. Up/Down arrows move the current entry across
available group headers and items while skipping separators. PageUp/PageDown
move by as many entries as fill the committed viewport height, and are handled
even when they cannot move further, so the key never escapes to page an
enclosing scrollable container. Home/End move to the first or last available
entry. Current entries scroll into view automatically. Enter and Space toggle a
current group or invoke a current item without transferring focus. When no entry
is current, activation establishes the first available entry before applying its
action.

## NavigationViewItem

Extends [`Pressable`](../pressable.md#overview). `Header` (string) is the label
text. `Glyph` (string?) is an optional prefix shown before the header. Renders
as `› Header` when selected or hovered, `· Header` otherwise. Pointer or
programmatic selection updates the owning `NavigationView`; the item remains
non-focusable and non-tab-stop.

## NavigationViewGroup

A collapsible labeled section. `Header` (string) is the group label rendered
with the code-owned expanded or collapsed disclosure glyph. `IsExpanded` (bool,
default true) controls sub-item visibility. Sub-items are `NavigationViewItem`
instances owned by the group's `Items` collection. Enter or Space on the current
group toggles its expansion while the owning `NavigationView` retains focus.
Collapsing a group whose descendant is current moves current state to the group
header and repairs any now-hidden selection.

## NavigationViewSeparator

A non-interactive horizontal divider line. Not focusable, not hit-testable.

## Code-owned glyphs

Idle/current item markers, group disclosure markers, and navigation separators
resolve from `the code-owned navigation glyph defaults`. `IdleMarker`,
`CurrentMarker`, `CollapsedGlyph`, `ExpandedGlyph`, and
`NavigationViewSeparator.Glyph` provide validated local overrides.
`ResetMarkers()`, `ResetGlyphs()`, and `ResetGlyph()` clear the corresponding
item, group, and separator overrides.

## Example

![The NavigationView control rendered in the live showcase](../../images/controls/navigation-view.png)

```csharp
var nav = new NavigationView { Header = "MY APP" };
nav.Items.Add(new NavigationViewItem { Header = "Dashboard", Glyph = "📊" });

var settings = new NavigationViewGroup { Header = "Settings" };
settings.Items.Add(new NavigationViewItem { Header = "General" });
settings.Items.Add(new NavigationViewItem { Header = "Advanced" });
nav.Items.Add(settings);

nav.Items.Add(new NavigationViewSeparator());
nav.FooterItems.Add(new NavigationViewItem { Header = "Quit", Glyph = "🚪" });
nav.SelectItem((NavigationViewItem)nav.Items[0]);

nav.SelectionChanged += (_, _) =>
    Console.WriteLine($"Selected: {nav.SelectedItem?.Header}");
```

Ampersands in item and group headers declare
[access keys](../../concepts/access-keys.md#focus-and-semantic-actions). An item
mnemonic focuses the view, makes that item current, and invokes/selects it. A
group mnemonic focuses the view, makes the group current, and toggles expansion.

## Expected behavior

Cover typed item addition, owned and foreign programmatic selection, retained
owner focus with group-and-item keyboard navigation, Enter/Space group
expand/collapse, selection repair for collapsed descendants, separator
non-interactivity, footer item separation, header rendering, direct and grouped
pointer selection, forwarding-handler cleanup after grouped item removal or
clear, item removal clearing selection, and final cells.
