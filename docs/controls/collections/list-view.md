# ListView

## ListView contract

`ListView` is a focusable selection control over an owned item snapshot and
caller-configurable template. The control realizes every item into a private
vertical `Stack` armed with the intrinsic
[`AutoScroll`](../../concepts/scrolling.md) contract. It makes no virtualization
or recycling claim.

Each template result is wrapped by one ordinary pressable `ListItem`. The
ListView owns focus and current-item navigation; the wrapper owns activation,
selected/current visual facts, and exactly one template control through
inherited `Content`. Selection propagates through that realized control subtree
so the theme treats the complete row as one selected item; an explicit complete
local appearance on template content remains authoritative.

By default, ListView is a quiet borderless collection region whose fill comes
from the active theme's `ListView.normal` style. The continuous plane is its
boundary; callers may opt into an inherited frame or replace the background
through the shared [chrome contract](../../concepts/styling.md#shared-chrome).

ListView paints its complete arranged surface with its normal or disabled
appearance. Normal and pointer-over realized items keep a transparent background
when their theme states omit one, so the owning surface remains continuous. A
`VisualState.Selected` overlay may paint the complete row instead of changing
only label cells.

## API

| Member                                                 | Default                  | Purpose                                                          |
| ------------------------------------------------------ | ------------------------ | ---------------------------------------------------------------- |
| `Items`                                                | Empty snapshot           | Copies data items before realizing presentation controls.        |
| `ItemTemplate`                                         | Invariant-culture `Text` | Creates one unique detached control per item.                    |
| `SelectionMode`                                        | `Single`                 | Allows no selection, one selection, or multiple selections.      |
| `SelectedIndex`, `SelectedItem`, `SelectedItems`       | `-1`, `null`, empty      | Read or change committed selection.                              |
| `ActiveIndex`                                          | `-1`                     | Identify the keyboard-navigation row.                            |
| `ScrollBars`, `ShowScrollBars`                         | Vertical automatic rails | Configure overflow visibility policy.                            |
| `ScrollBarStyle`, `ActualScrollBarStyle`               | `null`, Theme style      | Override or inspect the complete generated-rail presentation.    |
| `SelectionChanging`, `SelectionChanged`, `ItemInvoked` | No subscribers           | Cancel a proposal or observe committed selection and activation. |

## Behavior

- `Items` rejects null collection replacement and copies the complete
  `IReadOnlyList<object?>` before realization. Its returned owner-backed view
  cannot mutate the snapshot. Null items are passed unchanged to the template.
- `ItemTemplate(object?)` must return one unique, detached, undisposed non-null
  control per item. The entire candidate set is built and validated before the
  previous realized tree is detached. Successful replacement disposes every old
  wrapper and template control; failure preserves items, template, selection,
  parents, and cells.
- The invariant-culture default template creates one `Text` control from each
  item. Custom templates may return Unicode and variable-height controls.
- `ListSelectionMode.None`, `Single`, and `Multiple` permit zero, at most one,
  or many indexes. Narrowing a mode normalizes selection deterministically by
  retaining the lowest applicable index.
- `SelectedIndex` returns the lowest selected index; `-1` clears. Invalid
  indexes throw, and non-negative assignment in None mode is rejected. Assigning
  an unavailable realized row is ignored and preserves the existing valid
  selection. `SetSelected(index, bool)` changes one index without replacing
  other Multiple selections; selecting an unavailable realized row returns
  `false` and leaves valid selection unchanged. `SelectedItem` and the stable
  owner-backed `SelectedItems` view always reflect committed ascending index
  order.
- Replacing `Items` remaps selected and active rows by item equality, preserving
  each item when it remains in the new snapshot even if its index changes. Equal
  duplicate values map by occurrence: the first old occurrence maps to the first
  new occurrence, the second to the second, and so on. Unmatched occurrences are
  removed. The snapshot map uses equality buckets and runs in expected
  `O(old count + new count)` time. Removed selected items leave selection
  cleared or narrowed to the remaining valid items. A removed or unavailable
  active row falls back to the available realized row with the smallest absolute
  distance from the clamped prior position, preferring the lower index on a tie;
  when the prior position is beyond the new snapshot this becomes the last
  available row, or `-1` when no row is available. Insert, remove, and replace
  notifications from an observed collection use the same stable index and item
  rules.
- `ActiveIndex` identifies the row used by keyboard selection and invocation.
  Keyboard navigation keeps it synchronized with committed selection in Single
  and Multiple modes; None mode retains active navigation without selection.
  `VerticalOffset` exposes the composed viewport offset. `ScrollBars`,
  `ShowScrollBars` and `ScrollBarStyle` forward the common overflow policy to
  the owned viewport, so a ListView uses the same canonical rail behavior as any
  scrollable `Container` rather than a private scrolling dialect.
- `SelectionChanging` receives owned sorted added/removed index memories and may
  cancel before commit. `SelectionChanged` reports the same committed delta
  after all selected views and visual states update. Reentrant changes advance a
  transaction version so a stale outer proposal cannot overwrite them. Mode or
  item-realization changes invalidate pending proposals even when the selected
  index set was already empty; a reentrant change to None mode rejects any
  pending non-empty proposal.
- `ItemInvoked` reports index, borrowed item, and `ActivationCause` for Enter or
  eligible primary pointer invocation.

## Interaction and layout

Arrows keep focus on the ListView and move its active index in stable realized
order while skipping effectively hidden or disabled template controls. In Single
and Multiple modes, every move also replaces selection with the active row; a
cancelled `SelectionChanging` transaction leaves both values unchanged. None
mode moves only the active index. Home/End choose the first or last eligible
item. PageUp/PageDown advance by at least one and otherwise by the committed
viewport height. Every successful move uses the composed `BringIntoView` path.

Space follows press/release activation and changes selection; Enter invokes
without changing it. Primary pointer release selects and invokes. In Multiple
mode, Control toggles one index, Shift selects the inclusive range from the
stable anchor while skipping unavailable rows, and an unmodified activation
replaces selection. In Single mode activation replaces the sole selection; None
mode still permits navigation and invocation.

Pointer hit testing targets the pressable item wrapper rather than swallowing
activation in its display child. Capture, focus loss, disable, detach, and
disposal therefore reuse the same cancellation guarantees as `Button` and other
`Pressable` controls.

Disabled realized item content remains visible, but its row is not eligible for
pointer activation, keyboard navigation, or selection. An empty `Items` snapshot
has no active or selected row and renders only the ListView surface.

The `ListView` owns keyboard focus but never paints a list-wide hover
appearance. Physical `PointerOver` state remains observable on the list
ancestry, while the directly targeted internal item wrapper changes only
foreground and border semantics over the unchanged owner background. Selection
may paint the paired selection background.

## Example

```csharp
var list = new ListView
{
    Items = files,
    SelectionMode = ListSelectionMode.Single,
};
```

## Expected behavior

Cover empty/items changes, selection modes/events/cancellation, keyboard and
pointer modifiers, invoke, scrolling/bring-into-view, resize, disabled items,
Unicode/variable height, template failures/ownership, focus, and final cells.

Tests retain actual template controls to prove unique parents and deterministic
disposal, compare final Unicode cells and wide-cell ownership, assert stable
selected-view identity, drive routed keyboard and pointer input through focus
and capture managers, and verify active-item scrolling across resize-sized
viewports. Later performance tests cover 1,000 fully realized items and retained
memory; they must not relabel realization as virtualization.
