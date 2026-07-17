# Spinner and Chase Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship deterministic `Spinner` and `ChaseIndicator` display controls
driven by a reusable dispatcher timer, with complete docs, surface evidence, and
Showcase pages.

**Architecture:** `DispatcherTimer` owns a `TimeProvider` timer whose callback
only coalesces and posts work to its dispatcher. Each sealed control owns one
timer for its attachment lifetime and keeps its own animation phase; there is no
animation framework or shared control base. `Application` propagates one
optional clock to owned time-aware services so mounted tests advance animations
without sleeping.

**Tech Stack:** .NET 10, C# 14, `TimeProvider` and `ITimer`, xUnit v3, Shouldly,
Microsoft Testing Platform, SharpVision retained controls and semantic terminal
canvas.

---

## File map

- `src/SharpVision/Threading/DispatcherTimer.cs`: public coalescing periodic
  timer.
- `src/SharpVision/Threading/Dispatcher.cs`: dispatcher clock ownership.
- `src/SharpVision/Runtime/Application.cs`: optional application clock
  propagation.
- `tests/SharpVision.Tests/Input/ManualTimeProvider.cs` and new
  `ManualTimer.cs`: deterministic timer scheduling.
- `tests/SharpVision.Tests/Threading/DispatcherTimerTests.cs`: timer behavior
  proof.
- `tests/SharpVision.Tests/Support/ComponentSurface.cs`: deterministic mounted
  time advancement.
- `src/SharpVision/Controls/SpinnerPattern.cs` and `Spinner.cs`: one-cell
  spinner.
- `src/SharpVision/Controls/ChasePattern.cs` and `ChaseIndicator.cs`: bouncing
  track.
- Matching control and surface test files under
  `tests/SharpVision.Tests/Controls/`.
- Dedicated normative control docs under `docs/controls/display/`.
- Dedicated Showcase panes and tests for both controls.

### Task 1: Deterministic dispatcher timer

**Files:**

- Create: `src/SharpVision/Threading/DispatcherTimer.cs`
- Modify: `src/SharpVision/Threading/Dispatcher.cs`
- Modify: `tests/SharpVision.Tests/Input/ManualTimeProvider.cs`
- Create: `tests/SharpVision.Tests/Input/ManualTimer.cs`
- Create: `tests/SharpVision.Tests/Threading/DispatcherTimerTests.cs`

- [ ] **Step 1: Extend the manual provider with timer scheduling**

Override `CreateTimer`, register one separate `ManualTimer` named type, and make
`Advance` synchronously fire due timers in due-time and creation order until no
timer is due. `Change` accepts infinite or values through 2,147,483,647 ms.
Disposal removes the timer and is idempotent.

    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        Register(timer);
        _ = timer.Change(dueTime, period);
        return timer;
    }

- [ ] **Step 2: Write failing timer tests**

Create tests named:

    Constructor_WhenIntervalIsOutsideSupportedRange_RejectsBeforeConstruction
    Start_WhenOneIntervalElapses_RaisesTickOnDispatcher
    Stop_WhenTickWasPosted_SuppressesQueuedDelivery
    Interval_WhenChangedWhileRunning_RestartsCompleteCadence
    Advance_WhenDispatcherIsBlocked_CoalescesToOnePendingTick
    Dispose_WhenCalledRepeatedly_SuppressesFutureTicks
    Tick_WhenHandlerThrows_UsesDispatcherUnhandledPolicy

Use a 200 ms timer; prove 199 ms yields zero ticks and the final millisecond
raises exactly one tick with `dispatcher.CheckAccess()` true.

- [ ] **Step 3: Run the tests and verify the expected red state**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*DispatcherTimerTests" --timeout 60s

Expected: compilation fails because `DispatcherTimer` and clock-aware
`Dispatcher.Start` do not exist.

- [ ] **Step 4: Add dispatcher clock ownership**

Use this source-compatible signature:

    public static Dispatcher Start(
        int capacity = 4096,
        string? name = null,
        TimeProvider? timeProvider = null)

Resolve null to `TimeProvider.System`, retain it privately, and expose it only
internally for `DispatcherTimer`.

- [ ] **Step 5: Implement the public timer**

