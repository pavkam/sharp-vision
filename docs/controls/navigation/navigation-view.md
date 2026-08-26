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

`NavigationViewItem`, `NavigationViewGroup`, and `NavigationViewSeparator` are
the three entry types this page also documents, since each exists only to
populate a `NavigationView`.

## Inheritance

```mermaid
classDiagram
    ControlBase <|-- CompositeControlBase
    CompositeControlBase <|-- NavigationView
    ControlBase <|-- InputBase
    InputBase <|-- NavigationViewItem
    ControlBase <|-- NavigationViewGroup
    ControlBase <|-- NavigationViewSeparator
```

## API

| Member                                                     | Type                                                    | Default        | Description                                                                   |
| ---------------------------------------------------------- | ------------------------------------------------------- | -------------- | ----------------------------------------------------------------------------- |
| `Header`                                                   | `string?`                                               | `null`         | Shows an optional bold title above the main section.                          |
| `Items`                                                    | `NavigationViewEntryCollection`                         | Empty          | Holds main items, groups, and separators in a scrollable section.             |
| `FooterItems`                                              | `NavigationViewEntryCollection`                         | Empty          | Holds equivalent entries pinned below the main section.                       |
| `SelectedItem`                                             | `NavigationViewItem?`                                   | `null`         | Read-only; the selected item across both sections.                            |
| `ScrollBarStyle`                                           | `ScrollBarStyle?`                                       | `null`         | Local style for the generated bar; `null` keeps theme resolution.             |
| `ActualScrollBarStyle`                                     | `ScrollBarStyle`                                        | Resolved       | Read-only; the style applied to the generated bar.                            |
| `Extent`                                                   | `Size`                                                  | Empty          | Read-only; the committed non-negative content extent of the scroll container. |
| `Viewport`                                                 | `Size`                                                  | Empty          | Read-only; the committed non-negative visible extent of the scroll container. |
| `HorizontalOffset`                                         | `int`                                                   | `0`            | The valid horizontal content offset of the generated scroll container.        |
| `VerticalOffset`                                           | `int`                                                   | `0`            | The valid vertical content offset of the generated scroll container.          |
| `LineSize`                                                 | `int`                                                   | `1`            | Non-negative wheel-scroll increment forwarded to the bar.                     |
| `PageOverlap`                                              | `int`                                                   | `0`            | Non-negative context retained by PageUp and PageDown.                         |
| `SelectItem(NavigationViewItem item)`                      | `void`                                                  | —              | Selects one item owned by this view without moving keyboard focus.            |
| `ScrollBy(int x, int y, ScrollCause cause = Programmatic)` | `bool`                                                  | —              | Scrolls the generated scroll container by signed cell deltas.                 |
| `BringItemIntoView(NavigationViewItem item)`               | `bool`                                                  | —              | Scrolls minimally to expose one owned entry.                                  |
| `SelectionChanged`                                         | `EventHandler<NavigationViewSelectionChangedEventArgs>` | No subscribers | Reports a committed selection change.                                         |
| `ScrollChanged`                                            | `EventHandler<ScrollChangedEventArgs>`                  | No subscribers | Raised after the generated scroll container's offset commits.                 |

## Behavior

- `Header` is hidden when null or empty.
- `Items` and `FooterItems` accept `NavigationViewItem`, `NavigationViewGroup`,
  and `NavigationViewSeparator` through the same typed overloads.
- Moving an entry within either collection reorders the existing identity
  without detaching, reparenting, blurring, or reattaching it.
- `SelectItem` rejects null and items owned by another navigation view.
- `SelectionChanged` fires after a committed selection change. If an
  `IsSelected` or `SelectedItem` observer synchronously selects another item,
  the newer selection owns the visual markers, public property, and typed event;
  the superseded transition publishes nothing further.

The view is the single sidebar tab stop (`TabNavigation.None`); item and group
faces never receive keyboard focus themselves. Up and Down arrows move the
current entry across the available group headers and items while skipping
separators. PageUp and PageDown move by as many entries as fill the committed
viewport height minus `PageOverlap`, and they are handled even when the cursor
cannot move any further, so the key never escapes to page an enclosing
scrollable container. Home and End move to the first or last available entry.
The current entry scrolls into view automatically. Enter and Space toggle a
current group or invoke a current item without transferring focus, firing once
per key hold and only with activation-eligible modifiers, while the navigation
keys repeat while held. When no entry is current, activation first establishes
the first available entry and then applies its action.

`LineSize` forwards the mouse wheel's cell step to the generated scroll
container; keyboard Up and Down always move by exactly one entry regardless of
this value.

## NavigationViewItem

