# UI Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the dispatcher-affine mutable control tree, deterministic
measure/arrange layout, routed input, focus, capture, visual-state styling, and
application lifecycle that connect typed terminal events to semantic frames.

**Architecture:** A dedicated `Dispatcher` thread serializes every control
mutation and callback. `Control` owns validated box-model properties and
phase-specific invalidation; layout resolves fixed, percent, automatic, and
proportional values without recursive re-entry. `Application` adapts the
terminal `Session` to coalesced dispatcher records, routes input through focus
or hit testing, renders a borrowed back frame, and raises resize/frame/idle
events only after their documented commit points.

**Tech Stack:** .NET 10, C# 14, `Rune`, spans and memory,
`SharpVision.Terminal.Geometry`, `SharpVision.Terminal.Rendering`,
`SharpVision.Terminal.Runtime`, `TimeProvider`, xUnit v3, Shouldly

---

## Non-negotiable boundaries

- Controls are ordinary mutable objects. This phase adds no virtual tree,
  reconciliation, function component, hook, or hidden renderer wiring.
- One dispatcher thread owns the attached tree, styles, focus, capture, layout,
  frame scheduling, and user callbacks. Validation and affinity checks occur
  before observable mutation.
- No user callback runs while a queue, tree, focus, capture, or style lock is
  held. Event routes are snapshots and remain stable during mutation.
- Width/height describe the border box. Margin is external, padding internal,
  deflation saturates at zero, and min/max clamp the border box.
- Percentages resolve from the final containing content box. During unbounded
  measure they use intrinsic size; proportional values consume deterministic
  remaining space during arrangement.
- Layout and render invalidation coalesce. Reentrant measure, arrange, or render
  calls throw; invalidation during a phase schedules a later pass.
- Resize commits the newest size, completes root layout, raises resize with
  committed bounds, and then schedules rendering.
- `Idle` fires once per transition to no ready input, posted work, timer,
  layout, or render work, directly before the dispatcher waits. A render in
  flight is not idle.
- Keys route to focus. Pointer capture wins over reverse-z hit testing. Preview
  runs root-to-target, bubble target-to-root, and handled-event opt-in remains
  explicit.
- Controls render only to `Rendering.Canvas`; UI code never emits terminal byte
  sequences.

## File map

### Threading and lifecycle

- `src/SharpVision/Threading/Dispatcher.cs`: bounded FIFO work queue, dedicated
  thread ownership, `CheckAccess`, `VerifyAccess`, `Post`, `InvokeAsync`,
  pending phase leases, idle transition, shutdown, and exception propagation.
- `src/SharpVision/Threading/UnhandledEventArgs.cs`: dispatcher callback failure
  value with explicit continue/stop policy.
- `src/SharpVision/Runtime/Application.cs`: terminal-session adapter, lifecycle,
  resize coalescing, phase scheduling, input routing, frame production, and
  shutdown.
- `src/SharpVision/Runtime/Events.cs`: immutable resize and frame-complete event
  arguments plus cancellable stopping state.

### Layout and controls

- `src/SharpVision/Layout/{Kind,Length,Constraint,Thickness,Alignment,Visibility}.cs`:
  validated box-model primitives.
- `src/SharpVision/Layout/Tracks.cs`: deterministic fixed/percent/auto/star
  allocation with cumulative rounding.
- `src/SharpVision/Layout/Engine.cs`: guarded root measure/arrange transaction
  and committed-size tracking.
- `src/SharpVision/Controls/{Control,Container,Children,Invalidation}.cs`:
  mutable base, one-parent collection, recursive attachment, box layout,
  rendering, hit testing, and phase invalidation.

### Input and styling

- `src/SharpVision/Input/{Phase,Event,RoutedEventArgs,Events,Router}.cs`: typed
  event identifiers, handler registration, stable preview/bubble routes, and
  terminal payload adapters.
- `src/SharpVision/Input/{FocusManager,CaptureManager}.cs`: eligibility,
  transactional focus, tab order, pointer capture, cleanup, and targeting.
- `src/SharpVision/Styling/{State,Appearance,Style,Resolver}.cs`: mutable style
  resources, state overlays, subscriptions, precedence, and renderer-style
  resolution.

### Tests and specs

- `tests/SharpVision.Tests/Support/`: recording controls, dispatcher probes,
  runtime sink/transport, and frame oracle helpers.
