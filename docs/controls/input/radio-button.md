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

Changing group, parent, or checked state updates both affected groups
atomically. Reentrant handlers observe the committed selection.

## Interaction

Space/pointer selects. Arrow keys move focus and selection to the next eligible
member using orientation and wrapping policy. Disabled, hidden, collapsed, or
detached members are skipped.

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