Implement:

    public sealed class DispatcherTimer: IDisposable
    {
        public DispatcherTimer(Dispatcher dispatcher, TimeSpan interval);
        public event EventHandler? Tick;
        public TimeSpan Interval { get; set; }
        public bool IsRunning { get; }
        public void Start();
        public void Stop();
        public void Dispose();
    }

The provider callback atomically coalesces one pending post. Queue-full periods
are dropped; shutdown stops posting. The posted callback verifies running state,
disposal, and generation before raising `Tick`. Start, stop, and interval
mutation verify dispatcher access. Dispose is thread-safe. Validate 1 through
2,147,483,647 ms before mutation.

- [ ] **Step 6: Run focused timer and dispatcher tests**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*DispatcherTimerTests|*DispatcherTests" --timeout 60s

Expected: all selected tests pass with no warnings.

- [ ] **Step 7: Commit**

  git add src/SharpVision/Threading/Dispatcher.cs \
  src/SharpVision/Threading/DispatcherTimer.cs \
  tests/SharpVision.Tests/Input/ManualTimeProvider.cs \
  tests/SharpVision.Tests/Input/ManualTimer.cs \
  tests/SharpVision.Tests/Threading/DispatcherTimerTests.cs git commit -m "feat:
  add deterministic dispatcher timer"

### Task 2: Application clock and mounted test support

**Files:**

- Modify: `src/SharpVision/Runtime/Application.cs`
- Modify: `tests/SharpVision.Tests/Runtime/ApplicationTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`

- [ ] **Step 1: Write failing application clock tests**

Construct `Application` with a final `ManualTimeProvider`, start it, create a
dispatcher timer, advance the clock, and assert its tick. Add a surface-support
test that advances 200 ms and waits for the resulting frame and idle transition.

- [ ] **Step 2: Run the tests and verify the expected red state**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ApplicationTests|*ComponentSurface*Tests" --timeout 60s

Expected: compilation fails because the clock parameters and advancement helper
do not exist.

- [ ] **Step 3: Propagate one application clock**

Add `TimeProvider? timeProvider = null` after `hostLease`, resolve it once, and
pass it to `Dispatcher.Start`, `Session`, `Renderer`, and the later
`PointerManager`. Move renderer construction into the constructor. Preserve all
existing ownership and disposal.

- [ ] **Step 4: Add deterministic surface advancement**

Allow `ComponentSurface.MountAsync` to receive an optional `ManualTimeProvider`.
Store it and add:

    internal async Task AdvanceAsync(TimeSpan value, string description)

Subscribe to `FrameRendered` and `Idle`, advance the clock, post a dispatcher
barrier, and wait for rendering to settle. Validate arguments and report the
latest modeled screen on timeout.

- [ ] **Step 5: Run focused tests and commit**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ApplicationTests|*ComponentSurface*Tests" --timeout 60s git
  add src/SharpVision/Runtime/Application.cs \
  tests/SharpVision.Tests/Runtime/ApplicationTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurface.cs git commit -m "feat:
  propagate application time provider"

### Task 3: Spinner control

**Files:**

- Create: `src/SharpVision/Controls/SpinnerPattern.cs`
- Create: `src/SharpVision/Controls/Spinner.cs`
- Create: `tests/SharpVision.Tests/Controls/SpinnerTests.cs`
- Create: `tests/SharpVision.Tests/Controls/SpinnerSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`

- [ ] **Step 1: Write failing tests**

Cover defaults, exact Braille/dense/ASCII frames, invalid patterns and
intervals, pattern reset, one-cell measure, zero bounds, 200 ms cadence,
pause/resume, hidden ancestor, detach/reattach, attached-root disposal,
dispatcher affinity, and interaction exclusions.

Mounted proof:

    var clock = new ManualTimeProvider();
    var spinner = new Spinner();
    await using var surface = await ComponentSurface.MountAsync(
        spinner, new Size(1, 1),
        TestContext.Current.CancellationToken, clock);
    surface.ShouldRender("⠋");
    await surface.AdvanceAsync(
        TimeSpan.FromMilliseconds(200), "advance Spinner");
    surface.ShouldRender("⠙");

- [ ] **Step 2: Run and verify red**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*SpinnerTests|*SpinnerSurfaceTests" --timeout 60s