- `tests/SharpVision.Tests/{Threading,Layout,Controls,Input,Styling,Runtime}/`:
  focused public-behavior and ordering suites.
- `docs/concepts/{threading,layout,styling,focus,input-routing,lifecycle-events}.md`:
  exact shipped APIs and ordering.
- `docs/controls/control.md` and architecture/testing documents: ownership,
  canvas boundary, event loop, correctness oracle, and performance contract.

## Task 1: Add validated layout primitives

**Files:**

- Create: `src/SharpVision/Layout/Kind.cs`
- Create: `src/SharpVision/Layout/Length.cs`
- Create: `src/SharpVision/Layout/Constraint.cs`
- Create: `src/SharpVision/Layout/Thickness.cs`
- Create: `src/SharpVision/Layout/Alignment.cs`
- Create: `src/SharpVision/Layout/Visibility.cs`
- Create: `tests/SharpVision.Tests/Layout/PrimitiveTests.cs`
- Modify: `docs/concepts/layout.md`

- [x] **Step 1: Write the failing primitive contract tests**

  Test `Length.Auto`, `Cells(0)`, `Percent(0..100)`, and `Star(positive)`, both
  bounded and unbounded `Constraint` axes, asymmetric non-negative `Thickness`,
  saturating deflation, enum validation, equality, and validation before value
  replacement. Use the public shape below.

  ```csharp
  var length = Length.Percent(37.5);
  length.Kind.ShouldBe(Kind.Percent);
  length.Value.ShouldBe(37.5);
  new Constraint(width: null, height: 20).IsWidthBounded.ShouldBeFalse();
  new Thickness(1, 2, 3, 4).Deflate(new Size(2, 3)).ShouldBe(default);
  ```

- [x] **Step 2: Run the focused tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*PrimitiveTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because `SharpVision.Layout` does not exist.

- [x] **Step 3: Implement immutable contextual primitives**

  `Kind` is `Auto`, `Cells`, `Percent`, or `Star`. `Length` stores a finite
  `double` only for value-bearing kinds: cells and percent are non-negative,
  percent is at most 100, and star is strictly positive. `Constraint` uses
  nullable non-negative integer axes. `Thickness` has left/top/right/bottom,
  checked horizontal/vertical sums, and saturating `Deflate(Size/Rect)`.
  Horizontal alignment is left/center/right/stretch; vertical alignment is
  top/center/bottom/stretch; visibility is visible/hidden/collapsed. Every
  public constructor validates enums and numeric ranges with documented
  exceptions.

- [x] **Step 4: Verify primitives and docs**

  Run the focused command from Step 2 and `make lint`. Expected: all discovered
  tests pass and XML/Markdown validation reports no error.

- [x] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Layout tests/SharpVision.Tests/Layout docs/concepts/layout.md
  git commit -m "feat: add UI layout primitives"
  ```

## Task 2: Build the dispatcher thread and idle transition

**Files:**

- Create: `src/SharpVision/Threading/Dispatcher.cs`
- Create: `src/SharpVision/Threading/UnhandledEventArgs.cs`
- Create: `tests/SharpVision.Tests/Threading/DispatcherTests.cs`
- Modify: `docs/concepts/threading.md`
- Modify: `docs/concepts/lifecycle-events.md`

- [x] **Step 1: Write failing ownership and queue tests**

  Prove `Start` creates one named background thread, callbacks are FIFO,
  `CheckAccess` is true only there, `VerifyAccess` fails elsewhere,
  `InvokeAsync` transfers result/exception/cancellation, queue capacity rejects
  before enqueue, callback failure is reported outside the queue lock, disposal
  cancels pending invocations, and disposal is idempotent.

  ```csharp
  await using var dispatcher = Dispatcher.Start(capacity: 32);
  var owner = await dispatcher.InvokeAsync(Environment.CurrentManagedThreadId);
  dispatcher.CheckAccess().ShouldBeFalse();
  owner.ShouldNotBe(Environment.CurrentManagedThreadId);
  ```

  Add an idle test that posts work from the idle handler and requires the new
  work to run before another wait. Hold an internal pending-phase lease and
  prove idle does not fire until it is released.

- [x] **Step 2: Run the focused tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*DispatcherTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because `Dispatcher` is absent.

- [x] **Step 3: Implement the bounded dedicated-thread loop**

  `Dispatcher.Start(int capacity = 4096, string? name = null)` starts exactly
  one thread. A private lock protects only queue and lifecycle state; actions
  are dequeued under the lock and invoked after it is released. `Post(Action)`
  and both `InvokeAsync(Action)`/`InvokeAsync<T>(Func<T>)` validate delegates
  and reject shutdown or capacity overflow. An internal disposable pending lease
  suppresses idle while asynchronous rendering is incomplete.

  Track an `idleRaised` transition bit. New queue work or pending work resets
  it. When queue and pending count reach zero, raise `Idle` once outside the
  lock, immediately recheck for handler-posted work, then block on a condition;
  never poll or sleep. Route action exceptions through `UnhandledException` and
  stop only when unhandled.

- [x] **Step 4: Verify dispatcher semantics**

  Run Step 2, then repeat the suite with `--repeat 20` if supported by the local
  runner; otherwise use an explicit 1,000-operation ordering test. Expected:
  stable order, no timeout, no busy-spin counter growth.

- [x] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Threading tests/SharpVision.Tests/Threading docs/concepts/threading.md docs/concepts/lifecycle-events.md
  git commit -m "feat: add single-thread UI dispatcher"
  ```

