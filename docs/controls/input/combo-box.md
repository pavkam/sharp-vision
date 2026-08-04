# ComboBox

## Overview

`ComboBox` derives directly from `ControlBase`. It shows the selected value in a
compact field and owns a private [Popup](../popups/popup.md#overview) containing
a [ListView](../collections/list-view.md#overview) that opens immediately below
the field. The Popup clears its surface before the list renders, so choices
never show through content behind the drop-down. The connected frame omits the
edge that adjoins the field: with below placement the first ListView row sits at
`ComboBox.Bounds.Bottom`, while the above fallback omits the bottom edge and
puts the last ListView row immediately before `ComboBox.Bounds.Y`. The other
three frame edges stay visible.

The list uses the same keyboard, pointer, selection, and scrolling semantics as
a standalone list. When the resolved appearance supplies a
`VisualState.Selected` background, the selected choice fills the complete
interior row, including trailing blank cells, following the
[ListView row rendering contract](../collections/list-view.md#overview).

The selected value is the field's face, so `ComboBox` exposes neither `Content`
nor `Children`. It owns exactly one popup-layer framework part, and that
`Popup.Content` owns the private ListView. Keyboard and pointer press mechanics
are composed from the same internal behavior `PressableBase` uses, without
taking on its single-content inheritance role.

## API

| Member                                   | Default             | Purpose                                                                                                        |
| ---------------------------------------- | ------------------- | -------------------------------------------------------------------------------------------------------------- |
| `Items`                                  | Empty snapshot      | Copies the available choices into the private single-selection list.                                           |
| `SelectedIndex`                          | `-1`                | Selects one choice or clears the selection.                                                                    |
| `AllowNull`                              | `true`              | Allows Delete/Backspace to clear the selection.                                                                |
| `Placeholder`                            | `Select…`           | Face text shown when no item is selected.                                                                      |
| `DropDownHeight`                         | `8` cells           | Caps the ListView interior; the connected Popup's three visible frame edges are additional.                    |
| `IsOpen`                                 | `false`             | Controls popup layout, rendering, and hit testing.                                                             |
| `ScrollBars`, `ShowScrollBars`           | ListView defaults   | Configure overflow visibility in the private list.                                                             |
| `ScrollBarStyle`, `ActualScrollBarStyle` | `null`, Theme style | Override or inspect the private list's complete rail presentation.                                             |
| `RowHeight`                              | `null`              | Forwards to the private list's fixed row height, or `null` to size each row to its content.                    |
| `PopupStyle`, `ResetPopupStyle`          | `default`           | Overrides or resets the private Popup's border and shadow together (see [Popup](../popups/popup.md#overview)). |
| `DropDownGlyph`                          | Code-owned          | Overrides the validated one-cell disclosure marker.                                                            |
| `SelectionChanged`                       | No subscribers      | Reports the selection after `SelectedIndex` commits.                                                           |
| `ItemTemplate`                           | ListView default    | Forwards directly to the private ListView's own `ItemTemplate`, realizing each popup row.                      |
| `TextSelector`                           | `null`              | Projects an item to its closed-field and type-ahead text; falls back to `Convert.ToString`.                    |

`TextSelector` drives both the closed field's displayed text and keyboard
type-ahead matching through the same projection, so the two cannot drift from
each other or from a separately assigned `ItemTemplate`. Leave it unset to keep
the default `Convert.ToString(item, CultureInfo.InvariantCulture)` behavior for
items whose `ToString` override is already display-ready.

## Default field chrome

The closed field uses the global `ThemeRole.Input` profile. Bundled themes paint
its normal face with `ThemeColor.Surface` and provide a one-cell border on every
edge. The intrinsic [shared chrome](../../concepts/styling.md#shared-chrome)
reserves those cells before the selected label and the drop-down indicator.
ComboBox owns that chrome; applications do not assign raw borders to the
specialized control.

The private ListView opts out of its standalone default border because the Popup
owns the connected drop-down frame. This avoids nested chrome while keeping the
ListView's selection, scrolling, and surface appearance inside the Popup.

## Behavior

- `Items` copies the non-null choices into the owned single-selection list. When
  no selection exists yet, assigning a non-empty list selects the first item and
  raises `SelectionChanged`.
- `SelectedIndex` is `-1` or an index within `Items`; a committed selection
  publishes `PropertyChanged(SelectedIndex)` before `SelectionChanged`.
- `AllowNull` enables clearing with Delete or Backspace, and `Placeholder`
  supplies the closed-face text while the selection is empty.
- `DropDownHeight` is a positive maximum, in terminal cells, for the visible
  list.
- `ScrollBars`, `ShowScrollBars`, and `ScrollBarStyle` forward the common
  overflow policy to the owned ListView, so long choice popups use the same
  rails as standalone lists and viewports.
- `IsOpen` controls Popup layout, rendering, hit testing, and one
  `OutsideInteraction.Dismiss` plane rooted at the ComboBox while the field
  keeps focus. The popup is at least as wide as the field, and `DropDownHeight`
  limits only the list interior; the Popup adds its three visible frame edges
  outside that limit and keeps the open list above later page content, as
  defined by the [Popup contract](../popups/popup.md#overview).

`ComboBox` has no typed-text input path; it only selects from `Items`. For an
editable suggestion field, compose `TextInput` with a `Popup` directly.

## Code-owned glyphs

`DropDownGlyph` is a validated one-cell local override for the closed-field
indicator. `ResetDropDownGlyph()` restores the code-owned default disclosure
glyph. Replacing the Theme repaints a field that has no local override.

## Interaction

Enter, Space, or a primary pointer click toggles the list. The ComboBox stays
focused while the arrow keys move the private current item, Enter chooses it and
closes the list, and Escape closes without changing the selection. Pointer
clicks route through the Popup frame to the realized ListView item and use the
same semantic invocation path as Enter. Closed list cells neither render nor hit
test. When a close begins in the list, keyboard focus returns to the visible
ComboBox field during [Popup closing](../popups/popup.md#api), before the list
becomes unavailable.

The private ListView receives wheel input first. A wheel that changes its scroll
offset keeps the drop-down open. A wheel over the open drop-down that cannot
scroll any further is swallowed without closing it, while a wheel aimed outside
the ComboBox plane closes the drop-down and is consumed.

> [!IMPORTANT]
>
> **Implementation gap:** An endpoint wheel inside the open list is currently
> treated like outside interaction: when the ListView has no scroll range or is
> already at the requested endpoint, the unhandled wheel dismisses the
> drop-down.

### Keyboard navigation inside the popup

The popup and the inner ListView never enter sequential traversal. Tab closes or
commits the popup and then continues once through application traversal. The
arrow keys (Up/Down/Left/Right), Home, End, Page Up, and Page Down move between
items through the ListView's own keyboard handler.

Printable Unicode scalars provide basic case-insensitive type-to-select. The
search starts after the current item, wraps around once, and falls back to the
latest character when a longer prefix has no match. Closing the popup clears the
prefix so a later popup session starts fresh.

## Example

![The ComboBox control rendered in the live showcase](../../images/controls/combo-box.png)

![The ComboBox control with its popup open in the live showcase](../../images/controls/combo-box-open.png)

```csharp
var density = new ComboBox
{
    Items = ["Compact", "Comfortable", "Spacious"],
    SelectedIndex = 1,
    DropDownHeight = 5,
};
```

## Expected behavior

Assigned items are copied, indices are validated, and the connected popup
renders its exact cells in both below and above placement. The popup renders
opaquely and hit tests only what is visible. Popup arrangement, Escape, and
mouse selection through the Popup behave as documented, and keyboard focus,
navigation, and activation follow the rules above. A wheel left unhandled inside
the list, or aimed outside the plane, dismisses the drop-down without scrolling
a parent, while a scrollable list keeps it open. Resize and style states are
handled, and rendering produces the exact documented cells.
