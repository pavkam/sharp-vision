# Modality Design

## Goal

Add application-wide modality that can constrain every interaction to one
logical plane without coupling the behavior to one visual control. A modal
`Popup`, modal `Window`, top-level menu bar, flyout, and arbitrarily deep
submenu chain must use the same mechanism.

An active modal plane must prevent background key, text, paste, pointer, focus,
hover, pressed-state, and capture interaction. Input outside the plane is
consumed. The plane may either request dismissal or ignore that input; dismissal
never replays the triggering input to the newly exposed background.

## Non-goals

- Modality does not add a virtual tree, reconciliation, function components, or
  hook-style state.
- Modality does not replace `Overlay`, popup promotion, placement, clipping, or
  z-order. Those mechanisms continue to determine appearance.
- Modality does not make `TabNavigation` responsible for pointer, capture, or
  programmatic-focus isolation.
- A modal presentation is not permanently attached to a control. The same
  `Popup`, `Window`, or arbitrary `Control` may be presented modally on one
  occasion and modelessly on another.

## Terms

- A **modal scope** is one active stack entry.
- A **modal plane** is the primary root, any explicitly included roots, and all
  of their owned descendants.
- The **active scope** is the youngest scope on the application-owned stack.
- A **route boundary** is the plane root at which a routed input preview starts
  and its bubble ends.

The terms plane and scope describe interaction membership and lifetime. They do
not create a new visual ownership role.

## Public model

`Application.Modality` exposes the application-owned `ModalityManager` after the
first resize attaches the control tree, alongside `Application.Focus` and
`Application.Capture`.

The core entry point is conceptually:

```csharp
var scope = application.Modality.Enter(
    dialog,
    OutsideInteraction.Ignore,
    initialFocus: firstField);
```

`Enter` returns a disposable `ModalScope`. Its public contract contains:

- the primary `Root`;
- the selected `OutsideInteraction` behavior;
- `IsActive` state;
- a `DismissRequested` event;
- an `Exited` event;
- `Include(Control)` for adding a disjoint plane root; and
- idempotent `Dispose()` for ending the scope.

`OutsideInteraction` has exactly two values:

- `Ignore` consumes outside input without a callback; and
- `Dismiss` consumes outside input and raises one `DismissRequested` callback
  for each qualifying input record delivered while the scope remains active.

The manager exposes the current `Active` scope for observation. It does not
expose a mutable stack.

### Validation

`Enter` and `Include` require non-null, attached, undisposed roots owned by the
same application. A root must be effectively visible and enabled when it joins
the plane. `initialFocus`, when supplied, must be an eligible descendant of one
plane root.

Plane roots must be disjoint. Including the same root twice, an ancestor of an
existing root, or a descendant already covered by an existing root is rejected
before state changes. Descendants need no explicit registration because owned
ancestry already supplies membership.

All manager and scope mutation is dispatcher-affine. Public validation occurs
before observable state changes.

## Ownership and integration

One `ModalityManager` owns the attached application root and propagates through
every ownership slot as focus and pointer ownership do today. This makes the
policy available to `Router`, `FocusManager`, `PointerManager`, built-in
controls, and externally derived controls without exposing manager references
through public child collections.

Modality uses `Control.Parent` and the central owned-control registry. It never
assumes that a descendant appears in `Container.Children`; content roots, item
hosts, composite parts, and retained popup slots remain first-class members.

## Route construction

When no scope is active, routed input keeps the existing root-to-target preview
and target-to-root bubble contract.

When a scope is active, an eligible target is routed only between its matching
plane root and itself. Application-root and background ancestors outside that
boundary do not observe preview handlers, bubble handlers, or default behavior.
For multiple disjoint plane roots, the target's owning root supplies the unique
boundary.

`Router.Route` applies this boundary itself. Limiting only
`Application.Dispatch` would be insufficient because callers can route input
directly and pointer focus occurs before ordinary routed handlers.

A route already in progress retains its captured ancestry. Entering or exiting a
scope from one of its handlers affects subsequent input and does not rewrite the
initiating event halfway through preview or bubble.

Programmatic attempts to route input to a blocked control are rejected as a
contract violation. Application-originated input resolves an eligible target
before calling the router and therefore never uses that exceptional path.