## Task 3: Add the mutable control tree and invalidation

**Files:**

- Create: `src/SharpVision/Controls/Invalidation.cs`
- Create: `src/SharpVision/Controls/Control.cs`
- Create: `src/SharpVision/Controls/Container.cs`
- Create: `src/SharpVision/Controls/Children.cs`
- Create: `tests/SharpVision.Tests/Controls/TreeTests.cs`
- Create: `tests/SharpVision.Tests/Controls/PropertyTests.cs`
- Create: `tests/SharpVision.Tests/Support/ProbeControl.cs`
- Modify: `docs/controls/control.md`

- [ ] **Step 1: Write failing tree and mutation tests**

  Cover attachment to one dispatcher, detached construction/mutation, attached
  cross-thread rejection before mutation, null/duplicate/cycle/cross-parent
  child rejection, indexed insert/replace/remove/clear, recursive dispatcher
  propagation, parent cleanup, and idempotent disposal. Record invalidation so
  width/margin/collapse request measure, alignment requests arrange, and enabled
  or visible-hidden changes request render without falsely requesting measure.

  ```csharp
  var parent = new ProbeContainer();
  var child = new ProbeControl();
  parent.Children.Add(child);
  child.Parent.ShouldBeSameAs(parent);
  Should.Throw<ArgumentException>(() => parent.Children.Add(child));
  ```

- [ ] **Step 2: Run focused control tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*TreeTests" "*PropertyTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because the control types are absent.

- [ ] **Step 3: Implement ownership, state, and phase invalidation**

  `Control` exposes validated width/height/min/max/margin/padding/alignment,
  visibility, enabled, focusability, tab index, parent, dispatcher, desired
  size, and committed bounds. Setters call `VerifyAccess` when attached, compare
  before mutation, and raise `PropertyChanged` after the matching invalidation.
  Effective visibility/enabled state inherits through ancestors.

  `Container` owns a public read-only-reference `Children` collection. Mutation
  validates the complete operation before touching either tree, walks ancestors
  to reject cycles, recursively attaches/detaches dispatcher ownership, and
  notifies the host to release focus/capture on removal. `Invalidation` is a
  flags enum ordered Measure => Arrange => Render; promotion bubbles once toward
  the root and is coalesced by the host callback.

- [ ] **Step 4: Verify tree behavior and allocation ownership**

  Run Step 2 and the public XML-doc build. Expected: all tests pass; no child or
  dispatcher reference remains after detach/dispose.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Controls tests/SharpVision.Tests/Controls tests/SharpVision.Tests/Support/ProbeControl.cs docs/controls/control.md
  git commit -m "feat: add mutable control tree"
  ```

## Task 4: Implement box measure and arrange

**Files:**

- Create: `src/SharpVision/Layout/Engine.cs`
- Create: `tests/SharpVision.Tests/Layout/BoxLayoutTests.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `tests/SharpVision.Tests/Support/ProbeControl.cs`
- Modify: `docs/concepts/layout.md`
- Modify: `docs/controls/control.md`

