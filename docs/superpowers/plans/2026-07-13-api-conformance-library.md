# API Conformance (Library) Implementation Plan

<!-- markdownlint-disable MD013 -->

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make SharpVision's control API match .NET UI expectations — rename the
layout/render seams to the WPF names and add a first-class `View`/`Build()`
composition base that `Screen` also uses.

**Architecture:** Two behavior-preserving library changes. (1) A mechanical
rename of the three NVI seams `MeasureCore`/`ArrangeCore`/`RenderCore` →
`MeasureOverride`/`ArrangeOverride`/`OnRender`; the public non-virtual
`Measure`/`Arrange`/`Render` wrappers are unchanged. (2) A new
`View : Container` (capacity 1) with `protected abstract Control Build()` whose
result the runtime installs as the view's single child, exactly once, on the
first measure after attach; `Screen : View` inherits the hook and gains the
lifecycle order `OnAttach → Build → first frame → OnStarted`.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly. Test harness:
`Dispatcher.Start()` + `Control.Attach(dispatcher)` for direct control tests;
`Application` + `FakeTerminal` for screen/runtime tests.

**Scope note:** This plan covers the **library** only. The showcase rewrite
(each pane a `View` with `Build()`, deleting the 22 `GlobalUsings` aliases,
collapsing helpers) and re-sealing `Stack` are a **separate follow-up plan**,
authored after this one lands so pane code can be written against the real new
API. `Stack` therefore stays unsealed here (its only subclass, `ShowcasePane`,
still needs it). Full design:
`docs/superpowers/specs/2026-07-13-api-conformance-view-build-design.md`.

## Global Constraints

- Target .NET 10 and C# 14. File-scoped namespaces; `var` for locals; `using`
  directives after the `namespace`.
- One public/named type per file, named exactly after the type (`View` →
  `View.cs`). No nested named types, no two types per file.
- No primary constructors, no positional records. Declare constructors
  explicitly; validate arguments before assigning state.
- XML documentation on every public and internal type and member; document every
  thrown exception. Do not restate the signature.
- Validate every public argument before changing observable state; use
  `Debug.Assert` only for post-validation invariants.
- Controls are traditional mutable objects: no virtual trees, reconciliation, or
  hook-style state. `Build()` is one-shot construction, not reactive rendering.
- Tests: xUnit v3 + Shouldly, Arrange/Act/Assert, named
  `MethodName_WhenThis_ThatIsExpected`. Watch each new test fail for the
  expected reason first.
- Quality gate before every commit: `make format && make lint && make build`,
  plus the task's focused tests. Zero warnings, zero errors.
- Focused test command form:
  `dotnet test --project tests/SharpVision.Tests --filter-class "*ClassName" --timeout 120s`.

---

### Task 0: Establish a green baseline (precondition gate)

The working tree currently fails `make build` in `SharpVision.Terminal.Tests`
with 9 `CS0104` ambiguity errors (unrelated `runtime-protocol-router` work):
`Metrics` (ambiguous between `SharpVision.Terminal.Geometry.Metrics` and
`SharpVision.Terminal.Rendering.Metrics`, in
`tests/SharpVision.Terminal.Tests/GeometryCases/MetricsTests.cs`) and `Encoder`
(ambiguous between `SharpVision.Terminal.Rendering.Encoder` and
`System.Text.Encoder`, in
`tests/SharpVision.Terminal.Tests/Rendering/EquivalenceTests.cs` and
`RandomizedRenderingTests.cs`). These are **pre-existing, out-of-scope** for
this plan and belong to the branch owner.

**Files:**

- Possibly modify:
  `tests/SharpVision.Terminal.Tests/GeometryCases/MetricsTests.cs`,
  `tests/SharpVision.Terminal.Tests/Rendering/EquivalenceTests.cs`,
  `tests/SharpVision.Terminal.Tests/Rendering/RandomizedRenderingTests.cs`

- [ ] **Step 1: Confirm current state**

Run: `dotnet build SharpVision.slnx -clp:ErrorsOnly -nologo` Expected:
`Build FAILED` with the `CS0104` errors above, OR `Build succeeded` if the owner
already fixed them.

