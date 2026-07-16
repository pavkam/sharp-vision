# Control and integration testing

## Control and integration testing

Every concrete control tests property validation before mutation,
measure/arrange/render invalidation, ownership, dispatcher affinity, focus,
pointer capture, keyboard/pointer parity, disabled/hidden state, visual-state
composition, zero/tiny bounds, resize, events, and final semantic cells.

Layout tests use recording controls to assert measure constraints, desired size,
arranged slots, call order, cache invalidation, non-reentrancy, rounding, and
clipping. Routed-input tests record route, phase, source, handled state, local
coordinates, mutation during dispatch, default behavior, and cleanup.

Control-render tests inspect final `Frame` cells and copied grapheme bytes. They
cover nested clips, later-child overwrite, padding, hidden/collapsed subtrees,
zero bounds, combining sequences, wide CJK and emoji ZWJ ownership, resolved
state styles, default cursor preservation, render-time invalidation, and
exception recovery. Private draw-call recordings supplement these semantic
oracles; they never replace them.

Intrinsic chrome proof lives on the common `Control` surface rather than on
wrapper-control suites. `ControlBorderReservationTests`,
`ContainerAutoSizeTests`, and `ContainerScrollGeometryTests` cover base
border-before-padding reservation, saturated combined insets, shrink wrapping,
and scrollbar containment. `IntrinsicBorderTests` and `IntrinsicShadowTests`
cover validation-before-mutation, partial edges, exact glyphs/cells, composite
and block shadows, wide-grapheme styling, visual overflow, hit testing, and
ancestor clipping. `ButtonTests` requires immediate and post-layout pressed
content parity, while `TextInputTests` proves editor, caret, and private rails
remain inset exactly once.

## External extension proof

`SharpVision.Consumer.Tests` references only the production `SharpVision`
project and receives no friend access. Its independently compiled `Gauge`,
`FlowPanel`, `OverflowPanel`, `InteractiveProbe`, `ExternalContentControl`, and
`ExternalToggleChip` prove protected property and visual-state mutation,
Unicode-aware measurement/rendering, ordinary and unclipped custom layout,
direct-child layout, public single-content ownership, lifecycle publication,
focus, capture, and capture-cancellation ordering. `ExternalContentControl` also
proves inherited content layout, rendering, hit testing, and committed change
observation without tree internals. `ExternalToggleChip` proves an unfriended
third party can derive from `Pressable`, assign inherited `Content`, and
activate checked styling without internal access. The external `FlowPanel`
proves that setting `BorderThickness` insets owned leaves without third-party
box-model plumbing; `Gauge.OnRender` calls `RenderChrome` before custom content
drawn through `ContentBounds`. A reflection guard fails if the product friends
either the consumer project or the production showcase.

`SharpVision.Tests` deliberately retains friend access for internal invariant
tests and therefore cannot serve as third-party API proof. The unfriended
consumer project also contains retained `StatusCard` and typed `TagCloud`
specimens. `make test` packs both production projects into a temporary local
feed, verifies their XML documentation and assets, then restores, builds, and
runs those specimens against the packages rather than project references.

## End-to-end path

Representative tests start with raw terminal key/mouse/paste/resize bytes and
exercise decoder, dispatcher, hit testing/focus, control behavior, invalidation,
layout, cell drawing, frame diff, encoder, and captured output bytes. Assertions
cover intermediate typed boundaries only when they are public contracts; final
bytes and virtual screen are mandatory.

### Mounted component surfaces

Focused control regression tests use `ComponentSurface` when behavior crosses
input, layout, styling, and rendering boundaries. `MountAsync` places one
detached control in a fixed-size host, starts a real `Application`, and returns
only after the first renderer write is applied to an independent semantic
screen. The host begins as a neutral focus anchor so a real Tab byte can move
focus into the mounted component without calling `FocusManager` directly.

Tests drive `surface.Pointer` with center- or cell-relative `MoveToAsync` and
`ClickAsync`, stateful `PressAsync`/`MovePressedToAsync`/`ReleaseAsync`, unit
`WheelAsync`, or complete `DragAsync`. Center clicks may carry Shift, Alt, and
Control terminal modifiers for selection-policy tests. Relative cells are
validated against the target's current arranged bounds on the UI dispatcher;
foreign, empty, negative, and right/bottom-edge targets fail before input is
queued. A wheel action accepts exactly one horizontal or vertical unit delta.
Complete drag notation is concise, while the stateful form exposes capture and
pressed-state checkpoints:

