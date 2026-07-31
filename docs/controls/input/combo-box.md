# ComboBox

## ComboBox contract

`ComboBox` derives directly from `Control`. It displays one selected value in a
compact field and owns a private [Popup](../popups/popup.md#popup-contract)
containing a [ListView](../collections/list-view.md#listview-contract)
immediately below the field when open. The Popup clears its surface before the
list renders, so choices never show through content behind the drop-down. Its
connected frame omits the edge that adjoins the field: below placement puts the
first ListView row at `ComboBox.Bounds.Bottom`, while an above fallback omits
the bottom edge and puts the last ListView row immediately before
`ComboBox.Bounds.Y`. The remaining three frame edges stay visible.

The list uses the same keyboard, pointer, selection, and scrolling semantics as
a standalone list. When the resolved appearance supplies a
`VisualState.Selected` background, the selected choice fills the complete
interior row, including trailing blank cells, under the
[ListView row rendering contract](../collections/list-view.md#listview-contract).

The selected value is the field's face; `ComboBox` therefore exposes neither
`Content` nor `Children`. It owns exactly one popup-layer framework part, and
that `Popup.Content` owns the private ListView. Keyboard and pointer press
mechanics are composed from the same internal behavior used by `Pressable`,
without claiming its single-content inheritance role.

## API

| Member                                   | Default             | Purpose                                                                                     |
| ---------------------------------------- | ------------------- | ------------------------------------------------------------------------------------------- |
| `Items`                                  | Empty snapshot      | Copies the available choices into the private single-selection list.                        |
| `SelectedIndex`                          | `-1`                | Selects one choice or clears selection.                                                     |
| `AllowNull`                              | `true`              | Allows Delete/Backspace to clear the selection.                                             |
| `Placeholder`                            | `Select…`           | Face text shown when no item is selected.                                                   |
| `DropDownHeight`                         | `8` cells           | Caps the ListView interior; the connected Popup's three visible frame edges are additional. |
| `IsOpen`                                 | `false`             | Controls popup layout, rendering, and hit testing.                                          |
| `ScrollBars`, `ShowScrollBars`           | ListView defaults   | Configure overflow visibility in the private list.                                          |
| `ScrollBarStyle`, `ActualScrollBarStyle` | `null`, Theme style | Override or inspect the private list's complete rail presentation.                          |
| `DropDownGlyph`                          | Code-owned          | Overrides the validated one-cell disclosure marker.                                         |
| `SelectionChanged`                       | No subscribers      | Reports selection after `SelectedIndex` commits.                                            |
| `ItemTemplate`                           | ListView default    | Forwards directly to the private ListView's own `ItemTemplate`, realizing each popup row.   |
| `TextSelector`                           | `null`              | Projects an item to its closed-field and type-ahead text; falls back to `Convert.ToString`. |

`TextSelector` drives both the closed field's displayed text and keyboard
type-ahead matching through the same projection, so the two cannot drift from
each other or from a separately assigned `ItemTemplate`. Leave it unset to keep
the existing `Convert.ToString(item, CultureInfo.InvariantCulture)` behavior for
items whose `ToString` override is already display-ready.

## Default field chrome

The closed field selects the global `ThemeRole.Input` profile. Bundled themes
paint its normal face with `ThemeColor.Surface` and provide a one-cell border on
every edge. The intrinsic
[shared chrome](../../concepts/styling.md#shared-chrome) reserves those cells
before the selected label and drop-down indicator. ComboBox owns that chrome;
applications do not assign raw borders to the specialized control.

The private ListView opts out of its standalone default border because Popup
owns the connected drop-down frame. This prevents nested chrome while retaining
the ListView's selection, scrolling, and surface appearance inside the Popup.

## Behavior

- `Items` copies non-null choices into the owned single-selection list.
- `SelectedIndex` is `-1` or an index within `Items`; a committed selection
  publishes `PropertyChanged(SelectedIndex)` before `SelectionChanged`.
- `AllowNull` enables Delete/Backspace clearing, and `Placeholder` supplies the
  closed-face text while the selection is empty.
- `DropDownHeight` is a positive terminal-cell maximum for the visible list.
- `ScrollBars`, `ShowScrollBars`, and `ScrollBarStyle` forward the common
  overflow policy to the owned ListView, so long choice popups use the same
  rails as standalone lists and viewports.
- `IsOpen` controls Popup layout, rendering, hit testing, and one
  `OutsideInteraction.Dismiss` plane rooted at ComboBox while the field remains
  the focus owner. The popup width is at least the field width, while
  `DropDownHeight` limits only the list interior; the Popup adds its three
  visible frame edges outside that limit and keeps the open list above later
  page content as defined by the
  [Popup contract](../popups/popup.md#popup-contract).

`ComboBox` has no typed-text input path; it selects from `Items` only. Compose
`TextInput` with a `Popup` directly for an editable suggestion field.

## Code-owned glyphs

`DropDownGlyph` is a validated one-cell local override for the closed-field
indicator. `ResetDropDownGlyph()` returns the field to
`the code-owned disclosure glyph defaults.DropDown`. Theme replacement repaints
an existing non-overridden field.

## Interaction

Enter, Space, or a primary pointer click toggles the list. ComboBox remains
focused while arrows navigate the private current item, Enter chooses it and
closes the list, and Escape closes without changing the selection. Pointer
clicks route through the Popup frame to the realized ListView item, using the
same semantic invocation path as Enter. Closed list cells neither render nor hit
test. A close that began in the list restores keyboard focus to the visible
ComboBox field during [Popup closing](../popups/popup.md#api), before the list
becomes unavailable.

The private ListView receives wheel input first. A wheel that changes its scroll
offset keeps the drop-down open. When the ListView has no range or is at the
requested endpoint, the unhandled wheel closes the Dismiss plane and is not
offered to an ancestor viewport. A wheel targeted outside the ComboBox plane has
the same close-and-consume behavior.

### Keyboard navigation inside the popup

The popup and inner ListView never enter sequential traversal. Tab closes or
commits the popup and then continues once through application traversal. Arrow
keys (Up/Down/Left/Right), Home, End, Page Up, and Page Down navigate between
items through the ListView's own keyboard handler.

Printable characters provide basic case-insensitive type-to-select. The search
starts after the current item, wraps once, and falls back to the latest
character when a longer prefix has no match.

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

Cover item copying, index validation, connected below and above popup cells,
opaque rendering, hit testing, popup arrangement, Escape, mouse selection
through the Popup, keyboard focus/navigation/activation, inside-unhandled and
outside wheel dismissal without parent scrolling, scrollable-list retention,
resize, style states, and exact cells.
