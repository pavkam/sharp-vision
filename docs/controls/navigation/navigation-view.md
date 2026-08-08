# NavigationView

## Overview

`NavigationView` is a sidebar navigation control with typed items, groups,
separators, an optional header, and a pinned footer section. It extends
[`CompositeControlBase`](../composite-control.md#overview) with an internal
`Dock` layout: the header docks to the top, the footer docks to the bottom, and
a scrollable items stack fills the remainder.

The view defaults to no border and the active theme's `NavigationView.normal`
fill. That single continuous plane forms the sidebar boundary, rather than a
frame around every navigation region. The inherited chrome properties remain
open to caller overrides. Item rows render with transparent normal and hover
backgrounds so the view keeps one continuous surface; pointer-over and selected
rows use their matching theme states. Optional caller chrome follows the shared
[chrome contract](../../concepts/styling.md#shared-chrome).

Items, groups, and separators are managed through typed collections. Selection
is flat across all `NavigationViewItem` entries in both the main and footer
sections. Group headers participate in the current keyboard order but never
become `SelectedItem`, and separators are skipped entirely.

## API

| Member                 | Default                | Purpose                                                            |
| ---------------------- | ---------------------- | ------------------------------------------------------------------ |
| `Header`               | `null`                 | Shows an optional bold title above the main section.               |
| `Items`                | Empty typed collection | Holds main items, groups, and separators in a scrollable section.  |
| `FooterItems`          | Empty typed collection | Holds equivalent entries pinned below the main section.            |
| `SelectedItem`         | `null`, read-only      | Reports the selected `NavigationViewItem` across both sections.    |
| `SelectItem(...)`      | —                      | Selects one item owned by this view without moving keyboard focus. |
| `SelectionChanged`     | No subscribers         | Reports a committed selection change.                              |
| `ScrollBarStyle`       | `null`                 | Local style for the generated bar; null resolves to `ThinLine`.    |
| `ActualScrollBarStyle` | `ThinLine`, read-only  | Reports the style applied to the generated bar.                    |
| `LineSize`             | `1` cell               | Non-negative wheel-scroll increment forwarded to the bar.          |
| `PageOverlap`          | `0` cells              | Non-negative context retained by PageUp and PageDown.              |

## Behavior

- `Header` (string?) is an optional title rendered bold at the top. It is hidden
  when null or empty.
- `Items` (NavigationViewEntryCollection) is the typed main collection and
  accepts `NavigationViewItem`, `NavigationViewGroup`, and
  `NavigationViewSeparator`.
- `FooterItems` (NavigationViewEntryCollection) holds the footer entries pinned
  to the bottom, with the same typed overloads.
- `SelectedItem` (NavigationViewItem?) is the currently selected item, or null.
- `SelectItem(NavigationViewItem)` selects an item this view owns without moving
  keyboard focus. It rejects null and items owned by another navigation view.
- `SelectionChanged` fires after a committed selection change.

The view is the single sidebar tab stop (`TabNavigation.None`); item and group
faces never receive keyboard focus themselves. Up and Down arrows move the
current entry across the available group headers and items while skipping
separators. PageUp and PageDown move by as many entries as fill the committed
viewport height minus `PageOverlap`, and they are handled even when the cursor
cannot move any further, so the key never escapes to page an enclosing
scrollable container. Home and End move to the first or last available entry.
The current entry scrolls into view automatically. Enter and Space toggle a
current group or invoke a current item without transferring focus. When no entry
is current, activation first establishes the first available entry and then
applies its action.

`LineSize` forwards the mouse wheel's cell step to the generated scroll
container; keyboard Up and Down always move by exactly one entry regardless of
this value.

## NavigationViewItem

Extends [`PressableBase`](../pressable.md#overview). The inherited `Text`
(string) is the label text, and `Glyph` (string?) is an optional prefix shown
before it. The item renders as `› Text` when selected or hovered and as `· Text`
otherwise. Pointer or programmatic selection updates the owning
`NavigationView`; the item itself stays non-focusable and outside the tab order.

Activation raises `Invoked` (`EventHandler<ActivationEventArgs>`), then invokes
the inherited `Command` with `CommandParameter` when one is bound and
`CanExecute` allows it. `PerformInvoke()` activates the item programmatically
through the same path when it is enabled and visible.

## NavigationViewGroup

A collapsible labeled section. `Header` (string) is the group label, rendered
with the code-owned expanded or collapsed disclosure glyph. `Expanded` (bool,
default true) controls sub-item visibility. Sub-items are `NavigationViewItem`
instances owned by the group's `Items` collection. Enter or Space on the current
group toggles its expansion while the owning `NavigationView` keeps focus.
Collapsing a group whose descendant is current moves the current state to the
group header and repairs any now-hidden selection.

## NavigationViewSeparator

A non-interactive horizontal divider line. It is not focusable and not
hit-testable.

## Code-owned glyphs

Idle and current item markers, group disclosure markers, and navigation
separators resolve from the code-owned navigation glyph defaults. `IdleMarker`,
`CurrentMarker`, `CollapsedGlyph`, `ExpandedGlyph`, and
`NavigationViewSeparator.Glyph` provide validated local overrides.
`ResetMarkers()`, `ResetGlyphs()`, and `ResetGlyph()` clear the corresponding
item, group, and separator overrides.

## Example

![The NavigationView control rendered in the live showcase](../../images/controls/navigation-view.png)

```csharp
var nav = new NavigationView { Header = "MY APP" };
nav.Items.Add(new NavigationViewItem { Text = "Dashboard", Glyph = "📊" });

var settings = new NavigationViewGroup { Header = "Settings" };
settings.Items.Add(new NavigationViewItem { Text = "General" });
settings.Items.Add(new NavigationViewItem { Text = "Advanced" });
nav.Items.Add(settings);

nav.Items.Add(new NavigationViewSeparator());
nav.FooterItems.Add(new NavigationViewItem { Text = "Quit", Glyph = "🚪" });
nav.SelectItem((NavigationViewItem)nav.Items[0]);

nav.SelectionChanged += (_, _) =>
    Console.WriteLine($"Selected: {nav.SelectedItem?.Text}");
```

Ampersands in item and group headers declare
[access keys](../../concepts/access-keys.md#focus-and-semantic-actions). An item
mnemonic focuses the view, makes that item current, and invokes and selects it.
A group mnemonic focuses the view, makes the group current, and toggles its
expansion.

## Expected behavior

- The typed collections accept their documented entry types, and programmatic
  selection accepts owned items while rejecting foreign ones.
- The view keeps keyboard focus while navigating across group headers and items,
  Enter and Space expand or collapse groups, and collapsing a group repairs a
  now-hidden current entry or selection.
- Separators are non-interactive, footer items stay separated from the main
  section, and the header renders as documented.
- Pointer selection works both directly and through grouped items, and
  forwarding handlers are cleaned up when grouped items are removed or the
  collection is cleared.
- Removing the selected item clears the selection.
- Final rendering resolves deterministically to the exact cells.
