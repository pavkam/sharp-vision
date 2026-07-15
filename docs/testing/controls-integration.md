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

Intrinsic chrome tests set border and shadow properties on ordinary controls.
They prove border reservation and partial-edge glyphs, shadow visual overflow,
composite and block footprints, signed-offset clipping, wide-cell ownership,
validation-before-mutation, and hit-test exclusion. A chrome-rendering `Dock`
supplies a distinct frame node when the subject overrides `OnRender` without
calling `RenderChrome`.

## End-to-end path

Representative tests start with raw terminal key/mouse/paste/resize bytes and
exercise decoder, dispatcher, hit testing/focus, control behavior, invalidation,
layout, cell drawing, frame diff, encoder, and captured output bytes. Assertions
cover intermediate typed boundaries only when they are public contracts; final
bytes and virtual screen are mandatory.

`TerminalInputTests` sends real UTF-8 plus focus, SGR pixel mouse, bracketed
paste, and Kitty keyboard sequences through `Session`. It asserts focused route
payloads, pixel-to-cell inference, owned paste bytes, repeat action, control
mutation, completed frame callbacks, and the final UTF-8 bytes written by the
renderer transport. `ResizeRenderTests` proves zero-cell suspension resumes with
committed layout before its first positive frame.

`DisplayPanelTests` composes Grid, intrinsically bordered Dock surfaces, Stack,
Overlay, Canvas, and Text under a real `Application` backed by `FakeTerminal`.
It proves startup bytes, committed bounds, exact semantic cells, wide-cell
continuation ownership, removal damage, text mutation, and resize reflow on the
same dispatcher-owned tree. Fresh semantic frames confirm removed content does
not survive, while later transport writes prove incremental output followed each
mutation.

`InteractiveControlTests` composes Button, CheckBox, RadioButton, TextInput,
ScrollBar, an `AutoScroll`-enabled Stack, and List under one real application.
Raw SGR cell clicks, Kitty Enter, wheel input, UTF-8 CJK, item removal, terminal
focus loss, and resize prove ordered activation/selection events, focus and
capture cleanup, exact semantic cells, wide-cell ownership, incremental bytes,
and cleared stale item rows. A separate editor path adds owned bracketed paste
containing a combining sequence, legacy Left, and Backspace checkpoints.

`ScrollingTests` first sends 20 raw SGR wheel reports into nested hidden-bar
Stacks with intrinsic `AutoScroll` and proves exact inner consumption, outward
remainder, and resize clamping. A second application uses automatic bars on both
nested axes, inferred pixel coordinates, and wide Unicode content. It proves
pixel thumb dragging, horizontal and vertical remainder, focus reveal through
both viewports, exact outer thumb cells, capture release, and removal of outer
bars after a larger pixel-dimensioned resize.

## Controls with state machines

Phase 5 buttons, toggles, radio groups, text editing, selection, menus, popups,
windows, scrollbars, and intrinsically scrolling containers must enumerate
valid/invalid transitions and event order. Fake clocks drive hover/open delays,
timers, idle, and repeated input without wall-clock sleeps.