## Keyboard, text, and paste

Key input targets the focused in-plane control. If no in-plane control is
focused, it targets the active scope's primary root so dialog and menu defaults
remain reachable.

Text and paste target only an eligible focused control. They are ignored when
the active plane has no focused text recipient; they never fall through to a
background focus target.

Unhandled Tab and Shift+Tab traverse the active plane in primary-root then
included-root order. Each root retains its existing local `TabIndex`, ownership,
and `TabNavigation` rules. Traversal wraps within the active plane and cannot
escape to the parent scope or application root.

## Focus

Entering a scope snapshots the current focus target. It then focuses the
validated `initialFocus`, or the first eligible descendant of the primary root,
or the primary root itself when it can focus. A plane with no focusable control
remains valid; key input still targets its primary root.

Explicit focus, pointer-triggered focus, and traversal requests outside the
active plane return false. Focus cleanup caused by detach, disable, hide, or
disposal remains non-cancellable.

Exiting restores the saved target when it remains eligible in the newly active
parent plane. When a nested scope's saved target is no longer eligible, focus
moves to the first eligible control in the parent plane. When no modal scope
remains, an ineligible saved target yields no focus rather than choosing an
unrelated application control.

Focus restoration uses `FocusReason.Restore` and retains the existing
transactional changing/lost/gained event order.

## Pointer targeting, hover, pressed state, and capture

Hit testing remains a geometric query and may identify a background control.
`PointerManager` filters that physical result through the active scope before
changing hover, focus, pressed-origin, or delivery state.

An outside move clears any in-plane hover path without entering a background
hover path. An outside press, release, or wheel action never reaches a
background control. The raw `Application.Pointer` position continues to report
the terminal's physical pointer coordinates.

Capture owned by an eligible in-plane control continues to win over physical hit
testing, including drags that leave the plane's visual bounds. Entering a scope
cancels capture, hover, and press bookkeeping owned outside the new plane. New
capture requests outside the active plane return false. Capture is never
restored when a scope exits.

For `OutsideInteraction.Dismiss`, an uncaptured primary press or wheel outside
every plane root raises `DismissRequested`. Pointer movement alone does not
dismiss. One input record raises at most one callback regardless of plane root
count; a later outside press or wheel may request dismissal again if the owner
deliberately retained the scope.

The input that requested dismissal is permanently consumed. Even when the
callback closes the scope synchronously, the manager does not hit test again or
dispatch that same input to the background.

## Nested scopes and lifetime

Scopes form a dispatcher-owned LIFO stack. Only the youngest scope governs
interaction. Entering a child scope suspends its parent's interaction without
removing or hiding the parent plane.

Disposing the active scope exits it. Disposing an older scope first unwinds all
younger scopes in reverse order, then exits the requested scope. This rule lets
owner teardown close nested dialogs and menu planes without leaving an orphaned
scope. Each scope publishes `Exited` once after its active state commits false.

If a primary root detaches, becomes unavailable, or is disposed, its scope and
all younger scopes unwind automatically. An unavailable included root is
removed; loss of the primary root ends the scope. Application shutdown unwinds
every scope without attempting to restore focus into a stopping tree.

Entering is transactional. If capture cancellation, focus movement, or a
callback fails, the new scope rolls back before `Enter` throws, and the earliest
failure remains authoritative. Exiting commits stack state first, attempts all
capture/focus cleanup and notifications, then rethrows the earliest failure. No
failure may leave a scope active without returning its handle.

`DismissRequested` runs after the manager commits that the outside input is
consumed. Its handler may synchronously close or dispose the scope. If the
callback fails without closing, the scope remains active and continues to block
background interaction.

## Popup and Window convenience APIs

The manager is the authoritative primitive. `Popup.OpenModal` and
`Window.ShowModal` are thin retained-control conveniences, not separate modal
implementations.

`Popup.OpenModal` opens the popup, enters a scope rooted at that popup, and
returns the scope. Its default outside behavior is `Dismiss`. A dismissal
request sets `IsOpen` false. Any ordinary popup close exits the scope before
content becomes unavailable, allowing focus restoration while the old subtree is
still valid. Failure during scope entry closes the popup again before the
exception escapes.