- [ ] **Step 2: If still failing, disambiguate (confirm intended type with the
      owner first)**

Add a file-scoped alias after the `namespace` line of each affected file so the
intended type wins:

```csharp
// MetricsTests.cs (GeometryCases → the Geometry type)
using Metrics = SharpVision.Terminal.Geometry.Metrics;

// EquivalenceTests.cs and RandomizedRenderingTests.cs (Rendering tests → the Rendering type)
using Encoder = SharpVision.Terminal.Rendering.Encoder;
```

- [ ] **Step 3: Verify green baseline**

Run: `make build && make test` Expected: build succeeds with zero
warnings/errors; the full suite passes at or above the configured minimum. Do
not start Task 1 until this holds.

---

### Task 1: Rename layout/render seams to WPF names

Pure, behavior-preserving rename across the base, every control, the three
test-support subclasses, and five docs. `MeasureCore`→`MeasureOverride`,
`ArrangeCore`→`ArrangeOverride`, `RenderCore`→`OnRender`. The public wrappers
`Measure`/`Arrange`/`Render` and their internal call sites
(`src/SharpVision/Controls/Control.cs:486`, `:580`, `:625`) keep their logic;
only the invoked method name changes.

**Files:**

- Modify: `src/SharpVision/Controls/Control.cs` (declarations near `:888`,
  `:896`, `:931`; call sites `:486`, `:580`, `:625`) and every control that
  overrides a seam (`Border`, `Button`, `Canvas`, `CheckBox`, `ComboBox`,
  `Dock`, `FigletText`, `Grid`, `List`, `ListItem`, `Menu`, `MenuItem`,
  `Overlay`, `Popup`, `RadioButton`, `RichText`, `ScrollBar`, `ScrollView`,
  `Screen`, `Shadow`, `Stack`, `Table`, `Text`, `TextInput`, `Window`)
- Modify (tests): `tests/SharpVision.Tests/Support/ChromeProbe.cs`,
  `DemoPanel.cs`, `ProbeControl.cs`
- Modify (docs): `docs/controls/control.md`,
  `docs/architecture/rendering-pipeline.md`,
  `docs/concepts/theming-new-controls.md`, `docs/concepts/layout.md`,
  `docs/concepts/styling.md`
- Test: `tests/SharpVision.Tests/Controls/OverrideSeamTests.cs` (create)

**Interfaces:**

- Produces:
  `protected virtual Size Control.MeasureOverride(Constraint constraint)`,
  `protected virtual void Control.ArrangeOverride(Rect bounds)`,
  `protected virtual void Control.OnRender(TerminalCanvas canvas)`.

- [ ] **Step 1: Write the failing guard test**

Create `tests/SharpVision.Tests/Controls/OverrideSeamTests.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>Verifies the WPF-named layout override seams are the extension points.</summary>
public sealed class OverrideSeamTests
{
    /// <summary>Verifies a control's MeasureOverride result flows into DesiredSize.</summary>
    [Fact]
    public void MeasureOverride_WhenControlReportsContent_DrivesDesiredSize()
    {
        FixedContent control = new();

        control.Measure(new Constraint(20, 6));

        control.DesiredSize.ShouldBe(new Size(7, 3));
    }

    private sealed class FixedContent: Control
    {
        protected override Size MeasureOverride(Constraint constraint) => new(7, 3);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*OverrideSeamTests" --timeout 120s`
Expected: FAIL — compile error
`'Control' does not contain a definition for 'MeasureOverride'` (the base still
declares `MeasureCore`).

- [ ] **Step 3: Apply the rename across code**

Run (macOS `sed`):

```bash
grep -rl 'MeasureCore\|ArrangeCore\|RenderCore' src tests --include='*.cs' \
  | grep -v '/obj/' \
  | xargs sed -i '' \
      -e 's/\bMeasureCore\b/MeasureOverride/g' \
      -e 's/\bArrangeCore\b/ArrangeOverride/g' \
      -e 's/\bRenderCore\b/OnRender/g'
```