- [ ] **Step 1: Write failing box-model tests**

  Test fixed/auto/percent/star width and height, bounded versus unbounded
  measure, min/max, margin/padding, every alignment, stretch with explicit
  length, collapse, zero/tiny deflation, cached repeated measure, resize
  remeasure, and invalidation during measure/arrange. `ProbeControl` records its
  content constraint and arranged content rectangle.

  ```csharp
  var child = new ProbeControl(new Size(7, 3))
  {
      Width = Length.Percent(50),
      Margin = new Thickness(1),
      Padding = new Thickness(1),
  };
  engine.Layout(child, new Size(20, 10));
  child.Bounds.Width.ShouldBe(10);
  ```

- [ ] **Step 2: Run focused layout tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*BoxLayoutTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: tests fail because `Engine` and control phases do not exist.

- [ ] **Step 3: Implement guarded two-phase layout**

  Add internal `Measure(Constraint)` and `Arrange(Rect)` entry points with
  protected `MeasureCore(Constraint)` and `ArrangeCore(Rect)` extension points.
  Measure deflates margin/padding, treats percent/star as intrinsic on an
  unbounded axis, resolves percent only on bounded axes, adds padding, clamps
  the border box, and stores desired border size. Arrange deflates margin,
  resolves fixed/percent/auto/star from the final slot, clamps, aligns using
  cumulative integer edges, commits `Bounds`, then passes the padded content
  rectangle to the extension point.

  `Engine.Layout(Control root, Size size)` is dispatcher-affine, rejects nested
  calls, drains measure then arrange invalidation, and leaves invalidation
  raised during a pass queued for one later transaction. Collapsed controls
  desire zero, clear committed bounds, and never invoke core methods.

- [ ] **Step 4: Verify deterministic layout**

  Run Step 2 plus a 10,000-case fixed-seed test over valid lengths, margins,
  padding, alignment, and sizes. Assert bounds are non-negative, contained after
  saturated deflation, and identical for the same seed.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Layout/Engine.cs src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Layout tests/SharpVision.Tests/Support/ProbeControl.cs docs/concepts/layout.md docs/controls/control.md
  git commit -m "feat: add deterministic box layout"
  ```

## Task 5: Add percentage and proportional track allocation

**Files:**

- Create: `src/SharpVision/Layout/Tracks.cs`
- Create: `tests/SharpVision.Tests/Layout/TrackTests.cs`
- Create: `tests/SharpVision.Tests/Layout/RandomizedTrackTests.cs`
- Modify: `docs/concepts/layout.md`
- Modify: `docs/testing/correctness-model.md`

- [ ] **Step 1: Write failing allocator tests**

  Test fixed reservation, percent of final content space, automatic intrinsic
  maxima, star weight distribution, deficits, min/max clamps, zero space,
  percentage under unbounded measure, spans, and cumulative rounding where
  allocated cells sum exactly to the available axis.

  ```csharp
  var result = Tracks.Resolve(
      available: 11,
      [Length.Percent(50), Length.Star(1), Length.Star(1)],
      automatic: [0, 0, 0]);
  result.Sum().ShouldBe(11);
  result.ShouldBe([6, 3, 2]);
  ```

- [ ] **Step 2: Run track tests and verify RED**

  Run the layout filter from Task 4. Expected: compile failure for `Tracks`.

- [ ] **Step 3: Implement cumulative-edge allocation**

  Resolve fixed and clamped auto tracks first. Percent edges are calculated from
  the final available axis, not a shrinking remainder. Star tracks divide the
  remaining non-negative cells by finite positive weights. Convert fractional
  cumulative edges with one documented midpoint rule; adjacent tracks reuse the
  same edge and the last eligible track receives the exact remainder. In
  unbounded measure, percent and star return their intrinsic automatic request.
  Reject mismatched array lengths and invalid track definitions before writing
  caller output.

- [ ] **Step 4: Verify the randomized invariant**

  Generate 20,000 valid track sets at fixed seed `0x4A70`. Assert no negative
  extent, no overflow, exact bounded sum, min/max compliance, stable repeat, and
  no allocation after warm-up with caller-owned spans.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Layout/Tracks.cs tests/SharpVision.Tests/Layout docs/concepts/layout.md docs/testing/correctness-model.md
  git commit -m "feat: allocate percentage and proportional tracks"
  ```

## Task 6: Add stable routed events

**Files:**

