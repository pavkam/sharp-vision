# Flyout

## Overview

`Flyout` is a direct [`Popup`](popup.md#overview) specialization for
interactive, anchored, light-dismiss content. The Flyout object is the popup
surface itself; it does not forward state to a private Popup.

## API

| Member                | Default        | Purpose                                           |
| --------------------- | -------------- | ------------------------------------------------- |
| `Content`             | `null`         | The flyout body; the flyout owns it.              |
| `Anchor`              | `null`         | Identifies the sibling used for placement.        |
| `Placement`           | `Below`        | Selects the preferred anchor-relative placement.  |
| `IsOpen`              | `false`        | Controls the direct inherited popup presentation. |
| `CloseOnEscape`       | `true`         | Lets a bubbling Escape close the surface.         |
| `FocusOnOpen`         | `true`         | Transfers focus to the first eligible descendant. |
| `ShowAnchorIndicator` | `false`        | Draws an arrow toward the anchor when enabled.    |
| `Closing`, `Closed`   | No subscribers | Expose the inherited ordered surface lifecycle.   |

`ShowAt(anchor)` validates the anchor, assigns it, and opens the same Flyout
instance. Opening installs light-dismiss observation, closes any other open
Flyout beneath the same logical root, and keeps normal routed ancestry. A
primary press outside the Flyout and its anchor closes it without replaying the
press to the background. Moving or resizing the anchor closes the open Flyout,
so a stale placement is never kept on screen.

Automatic `IsOpen` presentation uses light dismissal without creating a modal
scope. The inherited `OpenModal` API remains available when a caller explicitly
needs application modality. Placement, flipping, root clamping, elevation,
frame, shadow, content ownership, lifecycle, and disposal follow the Popup and
[floating-surface](../../concepts/floating-surfaces.md#overview) contracts.

## Example

![The Flyout control rendered in the live showcase](../../images/controls/flyout.png)

```csharp
var flyout = new Flyout
{
    Content = new Stack { Children = { confirmButton, cancelButton } },
};

owner.Children.Add(flyout);
flyout.ShowAt(triggerButton);
```

## Expected behavior

- The Flyout is a direct Popup with no nested presentation Popup, and `ShowAt`
  validates its argument and uses exactly that anchor.
- Placement, rendering, and `SurfaceBounds` behave as inherited from Popup.
- A press outside dismisses the flyout without replaying the press, Escape
  closes it, and opening transfers focus to the first eligible descendant.
- Opening one flyout closes a sibling under the same logical root, and moving or
  resizing the anchor closes the open flyout.
- The lifecycle events fire in their documented order, and detaching or
  disposing the flyout cleans it up.
- Final rendering resolves to the expected semantic cells.