- [ ] **Step 4: Run the guard test and the full suite to verify green**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*OverrideSeamTests" --timeout 120s`
Expected: PASS. Run: `make build` Expected: `Build succeeded`, zero warnings.

- [ ] **Step 5: Update the docs**

Run:

```bash
sed -i '' \
  -e 's/\bMeasureCore\b/MeasureOverride/g' \
  -e 's/\bArrangeCore\b/ArrangeOverride/g' \
  -e 's/\bRenderCore\b/OnRender/g' \
  docs/controls/control.md docs/architecture/rendering-pipeline.md \
  docs/concepts/theming-new-controls.md docs/concepts/layout.md docs/concepts/styling.md
```

Then read `docs/controls/control.md` (the "Layout extension points" and "Styling
extension point" sections) and fix any surrounding prose that names the old
seams, so wording stays accurate (e.g. "`OnRender`" reads correctly where it
previously said "`RenderCore`").

- [ ] **Step 6: Verify and commit**

Run: `make format && make lint && make build && make test` Expected: all green,
test count unchanged except +1 (the new guard test).

```bash
git add -A
git commit -m "refactor: rename layout/render seams to WPF names (MeasureOverride/ArrangeOverride/OnRender)"
```

---

### Task 2: Add the `View` composition base

New `public abstract class View : Container` with a capacity-one child produced
once by `Build()`. Mirrors the capacity-1 pattern of `Border` for layout, adds
lazy build-on-first-measure-after-attach.

**Files:**

- Create: `src/SharpVision/Controls/View.cs`
- Test: `tests/SharpVision.Tests/Controls/ViewTests.cs`

**Interfaces:**

- Consumes: `Control.Measure(Constraint)`, `Control.Attach(Dispatcher)`,
  `Children.SetOnly(Control?)`, `MeasureOverride`/`ArrangeOverride` (from Task
  1).
- Produces: `public abstract class View : Container`;
  `protected abstract Control Build()`; `protected Control? Content { get; }`.

- [ ] **Step 1: Write the failing tests**

Create `tests/SharpVision.Tests/Controls/ViewTests.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Threading;

/// <summary>Verifies View builds its content once, after attach, before first layout use.</summary>
public sealed class ViewTests
{
    /// <summary>Verifies Build runs once on the first measure after attach and installs its result.</summary>
    [Fact]
    public async Task Build_WhenViewMeasuredAfterAttach_RunsOnceAndInstallsContentAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeControl content = new() { Width = Length.Cells(5), Height = Length.Cells(2) };
            CountingView view = new(content);
            view.Attach(dispatcher);

            view.Measure(new Constraint(20, 6));
            view.Measure(new Constraint(20, 6));

            view.BuildCount.ShouldBe(1);
            view.Installed.ShouldBeSameAs(content);
            view.DesiredSize.ShouldBe(new Size(5, 2));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a detached, unmeasured view never builds.</summary>
    [Fact]
    public void Build_WhenViewIsDetached_IsNotCalled()
    {
        ProbeControl content = new();
        CountingView view = new(content);

        view.Measure(new Constraint(20, 6));

        view.BuildCount.ShouldBe(0);
        view.Installed.ShouldBeNull();
    }

    /// <summary>Verifies a null Build result is rejected.</summary>
    [Fact]
    public async Task Build_WhenResultIsNull_ThrowsInvalidOperationAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            NullView view = new();
            view.Attach(dispatcher);

            _ = Should.Throw<InvalidOperationException>(() => view.Measure(new Constraint(20, 6)));
        }, TestContext.Current.CancellationToken);
    }

    private sealed class CountingView: View
    {
        private readonly Control _content;

        internal CountingView(Control content) => _content = content;

        internal int BuildCount { get; private set; }

        internal Control? Installed => Content;

        protected override Control Build()
        {
            BuildCount++;
            return _content;
        }
    }

    private sealed class NullView: View
    {
        protected override Control Build() => null!;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ViewTests" --timeout 120s`
Expected: FAIL — compile error, `View` does not exist.

- [ ] **Step 3: Create the `View` class**

Create `src/SharpVision/Controls/View.cs`:

```csharp
// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>A composable control whose single content child is produced once by <see cref="Build"/>.</summary>
/// <remarks>
/// Derive from <see cref="View"/> to build a reusable component from existing controls. Implement
/// <see cref="Build"/> to return the content root; the runtime installs it as the view's only child
/// the first time the view is measured after attachment. This is one-shot construction, not reactive
/// rendering: after <see cref="Build"/> runs, mutate the returned subtree like any other control tree.
/// </remarks>
public abstract class View: Container
{
    private bool _built;

    /// <summary>Initializes an empty capacity-one composable view.</summary>
    protected View() : base(capacity: 1)
    {
    }

    /// <summary>Gets the built content child, or null before <see cref="Build"/> has run.</summary>
    protected Control? Content => Children.Count == 0 ? null : Children[0];

    /// <summary>Produces this view's content root. Called once, after attachment and before the first
    /// layout pass. Must return a non-null control; return a layout container for multiple children.</summary>
    /// <returns>The non-null content root installed as this view's only child.</returns>
    protected abstract Control Build();

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        EnsureBuilt();

        if (Content is not { } child)
        {
            return default;
        }

        child.Measure(constraint);
        return new Size(
            child.DesiredSize.Width + child.Margin.Horizontal,
            child.DesiredSize.Height + child.Margin.Vertical);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) =>
        Content?.Arrange(bounds, widthResolved: true, heightResolved: true);

    // Build lazily, only while attached, so detached trees stay inert and the content tree can read
    // attached context (dispatcher, and for Screen the running Application). Measure only runs on an
    // attached, laid-out tree, so this is the first point where building is both safe and meaningful.
    private void EnsureBuilt()
    {
        if (_built || Dispatcher is null)
        {
            return;
        }

        Control content = Build() ??
            throw new InvalidOperationException("View.Build must return a non-null control.");
        Children.SetOnly(content);
        _built = true;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ViewTests" --timeout 120s`
Expected: PASS (all three).

- [ ] **Step 5: Verify and commit**

Run: `make format && make lint && make build` Expected: green, zero warnings.

```bash
git add src/SharpVision/Controls/View.cs tests/SharpVision.Tests/Controls/ViewTests.cs
git commit -m "feat: add View composition base with one-shot Build() hook"
```

---

### Task 3: Make `Screen` derive from `View`; wire the lifecycle order

`Screen : View` so screens compose through `Build()`. Screen keeps its
`Application` binding and `OnAttach`/`OnStarted`/`OnDispose` hooks and drops its
own layout overrides (it inherits `View`'s single-child passthrough). Because
measure runs after `screen.Attach(application)` (which calls `OnAttach`), the
observable order becomes `OnAttach → Build → OnStarted`.

**Files:**

- Modify: `src/SharpVision/Controls/Screen.cs` (change base to `View`; remove
  the `MeasureOverride`/`ArrangeOverride` overrides — lines ~`:27-56` in current
  file)
- Test: `tests/SharpVision.Tests/Runtime/ScreenTests.cs` (extend)

**Interfaces:**

- Consumes: `View.Build()`, `View` layout (Task 2); `Application`,
  `FakeTerminal` test harness.
- Produces: `public abstract class Screen : View` with unchanged
  `OnAttach`/`OnStarted`/`OnDispose` and the documented lifecycle order.

- [ ] **Step 1: Write the failing test (lifecycle order includes Build)**

Add to `tests/SharpVision.Tests/Runtime/ScreenTests.cs`, and update the existing
`ProbeScreen` to implement `Build`:

```csharp
    /// <summary>Verifies Build runs after OnAttach and before OnStarted.</summary>
    [Fact]
    public async Task Build_WhenApplicationStarts_RunsAfterAttachAndBeforeStartedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        using ProbeScreen screen = new();
        await using Application application = new(
            screen,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        screen.Attach(application);

        await application.StartAsync(TestContext.Current.CancellationToken);

        screen.Order.ShouldBe(["attach", "build", "started"]);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ProbeScreen: SharpVision.Controls.Screen
    {
        internal ProbeScreen() => Order = [];

        internal List<string> Order { get; }

        protected override void OnAttach(Application application) => Order.Add("attach");

        protected override void OnStarted(Application application) => Order.Add("started");

        protected override Control Build()
        {
            Order.Add("build");
            return new ProbeControl();
        }
    }
```

Replace the file's existing `private sealed class ProbeScreen` with the version
above (it now also overrides `Build`), and add the necessary usings at the top:
`using SharpVision.Controls;` and `using SharpVision.Terminal.Geometry;` if not
already present. The existing
`Attach_WhenApplicationStarts_RunsHooksInOrderAsync` test keeps asserting
`["attach", "started"]` — leave it, since its `ProbeScreen` now also records
"build"; update that assertion to `["attach", "build", "started"]`.

- [ ] **Step 2: Run to verify it fails**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ScreenTests" --timeout 120s`
Expected: FAIL — `Screen` does not declare `Build` (it still derives from
`Container`), so the `override` does not compile.

- [ ] **Step 3: Change `Screen`'s base and remove its layout overrides**

In `src/SharpVision/Controls/Screen.cs`: change
`public abstract class Screen: Container` to
`public abstract class Screen: View`. Delete the `#region Layout` block (the
`MeasureOverride` and `ArrangeOverride` overrides) — `View` now provides
single-child layout. Keep the constructor (Stretch alignments), the
`Application` binding region, `OnAttach`/`OnStarted`/`OnDispose`, and the
`OnUnavailable` override. Remove now-unused `using SharpVision.Layout;` /
`using SharpVision.Terminal.Geometry;` if the compiler flags them.

- [ ] **Step 4: Run to verify pass**

Run:
`dotnet test --project tests/SharpVision.Tests --filter-class "*ScreenTests" --timeout 120s`
Expected: PASS.

- [ ] **Step 5: Verify build (Gallery will now fail — expected, fixed in
      Task 4)**

Run: `dotnet build src/SharpVision/SharpVision.csproj -clp:ErrorsOnly -nologo`
Expected: `Build succeeded` (the library itself compiles). Run:
`dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj -clp:ErrorsOnly -nologo`
Expected: FAIL — `Gallery` does not implement the inherited abstract `Build`.
Task 4 fixes this. Do not commit until Task 4 restores a green build.

---

### Task 4: Migrate `Gallery` to `Build()` (minimal, keeps showcase green)

Minimal change only: `Gallery` keeps building its tree in the constructor but
stores the root in a field and returns it from `Build()` instead of adding it to
`Children`. The full showcase rewrite is the follow-up plan.

**Files:**

- Modify: `src/SharpVision.Showcase/Gallery.cs`

**Interfaces:**

- Consumes: `Screen.Build()` (Task 3).

- [ ] **Step 1: Store the root and return it from Build**

In `src/SharpVision.Showcase/Gallery.cs`: add a field
`private readonly ControlDock _root;`. In the constructor, replace the final
`Children.Add(layout);` with `_root = layout;` (leave the rest of the
constructor — the `Select(0)` call and all field assignments — unchanged; the
detached `_root` subtree is assembled in the constructor and installed by `View`
at first measure). Add the override:

```csharp
    /// <inheritdoc/>
    protected override Control Build() => _root;
```

- [ ] **Step 2: Build the showcase**

Run:
`dotnet build src/SharpVision.Showcase/SharpVision.Showcase.csproj -clp:ErrorsOnly -nologo`
Expected: `Build succeeded`.

- [ ] **Step 3: Run the showcase tests; fix any ctor-time-Children assumption**

Run: `dotnet test --project tests/SharpVision.Showcase.Tests --timeout 180s`
Expected: PASS. If a test inspects `gallery.Children` synchronously before the
app starts, it will now see an empty collection (the root installs at first
measure). Update such assertions to drive the app (attach + `StartAsync`) or to
assert against `gallery.Sidebar`/`gallery.Content` (still set in the
constructor) instead. Re-run until green.

- [ ] **Step 4: Verify full solution and commit**

Run: `make format && make lint && make build && make test` Expected: all green.

```bash
git add src/SharpVision/Controls/Screen.cs tests/SharpVision.Tests/Runtime/ScreenTests.cs src/SharpVision.Showcase/Gallery.cs
git commit -m "feat: Screen derives from View with OnAttach->Build->OnStarted lifecycle"
```

---

### Task 5: Documentation and agent-guide sync

Document `View`/`Build()`, the `Screen` lifecycle order, and the seam names.

**Files:**

- Modify: `docs/concepts/screen.md` (add the `Build()` hook and lifecycle order)
- Modify: `docs/controls/control.md` (note `View` as the composition base in the
  layout/extension section)
- Create: `docs/concepts/custom-components.md` (when to compose via
  `View`/`Build()` vs. derive from an abstract base)
- Modify: `docs/concepts/index.md` (add a link to the new concept page)
- Modify: `AGENTS.md` (note `View`/`Build()` as the composition pattern and the
  `*Override`/`OnRender` seam names)

- [ ] **Step 1: Update `docs/concepts/screen.md`**

Document that a concrete screen overrides `protected override Control Build()`
to return its content root, that the runtime calls `Build()` once after
`OnAttach` and before the first frame, and that the lifecycle order is
`OnAttach → Build → first committed frame → OnStarted → OnDispose`. Update the
existing prose that says screens "build their UI ... and override `OnAttach` or
`OnStarted`" to reflect `Build()`.

- [ ] **Step 2: Create `docs/concepts/custom-components.md`**

Write a concept page: `View : Container` is the composition base; implement
`Build()` to return the content root; the runtime installs it once on first
attach; primitives are sealed by design, so compose with `View` (or derive from
`Container`/`Pressable` for a genuinely new primitive). Include a minimal
example:

```csharp
public sealed class LoginPanel : View
{
    protected override Control Build() =>
        new Stack
        {
            Spacing = 1,
            Children = { new Text("Sign in"), new TextInput(), new Button { Content = new Text("Go") } },
        };
}
```

- [ ] **Step 3: Link the new page and update `control.md`**

Add the `custom-components.md` link under `docs/concepts/index.md`'s concept
map. In `docs/controls/control.md`, add a short note in the layout-extension
section pointing to `View`/`Build()` as the way to build composite controls.

- [ ] **Step 4: Update `AGENTS.md`**

Under "UI correctness," add one line: the composition pattern is `View` +
`protected override Control Build()`; the layout/render override seams are
`MeasureOverride`/`ArrangeOverride`/`OnRender`.

- [ ] **Step 5: Verify docs and commit**

Run: `make lint` Expected: no Markdown or link failures.

```bash
git add docs/concepts/screen.md docs/concepts/custom-components.md docs/concepts/index.md docs/controls/control.md AGENTS.md
git commit -m "docs: document View/Build composition and WPF override seam names"
```

---

## Self-Review

**Spec coverage:**

- Decision 1 (`*Core` → WPF names) → Task 1. ✓
- Decision 2 (`View` base + `Build()`, primitives stay sealed) → Task 2. ✓
- Decision 6/7 (`View` name, `Build()` returns `Control`) → Task 2
  (`protected abstract Control Build()`). ✓
- `Screen : View` + lifecycle order → Task 3. ✓
- Keep the codebase green (only Screen subclass is `Gallery`) → Task 4. ✓
- Docs + `AGENTS.md` sync → Task 5. ✓
- Precondition (green baseline) → Task 0. ✓
- Decision 3 (`Stack` re-seal) and Decision 4/5 (aliases + full showcase
  rewrite) → **deferred to the follow-up plan** by explicit scope note. `Stack`
  stays unsealed here because `ShowcasePane` still subclasses it. ✓ (intentional
  gap, documented)

**Placeholder scan:** No TBD/TODO; every code step shows complete code; every
command shows expected output. ✓

**Type consistency:** `MeasureOverride(Constraint)`, `ArrangeOverride(Rect)`,
`OnRender(TerminalCanvas)` used identically in Tasks 1–3. `View` API (`Build()`
returning `Control`, protected `Content`) is consistent between Task 2's
implementation and Task 3's `ProbeScreen`/Task 4's `Gallery` usage.
`Children.SetOnly`, `Control.Attach(Dispatcher)`, `Dispatcher.InvokeAsync` match
the verified codebase signatures. ✓
