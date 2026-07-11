# Popup

## Popup contract

`Popup` displays one owned child in an overlay layer relative to an anchor,
pointer position, or explicit screen rectangle. It may establish a focus scope
and modal boundary.

## API

- `Child` uses managed ownership.
- `Anchor`, `Placement`, `HorizontalOffset`, and `VerticalOffset` define
  position. Invalid detached anchors fail opening without partial state.
- `IsOpen`, `IsModal`, `TakesFocus`, `CloseOnEscape`, and
  `CloseOnOutsidePointer` define behavior.
- `Opening`/`Closing` are cancellable; `Opened`/`Closed` report committed state
  and cause.

Placement tries documented preferred/fallback sides, constrains to the terminal
viewport, and uses automatic scrolling when content cannot fit. Resize
repositions after root layout and before the frame.

## Interaction

Opening records valid focus/capture state. Modal popups block hit testing and
focus outside their scope. Closing releases capture and restores the recorded
owner or nearest valid fallback.

## Example

```csharp
var popup = new Popup
{
    Child = details,
    Anchor = helpButton,
    Placement = PopupPlacement.Below,
};
```

## Test obligations

Cover placement/fallback/viewport constraints, detached anchor, open/close
cancellation/order, outside click/Escape, modal routing, focus/capture restore,
nested popups, resize, tiny terminal scrolling, ownership, and final cells.