- Create: `src/SharpVision/Input/Phase.cs`
- Create: `src/SharpVision/Input/Event.cs`
- Create: `src/SharpVision/Input/RoutedEventArgs.cs`
- Create: `src/SharpVision/Input/Events.cs`
- Create: `src/SharpVision/Input/Router.cs`
- Create: `tests/SharpVision.Tests/Input/RoutingTests.cs`
- Create: `tests/SharpVision.Tests/Support/RecordingControl.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `docs/concepts/input-routing.md`

- [ ] **Step 1: Write failing route and mutation tests**

  Test preview root-to-target, bubble target-to-root, `OriginalSource`, current
  `Source`, phase, handled suppression, handled-events-too, handler removal,
  duplicate registration, payload type safety, and exceptions. During preview,
  detach/reparent the target and prove the current route remains the original
  snapshot while the next route observes the new tree.

  ```csharp
  target.AddHandler(Events.Key, handler);
  Router.Route(target, Events.Key, new KeyEventArgs(stroke));
  order.ShouldBe(["root-preview", "target-preview", "target", "root"]);
  ```

- [ ] **Step 2: Run routing tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*RoutingTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because routed events do not exist.

- [ ] **Step 3: Implement typed identifiers and snapshot dispatch**

  `Event<TArgs>` is an immutable typed identifier with name and routing
  strategy. `Control.AddHandler` validates event/handler, dispatcher access, and
  duplicate identity, and returns an idempotent registration. `RoutedEventArgs`
  exposes immutable original source, mutable controlled source, phase, handled,
  and terminal payload through sealed key/text/pointer/paste/focus subclasses.

  `Router.Route` snapshots ancestors into pooled or stack-backed temporary
  storage, invokes preview then bubble without locks, skips ordinary handlers
  after handled, invokes opt-in handlers, and returns storage with clearing.
  Tree mutation affects later routes only. Default control behavior runs after
  bubble only when unhandled.

- [ ] **Step 4: Verify route behavior and leaks**

  Run Step 2, then dispose registrations and detach the tree; weak references to
  handlers and controls must become collectible after forced test GC.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Input src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Input tests/SharpVision.Tests/Support/RecordingControl.cs docs/concepts/input-routing.md
  git commit -m "feat: add routed UI events"
  ```

## Task 7: Implement focus, hit testing, and pointer capture

**Files:**

- Create: `src/SharpVision/Input/FocusManager.cs`
- Create: `src/SharpVision/Input/CaptureManager.cs`
- Create: `tests/SharpVision.Tests/Input/FocusTests.cs`
- Create: `tests/SharpVision.Tests/Input/PointerTests.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `docs/concepts/focus.md`
- Modify: `docs/concepts/input-routing.md`

- [ ] **Step 1: Write failing focus and pointer tests**

  Focus tests cover eligibility, explicit focus, cancellable preview, manager
  update before lost/gained callbacks, tab/shift-tab order by tab index then
  tree order, wrap, hidden/disabled/detached cleanup, and mutation during
  notification. Pointer tests cover reverse-z hit testing, clipping, local
  coordinates, disabled exclusion, capture precedence, explicit release, detach
  release, terminal-focus loss, hover transitions, and pressed cancellation.

- [ ] **Step 2: Run focused input tests and verify RED**

  Run the `*FocusTests` and `*PointerTests` classes. Expected: compile failure
  for both managers.

- [ ] **Step 3: Implement transactional managers**

  `FocusManager.Focus(Control?)` verifies dispatcher access and membership,
  snapshots old/new routes, raises cancellable preview, commits `Focused` before
  lost/gained events, and invalidates only affected controls. `MoveNext`
  enumerates the attached visible enabled focusable tree deterministically.

  `Control.HitTest(Point)` rejects outside committed clip/visibility and
  searches container children from highest z-order to lowest before returning
  itself. `CaptureManager` owns at most one attached enabled control; capture
  routes all pointer values there until release. Detach, collapse, disable,
  terminal focus loss, or disposal releases capture, clears hover/pressed state,
  and emits one cancellation event where required.

- [ ] **Step 4: Verify manager cleanup**

  Run both suites plus tree tests. Expected: no manager references detached
  controls and every state invalidation occurs on the dispatcher.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Input src/SharpVision/Controls tests/SharpVision.Tests/Input docs/concepts/focus.md docs/concepts/input-routing.md
  git commit -m "feat: add focus and pointer targeting"
  ```

## Task 8: Add reactive visual-state styling

**Files:**

