# Tooltip

## Tooltip contract

`Tooltip` is a direct [`Popup`](popup.md#popup-contract) specialization for
passive, delayed information. The Tooltip object is the owned popup-layer
surface associated with its anchor; it does not create a private Popup.

## API

| Member                         | Default          | Purpose                                                    |
| ------------------------------ | ---------------- | ---------------------------------------------------------- |
| `Content`, `Text`              | `null`, `null`   | Supply rich content or text shorthand.                     |
| `Anchor`, `Placement`          | `null`, `Below`  | Identify and position relative to the trigger.             |
| `ShowDelay`, `HideDelay`       | 500 ms, 100 ms   | Configure deterministic hover/focus presentation timing.   |
| `IsOpen`                       | `false`          | Reports or directly controls inherited popup presentation. |
| `FocusOnOpen`, `CloseOnEscape` | `false`, `false` | Keep the passive surface out of focus and Escape behavior. |
| `IsHitTestVisible`             | `false`          | Prevent the tooltip from becoming a pointer target.        |

`SetText` and `SetContent` create or update one Tooltip associated with a
non-null anchor. Overloads set placement and show delay. `GetTooltip` returns
that exact surface. `ClearTooltip` closes and detaches it; a later Set call may
reuse the anchor's registered framework-part slot with a new Tooltip.

The attached Tooltip listens to anchor pointer entry/exit, focus gain/loss, and
pointer press. Hover or focus starts one show timer. Exit starts one hide timer;
focus loss or press hides immediately. Overlapping hover and focus transitions
restart a single timer subscription rather than stacking callbacks. The timers
use the owning dispatcher, stop on detach, and are disposed with the Tooltip.

Tooltip fixes passive surface policy: no automatic modal scope, focus transfer,
keyboard navigation, Escape handling, or hit testing. Once available, the
Tooltip measures and arranges itself against the anchor's root so the first open
frame has committed content geometry. Placement, edge flipping, root clamping,
elevation, frame, lifecycle, and ownership otherwise follow Popup and the
[floating-surface contract](../../concepts/floating-surfaces.md#floating-surface-contract).

## Example

![The Tooltip control rendered in the live showcase](../../images/controls/tooltip.png)

```csharp
Tooltip.SetText(
    saveButton,
    "Save your work",
    PopupPlacement.Above,
    TimeSpan.FromMilliseconds(500));
```

## Expected behavior

Cover direct Popup inheritance and absence of a nested Popup; text and rich
content ownership; attached Set/Get/Clear/Set reuse; argument and interval
validation; pointer and focus delays with overlapping triggers; immediate press
and focus-loss dismissal; passive focus, keyboard, modality, and hit-test
exclusions; first-open geometry and rendered text; lifecycle cleanup; and
disposal without retained anchor or timer subscriptions.
