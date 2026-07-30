# Flyout

## Flyout contract

`Flyout` is a direct [`Popup`](popup.md#popup-contract) specialization for
interactive, anchored, light-dismiss content. The Flyout object is the popup
surface; it does not forward state to a private Popup.

## API

| Member                | Default        | Purpose                                           |
| --------------------- | -------------- | ------------------------------------------------- |
| `Content`             | `null`         | Owns the flyout body.                             |
| `Anchor`              | `null`         | Identifies the sibling used for placement.        |
| `Placement`           | `Below`        | Selects preferred anchor-relative placement.      |
| `IsOpen`              | `false`        | Controls direct inherited popup presentation.     |
| `CloseOnEscape`       | `true`         | Lets bubbling Escape close the surface.           |
| `FocusOnOpen`         | `true`         | Transfers focus to the first eligible descendant. |
| `ShowAnchorIndicator` | `false`        | Draws an arrow toward the anchor when enabled.    |
| `Closing`, `Closed`   | No subscribers | Expose the inherited ordered surface lifecycle.   |

`ShowAt(anchor)` validates the anchor, assigns it, and opens the same Flyout.
Opening installs light-dismiss observation, closes another open Flyout beneath
the same logical root, and retains normal routed ancestry. A primary press
outside the Flyout and its anchor closes it without replaying the press to the
background. Moving or resizing the anchor closes the open Flyout so stale
placement is never retained.

Automatic `IsOpen` presentation uses light dismissal without creating a modal
scope. The inherited `OpenModal` API remains available when a caller explicitly
needs application modality. Placement, flipping, root clamping, elevation,
frame, shadow, content ownership, lifecycle, and disposal follow the Popup and
[floating-surface](../../concepts/floating-surfaces.md#floating-surface-contract)
contracts.

## Example

```csharp
var flyout = new Flyout
{
    Content = new Stack { Children = { confirmButton, cancelButton } },
};

owner.Children.Add(flyout);
flyout.ShowAt(triggerButton);
```

## Expected behavior

Cover direct Popup inheritance and absence of a nested Popup; `ShowAt`
validation and exact anchor; inherited placement, rendering, and
`SurfaceBounds`; outside light dismiss without replay; Escape; focus transfer;
sibling exclusion; anchor movement; lifecycle order; detach/disposal cleanup;
and final semantic cells.
