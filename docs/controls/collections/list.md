# List

## List contract

`List` is a focusable selection control over an owned item snapshot and
caller-configurable template. The first milestone deliberately realizes every
item into a private vertical `Stack` armed with the intrinsic
[`AutoScroll`](../../concepts/scrolling.md) contract. It makes no virtualization
or recycling claim.

Each template result is wrapped by one ordinary pressable `ListItem`. The
wrapper owns focus, activation, selected visual state, and exactly one template
control; selection state propagates through that realized subtree so inherited
`State.Checked` styling reaches the cells that actually render.

When the resolved style supplies a background, the List paints its complete
arranged surface with that normal or disabled appearance. Each realized item
paints its complete row with the resolved item state, so a `State.Checked`
overlay visibly highlights selected rows instead of changing only the label
cells.

## API

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
- `SelectionMode.None`, `Single`, and `Multiple` permit zero, at most one, or
  many indexes. Narrowing a mode normalizes selection deterministically by
  retaining the lowest applicable index.
- `SelectedIndex` returns the lowest selected index; `-1` clears. Invalid
  indexes throw, and non-negative assignment in None mode is rejected.
  `SetSelected(index, bool)` changes one index without replacing other Multiple
  selections. `SelectedItem` and the stable owner-backed `SelectedItems` view
  always reflect committed ascending index order.
- `ActiveIndex` is independent navigation state and `VerticalOffset` exposes the
  composed viewport offset. `ScrollBars`, `ShowScrollBars`, `ScrollBarChrome`,
  and `ScrollBarFill` forward the common overflow policy to the owned viewport,
  so a List uses the same canonical rail behavior as any scrollable `Container`
  rather than a private scrolling dialect.
- `SelectionChanging` receives owned sorted added/removed index memories and may
  cancel before commit. `SelectionChanged` reports the same committed delta
  after all selected views and visual states update. Reentrant changes advance a
  transaction version so a stale outer proposal cannot overwrite them.
- `ItemInvoked` reports index, borrowed item, and `ActivationCause` for Enter or
  eligible primary pointer invocation.

## Interaction and layout

Arrows move focus and active index in stable realized order while skipping
effectively hidden or disabled template controls. Home/End choose the first or
last eligible item. PageUp/PageDown advance by at least one and otherwise by the
committed viewport height. Every successful move uses the composed
`BringIntoView` path.

Space follows press/release activation and changes selection; Enter invokes
without changing it. Primary pointer release selects and invokes. In Multiple
mode, Control toggles one index, Shift selects the inclusive range from the
stable anchor, and an unmodified activation replaces selection. In Single mode
activation replaces the sole selection; None mode still permits navigation and
invocation.

Pointer hit testing targets the pressable item wrapper rather than swallowing
activation in its display child. Capture, focus loss, disable, detach, and
disposal therefore reuse the same cancellation guarantees as `Button` and other
`Pressable` controls.

## Example

```csharp
var list = new List
{
    Items = files,
    SelectionMode = SelectionMode.Single,
};
```

## Test obligations

Cover empty/items changes, selection modes/events/cancellation, keyboard and
pointer modifiers, invoke, scrolling/bring-into-view, resize, disabled items,
Unicode/variable height, template failures/ownership, focus, and final cells.

Tests retain actual template controls to prove unique parents and deterministic
disposal, compare final Unicode cells and wide-cell ownership, assert stable
selected-view identity, drive routed keyboard and pointer input through focus
and capture managers, and verify active-item scrolling across resize-sized
viewports. Later performance tests cover 1,000 fully realized items and retained
memory; they must not relabel realization as virtualization.
