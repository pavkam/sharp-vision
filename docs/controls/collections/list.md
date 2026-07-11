# List

## List contract

`List` is a focusable selection control over items and an item-template or
explicit control collection. It uses a scroll view for overflow.

## API

- `Items` rejects null collection replacement; item nullability follows the
  configured template contract.
- `ItemTemplate` creates/recycles item controls without leaking parent/style
  state between items.
- `SelectionMode` is none, single, or multiple.
- `SelectedIndex`, `SelectedItem`, and `SelectedItems` expose committed
  selection; invalid programmatic indexes throw.
- `SelectionChanging` is cancellable; `SelectionChanged` reports added/removed
  items after commit. `ItemInvoked` reports semantic activation.

## Interaction and layout

Arrows move focus/active item, Space changes selection, Enter invokes, and
Home/End/Page commands navigate through the viewport. Pointer selection follows
modifier policy. Active items are brought into view minimally.

The first milestone may realize all items; virtualization must not be claimed
until recycling, variable-height, focus, and accessibility tests prove it.

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
