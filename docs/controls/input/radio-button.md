# RadioButton

## RadioButton contract

`RadioButton` is a sealed [`Pressable`](../pressable.md#pressable-contract)
selection control. At most one owned member in the same effective group is
checked; a group may initially have none.

## API

- `IsChecked` is Boolean. User activation sets true and never toggles false.
- `GroupName` is nullable. A null name groups only radio buttons in the exact
  same ownership slot. A non-null name uses ordinal matching across every slot
  beneath the current ownership root.
- `Content` uses managed parent ownership.
- `Checked`, `Unchecked`, and `SelectionChanged` report old/new group members.

The shipped control events carry immutable `SelectionChangedEventArgs` with
previous/current members and activation cause. Group coordination stores no
global membership. Named discovery scans every role and layer, regardless of
hit-test or navigation participation; unnamed discovery inspects only the exact
owning slot rather than every child of the same parent. Attaching, reparenting,
or regrouping a checked member resolves the candidate group after ownership
commits, so detached trees cannot be retained.

Changing group, parent, or checked state updates both affected groups
atomically. Reentrant handlers observe the committed selection.

Selection commits old false and new true flags before notifications. `Unchecked`
precedes `Checked`, followed by `SelectionChanged` on the new member; if a
handler reselects, stale remaining outer notifications are suppressed.
Programmatic false is valid and leaves a group empty.

Property notifications follow the same staged commit: the old member's
`PropertyChanged(IsChecked)` observer already sees the new member selected. A
checked member moving groups resolves the destination group before publishing
`GroupName`.

## Interaction

Space/pointer selects. Arrow keys move focus and selection to the next eligible
member using orientation and wrapping policy. Disabled, hidden, collapsed, or
detached members are skipped.

Left/Up and Right/Down traverse eligible members in stable group tree order with
wrapping, then focus and select through the same transaction. Layout reserves a
one-cell semantic radio mark and an optional separator before capacity-one
Unicode content. Checked and focused foreground synchronizes to optional content
after attachment and every selection, focus, or local availability transition.
Disabled foreground always resolves to the muted role, including a retained
selected value.

## Example

```csharp
var compact = new RadioButton
{
    GroupName = "density",
    Content = new Text("Compact"),
};
```

## Test obligations

Cover group exclusivity, no-initial selection, programmatic changes, regrouping,
detach/reparent, event order/reentrancy, arrow navigation, disabled skipping,
exact-slot unnamed scope, named groups across non-container roles and popup
layers, Space/pointer parity, focus/styles, Unicode content, and final cells.
`RadioButtonSurfaceTests` mounts a real group and proves empty initial state,
Space and pointer causes, exclusivity, disabled-member skipping, arrow wrapping,
preselected content styling, disabled precedence, availability restoration,
Unicode ownership, and exact terminal-visible rows.