```csharp
await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelX: -1);
await surface.Pointer.DragAsync(
    bar,
    new Point(1, 0),
    new Point(10, 0));

await surface.Pointer.MoveToAsync(bar, new Point(1, 0));
await surface.Pointer.PressAsync();
surface.ShouldHaveState(bar, State.Hovered | State.Focused | State.Pressed);
await surface.Pointer.MovePressedToAsync(bar, new Point(10, 0));
await surface.Pointer.ReleaseAsync();
```

`surface.Keyboard.PressAsync` encodes supported navigation, editing, and Kitty
key actions, with a modifier overload for supported combinations. `TypeAsync`
emits one owned UTF-8 text action, `PasteAsync` emits one complete bracketed
paste transaction, and `CompleteCharacterAsync` emits distinct Kitty press and
release transitions. Typed text must be non-empty and contain no terminal
controls; unsupported key/modifier pairs fail before bytes are queued.

Those helpers emit terminal bytes; they never call `Router.Route`, `SetHovered`,
`SetPressed`, or `FocusManager.Focus` on the component. Input consumption is
acknowledged only when the terminal session requests its next read, after it has
synchronously decoded and routed the preceding bytes. The action then waits for
a dispatcher fence and application idle after routed work, layout, rendering,
and output. The fence causes a fresh idle transition even when disabled or
otherwise ignored input produces no invalidation. An idle notification from
before the current decode therefore cannot expose a partial action or strand a
no-op action.

`surface.UpdateAsync` runs an ordinary public mutation on the application
dispatcher and settles the same layout/render/output path. `surface.ResizeAsync`
replaces the modeled terminal surface, publishes a real `Dimensions` record
through `IResizeSource`, and completes only after `Application.Size` and the
final modeled frame match the positive requested cell geometry. Neither method
calls layout or rendering internals directly.

```csharp
await using var surface = await ComponentSurface.MountAsync(
    button,
    new Size(10, 5),
    TestContext.Current.CancellationToken);

await surface.Pointer.MoveToAsync(button);
await surface.Pointer.PressAsync();

surface.ShouldHaveState(
    button,
    State.Hovered | State.Focused | State.Pressed);
surface.ShouldRender("""

     ╭──────╮
     │Save  │
     ╰──────╯

    """);
```