- Create: `src/SharpVision/Styling/State.cs`
- Create: `src/SharpVision/Styling/Appearance.cs`
- Create: `src/SharpVision/Styling/Style.cs`
- Create: `src/SharpVision/Styling/Resolver.cs`
- Create: `tests/SharpVision.Tests/Styling/StyleTests.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `docs/concepts/styling.md`
- Modify: `docs/controls/control.md`

- [ ] **Step 1: Write failing style precedence tests**

  Cover normal, hovered, focused, checked, pressed, disabled, every pairwise
  combination, unset versus explicit terminal default, direct versus inherited
  style, resource replacement, change subscription cleanup, render-only color
  invalidation, measure-affecting padding invalidation, and disabled appearance
  not changing enabled behavior.

  ```csharp
  var style = new Style();
  style.Set(State.Normal, new Appearance(foreground: Color.Indexed(2)));
  style.Set(State.Focused, new Appearance(attributes: Attributes.Underline));
  style.Set(State.Disabled, new Appearance(foreground: Color.Indexed(8)));
  Resolver.Resolve(style, State.Focused | State.Disabled).Foreground
      .ShouldBe(Color.Indexed(8));
  ```

- [ ] **Step 2: Run style tests and verify RED**

  Run `*StyleTests`. Expected: compile failure because `SharpVision.Styling`
  does not exist.

- [ ] **Step 3: Implement mutable resources and deterministic overlays**

  `State` is flags with normal represented by zero. `Appearance` has optional
  foreground/background, terminal text attributes, padding, and border values;
  optional fields distinguish unset from explicit default. `Style.Set` validates
  one state key and raises a change record naming measure versus render impact.
  `Resolver` overlays normal, hovered, focused, checked, pressed, then disabled,
  so the documented conflict precedence is disabled > pressed > checked >
  focused > hovered > normal while independent fields combine.

  `Control.Style` subscribes weakly or explicitly unsubscribes on replacement,
  detach, and disposal. A missing direct style inherits the nearest ancestor
  style. Effective state is derived from behavior flags; appearance never
  mutates behavior.

- [ ] **Step 4: Verify styling through terminal cells**

  Extend `ProbeControl` to draw one Rune using its resolved terminal style.
  Render combined states into a `Frame` and assert exact `CellInfo` colors and
  attributes, not a private resolver call alone.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Styling src/SharpVision/Controls/Control.cs tests/SharpVision.Tests/Styling tests/SharpVision.Tests/Support/ProbeControl.cs docs/concepts/styling.md docs/controls/control.md
  git commit -m "feat: add reactive control styling"
  ```

## Task 9: Connect controls to grapheme-safe canvas rendering

**Files:**

- Create: `tests/SharpVision.Tests/Controls/RenderingTests.cs`
- Create: `tests/SharpVision.Tests/Support/FrameOracle.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/testing/controls-integration.md`

- [ ] **Step 1: Write failing control-render tests**

  Test root and nested clips, hidden versus collapsed rendering, z-order,
  padding, Unicode combining/emoji/wide clusters, child overwrite, zero/tiny
  bounds, state styles, and invalidation coalescing. A test control calls only
  `Canvas.Draw`; assert final `Frame` cells, lead/continuation ownership,
  styles, and cursor state.

- [ ] **Step 2: Run rendering tests and verify RED**

  Run `*RenderingTests`. Expected: failure because controls have no render
  phase.

- [ ] **Step 3: Implement clipped semantic rendering**

  Add internal `Render(Canvas)` and protected `RenderCore(Canvas)` to `Control`.
  The entry point verifies dispatcher access and committed layout, skips hidden
  and collapsed controls, creates a child canvas from the intersection of parent
  clip and `Bounds`, and clears render invalidation only after success.
  `Container` renders itself, then children in collection order; later children
  are higher z-order. A render invalidation raised during rendering remains set
  for the next frame.