Expected: compilation fails because the control and enum do not exist.

- [ ] **Step 3: Implement minimally**

Define `Braille`, `DenseBraille`, and `Ascii`. Store the approved Rune arrays.
Constructor defaults to left/top, non-focusable, and no hit testing. Implement
`Pattern`, `Interval`, and `IsPlaying` with validation before mutation.
`MeasureOverride` returns `(1,1)` and render draws one Rune.

`OnAttached` creates/subscribes/starts one timer. `OnDetached` and `OnDisposing`
idempotently stop, unsubscribe, and dispose it. Eligible ticks advance modulo
frame count only when effectively visible and playing, then invalidate render.

- [ ] **Step 4: Register passive surface evidence**

Add `Spinner` to `ComponentSurfaceCoverageTests` with mounted, hover excluded,
focus excluded, tab excluded, directional excluded, and press/release excluded
behavior backed by `SpinnerSurfaceTests`.

- [ ] **Step 5: Run focused tests and commit**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*Spinner*Tests|*ComponentSurfaceCoverageTests|*TreeTests" \
  --timeout 60s git add src/SharpVision/Controls/Spinner.cs \
  src/SharpVision/Controls/SpinnerPattern.cs \
  tests/SharpVision.Tests/Controls/SpinnerTests.cs \
  tests/SharpVision.Tests/Controls/SpinnerSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs git commit -m
  "feat: add Spinner control"

### Task 4: ChaseIndicator control

**Files:**

- Create: `src/SharpVision/Controls/ChasePattern.cs`
- Create: `src/SharpVision/Controls/ChaseIndicator.cs`
- Create: `tests/SharpVision.Tests/Controls/ChaseIndicatorTests.cs`
- Create: `tests/SharpVision.Tests/Controls/ChaseIndicatorSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`

- [ ] **Step 1: Write failing tests**

Cover defaults; all seven exact Unicode and ASCII fallback pairs; length five
positions `0,1,2,3,4,3,2,1,0`; length two `0,1,0`; validation; reset semantics;
timer lifecycle; visibility; desired size; clipping; narrow/wide policies; and
interaction exclusions. Mounted tests compare consecutive semantic screens.

- [ ] **Step 2: Run and verify red**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ChaseIndicatorTests|*ChaseIndicatorSurfaceTests" \
  --timeout 60s

Expected: compilation fails because the control and enum do not exist.

- [ ] **Step 3: Implement minimally**

Define `Circle`, `Diamond`, `Square`, `Up`, `Down`, `Left`, and `Right`. Store
the approved primary/fallback pairs as `ThemedGlyph` data and resolve with
`CellGlyph.Resolve`. Implement `Length >= 2`, `Pattern`, `Interval`, and
`IsPlaying`.

Keep `_position` and `_direction`. Reverse before crossing an endpoint. Pattern
and length changes reset to position zero and forward. Measure `(Length,1)`;
render the visible cells through the clipped canvas. Use the same timer
lifecycle as Spinner without a shared base class.

- [ ] **Step 4: Register evidence, run tests, and commit**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ChaseIndicator*Tests|*AmbiguousWidthControlTests|*ComponentSurfaceCoverageTests"
  \
  --timeout 60s git add src/SharpVision/Controls/ChaseIndicator.cs \
  src/SharpVision/Controls/ChasePattern.cs \
  tests/SharpVision.Tests/Controls/ChaseIndicatorTests.cs \
  tests/SharpVision.Tests/Controls/ChaseIndicatorSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs git commit -m
  "feat: add ChaseIndicator control"

### Task 5: Normative documentation

**Files:**

- Create: `docs/controls/display/spinner.md`
- Create: `docs/controls/display/chase-indicator.md`
- Modify: `docs/controls/index.md`
- Modify: `docs/concepts/threading.md`
- Modify: `docs/concepts/lifecycle-events.md`
- Modify: `docs/testing/controls-integration.md`
- Modify:
  `docs/superpowers/specs/2026-07-17-spinner-and-chase-indicator-design.md`

- [ ] **Step 1: Write control contracts**

