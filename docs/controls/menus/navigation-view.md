# NavigationView

## NavigationView contract

`NavigationView` provides a retained sidebar with typed items, collapsible
groups, separators, an optional header, and a pinned footer section. It extends
[`CompositeControl`](../composite-control.md#compositecontrol-contract) with a
private `Dock`: the header is docked top, the footer is docked bottom, and a
scrollable main `Stack` fills the remainder.

Items, groups, and separators enter only through typed collection overloads.
Selection is flat across all `NavigationViewItem` entries in the main and footer
sections, including expanded group descendants. Groups and separators are not
selected items.

This implementation was reconciled from the user-owned NavigationView slice in
commit `d0bc8e8`; its retained composite shape and public names are preserved.

## API

- `Header` is an optional title rendered bold at the top and hidden when null or
  empty.
- `Items` is the typed main collection accepting `NavigationViewItem`,
  `NavigationViewGroup`, and `NavigationViewSeparator` overloads.
- `FooterItems` exposes the same typed overloads for entries pinned to the
  bottom.
- `SelectedItem` returns the selected `NavigationViewItem`, or null.
- `VerticalOffset` reports the main scrolling section's cell offset.
- `SelectionChanged` fires after a changed selected identity and item visual
  state commit.

Tab navigation cycles within the sidebar. Up and Down navigate selectable items
in main, expanded-group, then footer order while skipping effectively hidden or
disabled entries. Focus or eligible pointer/keyboard activation selects an item.
Main items use the main scrolling host's `BringIntoView`; footer items remain
pinned and never change the main offset.

## NavigationViewItem

`NavigationViewItem` extends [`Pressable`](../pressable.md#pressable-contract).
`Header` is the non-null label and `Glyph` is an optional prefix. It renders `›`
when selected or hovered and `·` otherwise. Focusing an owned item selects it.
`Invoked` reports keyboard or pointer activation after the Pressable transition
completes.

## NavigationViewGroup

`NavigationViewGroup` is a focusable collapsible labeled section. `Header` is
the non-null label, rendered with `▼` while expanded or `▶` while collapsed.
`IsExpanded` defaults true. `AddItem`, `RemoveItem`, and `ClearItems` manage
retained `NavigationViewItem` descendants. Enter toggles a focused group.

## NavigationViewSeparator

`NavigationViewSeparator` is a non-focusable, non-hit-testable one-row
horizontal divider.

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
```

## Test obligations

Cover typed ownership and validation, header/main/footer composition, flat
selection and event order, pointer and keyboard parity, focus and tab-stop
policy, group expansion/collapse, disabled/hidden skipping, removal and
availability repair, separator non-interactivity, correct section scrolling,
footer pinning, Unicode/wide cells, tiny bounds, mutation, resize, stale-cell
clearing, final semantic cells, and representative showcase rendering.