Extends [`InputBase`](../input-base.md#overview), opting into press activation
and a command but not the shared caption capability - it owns its label text
directly. The item renders as `› Text` when selected or hovered and as `· Text`
otherwise. Pointer or programmatic selection updates the owning
`NavigationView`; the item itself stays non-focusable and outside the tab order.

| Member                       | Type                                | Default        | Description                                                                                                |
| ---------------------------- | ----------------------------------- | -------------- | ---------------------------------------------------------------------------------------------------------- |
| Inherited `Text`             | `string`                            | `""`           | The label text.                                                                                            |
| `Glyph`                      | `string?`                           | `null`         | An optional prefix shown before the label.                                                                 |
| `IsSelected`                 | `bool`                              | `false`        | Read-only; whether this entry is the navigation view's selected item.                                      |
| `StartAffix`                 | `Affix?`                            | `null`         | Optional leading edge-pinned decoration, reserved after the marker and glyph prefix and outside the label. |
| `EndAffix`                   | `Affix?`                            | `null`         | Optional trailing edge-pinned decoration, reserved outside the label at the content box's far edge.        |
| `Style`                      | `NavigationViewItemStyle?`          | `null`         | Gets or sets the complete local presentation, including the idle and current markers.                      |
| `ActualStyle`                | `NavigationViewItemStyle`           | Resolved       | Read-only; the complete local, theme-owned, or code-owned presentation.                                    |
| Inherited `Command`          | `ICommand?`                         | `null`         | Runs after `Invoked`, when bound and `CanExecute` allows it.                                               |
| Inherited `CommandParameter` | `object?`                           | `null`         | The borrowed parameter passed to `Command` queries and execution.                                          |
| `PerformInvoke()`            | `void`                              | —              | Activates the item programmatically when it is enabled and visible.                                        |
| `Invoked`                    | `EventHandler<ActivationEventArgs>` | No subscribers | Raised after keyboard or pointer activation requests navigation.                                           |

Activation raises `Invoked`, then invokes the inherited `Command` with
`CommandParameter` when one is bound and `CanExecute` allows it. That command
binding is captured before `Invoked`, so a callback may rebind or dispose the
item without changing the activation already in progress.

`StartAffix` and `EndAffix` reserve fixed cell columns beside the label, the
same seam `Button` and `HyperlinkButton` expose (see
[styling.md](../../concepts/styling.md#instance-content-affix)). The marker and
any `Glyph` prefix always stay outboard of both affixes; the reserved layout
inside the remainder of the content box is `[start][gap] label [gap][end]`. The
gap comes from `NavigationViewItemStyle.AffixGap`, declared directly on that
style rather than forwarded from a shared input style, since
`NavigationViewItem` does not route its label through the caption capability
other `InputBase` controls share. When the content box is too narrow for
everything, the label shrinks first, then the end affix drops whole, then the
start affix - never a partial cluster.

## NavigationViewGroup

A collapsible labeled section owned by `NavigationView.Items` or `FooterItems`.
Sub-items are `NavigationViewItem` instances owned by the group's own `Items`
collection. Enter or Space on the current group toggles its expansion while the
owning `NavigationView` keeps focus. Collapsing a group whose descendant is
current moves the current state to the group header and repairs any now-hidden
selection.

| Member        | Type                           | Default  | Description                                                                                    |
| ------------- | ------------------------------ | -------- | ---------------------------------------------------------------------------------------------- |
| `Header`      | `string`                       | `""`     | The group label, rendered with the resolved expanded or collapsed disclosure glyph.            |
| `Items`       | `NavigationViewItemCollection` | Empty    | Holds this group's sub-items.                                                                  |
| `IsExpanded`  | `bool`                         | `true`   | Controls sub-item visibility.                                                                  |
| `Style`       | `NavigationViewGroupStyle?`    | `null`   | Gets or sets the complete local presentation, including the disclosure glyphs and item indent. |
| `ActualStyle` | `NavigationViewGroupStyle`     | Resolved | Read-only; the complete local, theme-owned, or code-owned presentation.                        |

## NavigationViewSeparator

A non-interactive horizontal divider line. It is not focusable and not
hit-testable.

| Member        | Type                            | Default  | Description                                                             |
| ------------- | ------------------------------- | -------- | ----------------------------------------------------------------------- |
| `Style`       | `NavigationViewSeparatorStyle?` | `null`   | Gets or sets the complete local presentation, including the rule glyph. |
| `ActualStyle` | `NavigationViewSeparatorStyle`  | Resolved | Read-only; the complete local, theme-owned, or code-owned presentation. |

## Style-resolved glyphs

The idle and current item markers, the group's expanded and collapsed disclosure
glyphs, and the separator's own rule glyph resolve from each control's
`ActualStyle`, not from an individual property or `Reset*` method on the
control. `NavigationViewItemStyle` carries the immutable `IdleMarker` and
`CurrentMarker`; `NavigationViewGroupStyle` carries `CollapsedGlyph`,
`ExpandedGlyph`, and `ItemIndent`; `NavigationViewSeparatorStyle` carries
`Glyph`. When no local `Style` is assigned, `ActualStyle` completes from the
library's code-owned markers; the theme's `glyphs` family does not cover
navigation markers, so a local `Style` is the only way to move them.

> [!NOTE]
>
> To override a marker, assign a complete local `Style` — for example
> `item.Style = item.ActualStyle with { CurrentMarker = new Rune('▶') }` —
> rather than looking for a single-glyph property. Assigning `Style = null`
> returns the control to code-owned ownership; there is no separate reset method
> for an individual marker.

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

| Scope               | Observable evidence                                                       |
| ------------------- | ------------------------------------------------------------------------- |
| Public API          | Validation, defaults, state changes, and deterministic output.            |
| Integrated behavior | Cross-component behavior through the real ownership and routing boundary. |

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