Document purpose, inheritance, properties/defaults, validation/exceptions, exact
glyph tables, layout/rendering, wide-policy fallback, lifecycle, visibility,
examples, and test obligations. State disabled behavior and the fixed built-in
scope.

- [ ] **Step 2: Update shared contracts**

Replace “no timer API” with exact timer ordering, coalescing, bounds, exception,
shutdown, disposal, and no-idle-polling rules. Add deterministic consecutive
screen proof to control testing and both controls to the display catalog.

- [ ] **Step 3: Validate and commit docs**

  ./node_modules/.bin/prettier --write \
  docs/controls/display/spinner.md \
  docs/controls/display/chase-indicator.md \
  docs/controls/index.md docs/concepts/threading.md \
  docs/concepts/lifecycle-events.md docs/testing/controls-integration.md \
  docs/superpowers/specs/2026-07-17-spinner-and-chase-indicator-design.md npm
  run lint:markdown npm run lint:links npm run test:docs git add
  docs/controls/display/spinner.md \
  docs/controls/display/chase-indicator.md docs/controls/index.md \
  docs/concepts/threading.md docs/concepts/lifecycle-events.md \
  docs/testing/controls-integration.md \
  docs/superpowers/specs/2026-07-17-spinner-and-chase-indicator-design.md git
  commit -m "docs: specify animated indicators"

Expected: all documentation checks pass.

### Task 6: Showcase pages

**Files:**

- Create: `src/SharpVision.Showcase/Panes/SpinnerPane.cs`
- Create: `src/SharpVision.Showcase/Panes/ChaseIndicatorPane.cs`
- Create: `tests/SharpVision.Showcase.Tests/SpinnerPaneTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/ChaseIndicatorPaneTests.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`
- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Write failing inventory and page tests**

Require the exact 30-page catalog. Require Spinner examples for all patterns and
paused state. Require ChaseIndicator examples for all patterns, non-default
length, and paused state. Render pages and assert representative glyphs and
valid continuation ownership.

- [ ] **Step 2: Run and verify red**

  dotnet test --project
  tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests|*ShowcaseContentTests|*SpinnerPaneTests|*ChaseIndicatorPaneTests"
  \
  --timeout 60s

Expected: compilation and inventory failures because pages are absent.

- [ ] **Step 3: Implement pages and registration**

Build retained pages with `Doc.Page`, `Doc.Section`, `Doc.Example`, and
`Doc.Card`, using public APIs only. Keep running specimens live and paused
specimens `IsPlaying = false`. Register `ChaseIndicator` after Canvas and
`Spinner` after Slider. Update exact content maps and inventory prose from 28
to 30.

- [ ] **Step 4: Run focused tests and commit**

  dotnet test --project
  tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests|*ShowcaseContentTests|*SpinnerPaneTests|*ChaseIndicatorPaneTests"
  \
  --timeout 60s git add src/SharpVision.Showcase/Panes/SpinnerPane.cs \
  src/SharpVision.Showcase/Panes/ChaseIndicatorPane.cs \
  src/SharpVision.Showcase/Gallery.cs \
  tests/SharpVision.Showcase.Tests/SpinnerPaneTests.cs \
  tests/SharpVision.Showcase.Tests/ChaseIndicatorPaneTests.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs \
  docs/architecture/showcase.md docs/testing/showcase.md git commit -m "feat:
  showcase animated indicators"

### Task 7: Full verification

- [ ] **Step 1: Run focused suites**

  dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*DispatcherTimerTests|*Spinner*Tests|*ChaseIndicator*Tests|*ApplicationTests|*ComponentSurfaceCoverageTests"
  \
  --timeout 60s dotnet test --project
  tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*SpinnerPaneTests|*ChaseIndicatorPaneTests|*GalleryTests|*ShowcaseContentTests"
  \
  --timeout 60s

Expected: all selected tests pass without warnings.

- [ ] **Step 2: Run repository gates**

  make format make lint make build make test

Expected: zero formatting drift, lint failures, build warnings/errors, or test
failures, with discovered tests at or above the configured minimum.

- [ ] **Step 3: Inspect the intentional diff**

  git status --short git diff --check

Every changed file must belong to the timer, two controls, tests, docs, plan, or
Showcase scope. Preserve unrelated user work.
