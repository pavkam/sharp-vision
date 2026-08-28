# Control and integration testing

## Overview

Every concrete control is tested for property validation before mutation,
measure/arrange/render invalidation, ownership, dispatcher affinity, focus,
pointer capture, keyboard/pointer parity, disabled and hidden state,
visual-state composition, zero and tiny bounds, resize, events, and its final
semantic cells. The shared box-model surface tests mount an opaque child on a
distinct opaque parent and assert that every margin edge keeps the parent
background, every padding edge uses the child background, and an intervening
border preserves both planes. Captioned controls additionally prove
[access-key syntax and dispatch](../concepts/access-keys.md#expected-behavior):
exact marker-free cells, grapheme underline style, routed precedence, the
semantic access-key foreground, disabled-state preservation, keyboard action,
unavailable filtering, duplicate order, modal confinement, and legacy Alt stroke
and text suppression. Duplicate reachable keys are rejected within a mounted
tree and along an open submenu path. Generated list data, repeated documentation
chrome, and arrow-navigated catalog entries do not count as access-key captions.

Default-appearance tests require inactive and disabled semantic
foreground/border pairs for transparent interactive controls, complete triplets
for explicitly opaque faces, foreground/border-only hover and direct focus with
exact state precedence, visually inert pointer ancestry on layout containers,
transparent retained item faces, the opaque application Screen background,
role-specific component surfaces, and null backgrounds on transparent
composition controls. A repository test rejects ordinary control-page
assignments to foreground, background, border, underline, shadow, or
control-specific part colors, any decorative initial `ColorPicker` value, and
any per-state `Appearance` repair. The dedicated Border, Shadow, and Styling
concept pages are the narrow exception, because the assigned property is their
subject. Ordinary showcase examples therefore demonstrate the shipped control
defaults rather than carrying a second styling layer.

Every concrete public control has a focused detached-unit fixture that proves
its API contract, and a mounted-surface fixture that proves real application
evidence for each route it supports. Pointer activation, keyboard activation,
and pointer activation forwarded by a retained child are distinct obligations:
proving one route never substitutes for another, even when multiple routes
produce the same selection or invocation event. This is a per-control review
discipline enforced at PR time, not a reflection-based inventory a build step
checks automatically - a mechanical catalog that only asserts "a test exists"
for every exported type, without exercising real behavior, is a test smell in
this codebase and is deliberately not part of the suite.

Toast surface evidence mounts notifications through both a Screen presentation
plane and an explicit Overlay. It proves all six edge positions, newest-nearest
stacking, elapsed entrance geometry, semantic cells, keyboard and capture-aware
pointer dismissal, veto, timeout ordering, focus passivity, and external-detach
cleanup through the real dispatcher and renderer.

`ComponentGeometrySurfaceTests` applies the same fixed margin, intrinsic border,
and padding through a single dedicated `ChromeProbe` control, proving exact
border-box placement, exact `ContentBounds` deflation, the expected frame corner
in the final modeled terminal, and that a resize down to and through zero-sized
content preserves the same insets and an intact corner. Every other concrete
control's own fixture separately proves its hover, focus, pressed, disabled,
resize, tiny-bound, and semantic activation evidence against its own real
content, rather than relying on a shared probe to stand in for it.

Controls whose pressed presentation changes geometry or chrome need explicit
pressed-frame evidence, not just a boolean `IsPressed` assertion. The Button
contract includes the complete translated face, released origin, foreground, and
shadow cells while the primary pointer remains held.

Layout tests use recording controls to assert measure constraints, desired size,
arranged slots, call order, cache invalidation, non-reentrancy, rounding, and
clipping. Routed-input tests record the route, phase, source, handled state,
local coordinates, mutation during dispatch, default behavior, and cleanup.

Control-render tests inspect the final `Frame` cells and the copied grapheme
bytes. They cover nested clips, later-child overwrite, padding, hidden and
collapsed subtrees, zero bounds, combining sequences, wide CJK and emoji ZWJ
ownership, resolved state styles, default cursor preservation, render-time
invalidation, and exception recovery. Private draw-call recordings supplement
these semantic oracles; they never replace them.

Intrinsic chrome proof lives on the common `ControlBase` surface rather than on
wrapper-control suites. `ControlBaseTests` and `ContainerTests` cover base
border-before-padding reservation, saturated combined insets, shrink wrapping,
and scrollbar containment. `BorderTests` and `ShadowTests` cover validation
before mutation, partial edges, exact glyphs and cells, composite and block
shadows, wide-grapheme styling, signed and extreme offsets, layout-neutral
visual overflow, hit testing, transitive nesting, per-sibling clip isolation,
unclipped intermediates, explicit hard boundaries, and caller canvas clipping.
Curated-theme and mounted-showcase proof additionally requires composite shadow
cells to differ from the application background at Basic16 depth.
`ContainerTests` proves shadows neither change extent nor escape the viewport.
Mounted Button and Popup tests exercise the same paths through retained layout
and popup promotion. `ButtonTests` requires immediate and post-layout pressed
content parity, including translated faces outside the arranged hit bounds,
while `TextInputTests` proves the editor, caret, and private rails stay inset
exactly once.

## End-to-end path

Representative tests start with raw terminal key, mouse, paste, and resize bytes
and exercise the decoder, dispatcher, hit testing and focus, control behavior,
invalidation, layout, cell drawing, frame diff, encoder, and the captured output
bytes. Intermediate typed boundaries are asserted only when they are public
contracts; the final bytes and virtual screen are always mandatory.

`ModalityManagerTests` applies that complete path to the
[modal interaction contract](../concepts/modality.md#expected-behavior). One
real Application receives UTF-8 text, bracketed paste, Tab, terminal-focus
reports, SGR movement, primary press and release, wheel input, and a pixel-aware
resize. The test requires only in-plane routes while the modal is active,
physical outside coordinates without background hover, exactly one dismiss
callback, no replay to the exposed Button, a fresh post-dismissal input route,
the final semantic cells, and the emitted UTF-8 bytes. `ApplicationTests`
separately proves first-resize service publication, resize identity, raw record
targeting, and shutdown unwind when callbacks fail. `ModalityManagerTests`'
clipboard cases drive modal Control+C, Control+X, and Control+V through the
application, require the same handled arguments to reach only `handledEventsToo`
preview observers inside the captured plane, and mutate both the target ancestry
and the scope state during the edit to prove the current route stays stable.

`ModalityManagerTests` attempts modal reentry and inclusion from detach, hide,
disable, and disposal callbacks after temporarily restoring the affected
control. It requires the guarded subtree to remain unavailable while unrelated
roots and ancestor planes remain valid, including automatic focus selection that
skips the guarded descendant. Its `ModalityManagerTests.Focus.cs` partial proves
failed-entry rollback publishes every observed scope youngest first, nested exit
restores before `Exited`, failed focus callbacks leave coherent `Focused` and
`IsFocused` facts, and reentrant shutdown or unavailability strengthens a
deferred teardown before its notifications publish.

### Mounted component surfaces

Focused control regression tests use `ComponentSurface` when behavior crosses
the input, layout, styling, and rendering boundaries. `MountAsync` places one
detached control in a fixed-size host, starts a real `Application`, and returns
only after the first renderer write has been applied to an independent semantic
screen. The host begins as a neutral focus anchor, so a real Tab byte can move
focus into the mounted component without calling `FocusManager` directly.

Tests drive `surface.Pointer` with `MoveToAsync`, `PressAsync`, `ReleaseAsync`,
`ClickAsync`, `LeaveAsync`, or captured drag operations, and drive
`surface.Keyboard` with supported typed key codes, Shift+Tab, and distinct Kitty
character press and release actions. These helpers emit terminal bytes; they
never call `Router.Route`, mutate hover directly, call `SetPressed`, or call
`FocusManager.Focus` on the component. Each action waits until the transport has
consumed its bytes and the application has reached idle after routed work,
layout, rendering, and output.

```csharp
await using var surface = await ComponentSurface.MountAsync(
    button,
    new Size(10, 5),
    TestContext.Current.CancellationToken);

await surface.Pointer.MoveToAsync(button);
await surface.Pointer.PressAsync();

surface.ShouldHaveState(
    button,
    VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
surface.ShouldRender("""

     ╭──────╮
     │Save  │
     ╰──────╯

    """);
```

`ShouldRender` compares every final surface row and right-pads omitted trailing
blank cells by measured terminal-cell width rather than UTF-16 length, so wide
and combining graphemes keep their cell geometry. Whole-surface text is a
reviewable appearance oracle, not sufficient proof by itself: every scenario
also asserts public control state and representative semantic cells, including
resolved colors, attributes, continuation ownership, border cells, and shadow
cells. This keeps the mounted path aligned with the
[input-routing](../concepts/input-routing.md#input-routing),
[focus](../concepts/focus.md#overview),
[visual-state](../concepts/styling.md#visual-states), and
[rendering-equivalence](rendering.md#rendering-equivalence-testing) contracts.
The StatusBar composition fixture additionally embeds an unstyled CheckBox in an
accent surface and requires exact theme colors through normal, hover, focused,
checked, and disabled precedence; showcase-specific appearance repair is not an
acceptable oracle.

Action timeouts report the action and the latest screen. Snapshot mismatches
retain row boundaries and cell differences. Tests isolate held-pointer scenarios
or release capture before reuse, so state cannot leak between surfaces.

`ComponentCompositionSurfaceTests` places heterogeneous controls on one root and
drives forward and reverse Tab, local arrow behavior, hover transfer, and press
activation. A deep Overlay-to-Window-to-GroupBox-to-Expander-to-CheckBox tree
proves preview routing, handled bubble termination, pointer ancestry,
focus-within, and transitive capture and focus cleanup.

Every control that supports disabled state proves `IsEnabled=false` both on a
real mounted surface and in isolation - mounted rendering, hover, focus, Tab,
input routing, and appearance in its own `*SurfaceTests` fixture, and detached
validation and property behavior in its own unit fixture - rather than in only
one place.

### Visibility contract

The three-state [`Visibility`](../concepts/box-model.md#expected-behavior)
contract is deliberately not Boolean: `IsVisible` participates in layout,
rendering, and input; `Hidden` keeps its measured/arranged slot but renders
nothing and accepts no input; `Collapsed` contributes no desired size, receives
no arranged geometry, renders nothing, and forces its parent to reflow siblings
and dependent chrome. `ControlBase` enforces those leaf mechanics for every
control, and a `IsVisible`-to-`Hidden` transition invalidates only rendering —
never Measure — while a transition to or from `Collapsed` always invalidates
Measure. A host that manages children or single content still owns everything
`ControlBase` does not: spacing, track contribution, desired-size aggregation,
scroll extents and offsets, item realization, and stale-cell cleanup.

Every structural host - a container that manages children, a `ContentControl`,
or an items host - proves that same contract for its own children: a `Leaf`
control needs no dedicated Visibility fixture at all, because `ControlBase`
alone already proves the whole leaf contract and a per-leaf repeat would prove
nothing new, but a host that owns spacing, track contribution, desired-size
aggregation, scroll extents, or item realization proves each of those against
its own real content.

Mounted proof for a structural host uses a full `IsVisible` → `Hidden` →
`Collapsed` → `IsVisible` transition on a live `ComponentSurface`, not only the
initial mounted state: an opaque sibling background proves the committed final
cells at every phase, and a pointer probe proves the committed hit targets at
every phase, including that a `Hidden` phase freezes geometry while a
`Collapsed` phase reflows it. `StackSurfaceTests`, `DockSurfaceTests`, and
`GridSurfaceTests` follow this pattern for their respective layout algorithms.

`TerminalInputTests` sends real UTF-8 plus focus, SGR pixel mouse, bracketed
paste, and Kitty keyboard sequences through `Session`. It asserts focused route
payloads, pixel-to-cell inference, owned paste bytes, repeat action, control
mutation, completed frame callbacks, and the final UTF-8 bytes written by the
renderer transport. `ApplicationTests` proves zero-cell suspension resumes with
committed layout before its first positive frame.

`DisplayPanelTests` composes Grid, Dock, Stack, Overlay, Text, and a distinct
intrinsically bordered `Dock` frame under a real `Application` backed by
`FakeTerminal`. It proves startup bytes, committed bounds, exact semantic cells,
wide-cell continuation ownership, removal damage, text mutation, and resize
reflow on the same dispatcher-owned tree. Fresh semantic frames confirm removed
content does not survive, and later transport writes prove that incremental
output followed each mutation.

`ImageSurfaceTests` mounts the public passive Image control under unsupported
capabilities and requires the complete semantic shade plus the alternate-text
fallback. `ApplicationTests` sends an exact pixel-dimensioned resize through the
real Application and requires public Image fallback bytes before sixel output
with the same exact metrics. Separate Kitty cases require remote delete then
flush before transport disposal and prove that a cleanup write failure cannot
skip Session disposal. Image control tests cover later Window and Popup
occlusion, real RGBA and PNG sources, all stretch modes, and forced fallback. A
forced fallback stays source-backed; a later overlay makes its placement
ineffective. The mounted status test publishes changed Application capabilities
and requires the inherited Kitty, sixel, and iTerm2 state plus origin to update.

`InteractiveControlTests` composes Button, CheckBox, RadioButton, TextInput,
ScrollBar, an intrinsically scrollable Stack, and ListView under one real
application. Raw SGR cell clicks, Kitty Enter, wheel input, UTF-8 CJK, item
removal, terminal focus loss, and resize prove ordered activation and selection
events, focus and capture cleanup, exact semantic cells, wide-cell ownership,
incremental bytes, and cleared stale item rows. A separate editor path adds
owned bracketed paste containing a combining sequence, legacy Left, and
Backspace checkpoints.

`SliderSurfaceTests` mounts one rail and proves hover, Tab focus, directional
keys, direct pointer selection, capture, semantic press state, exact cells, and
disable cleanup. `ColorPickerSurfaceTests` mounts its retained true-color branch
and proves owner hover, focus delegation, semantic selection, preview cells,
composition ownership, and transitive capture cleanup. The component behavior
catalog requires both fixtures before either exported control is accepted.

`CalendarSurfaceTests` mounts one complete six-week month and drives real Tab,
directional, activation, pointer, blocked-date, and unavailable input. Its
reviewable 32-by-10 surface and semantic-cell assertions distinguish hover, the
focused active date, a pending interval, committed selection, disabled dates,
and authored markup. The exported behavior and shared box-model catalogs require
the same fixture before `Calendar` is accepted.

`ContainerTests` first sends 20 raw SGR wheel reports into nested hidden-bar
Stacks with intrinsic `AutoScroll` and proves exact inner consumption, the
outward remainder, and resize clamping. A second application uses automatic bars
on both nested axes, inferred pixel coordinates, and wide Unicode content. It
proves pixel thumb dragging, horizontal and vertical remainder, focus reveal
through both viewports, exact outer thumb cells, capture release, and removal of
the outer bars after a larger pixel-dimensioned resize.

## Controls with state machines

Buttons, toggles, radio groups, text editing, selection, menus, popups, windows,
scrollbars, and intrinsic container scrolling must enumerate their valid and
invalid transitions and their event order. Fake clocks drive hover and open
delays, timers, idle, and repeated input, so no test sleeps on the wall clock.

The text-selection aspect of `ControlBaseTests` and its surface siblings freeze
the default opt-out, cross-child semantic order, source-identity invalidation,
Unicode-safe keyboard and pointer ranges, common multi-click and unmodified
keyboard navigation, authoritative aggregate arbitration, final-cell styling,
capture cleanup, and routed clipboard copy for ordinary controls.
`DocumentSelectionTests` freezes the normalized semantic stream, directional
API, grapheme validation, mutation/reflow reconciliation, source identity, and
bounded row indexing. `DocumentSelectionSurfaceTests` mounts the real control
and proves click-versus-drag arbitration across retained children, complete-cell
selection styling, routed clipboard ownership, Shift navigation and reveal, and
50-millisecond nested autoscroll with lifecycle and modal cancellation. The
separate fixtures are intentionally selection-system suites rather than a
reflection-based control inventory.

Animated display controls use the same deterministic application clock. Tests
advance one interval at a time and compare consecutive complete semantic
screens, not private frame indices. They prove pause and resume, interval
restart, effective visibility, attachment, detachment, disposal, width-policy
fallback, and timer coalescing — all without sleeping. `SpinnerSurfaceTests` and
`ChaseIndicatorSurfaceTests` provide the mounted evidence required by the
public-control behavior catalog.

Menu mounted-surface proof sends real pointer motion, primary press and release,
Tab, Shift+Tab, arrows, Enter, Space, and Escape. It asserts the menu remains
the single focus stop, private faces receive hover and pressed states, compact
rows and shortcuts share one trailing edge, an armed submenu switches on pointer
or keyboard selection, generic popup ancestry stays intact, and closing restores
focus before submenu content becomes unavailable. Menu modality proof
additionally requires the same top-menu-rooted scope through sibling switching,
command rows, and arbitrary nested popup depth, plus outside dismissal without
background activation and restoration of a containing Window scope.

Popup mounted proof exercises automatic `IsOpen` Dismiss modality and explicit
`OpenModal` policy and focus selection; Window proof exercises `ShowModal`
rather than simulating isolation with disabled backgrounds. It requires the
default Dismiss and Ignore policies, Tab confinement, Escape/default/cancel
behavior, outside press and wheel consumption, unhandled in-plane wheel
completion, scroll-child retention, focus restoration, visibility and open-state
cleanup, and external Popup scope disposal that closes the transient surface.
Overlay Window coverage also resizes a previously trailing-positioned Window and
proves its complete border box is pushed back inside the latest client bounds.
Window chrome proof compares exact semantic cells for light, rounded, heavy,
paired, and ASCII bracketed close chrome; it also drives hover, press, captured
movement, release, capture loss, both close edges, title-lane collision
avoidance, and dialog Escape fallback through mounted controls.

Floating-surface architecture proof treats public identity as observable
behavior. The compatibility snapshot pins `Window` and `Popup` deriving from
`FloatingSurfaceBase`, `Dialog<TResult>` from Window, file dialogs and
MessageBox from Dialog, and Flyout and Tooltip from Popup. Mounted ownership
tests require the same concrete object to be presented, rendered, modal,
removed, and disposed; a nested Window or Popup cannot satisfy that evidence.
Overlay tests own all absolute-offset and z-order behavior, while architecture
tests reject the retired layout Canvas without affecting the terminal Frame
Canvas drawing tests.

`FilePickerDialogSurfaceTests` exercises the public asynchronous presentation,
real temporary-directory enumeration, file-only result selection, semantic
Escape and external cancellation, Ignore-plane background consumption, focus
restoration, host removal, and the selected-row semantic cells. The same
retained instance is resized through wide, normal, and smaller-than-minimum
surfaces; its centered Window stays bounded while its ListView glyphs and
selection contrast come from the shared control and theme paths. A tall mounted
surface proves the ListView consumes the available height without exceeding the
configured visible-row cap. Deterministic dialog tests prove filters, hidden
toggling, multiple selection, directory invocation, recoverable failure
retention, and late stale-generation rejection.

InputBase command tests use counting, throwing, and synchronously reentrant
`ICommand` event accessors. They prove nested property and accessor replacement
settles on one final subscription, superseded candidates are removed, failed
add/remove operations remain retryable, same-reference assignment does not
churn, and disposal exhaustively releases every known handler.

## Data-binding proof

Binding tests cover scalar modes, nested replacement and null recovery,
conversion, event ordering, validation, lifetime, dispatcher affinity,
observable collection actions, and item and selection coordination. Adversarial
collection tests cover throwing and reentrant event accessors, staged
notifications, stale observation generations, detached replacement, and old
deltas queued before a newer source-path revision. A worker burst publishes
10,000 changes while the dispatcher is occupied and must commit only the latest
value in one target update. A warmed allocation test bounds scalar updates at
256 managed bytes each and proves reverse updates cannot recurse. Culture-aware
control tests assign customized equal-named `CultureInfo` clones and prove
reference-identity commitment, exact notification and invalidation, edit-buffer
refresh, rendered separators and names, and synchronization into retained owned
controls; reassigning the identical instance remains silent.

## Required evidence

| Layer           | Observation                                                                   |
| --------------- | ----------------------------------------------------------------------------- |
| Control unit    | Public defaults, validation, state, ownership, invalidation, and event order. |
| Mounted surface | Application context, routed input, layout, cells/styles, focus, and capture.  |
| Cross-layer     | Terminal bytes produce the expected retained state and output.                |

Every shipped control covers normal, interactive, disabled, tiny, Unicode, and
resize behavior and has a representative showcase page.