- [ ] **Step 4: Verify cell and UI tests**

  Run control rendering plus terminal canvas/randomized rendering tests.
  Expected: both layers agree on wide-cell repair and clipping.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Controls tests/SharpVision.Tests/Controls tests/SharpVision.Tests/Support/FrameOracle.cs docs/architecture/rendering-pipeline.md docs/testing/controls-integration.md
  git commit -m "feat: render controls to semantic frames"
  ```

## Task 10: Add application lifecycle, resize, input, and idle

**Files:**

- Create: `src/SharpVision/Runtime/Events.cs`
- Create: `src/SharpVision/Runtime/Application.cs`
- Create: `tests/SharpVision.Tests/Runtime/ApplicationTests.cs`
- Create: `tests/SharpVision.Tests/Runtime/OrderingTests.cs`
- Create: `tests/SharpVision.Tests/Support/FakeTerminal.cs`
- Modify: `docs/architecture/runtime-event-loop.md`
- Modify: `docs/concepts/lifecycle-events.md`
- Modify: `docs/concepts/threading.md`

- [ ] **Step 1: Write failing lifecycle and ordering tests**

  Use a deterministic fake terminal boundary to prove starting precedes terminal
  mode startup; started follows first resize, committed layout, and completed
  frame; input targets focus/capture/hit test; resize storms deliver only newest
  dimensions after layout but before frame; `FrameRendered` follows flush;
  `Idle` follows input/timers/layout/render and fires once before wait; work
  posted by idle drains immediately; close/fault/cancellation raises stopping
  once, restores terminal modes, then stopped. Handler exceptions preserve
  identity and cleanup diagnostics remain secondary.

  ```csharp
  events.ShouldBe([
      "starting", "layout:120x40", "resize:120x40",
      "frame", "started", "idle",
  ]);
  ```

- [ ] **Step 2: Run runtime tests and verify RED**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*ApplicationTests" "*OrderingTests" --minimum-expected-tests 1 --timeout 60s
  ```

  Expected: compile failure because `Application` is absent.

- [ ] **Step 3: Implement the terminal-to-dispatcher host**

  `Application` owns `Dispatcher`, root `Control`, terminal `Session`,
  `Rendering.Renderer`, focus/capture/router, and the active frame. It
  implements terminal `ISink` only by copying immutable records into a bounded
  queue; resize uses one atomic newest-value slot and one queued wake.
  Dispatcher drain order is posted work, input, resize/timers, layout, render
  completion/start, callbacks, then idle.

  First resize attaches the root, runs `Engine`, raises committed `Resize`,
  renders a frame, then raises `Started`. Rendering holds a dispatcher pending
  lease and back frame until the renderer's write/flush completes; completion is
  posted back before `FrameRendered`, and invalidations accumulated in flight
  schedule the next frame. Zero-cell sizes commit suspended layout and no frame.
  Stop is idempotent, rejects new work, releases focus/capture, cancels session,
  awaits reverse mode cleanup, disposes owned frames/resources, and preserves
  the primary exception.

- [ ] **Step 4: Verify no-spin lifecycle**

  Run Step 2 with a fake waiter that counts blocks and wakes. Assert an
  unchanged idle application blocks exactly once, a queued tick is distinct from
  idle, and no loop iteration occurs without work or a wake.

- [ ] **Step 5: Commit**

  ```bash
  git add src/SharpVision/Runtime tests/SharpVision.Tests/Runtime tests/SharpVision.Tests/Support/FakeTerminal.cs docs/architecture/runtime-event-loop.md docs/concepts/lifecycle-events.md docs/concepts/threading.md
  git commit -m "feat: add UI application runtime"
  ```

## Task 11: Add cross-layer and randomized guarantees

**Files:**

- Create: `tests/SharpVision.Tests/Integration/TerminalInputTests.cs`
- Create: `tests/SharpVision.Tests/Integration/ResizeRenderTests.cs`
- Create: `tests/SharpVision.Tests/Layout/RandomizedLayoutTests.cs`
- Create:
  `tests/SharpVision.Tests/Performance/InfrastructurePerformanceTests.cs`
- Modify: `docs/testing/controls-integration.md`
- Modify: `docs/testing/correctness-model.md`
- Modify: `docs/testing/performance.md`

- [ ] **Step 1: Prove raw terminal input through final cells**

  Feed UTF-8, legacy/Kitty keys, focus, cell/pixel mouse, paste, and resize
  bytes through the real terminal `Decoder`, `Runtime.Session`, UI
  `Application`, routed controls, layout, and `Renderer` into a recording
  transport. Apply output to the independent virtual screen and assert
  focused/captured control state plus final cells. Do not replace parser,
  router, layout, or encoder with mocks.

- [ ] **Step 2: Add fixed-seed hostile layout mutation**

  Seed `0x51A4_7001` generates attach/detach, valid property changes,
  visibility, resize, focus, capture, and invalidation between 0×0 and 240×80.
  After each drain, assert one parent, dispatcher consistency, non-negative
  contained bounds, valid focus/capture, no half-wide cells, and
  incremental/full screen equivalence. Failure reports seed, case, operation,
  tree, bounds, and frame.