`Window.ShowModal` makes the window visible, enters a scope rooted at the
window, and returns the scope. Its default outside behavior is `Ignore`. With
`Dismiss`, an outside request raises the window's existing `Closing` request;
the window remains modal unless the handler hides it or disposes the scope.
Changing `Visibility` away from visible exits the scope. This preserves the
current `Window.Closing` ownership contract instead of inventing an implicit
close result.

Neither convenience adds `IsModal`. The returned scope represents that one
presentation and remains the composable lifetime handle. Explicitly disposing
that handle ends modality without changing `IsOpen` or `Visibility`; callers
that want to close the visual surface also change its ordinary lifecycle state.

## Menu planes

The topmost menu participating in an open submenu chain owns one modal scope. It
enters that scope when the first submenu opens and keeps it alive until the
complete chain closes. The topmost `Menu` is the primary plane root.

Every retained submenu popup and nested `Menu` is already an owned descendant,
so the complete chain joins automatically. A nested menu therefore reuses the
topmost menu plane instead of pushing another scope.

Pointer movement over another item in the main menu stays inside the plane. An
armed menu may close the previous sibling popup and open the new sibling popup
without briefly ending the scope. Moving to an item without a submenu closes the
sibling popup but retains the plane until the menu interaction ends.

Leaf invocation, Escape at the menu root, or outside dismissal closes the whole
popup chain and exits the menu scope. A menu opened inside a modal window may
push a temporary child scope rooted at that menu; closing it restores focus to
the parent dialog plane.

The existing popup promotion and placement rules remain unchanged. Scope route
boundaries prevent a submenu popup's application-root light-dismiss handler from
pre-empting an in-plane main-menu or sibling-submenu interaction. Outside input
is handled once by the menu scope.

## Rendering and layout

Modality performs no reparenting and adds no wrapper control. Existing `Overlay`
z-order, `Popup` promotion, intrinsic chrome, placement fallback, measurement,
arrangement, clipping, and rendering determine visual output.

Callers remain responsible for placing a modal `Window` in an appropriate
overlay or canvas position. Popup and submenu surfaces retain the shared popup
layer. A modal scope may optionally be paired with an ordinary visual scrim, but
the scrim is not required for interaction isolation and has no privileged input
behavior.

## Documentation and showcase

The normative shared contract belongs in
`docs/concepts/modality.md#modality-contract`. The concept index links it, and
the focus, input-routing, lifecycle, runtime-loop, popup, window, menu,
menu-item, overlay, controls-integration, and showcase specifications link to
the exact sections they depend on rather than repeating the rules.

The Window showcase replaces its cosmetic "Modal dialog" specimen with a real
modal presentation that demonstrates ignored outside input, focus trapping,
default/cancel behavior, and restoration. The Popup showcase demonstrates
dismissal whose press does not activate the background. The Menu showcase
demonstrates a main menu, sibling switching, and a nested submenu in one plane.

## Proof obligations

Focused manager tests prove:

- entry validation, disjoint membership, active-stack order, older-scope
  unwinding, unavailable-root cleanup, shutdown, and callback failure;
- route boundaries for preview, bubble, defaults, direct `Router.Route`, and
  routes that enter or exit a scope during dispatch;
- initial focus, explicit focus rejection, forward/reverse traversal across
  multiple roots, nested restoration, and unavailable saved targets;
- activation-time capture cancellation, in-plane capture outside visual bounds,
  blocked background capture, hover exit without background entry, and press
  cleanup;
- ignored and dismissing outside press/wheel behavior, one request per input,
  and permanent consumption of the dismissing input; and
- key, text, paste, terminal-focus, resize, and zero-focus modal behavior.

Mounted component and integration tests drive raw terminal bytes through the
real application, dispatcher, parser, focus, pointer, controls, layout, and
renderer. They prove modal Popup and Window helpers, background non-activation,
Tab trapping, focus restoration, a main-menu sibling switch, arbitrary submenu
depth, nested menu-in-dialog scopes, final semantic cells, and emitted bytes.

Randomized fixed-seed tests generate valid enter/include/dismiss/hide/dispose
sequences. After every step, focus, capture, hover, press origin, and routed
targets must be null or members of the active plane; stack depth and retained
scope references must remain bounded by live scopes.
