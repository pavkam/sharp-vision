# Control and integration testing

## Control and integration testing contract

Every concrete control tests property validation before mutation,
measure/arrange/render invalidation, ownership, dispatcher affinity, focus,
pointer capture, keyboard/pointer parity, disabled/hidden state, visual-state
composition, zero/tiny bounds, resize, events, and final semantic cells. Shared
box-model surface tests mount an opaque child on a distinct opaque parent and
assert that every margin edge retains the parent background, every padding edge
uses the child background, and an intervening border preserves both planes.
Captioned controls additionally prove
[access-key syntax and dispatch](../concepts/access-keys.md#expected-behavior):
exact marker-free cells, grapheme underline style, routed precedence, semantic
access-key foreground, disabled-state preservation, keyboard action, unavailable
filtering, duplicate order, modal confinement, and legacy Alt stroke/text
suppression. Showcase inventory additionally rejects duplicate reachable keys
within a page, across the active application tree, or along an open submenu
path. Generated list data, repeated documentation chrome, and arrow-navigated
catalog entries are not access-key captions.

Default-appearance tests require inactive and disabled semantic
foreground/border pairs for transparent interactive controls, complete triplets
for explicitly opaque faces, foreground/border-only hover and direct focus with
exact state precedence, visually inert pointer ancestry on layout containers,
transparent retained item faces, the opaque application Screen background,
role-specific component surfaces, and null backgrounds on transparent
composition controls. A repository test rejects ordinary control-page
assignments to foreground, background, border, underline, shadow, or
control-specific part colors, any decorative initial `ColorPicker` value, and
any per-state `Appearance` repair. Dedicated Border, Shadow, and Styling concept
pages are the narrow exception because the assigned property is their subject.
Ordinary showcase examples therefore prove the shipped control defaults rather
than carrying a second styling layer.

The exported-control inventory is guarded twice. The focused-unit catalog
requires every concrete public control to name the fixture that proves its
detached API contract. The mounted-surface catalog separately requires real
application evidence for each supported route. Pointer activation, keyboard
activation, and pointer activation forwarded by a retained child are distinct
obligations: evidence for one route never satisfies another. This distinction
also applies when multiple routes produce the same selection or invocation
event.

The common mounted geometry matrix applies the same fixed margin, intrinsic
border, and padding to every exported concrete control. It requires exact
border-box placement, exact `ContentBounds` deflation, and the expected frame
corner in the final modeled terminal. Specialized fixtures still own hover,
focus, pressed, disabled, resize, tiny-bound, and semantic activation evidence;
the common matrix prevents any control from escaping the shared box model or
chrome pipeline.

Controls whose pressed presentation changes geometry or chrome require explicit
pressed-frame evidence in addition to a boolean `IsPressed` assertion. The
Button contract includes the complete translated face, released origin,
foreground, and shadow cells while the primary pointer remains held.

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
and block shadows, wide-grapheme styling, signed and extreme offsets,
layout-neutral visual overflow, hit testing, transitive nesting, per-sibling
clip isolation, unclipped intermediates, explicit hard boundaries, and caller
canvas clipping. Curated-theme and mounted-showcase proof additionally requires
composite shadow cells to differ from the application background at Basic16
depth. `ContainerScrollGeometryTests` proves shadows neither change extent nor
escape the viewport. Mounted Button and Popup tests exercise the same paths
through retained layout and popup promotion. `ButtonTests` requires immediate
and post-layout pressed content parity, including translated faces outside
arranged hit bounds, while `TextInputTests` proves editor, caret, and private
rails remain inset exactly once.

## End-to-end path

Representative tests start with raw terminal key/mouse/paste/resize bytes and
exercise decoder, dispatcher, hit testing/focus, control behavior, invalidation,
layout, cell drawing, frame diff, encoder, and captured output bytes. Assertions
cover intermediate typed boundaries only when they are public contracts; final
bytes and virtual screen are mandatory.

`ModalityIntegrationTests` applies that complete path to the
[modal interaction contract](../concepts/modality.md#expected-behavior). One
real Application receives UTF-8 text, bracketed paste, Tab, terminal-focus
reports, SGR movement, primary press/release, wheel, and pixel-aware resize. The
test requires only in-plane routes while active, physical outside coordinates
without background hover, one dismiss callback, no replay to the exposed Button,
a fresh post-dismissal input route, final semantic cells, and emitted UTF-8
bytes. `ApplicationModalityTests` separately proves first-resize service
publication, resize identity, raw record targeting, and shutdown unwind when
callbacks fail. Its clipboard cases drive modal Control+C, Control+X, and
Control+V through the application, require the same handled arguments at only
`handledEventsToo` preview observers inside the captured plane, and mutate both
target ancestry and scope state during the edit to prove the current route
remains stable.

`ModalityManagerTests` attempts modal reentry and inclusion from detach, hide,
disable, and disposal callbacks after temporarily restoring the affected
control. It requires the guarded subtree to remain unavailable while unrelated
roots and ancestor planes remain valid, including automatic focus selection that
skips the guarded descendant. `ModalityFocusTests` proves failed-entry rollback
publishes every observed scope youngest first, nested exit restores before
`Exited`, failed focus callbacks leave coherent `Focused` and `IsFocused` facts,
and reentrant shutdown or unavailability strengthens a deferred teardown before
its notifications publish.

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
The StatusBar composition fixture additionally embeds an unstyled CheckBox in an
accent surface and requires exact theme colors through normal, hover, focused,
checked, and disabled precedence; showcase-specific appearance repair is not an
acceptable oracle.

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

`DisplayPanelTests` composes Grid, Dock, Stack, Overlay, Text, and a distinct
intrinsically bordered `Dock` frame under a real `Application` backed by
`FakeTerminal`. It proves startup bytes, committed bounds, exact semantic cells,
wide-cell continuation ownership, removal damage, text mutation, and resize
reflow on the same dispatcher-owned tree. Fresh semantic frames confirm removed
content does not survive, while later transport writes prove incremental output
followed each mutation.

`ImageSurfaceTests` mounts the public passive Image control under unsupported
capabilities and requires the complete semantic shade plus alternate-text
fallback. `ApplicationGraphicsTests` sends an exact pixel-dimensioned resize
through the real Application and requires public Image fallback bytes before
sixel output with the same exact metrics. Separate Kitty cases require remote
delete then flush before transport disposal and prove a cleanup write failure
cannot skip Session disposal. Image control tests cover later Window and Popup
occlusion; the runnable Image showcase is mounted at narrow, normal, and wide
widths and validates real RGBA/PNG sources, all stretch modes, forced fallback,
and later semantic overlap badges. Its forced fallback remains source-backed;
the later overlay makes its placement ineffective. The mounted status test
publishes changed Application capabilities and requires the inherited Kitty,
sixel, and iTerm2 state plus origin to update.

`InteractiveControlTests` composes Button, CheckBox, RadioButton, TextInput,
ScrollBar, an intrinsically scrollable Stack, and ListView under one real
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

`CalendarSurfaceTests` mounts one complete six-week month and drives real Tab,
directional, activation, pointer, blocked-date, and unavailable input. Its
reviewable 32-by-10 surface and semantic-cell assertions distinguish hover,
focused active date, pending interval, committed selection, disabled dates, and
authored markup. The exported behavior and shared box-model catalogs require the
same fixture before `Calendar` is accepted.

`ScrollingTests` first sends 20 raw SGR wheel reports into nested hidden-bar
Stacks with intrinsic `AutoScroll` and proves exact inner consumption, outward
remainder, and resize clamping. A second application uses automatic bars on both
nested axes, inferred pixel coordinates, and wide Unicode content. It proves
pixel thumb dragging, horizontal and vertical remainder, focus reveal through
both viewports, exact outer thumb cells, capture release, and removal of outer
bars after a larger pixel-dimensioned resize.

## Controls with state machines

Buttons, toggles, radio groups, text editing, selection, menus, popups, windows,
scrollbars, and intrinsic container scrolling must enumerate valid/invalid
transitions and event order. Fake clocks drive hover/open delays, timers, idle,
and repeated input without wall-clock sleeps.

Animated display controls use the same deterministic application clock. Tests
advance one interval at a time and compare consecutive complete semantic
screens, not private frame indices. They prove pause/resume, interval restart,
effective visibility, attachment, detachment, disposal, width-policy fallback,
and timer coalescing without sleeping. `SpinnerSurfaceTests` and
`ChaseIndicatorSurfaceTests` provide the mounted evidence required by the
public-control behavior catalog.

Menu mounted-surface proof sends real pointer motion, primary press/release,
Tab, Shift+Tab, arrows, Enter, Space, and Escape. It asserts the menu remains
the single focus stop, private faces receive hover and pressed states, compact
rows and shortcuts share one trailing edge, an armed submenu switches on pointer
or keyboard selection, generic popup ancestry stays intact, and closing restores
focus before submenu content becomes unavailable. Menu modality proof
additionally requires the same top-menu-rooted scope through sibling switching,
command rows, and arbitrary nested popup depth, plus outside dismissal without
background activation and restoration of a containing Window scope.

Popup mounted proof exercises automatic `IsOpen` Dismiss modality and explicit
`OpenModal` policy/focus selection; Window proof exercises `ShowModal` rather
than simulating isolation with disabled backgrounds. It requires default Dismiss
and Ignore policies, Tab confinement, Escape/default/cancel behavior, outside
press and wheel consumption, unhandled in-plane wheel completion, scroll-child
retention, focus restoration, visibility/open-state cleanup, and external Popup
scope disposal that closes the transient surface. Overlay Window coverage also
resizes a previously trailing-positioned Window and proves its complete border
box is pushed back inside the latest client bounds. Window chrome proof compares
exact semantic cells for light, rounded, heavy, paired, and ASCII bracketed
close chrome; it also drives hover, press, captured movement, release, capture
loss, both close edges, title-lane collision avoidance, and dialog Escape
fallback through mounted controls.

Floating-surface architecture proof treats public identity as observable
behavior. Reflection and consumer-contract tests require `Window` and `Popup` to
derive from `FloatingSurface`, `Dialog<TResult>` from Window, file dialogs and
MessageBox from Dialog, and Flyout and Tooltip from Popup. Mounted ownership
tests require the same concrete object to be presented, rendered, modal,
removed, and disposed; a nested Window or Popup cannot satisfy that evidence.
Overlay tests own all absolute-offset and z-order behavior, while architecture
tests reject the retired layout Canvas without affecting terminal Frame Canvas
drawing tests.

`FilePickerDialogSurfaceTests` exercises the public asynchronous presentation,
real temporary-directory enumeration, file-only result selection, semantic
Escape and external cancellation, Ignore-plane background consumption, focus
restoration, host removal, and selected-row semantic cells. The same retained
instance is resized through wide, normal, and smaller-than-minimum surfaces; its
centered Window remains bounded while its ListView glyphs and selection contrast
come from the shared control and theme paths. A tall mounted surface proves the
ListView consumes available height without exceeding the configured visible-row
cap. Deterministic dialog tests prove filters, hidden toggling, multiple
selection, directory invocation, recoverable failure retention, and late
stale-generation rejection.

## Data-binding proof

Binding tests cover scalar modes, nested replacement and null recovery,
conversion, event ordering, validation, lifetime, dispatcher affinity,
observable collection actions, and item/selection coordination. A worker burst
publishes 10,000 changes while the dispatcher is occupied and must commit only
the latest value in one target update. A warmed allocation test bounds scalar
updates at 256 managed bytes each and proves reverse updates cannot recurse.

`SharpVision.Consumer.Tests` compiles nested two-way and observable selection
examples without friend-assembly access. `make test-binding-coverage` collects
Cobertura for binding production files and fails below 95% line or 90% branch
coverage, or when those files are absent.

## Required evidence

| Layer           | Observation                                                                   |
| --------------- | ----------------------------------------------------------------------------- |
| Control unit    | Public defaults, validation, state, ownership, invalidation, and event order. |
| Mounted surface | Application context, routed input, layout, cells/styles, focus, and capture.  |
| Cross-layer     | Terminal bytes produce the expected retained state and output.                |
| Consumer        | Public composition compiles without internals or friend access.               |

Every shipped control covers normal, interactive, disabled, tiny, Unicode, and
resize behavior and has a representative showcase page.