`ShouldRender` compares every final surface row and right-pads omitted trailing
blank cells by measured terminal-cell width rather than UTF-16 length. Wide and
combining graphemes therefore preserve their cell geometry. Whole-surface text
is a reviewable appearance oracle, not sufficient proof by itself. Every
scenario also asserts public control state and representative semantic cells,
including resolved colors, attributes, continuation ownership, border cells, and
shadow cells. This keeps the mounted path aligned with the
[input-routing](../concepts/input-routing.md#input-routing),
[focus](../concepts/focus.md#focus-contract),
[visual-state](../concepts/styling.md#visual-states), and
[rendering-equivalence](rendering.md#rendering-equivalence-testing) contracts.
`ShouldHaveCursor` additionally compares the final semantic terminal cursor
position and DEC visibility state, validating its point against the current
resized surface.

Action timeouts report the action and latest screen. Snapshot mismatches retain
row boundaries and cell differences. Tests isolate held-pointer scenarios or
release capture before reuse, so state cannot leak between surfaces.

`TextSurfaceTests`, `FigletTextSurfaceTests`, `SeparatorSurfaceTests`, and
`ProgressBarSurfaceTests` use resize on the same mounted instance to prove
reflow, clipping exposure, axis-length recomputation, and removal of obsolete
cells. `CheckBoxSurfaceTests` proves tiny-to-full content reveal, while
`RadioButtonSurfaceTests` proves real group selection, disabled skipping, and
arrow wrapping. These fixtures supplement, rather than replace, exhaustive pure
state, validation, and geometry tests.

`TextInputSurfaceTests` proves placeholder and password appearance, focus and
semantic cursor, Unicode typing and cluster-safe navigation/deletion, Home/End,
wide selection, atomic paste, submit policy, read-only and disabled refusal,
pointer drag selection, wheel-driven owned rails, and resize offset repair.
`ScrollBarSurfaceTests` proves exact horizontal, vertical, resized, and tiny
geometry; combined state appearance; keyboard/button/track/wheel causes;
endpoint no-ops; captured thumb motion; and disable cleanup.

The layout-control surface audit is split by responsibility instead of hiding
everything behind snapshots. `StackSurfaceTests`, `GridSurfaceTests`, and
`DockSurfaceTests` prove mixed track allocation, ordering, collapsed-child
exclusion, intrinsic scrolling, span/padding hit targets, every dock edge,
resize, clipping, Unicode cells, and exact committed bounds. Their corresponding
`StackTests`, `GridTests`, and `DockTests` retain exhaustive automatic sizing,
deterministic remainder, unbounded measure, overflow, tiny-bound, and mutation
algorithms.

`OverlaySurfaceTests` and `CanvasSurfaceTests` prove visual and hit-test
precedence, visibility/removal repair, common-slot alignment, percentage
repositioning, negative/oversized clipping, and resize exposure. Exhaustive
transparent-child, popup-layer, scroll-gutter, and signed-origin rules remain in
`OverlayTests` and `CanvasTests`. `TableSurfaceTests` proves mixed columns,
headers, Unicode, clickable row reuse/removal, both-axis scrolling, resize
clamping, and stale-cell clearing; `TableTests` retains exhaustive column,
wrapping, viewport, mutation, and scroll-origin cache invariants.

`GroupBoxSurfaceTests` proves exact empty and wide-header frames, content
insets, tiny clipping, resize reveal, style inheritance, and continuation
ownership. `ExpanderSurfaceTests` proves exact expanded/collapsed appearance,
stale-content clearing, pointer/Space/Enter parity, focus, disabled refusal,
replacement, Unicode, tiny clipping, and resize reflow. `GroupBoxTests` and
`ExpanderTests` retain validation-before-mutation, ownership transfer,
desired-size, event order, and retained framework-part responsibilities.

`ListSurfaceTests` proves neutral-host focus entry, pointer and keyboard
selection/invocation parity, changed-event order, Up/End/Page navigation,
Control toggling, Shift ranges, disabled-item skipping, selected-state styling,
Unicode continuation ownership, bring-into-view offsets, resize clamping,
selection/active repair after replacement, and complete stale-row clearing.
`ListTests` retains snapshot/template atomicity, validation, ownership/disposal,
cancellation/reentrancy, selection-mode normalization, and common scrollbar
policy responsibilities.

`TabControlSurfaceTests` proves exact retained headers, dividers, separator and
selected content; pointer/keyboard parity; focus; selected-state styling;
disabled skipping and wrap; deterministic removal/content-replacement repair;
Unicode continuation ownership; header overflow/reveal; tiny clipping; resize;
and stale-cell clearing. `TabControlTests` and `TabItemTests` retain typed
collection validation, ownership transfer without disposal, stable-identity
insertion, nearest eligible repair, invalid selection, event order, retained
header identity, and selected-content layout responsibilities.

`TerminalInputTests` sends real UTF-8 plus focus, SGR pixel mouse, bracketed
paste, and Kitty keyboard sequences through `Session`. It asserts focused route
payloads, pixel-to-cell inference, owned paste bytes, repeat action, control
mutation, completed frame callbacks, and the final UTF-8 bytes written by the
renderer transport. `ResizeRenderTests` proves zero-cell suspension resumes with
committed layout before its first positive frame.

`DisplayPanelTests` composes Grid, Dock, Stack, Overlay, Canvas, Text, and a
distinct intrinsically bordered `Dock` frame under a real `Application` backed
by `FakeTerminal`. It proves startup bytes, committed bounds, exact semantic
cells, wide-cell continuation ownership, removal damage, text mutation, and
resize reflow on the same dispatcher-owned tree. Fresh semantic frames confirm
removed content does not survive, while later transport writes prove incremental
output followed each mutation.

`InteractiveControlTests` composes Button, CheckBox, RadioButton, TextInput,
ScrollBar, an intrinsically scrollable Stack, and List under one real
application. Raw SGR cell clicks, Kitty Enter, wheel input, UTF-8 CJK, item
removal, terminal focus loss, and resize prove ordered activation/selection
events, focus and capture cleanup, exact semantic cells, wide-cell ownership,
incremental bytes, and cleared stale item rows. A separate editor path adds
owned bracketed paste containing a combining sequence, legacy Left, and
Backspace checkpoints.

`ScrollingTests` first sends 20 raw SGR wheel reports into nested hidden-bar
Stacks with intrinsic `AutoScroll` and proves exact inner consumption, outward
remainder, and resize clamping. A second application uses automatic bars on both
nested axes, inferred pixel coordinates, and wide Unicode content. It proves
pixel thumb dragging, horizontal and vertical remainder, focus reveal through
both viewports, exact outer thumb cells, capture release, and removal of outer
bars after a larger pixel-dimensioned resize.

## Controls with state machines

Phase 5 buttons, toggles, radio groups, text editing, selection, menus, popups,
windows, scrollbars, and intrinsic container scrolling must enumerate
valid/invalid transitions and event order. Fake clocks drive hover/open delays,
timers, idle, and repeated input without wall-clock sleeps.