- [ ] **Step 3: Add allocation and throughput records**

  Warm and measure dispatcher post/drain, unchanged layout, sparse invalidation,
  80×24 render, routing depth 20, and resize coalescing. Gate deterministic
  allocation budgets; record runtime/OS/architecture and timing without making
  wall-clock time a local pass criterion.

- [ ] **Step 4: Run the Phase 4 focused suite**

  ```bash
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --minimum-expected-tests 1 --timeout 120s
  dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --minimum-expected-tests 1 --timeout 120s
  make lint
  ```

  Expected: every UI and terminal test passes; docs, links, analyzers, and XML
  documentation remain clean.

- [ ] **Step 5: Commit**

  ```bash
  git add tests/SharpVision.Tests docs/testing
  git commit -m "test: prove UI infrastructure end to end"
  ```

## Task 12: Publish and verify Phase 4

**Files:**

- Modify:
  `docs/architecture/{project-structure,runtime-event-loop,rendering-pipeline,memory-ownership,error-handling}.md`
- Modify:
  `docs/concepts/{threading,layout,styling,focus,input-routing,lifecycle-events}.md`
- Modify: `docs/controls/control.md`
- Modify: `docs/testing/{controls-integration,correctness-model,performance}.md`
- Modify: `docs/superpowers/plans/2026-07-11-phase-4-ui-infrastructure.md`

- [ ] **Step 1: Audit exact API and ownership documentation**

  Name every public type and default; units, validation, affinity, ownership,
  invalidation, phase ordering, event routing, focus/capture cleanup, resize,
  idle, rendering, safe degradation, and disposal. Link rules at their owning
  sections rather than duplicating them. Keep automatic scrolling and concrete
  standard controls assigned to Phase 5.

- [ ] **Step 2: Run formatting and generated checks**

  ```bash
  make format
  npm run check:unicode
  make lint
  ```

  Expected: no unintended diff; Unicode output current; analyzers, Prettier,
  Markdownlint, section links, skill links, and docs tests pass.

- [ ] **Step 3: Run build and every test assembly**

  ```bash
  make build
  make test
  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --configuration Release --no-build --minimum-expected-tests 1 --timeout 120s
  ```

  Expected: all six projects build with 0 warnings/errors; all discovered tests
  pass and the UI assembly has a non-zero count.

- [ ] **Step 4: Audit repository and placeholders**

  ```bash
  git diff --check
  git status --short
  rg -n "TODO|TBD|NotImplementedException" src tests docs scripts
  dotnet build SharpVision.slnx --configuration Release --no-restore
  ```

  Expected: only the intended final plan update remains, placeholder matches are
  limited to literal audit commands in historical plans, and Release build has 0
  warnings/errors.

- [ ] **Step 5: Commit the verified phase**

  ```bash
  git add docs/superpowers/plans/2026-07-11-phase-4-ui-infrastructure.md
  git commit -m "chore: complete UI infrastructure"
  ```

## Self-review record

- **Approved-design coverage:** Tasks map the approved traditional-control,
  dispatcher, layout, style, focus, capture, routed input, resize, lifecycle,
  idle, and terminal-canvas requirements. Concrete controls, panels, automatic
  scrolling, windows, menus, popups, and showcase pages remain Phase 5/6 work.
- **Ordering consistency:** Terminal session callbacks enqueue immutable values;
  only the dispatcher mutates UI state. Resize layout precedes resize callback,
  render completion precedes frame callback, and pending render suppresses idle.
- **Layout consistency:** `Length.Percent` uses 0–100, star requires positive
  weight, unbounded percent/star are intrinsic, and bounded cumulative edges
  allocate the exact final cell count.
- **Type consistency:** `Dispatcher`, `Control`, `Container`, `Children`,
  `Length`, `Constraint`, `Tracks`, `Event<TArgs>`, `Router`, `FocusManager`,
  `CaptureManager`, `Style`, `Resolver`, and `Application` are the canonical
  names used throughout. Names rely on their namespaces and do not repeat
  `SharpVision`, `Terminal`, or `Control` affixes.
- **Proof quality:** Unit, randomized, allocation, integration, and final
  virtual-screen tests exercise observable public state and bytes. No plan task
  relies on private call-graph assertions or a mock-only pipeline.
