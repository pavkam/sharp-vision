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
box-model plumbing; `Gauge.OnRenderContent` draws custom content through
`ContentBounds` while the sealed base pipeline supplies its configured chrome. A
reflection guard fails if the product friends either the consumer project or the
production showcase.

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

Tests drive `surface.Pointer` with `MoveToAsync`, `PressAsync`, `ReleaseAsync`,
`ClickAsync`, `LeaveAsync`, or captured drag operations, and drive
`surface.Keyboard` with supported typed key codes, Shift+Tab, and distinct Kitty
character press/release actions. Those helpers emit terminal bytes; they never
call `Router.Route`, direct hover mutation, `SetPressed`, or
`FocusManager.Focus` on the component. Each action waits until the transport
consumes its bytes and the application reaches idle after routed work, layout,
rendering, and output.

```csharp
await using var surface = await ComponentSurface.MountAsync(
    button,
    new Size(10, 5),
    TestContext.Current.CancellationToken);

await surface.Pointer.MoveToAsync(button);
await surface.Pointer.PressAsync();

surface.ShouldHaveState(
    button,
    VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
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

Action timeouts report the action and latest screen. Snapshot mismatches retain
row boundaries and cell differences. Tests isolate held-pointer scenarios or
release capture before reuse, so state cannot leak between surfaces.

`ComponentSurfaceCoverageTests` catalogs every exported concrete control and
requires an exact attributed evidence set for mounted rendering, hover or its
explicit exclusion, focus, Tab, directional keys, semantic press/release,
activation, unavailable-state cleanup, transient layers, and retained
composition. Adding a control or changing its behavior classification fails the
catalog until its mounted fixture supplies matching evidence. The composition
suite separately places heterogeneous controls on one root and drives forward
and reverse Tab, local arrow behavior, hover transfer, and press activation. A
deep Overlay to Window to GroupBox to Expander to CheckBox tree proves preview
routing, handled bubble termination, pointer ancestry, focus-within, and
transitive capture/focus cleanup.

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

`SliderSurfaceTests` mounts one rail and proves hover, Tab focus, directional
keys, direct pointer selection, capture, semantic press state, exact cells, and
disable cleanup. `ColorPickerSurfaceTests` mounts its retained true-color branch
and proves owner hover, focus delegation, semantic selection, preview cells,
composition ownership, and transitive capture cleanup. The component behavior
catalog requires both fixtures before either exported control is accepted.

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

Menu mounted-surface proof sends real pointer motion, primary press/release,
Tab, Shift+Tab, arrows, Enter, Space, and Escape. It asserts the menu remains
the single focus stop, private faces receive hover and pressed states, compact
rows and shortcuts share one trailing edge, an armed submenu switches on pointer
or keyboard selection, generic popup ancestry stays intact, and closing restores
focus before submenu content becomes unavailable.
