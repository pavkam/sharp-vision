# RadioButton

## RadioButton contract

`RadioButton` is a focusable selection control. At most one attached enabled or
disabled member in the same effective group is checked; a group may initially
have none.

## API

- `IsChecked` is Boolean. User activation sets true and never toggles false.
- `GroupName` is nullable; null groups by nearest radio-group container scope.
- `Content` uses managed parent ownership.
- `Checked`, `Unchecked`, and `SelectionChanged` report old/new group members.

The shipped control events carry immutable `SelectionChangedEventArgs` with
previous/current members and activation cause. Group coordination stores no
global membership: it scans the current owned tree using ordinal explicit names
scoped to the root, or null-name siblings scoped to their nearest parent.
Attaching, reparenting, or regrouping a checked member resolves the candidate
group after ownership commits, so detached trees cannot be retained.

Changing group, parent, or checked state updates both affected groups
atomically. Reentrant handlers observe the committed selection.

Selection commits old false and new true flags before notifications. `Unchecked`
precedes `Checked`, followed by `SelectionChanged` on the new member; if a
handler reselects, stale remaining outer notifications are suppressed.
Programmatic false is valid and leaves a group empty.

## Interaction

Space/pointer selects. Arrow keys move focus and selection to the next eligible
member using orientation and wrapping policy. Disabled, hidden, collapsed, or
detached members are skipped.

Left/Up and Right/Down traverse eligible members in stable group tree order with
wrapping, then focus and select through the same transaction. Layout reserves a
one-cell semantic radio mark and an optional separator before capacity-one
Unicode content.

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
Space/pointer parity, focus/styles, Unicode content, and final cells.
