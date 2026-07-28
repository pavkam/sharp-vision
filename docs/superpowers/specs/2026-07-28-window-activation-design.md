# Window activation design

## Goal

SharpVision applications expose one active `Window`, similar to the active-form
model in Delphi VCL. A primary pointer press anywhere inside a Window and a
focus transition into a Window both activate that Window. Activating another
Window deactivates the previous one.

Activation is application state. It is distinct from keyboard focus, pointer
capture, modality, and Overlay z-order.

## Public contract

`Application.ActiveWindow` is a nullable, read-only `Window` reference. It is
null when no available Window is active.

`Window.IsActive` is a read-only Boolean. It is true exactly when that Window is
the owning Application's `ActiveWindow`. Changing this value invalidates only
the Window's rendered appearance.

This change does not add activation events, a public setter, a public activation
method, or automatic z-order promotion.

## Activation rules

The Application activates the nearest Window ancestor of the relevant control:

1. Before focus and routing for a primary pointer press, it resolves the Window
   from the modal-eligible delivery target. That Window bounds generic pointer
   focus, so a press on non-focusable chrome or background activates the Window
   without moving focus into an application-shell ancestor.
2. After a focus transition commits, it resolves the Window from the newly
   focused control. Programmatic focus, pointer focus, keyboard traversal, and
   modal focus entry consequently share one rule.
3. A qualifying primary press or committed focus transition whose target has no
   Window ancestor clears the active Window.

Pointer records rejected as outside the active modal plane do not activate a
background Window. Captured delivery activates the Window containing the
modal-eligible capture target, matching the control that receives the press.
Non-primary presses, pointer moves, releases, leaves, and wheel records do not
change activation.

Setting a Window active commits the new Application reference and both Window
flags as one dispatcher-affine transition before user pointer handlers run.
Repeated activation of the same Window is idempotent.

## Availability and lifetime

An active Window must remain attached to the Application root, visible, enabled,
and undisposed. Hiding, collapsing, disabling, detaching, or disposing it clears
`Application.ActiveWindow` and `Window.IsActive`. The Application does not
automatically reactivate an older Window; a later pointer or focus action makes
the next choice explicit.

Application shutdown clears activation before the control tree is released.
Activation cleanup continues deterministically through existing unavailability
and disposal paths.

## Appearance

An active Window resolves the existing Window `FocusWithin` appearance overlay
even when keyboard focus remains elsewhere. This preserves the established
`ThemeColor.ActiveBorder` look without adding a new theme state. The public
`ContainsFocus` and `IsFocused` values retain their exact keyboard-focus
meaning.

When a focus transition activates a Window, appearance remains unchanged from
today. When a chrome or background press activates a Window without moving
focus, only the newly active Window receives active-border styling and the
previous Window returns to its normal border styling.

## Architecture

An internal application-owned activation coordinator stores the current Window
and owns the transition and cleanup rules. `Application.ActiveWindow` delegates
to that coordinator after the first resize initializes the attached tree and
returns null before initialization.

`PointerManager` receives an internal activation callback from Application. It
invokes that callback after modal target validation and before pointer focus and
routing. The callback returns the active Window as the focus boundary.
Standalone PointerManager construction keeps the callback optional, so existing
focused input tests remain isolated.

Application observes committed `FocusManager` transitions and forwards the new
focused control to the same coordinator. The coordinator walks ordinary
ownership ancestry through `Control.Parent`, selects the nearest Window, and
rejects unavailable or foreign candidates.

The coordinator observes only the active Window's visibility, enabled, and
parent changes. It unsubscribes during every replacement and shutdown, avoiding
retention of inactive Windows.

## Error handling

Activation itself invokes no public callback. Internal validation treats a
stale or unavailable candidate as no active Window. Appearance invalidation
uses the existing dispatcher-affine control mutation contract.

If later pointer focus or routed handlers throw, the already committed
activation remains, matching the target selected for that input record. Focus
callbacks that throw after a focus commit likewise leave activation aligned
with the committed focused control.

## Verification

Focused tests prove:

- `Application.ActiveWindow` and `Window.IsActive` begin inactive;
- primary presses on chrome, background, content, and descendants activate the
  nearest Window without unnecessary focus movement;
- activating a second Window deactivates and restyles the first;
- programmatic focus, pointer focus, keyboard traversal, and modal entry update
  activation through the committed focus target;
- presses and focus outside Windows clear activation;
- modal rejection cannot activate a background Window;
- non-primary and non-press pointer records do not activate Windows;
- hiding, collapsing, disabling, detaching, disposal, and Application shutdown
  clear activation;
- active-border rendering follows `IsActive` while `ContainsFocus` remains an
  independent focus fact.

The Window control specification, focus and input concepts, public XML
documentation, and Showcase Window page are updated to demonstrate and explain
the same contract. Final verification runs focused Window and Application tests,
then `make format`, `make lint`, `make build`, and `make test`.
