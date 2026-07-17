# Complete Component Behavior Coverage Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every exported concrete SharpVision control mounted behavioral
evidence for its applicable hover, focus, Tab, arrow, press/release, activation,
transient, and composition contracts.

**Architecture:** Keep `ComponentSurface` as the real `Application` boundary and
add an executable capability/evidence catalog over focused `*SurfaceTests`
fixtures. Reconcile the architecture merge without restoring the retired style
engine or public presentation parts, then add continuous mixed-root and
deep-ownership journeys that exercise real terminal bytes through layout,
routing, focus, capture, rendering, and the semantic screen.

**Tech Stack:** .NET 10, C# 14, xUnit v3, Shouldly, Microsoft Testing Platform
filters, SharpVision `Application`, deterministic component terminal/screen
fakes, Markdown/Prettier tooling.

---

## File structure

New test infrastructure has one responsibility per file:

- `tests/SharpVision.Tests/Support/ComponentBehavior.cs` defines evidence flags.
- `tests/SharpVision.Tests/Support/ComponentBehaviorEvidenceAttribute.cs`
  attaches evidence to test methods.
- `tests/SharpVision.Tests/Support/ComponentBehaviorRequirement.cs` stores one
  immutable catalog requirement.
- `tests/SharpVision.Tests/Controls/PrismSurfaceTests.cs` proves the missing
  effect control.
- `tests/SharpVision.Tests/Controls/TabBehaviorSurfaceTests.cs` replaces the
  intentionally deleted presentation-part fixture without resurrecting it.
- `tests/SharpVision.Tests/Controls/ComboBoxSurfaceTests.cs`,
  `MenuSurfaceTests.cs`, `PopupSurfaceTests.cs`, and `WindowSurfaceTests.cs`
  remove the transient-family deferrals.
- `tests/SharpVision.Tests/Integration/ComponentCompositionSurfaceTests.cs` owns
  sibling and deep-composition journeys.

Existing `ComponentSurface`, `ComponentKeyboard`, and `ComponentPointer` remain
the only component drivers. Existing per-control fixtures remain focused
evidence; they are migrated to `VisualState` and direct current APIs rather than
rewritten into a generic factory suite.

### Task 1: Reconcile normative contracts and restore a compiling test baseline

**Files:**

- Modify: `docs/controls/display/progress-bar.md`
- Modify: `docs/controls/display/separator.md`
- Modify: `docs/controls/layout/expander.md`
- Modify: `docs/controls/collections/tab-control.md`
- Modify: `tests/SharpVision.Tests/Controls/ProgressBarTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/SeparatorTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TabControlTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TabItemTests.cs`
- Modify: all existing `tests/SharpVision.Tests/Controls/*SurfaceTests.cs` files
  reported by the baseline build
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`

- [ ] **Step 1: Record the current compiler failure**

Run:

```bash
dotnet build tests/SharpVision.Tests/SharpVision.Tests.csproj --no-restore --nologo --verbosity quiet
```

Expected: FAIL with stale merged-test references including inaccessible `State`,
removed `FillMode`/`Style`/`ThemeTestSupport`, removed `HeaderPart`, removed
`SelectedItem`/`IsSelected`, retired glyph properties, and the deleted
`TabControlSurfaceTests` type.

- [ ] **Step 2: Update the four stale control contracts before changing their
      tests**

Make the docs agree with the approved architecture convergence:

```markdown
`ProgressBar` renders built-in full, track, and fractional block cells.
`UseSubCellResolution` selects eighth-cell resolution; callers style the result
through inherited appearance properties.

`Separator` renders the built-in `─` or `│` cell selected by `Orientation`. It
has no glyph customization surface.

`Expander` is the focusable semantic header owner. Its header is rendered
directly by the control and is not exposed as a public presentation child.

`TabControl` is one focusable header owner. `SelectedIndex` identifies the
selected page; `TabItem` exposes `Header` and `Content`, while presentation
headers remain private.
```

Remove claims for the retired `FillGlyph`, `TrackGlyph`, `IndeterminateGlyph`,
`HorizontalGlyph`, `VerticalGlyph`, `HeaderPart`, `SelectedItem`,
`HeaderOffset`, `IsSelected`, public insertion/replacement, and style-engine
types. Preserve the architecture design's required behavior: TabControl
wrapping/skipping, committed page participation, Expander header-only activation
bounds, deterministic progress rendering, and passive separator behavior.

- [ ] **Step 3: Migrate state assertions mechanically**

Replace every old state assertion with the public enum:

```csharp
surface.ShouldHaveState(control, VisualState.Normal);
surface.ShouldHaveState(
    control,
    VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
```

Replace `IsHovered` assertions with `IsPointerOver`. Remove `FillMode`; use
nullable `Background` only when the scenario needs an opaque fill. Remove
`Style` and `ThemeTestSupport` setup and assert the current direct appearance
properties or semantic cells instead.

- [ ] **Step 4: Replace retired presentation-part assertions with semantic-owner
      assertions**

Use direct controls and current selection identity:

```csharp
await surface.Pointer.ClickAsync(expander);
surface.ShouldHaveState(expander, VisualState.PointerOver | VisualState.Focused);

tabs.SelectedIndex.ShouldBe(1);
tabs.Items[tabs.SelectedIndex].ShouldBeSameAs(second);
first.Content.ShouldNotBeNull().Bounds.ShouldBe(default);
second.Content.ShouldNotBeNull().Bounds.ShouldBe(new Rect(0, 2, 20, 3));
```

Do not restore `HeaderPart`, `TabPresenter`, `SelectedItem`, `IsSelected`,
writable `TabItems` indexing, or removed style-engine types. Point the temporary
coverage map for `TabControl` and `TabItem` to `TabControlTests` until Task 6
creates mounted replacement evidence.

- [ ] **Step 5: Rewrite progress and separator unit expectations around current
      public APIs**

Use built-in exact-cell assertions:

```csharp
var progress = new ProgressBar
{
    Minimum = 0,
    Maximum = 8,
    Value = 3,
    UseSubCellResolution = false,
};
progress.Measure(new Constraint(8, 1));
progress.Arrange(new Rect(0, 0, 8, 1));
using var frame = new Frame(new Size(8, 1));
progress.Render(frame.Canvas);
string.Concat(
    Enumerable.Range(0, 8).Select(x => FrameOracle.Get(frame, new Point(x, 0))))
    .ShouldBe("███░░░░░");

var separator = new Separator { Orientation = Orientation.Vertical };
separator.Measure(new Constraint(1, 3));
separator.Arrange(new Rect(0, 0, 1, 3));
```

Retain validation tests for current properties and delete only assertions whose
public API was intentionally retired.

- [ ] **Step 6: Build until the test assembly compiles**

Run:

```bash
dotnet build tests/SharpVision.Tests/SharpVision.Tests.csproj --no-restore --nologo --verbosity quiet
```

Expected: PASS with zero warnings and zero errors. Do not require all tests to
pass yet; this task establishes a compiling post-merge baseline.

- [ ] **Step 7: Commit the baseline reconciliation**

```bash
git add \
  docs/controls/display/progress-bar.md \
  docs/controls/display/separator.md \
  docs/controls/layout/expander.md \
  docs/controls/collections/tab-control.md \
  tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/CheckBoxSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/DockSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ExpanderTests.cs \
  tests/SharpVision.Tests/Controls/GridSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/GroupBoxSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ListSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/OverlaySurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ProgressBarSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ProgressBarTests.cs \
  tests/SharpVision.Tests/Controls/RadioButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ScrollBarSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/SeparatorSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/SeparatorTests.cs \
  tests/SharpVision.Tests/Controls/StackSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TabControlTests.cs \
  tests/SharpVision.Tests/Controls/TabItemTests.cs \
  tests/SharpVision.Tests/Controls/TableSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TextInputSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TextSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs
git commit -m "test(controls): reconcile surface suite with current architecture"
```

Stage only files intentionally changed by this task; preserve the existing
user-owned Expander/showcase edits and deleted files.

### Task 2: Extend real-input harness behavior test-first

**Files:**

- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurface.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentKeyboard.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentPointer.cs`

- [ ] **Step 1: Add failing harness tests**

Add focused tests proving reverse traversal, separate keyboard held/released
state, pointer leave, and manager assertions:

```csharp
[Fact]
public async Task Keyboard_WhenShiftTabIsPressed_MovesFocusBackwardThroughMountedRootAsync()
{
    var first = new Button { Content = new ControlText("First") };
    var second = new Button { Content = new ControlText("Second") };
    var root = new Stack { Children = { first, second } };
    await using var surface = await ComponentSurface.MountAsync(
        root,
        new Size(16, 2),
        TestContext.Current.CancellationToken);

    await surface.Keyboard.PressAsync(Code.Tab);
    await surface.Keyboard.PressAsync(Code.Tab);
    await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);

    surface.ShouldHaveFocus(first);
}

[Fact]
public async Task Pointer_WhenTerminalLeaveArrives_ClearsHoverAndHeldStateAsync()
{
    var button = new Button { Content = new ControlText("Leave") };
    await using var surface = await ComponentSurface.MountAsync(
        button,
        new Size(10, 3),
        TestContext.Current.CancellationToken);

    await surface.Pointer.MoveToAsync(button);
    await surface.Pointer.PressAsync();
    await surface.Pointer.LeaveAsync();

    surface.ShouldHaveState(button, VisualState.Normal);
    surface.ShouldHaveCapture(null);
}
```

- [ ] **Step 2: Run the new tests and verify the expected API failures**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComponentSurfaceTests" --timeout 60s
```

Expected: FAIL to compile because `LeaveAsync`, `ShouldHaveFocus`,
`ShouldHaveCapture`, and Shift+Tab encoding are absent.

- [ ] **Step 3: Implement the minimal input encodings**

Add exact real-terminal sequences:

```csharp
(Code.Tab, Modifiers.Shift) =>
    _surface.SendAsync("\u001b[Z"u8.ToArray(), "press Shift+Tab"),
(Code.Escape, Modifiers.None) =>
    _surface.SendAsync("\u001b[27u"u8.ToArray(), "press Escape"),
```

Split Kitty character actions while retaining `CompleteCharacterAsync`:

```csharp
internal Task PressCharacterAsync(Rune value) => SendCharacterAsync(value, 1, "press");

internal Task ReleaseCharacterAsync(Rune value) => SendCharacterAsync(value, 3, "release");

internal async Task CompleteCharacterAsync(Rune value)
{
    await PressCharacterAsync(value);
    await ReleaseCharacterAsync(value);
}
```

Add terminal pointer leave:

```csharp
internal async Task LeaveAsync()
{
    await _surface.SendAsync("\u001b[<35;0;0M"u8.ToArray(), "leave terminal pointer surface");
    _lastPoint = null;
    _primaryModifiers = Modifiers.None;
    _primaryPressed = false;
}
```

- [ ] **Step 4: Use the control's complete appearance state and expose manager
      assertions**

Replace the hand-built subset in `ShouldHaveState`:

```csharp
var actual = control.GetAppearanceState();
actual.ShouldBe(expected);
```

Add exact assertions:

```csharp
internal void ShouldHaveFocus(Control? expected) =>
    _application.Focus.Focused.ShouldBeSameAs(expected);

internal void ShouldHaveCapture(Control? expected) =>
    _application.Capture.Captured.ShouldBeSameAs(expected);
```

Validate that a non-null expected control is owned by the mounted root before
asserting.

- [ ] **Step 5: Rerun focused harness tests**

Run the Task 2 test command.

Expected: PASS, including real decoder, dispatcher, focus/capture, render,
transport, and semantic-screen settling.

- [ ] **Step 6: Commit the harness extension**

```bash
git add tests/SharpVision.Tests/Support
git commit -m "test(controls): extend mounted interaction drivers"
```

### Task 3: Add the executable behavior evidence catalog

**Files:**

- Create: `tests/SharpVision.Tests/Support/ComponentBehavior.cs`
- Create:
  `tests/SharpVision.Tests/Support/ComponentBehaviorEvidenceAttribute.cs`
- Create: `tests/SharpVision.Tests/Support/ComponentBehaviorRequirement.cs`
- Create: `tests/SharpVision.Tests/Controls/PrismSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/TabBehaviorSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/ComboBoxSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/PopupSurfaceTests.cs`
- Create: `tests/SharpVision.Tests/Controls/WindowSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`

- [ ] **Step 1: Write failing catalog tests for complete types and capability
      evidence**

Add assertions that the catalog has no deferred set, exactly matches exported
concrete controls, declares one side of every applicability pair, and has
evidence covering all required flags:

```csharp
requirements.Keys.ShouldBe(exportedControls, ignoreOrder: true);
requirements.Values.ShouldAllBe(requirement =>
    HasExactlyOne(requirement.Behaviors, ComponentBehavior.Hover, ComponentBehavior.HoverExcluded) &&
    HasExactlyOne(requirement.Behaviors, ComponentBehavior.Focus, ComponentBehavior.FocusExcluded) &&
    HasExactlyOne(requirement.Behaviors, ComponentBehavior.Tab, ComponentBehavior.TabExcluded) &&
    HasExactlyOne(requirement.Behaviors, ComponentBehavior.Directional, ComponentBehavior.DirectionalExcluded) &&
    HasExactlyOne(requirement.Behaviors, ComponentBehavior.PressRelease, ComponentBehavior.PressReleaseExcluded));

private static bool HasExactlyOne(
    ComponentBehavior value,
    ComponentBehavior first,
    ComponentBehavior second)
{
    var selected = value & (first | second);
    return selected == first || selected == second;
}
```

- [ ] **Step 2: Run the catalog fixture and observe missing infrastructure**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComponentSurfaceCoverageTests" --timeout 60s
```

Expected: FAIL because the behavior types and evidence attributes do not exist.

- [ ] **Step 3: Define the immutable behavior model**

```csharp
[Flags]
internal enum ComponentBehavior
{
    None = 0,
    Mounted = 1 << 0,
    Hover = 1 << 1,
    HoverExcluded = 1 << 2,
    Focus = 1 << 3,
    FocusExcluded = 1 << 4,
    Tab = 1 << 5,
    TabExcluded = 1 << 6,
    Directional = 1 << 7,
    DirectionalExcluded = 1 << 8,
    PressRelease = 1 << 9,
    PressReleaseExcluded = 1 << 10,
    Activation = 1 << 11,
    UnavailableCleanup = 1 << 12,
    Transient = 1 << 13,
    Composition = 1 << 14,
}
```

```csharp
internal readonly struct ComponentBehaviorRequirement
{
    internal ComponentBehaviorRequirement(Type fixture, ComponentBehavior behaviors)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        Fixture = fixture;
        Behaviors = behaviors;
    }

    internal Type Fixture { get; }

    internal ComponentBehavior Behaviors { get; }
}
```

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
internal sealed class ComponentBehaviorEvidenceAttribute: Attribute
{
    internal ComponentBehaviorEvidenceAttribute(Type controlType, ComponentBehavior behaviors)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        ControlType = controlType;
        Behaviors = behaviors;
    }

    internal Type ControlType { get; }

    internal ComponentBehavior Behaviors { get; }
}
```

Create the six named surface fixture classes with XML summaries and no methods
yet. Tasks 4, 6, and 7 add their mounted scenarios before the evidence guard is
activated in Task 10. This keeps every committed slice compiling while the
catalog already has a concrete fixture destination for every public control.

```csharp
// PrismSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves Prism through mounted terminal surfaces.</summary>
public sealed class PrismSurfaceTests { }

// TabBehaviorSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves tab selection and navigation through mounted terminal surfaces.</summary>
public sealed class TabBehaviorSurfaceTests { }

// ComboBoxSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves ComboBox and its transient list through mounted terminal surfaces.</summary>
public sealed class ComboBoxSurfaceTests { }

// MenuSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves menu entries and navigation through mounted terminal surfaces.</summary>
public sealed class MenuSurfaceTests { }

// PopupSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves popup promotion and dismissal through mounted terminal surfaces.</summary>
public sealed class PopupSurfaceTests { }

// WindowSurfaceTests.cs
namespace SharpVision.Tests.Controls;

/// <summary>Proves window focus and interaction through mounted terminal surfaces.</summary>
public sealed class WindowSurfaceTests { }
```

- [ ] **Step 4: Populate all 31 current controls with no deferrals**

Use shared constants to keep the full map reviewable:

```csharp
const ComponentBehavior passive = ComponentBehavior.Mounted |
    ComponentBehavior.FocusExcluded | ComponentBehavior.TabExcluded |
    ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressReleaseExcluded;
const ComponentBehavior interactive = ComponentBehavior.Mounted |
    ComponentBehavior.Hover | ComponentBehavior.Focus | ComponentBehavior.Tab;
const ComponentBehavior ownedPressFace = ComponentBehavior.Mounted |
    ComponentBehavior.Hover | ComponentBehavior.FocusExcluded |
    ComponentBehavior.TabExcluded | ComponentBehavior.DirectionalExcluded;

private static readonly Dictionary<Type, ComponentBehaviorRequirement> _requirements = new()
{
    [typeof(ControlText)] = Requirement<TextSurfaceTests>(passive | ComponentBehavior.Hover),
    [typeof(FigletText)] = Requirement<FigletTextSurfaceTests>(passive | ComponentBehavior.Hover),
    [typeof(Prism)] = Requirement<PrismSurfaceTests>(passive | ComponentBehavior.Hover),
    [typeof(ProgressBar)] = Requirement<ProgressBarSurfaceTests>(passive | ComponentBehavior.HoverExcluded),
    [typeof(Separator)] = Requirement<SeparatorSurfaceTests>(passive | ComponentBehavior.HoverExcluded),
    [typeof(UiCanvas)] = Requirement<CanvasSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(Dock)] = Requirement<DockSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(Grid)] = Requirement<GridSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(Overlay)] = Requirement<OverlaySurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(Stack)] = Requirement<StackSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(GroupBox)] = Requirement<GroupBoxSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(Expander)] = Requirement<ExpanderSurfaceTests>(interactive | ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
    [typeof(Button)] = Requirement<ButtonSurfaceTests>(interactive | ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(CheckBox)] = Requirement<CheckBoxSurfaceTests>(interactive | ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(RadioButton)] = Requirement<RadioButtonSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(TextInput)] = Requirement<TextInputSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressReleaseExcluded | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(ComboBox)] = Requirement<ComboBoxSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Transient | ComponentBehavior.Composition),
    [typeof(ScrollBar)] = Requirement<ScrollBarSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(List)] = Requirement<ListSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressReleaseExcluded | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
    [typeof(Table)] = Requirement<TableSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(TabControl)] = Requirement<TabBehaviorSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
    [typeof(TabItem)] = Requirement<TabBehaviorSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(NavigationView)] = Requirement<NavigationViewSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressReleaseExcluded | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
    [typeof(NavigationViewGroup)] = Requirement<NavigationViewSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Composition),
    [typeof(NavigationViewItem)] = Requirement<NavigationViewSurfaceTests>(ownedPressFace | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(NavigationViewSeparator)] = Requirement<NavigationViewSurfaceTests>(passive | ComponentBehavior.HoverExcluded),
    [typeof(Menu)] = Requirement<MenuSurfaceTests>(interactive | ComponentBehavior.Directional | ComponentBehavior.PressReleaseExcluded | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
    [typeof(MenuItem)] = Requirement<MenuSurfaceTests>(ownedPressFace | ComponentBehavior.PressRelease | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup),
    [typeof(MenuSeparator)] = Requirement<MenuSurfaceTests>(passive | ComponentBehavior.HoverExcluded),
    [typeof(Popup)] = Requirement<PopupSurfaceTests>(passive | ComponentBehavior.Hover | ComponentBehavior.Transient | ComponentBehavior.Composition),
    [typeof(Window)] = Requirement<WindowSurfaceTests>(interactive | ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressReleaseExcluded | ComponentBehavior.Activation | ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition),
};

private static ComponentBehaviorRequirement Requirement<TFixture>(
    ComponentBehavior behaviors) => new(typeof(TFixture), behaviors);
```

`Requirement<TFixture>` supplies `typeof(TFixture)`. If a current contract
proves one applicability classification above is wrong, change the contract and
map together before adding evidence; do not omit the axis.

- [ ] **Step 5: Run the structural catalog guard**

Run the focused fixture again.

Expected: PASS for exact exported-type coverage and applicability pairs. Task 10
adds the evidence aggregation assertion after Tasks 4-9 populate the method
attributes.

- [ ] **Step 6: Commit the red catalog guard**

```bash
git add \
  tests/SharpVision.Tests/Support/ComponentBehavior.cs \
  tests/SharpVision.Tests/Support/ComponentBehaviorEvidenceAttribute.cs \
  tests/SharpVision.Tests/Support/ComponentBehaviorRequirement.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs \
  tests/SharpVision.Tests/Controls/PrismSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TabBehaviorSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ComboBoxSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/PopupSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/WindowSurfaceTests.cs
git commit -m "test(controls): catalog behavior requirements for every control"
```

### Task 4: Complete display, layout, and effect evidence

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/PrismSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/FigletTextSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ProgressBarSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/SeparatorSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/DockSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/GridSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/OverlaySurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/StackSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/GroupBoxSurfaceTests.cs`

- [ ] **Step 1: Add evidence attributes to existing mounted scenarios**

Annotate the method that actually proves each flag:

```csharp
[ComponentBehaviorEvidence(
    typeof(ProgressBar),
    ComponentBehavior.Mounted | ComponentBehavior.HoverExcluded |
    ComponentBehavior.FocusExcluded | ComponentBehavior.TabExcluded |
    ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressReleaseExcluded)]
[Fact]
public async Task Pointer_WhenMovedOverProgressBar_LeavesPassiveStateAndExactCellsAsync()
```

For containers, the mounted scenario must hover a real descendant and assert
both physical ancestry and direct targeting:

```csharp
await surface.Pointer.MoveToAsync(child);
root.IsPointerOver.ShouldBeTrue();
root.IsPointerDirectlyOver.ShouldBeFalse();
child.IsPointerDirectlyOver.ShouldBeTrue();
```

- [ ] **Step 2: Write a failing Prism mounted scenario**

```csharp
[ComponentBehaviorEvidence(
    typeof(Prism),
    ComponentBehavior.Mounted | ComponentBehavior.Hover |
    ComponentBehavior.FocusExcluded | ComponentBehavior.TabExcluded |
    ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressReleaseExcluded |
    ComponentBehavior.Composition)]
[Fact]
public async Task Render_WhenPrismOwnsUnicodeContent_AppliesScopedColorAndRoutesHoverAsync()
{
    var child = new ControlText("界");
    var prism = new Prism
    {
        Content = child,
        Phase = 0.25,
        CycleLength = 4,
        Direction = PrismDirection.Horizontal,
    };
    await using var surface = await ComponentSurface.MountAsync(
        prism,
        new Size(4, 1),
        TestContext.Current.CancellationToken);

    await surface.Pointer.MoveToAsync(child);

    prism.IsPointerOver.ShouldBeTrue();
    child.IsPointerDirectlyOver.ShouldBeTrue();
    surface.Cell(default).Text.ShouldBe("界");
    surface.Cell(new Point(1, 0)).IsContinuation.ShouldBeTrue();
}
```

- [ ] **Step 3: Run display/layout surface fixtures and fix only observed
      product defects**

Run:

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*SurfaceTests" --timeout 60s
```

Expected: the selected display/layout fixtures PASS. Any product defect gets a
focused failing regression before the smallest production correction.

- [ ] **Step 4: Commit display/layout evidence**

```bash
git add \
  tests/SharpVision.Tests/Controls/PrismSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TextSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/FigletTextSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ProgressBarSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/SeparatorSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/CanvasSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/DockSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/GridSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/OverlaySurfaceTests.cs \
  tests/SharpVision.Tests/Controls/StackSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/GroupBoxSurfaceTests.cs
git commit -m "test(controls): prove passive and layout component behavior"
```

Commit any production defect correction separately with its focused regression
and exact production file before this evidence-only commit.

### Task 5: Complete pressable, focus, Tab, and unavailable-state evidence

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/CheckBoxSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/RadioButtonSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ScrollBarSurfaceTests.cs`
- Modify when a regression requires it: corresponding files under
  `src/SharpVision/Controls/`

- [ ] **Step 1: Add failing held/released keyboard and pointer sequences**

Every semantic press owner must visibly prove the transition, not merely a final
click:

```csharp
await surface.Pointer.MoveToAsync(control);
await surface.Pointer.PressAsync();
surface.ShouldHaveState(
    control,
    VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);

await surface.Pointer.ReleaseAsync();
control.IsPressed.ShouldBeFalse();
surface.ShouldHaveCapture(null);
activations.ShouldBe(1);
```

For keyboard-capable press owners:

```csharp
await surface.Keyboard.PressCharacterAsync(new Rune(' '));
control.IsPressed.ShouldBeTrue();
await surface.Keyboard.ReleaseCharacterAsync(new Rune(' '));
control.IsPressed.ShouldBeFalse();
```

- [ ] **Step 2: Add cancellation and unavailable cleanup**

Cover pointer leave, disable while held, collapsed/removal cleanup where
applicable, and terminal focus loss. Assert zero activation, null capture, and
no stale `Pressed` flag.

- [ ] **Step 3: Add forward/reverse Tab and exact focus evidence**

Mount each focusable control with eligible siblings, drive Tab and Shift+Tab,
and assert `surface.ShouldHaveFocus`. Radio arrows must focus/check one member
and skip disabled members. ScrollBar arrows/pages adjust value without leaving
focus.

- [ ] **Step 4: Run each fixture separately and perform red-green fixes**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ButtonSurfaceTests|*CheckBoxSurfaceTests|*RadioButtonSurfaceTests|*ExpanderSurfaceTests|*ScrollBarSurfaceTests" \
  --timeout 60s
```

Expected: PASS with exact state and event assertions.

- [ ] **Step 5: Commit press/focus evidence**

```bash
git add \
  tests/SharpVision.Tests/Controls/ButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/CheckBoxSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/RadioButtonSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ExpanderSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ScrollBarSurfaceTests.cs
git commit -m "test(controls): prove press focus and traversal behavior"
```

Commit each required production correction separately with its focused test so
the user-owned `Expander.cs` change is never staged accidentally.

### Task 6: Complete editor, collection, tab, and navigation evidence

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/TabBehaviorSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextInputSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TableSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs`
- Modify when required: `src/SharpVision/Controls/TabControl.cs`, `TabItem.cs`,
  `TabItems.cs`, `List.cs`, `NavigationView.cs`, and `TextInput.cs`

- [ ] **Step 1: Add evidence to editor/list/navigation fixtures**

Prove that arrows remain inside each owner:

```csharp
surface.ShouldHaveFocus(list);
await surface.Keyboard.PressAsync(Code.Down);
list.CurrentIndex.ShouldBe(1);
surface.ShouldHaveFocus(list);

surface.ShouldHaveFocus(input);
await surface.Keyboard.PressAsync(Code.Left);
input.CaretIndex.ShouldBe(expectedCaret);
surface.ShouldHaveFocus(input);
```

Table remains passive and proves descendant hover/composition rather than
invented selection behavior.

- [ ] **Step 2: Write the failing replacement TabControl surface fixture**

Use only semantic public owners:

```csharp
[ComponentBehaviorEvidence(
    typeof(TabControl),
    ComponentBehavior.Mounted | ComponentBehavior.Hover | ComponentBehavior.Focus |
    ComponentBehavior.Tab | ComponentBehavior.Directional |
    ComponentBehavior.PressRelease | ComponentBehavior.Activation |
    ComponentBehavior.UnavailableCleanup | ComponentBehavior.Composition)]
[ComponentBehaviorEvidence(
    typeof(TabItem),
    ComponentBehavior.Mounted | ComponentBehavior.Hover |
    ComponentBehavior.FocusExcluded | ComponentBehavior.TabExcluded |
    ComponentBehavior.DirectionalExcluded | ComponentBehavior.PressReleaseExcluded |
    ComponentBehavior.Composition)]
[Fact]
public async Task Input_WhenTabHeadersNavigate_CommitsSelectionFocusAndContentAsync()
{
    var first = new TabItem { Header = "One", Content = new ControlText("First") };
    var disabled = new TabItem { Header = "Two", Content = new ControlText("Second"), IsEnabled = false };
    var third = new TabItem { Header = "界", Content = new ControlText("Third") };
    var tabs = new TabControl();
    tabs.Items.Add(first);
    tabs.Items.Add(disabled);
    tabs.Items.Add(third);
    await using var surface = await ComponentSurface.MountAsync(
        tabs,
        new Size(16, 4),
        TestContext.Current.CancellationToken);

    await surface.Keyboard.PressAsync(Code.Tab);
    await surface.Keyboard.PressAsync(Code.Right);

    surface.ShouldHaveFocus(tabs);
    tabs.SelectedIndex.ShouldBe(2);
    first.Content.ShouldNotBeNull().Bounds.ShouldBe(default);
    third.Content.ShouldNotBeNull().Bounds.ShouldNotBe(default);
}
```

Add pointer selection by clicking the exact header cell, Left/Right wrapping,
Home/End, disabled skipping, selected-removal repair, Unicode cells, and resize.

- [ ] **Step 3: Implement only missing TabControl behavior exposed by the red
      tests**

Keep `TabControl` as the one focus owner. Compute header cells internally for
rendering and pointer hit testing; do not recreate public header parts. Commit
page visibility when selection/items/availability changes rather than inside
measure/arrange. Implement Left/Right wrapping and Home/End selection over
eligible items.

- [ ] **Step 4: Run focused collection fixtures**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*TextInputSurfaceTests|*ListSurfaceTests|*TableSurfaceTests|*TabBehaviorSurfaceTests|*NavigationViewSurfaceTests" \
  --timeout 60s
```

Expected: PASS.

- [ ] **Step 5: Commit collection/navigation evidence**

```bash
git add \
  tests/SharpVision.Tests/Controls/TabBehaviorSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TextInputSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/ListSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/TableSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/NavigationViewSurfaceTests.cs \
  src/SharpVision/Controls/TabControl.cs \
  src/SharpVision/Controls/TabItem.cs \
  src/SharpVision/Controls/TabItems.cs \
  docs/controls/collections/tab-control.md
git commit -m "test(controls): prove directional collection behavior"
```

### Task 7: Remove transient-family coverage deferrals

**Files:**

- Modify: `tests/SharpVision.Tests/Controls/ComboBoxSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/PopupSurfaceTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/WindowSurfaceTests.cs`
- Modify when required: corresponding production controls

- [ ] **Step 1: Add a failing ComboBox open/navigate/commit/cancel scenario**

Mount with a later overlapping sibling to prove popup elevation. Drive Tab,
Space, Down, Enter, reopen, Escape, and pointer item selection. Assert ComboBox
remains focus owner, selected index changes only on commit, Popup cells
disappear after close, capture clears, and Tab continues once after closing.

```csharp
await surface.Keyboard.PressAsync(Code.Tab);
await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
combo.IsOpen.ShouldBeTrue();
surface.ShouldHaveFocus(combo);
await surface.Keyboard.PressAsync(Code.Down);
await surface.Keyboard.PressAsync(Code.Enter);
combo.SelectedIndex.ShouldBe(1);
combo.IsOpen.ShouldBeFalse();
surface.ShouldHaveFocus(combo);
```

- [ ] **Step 2: Add a failing Menu scenario**

Prove horizontal and vertical arrows, separator/disabled skipping, current item
hover, pointer and keyboard invocation, check/radio state, menu-level event
order, Tab dismissal policy, and no generated item entering global Tab
traversal.

```csharp
var menu = new Menu { Orientation = Orientation.Vertical };
var first = new MenuItem { Content = new ControlText("Open") };
var disabled = new MenuItem { Content = new ControlText("Disabled"), IsEnabled = false };
var check = new MenuItem { Content = new ControlText("Auto save"), Kind = MenuItemKind.Check };
menu.Items.Add(first);
menu.Items.Add(new MenuSeparator());
menu.Items.Add(disabled);
menu.Items.Add(check);

await surface.Keyboard.PressAsync(Code.Tab);
surface.ShouldHaveFocus(menu);
await surface.Keyboard.PressAsync(Code.Down);
menu.SelectedIndex.ShouldBe(3);
await surface.Keyboard.PressAsync(Code.Enter);
check.IsChecked.ShouldBeTrue();
```

- [ ] **Step 3: Add a failing Popup scenario**

Prove closed exclusion, open focus discovery, exact framed opaque cells,
promotion above a later sibling, Escape ordering (`Closing` before collapsed
cleanup, then `Closed`), resize placement, focus restoration by the owner
callback, and null capture after close.

```csharp
var anchor = new Button { Content = new ControlText("Open") };
var popupButton = new Button { Content = new ControlText("Inside") };
var popup = new Popup { Anchor = anchor, Content = popupButton };
var root = new Overlay { Children = { anchor, popup, new ControlText("underlay") } };
await using var surface = await ComponentSurface.MountAsync(
    root,
    new Size(20, 6),
    TestContext.Current.CancellationToken);

await surface.UpdateAsync(() => popup.IsOpen = true, "open popup");
surface.ShouldHaveFocus(popupButton);
await surface.Keyboard.PressAsync(Code.Escape);
popup.IsOpen.ShouldBeFalse();
surface.ShouldHaveCapture(null);
```

- [ ] **Step 4: Add a failing Window scenario**

Prove initial descendant focus, forward/reverse Tab through nested content,
Enter default and Escape cancel activation, title-bar drag capture/release on
Canvas, close-glyph activation, resize, and exact frame/shadow cells.

```csharp
var accept = new Button { Content = new ControlText("Accept"), IsDefault = true };
var cancel = new Button { Content = new ControlText("Cancel"), IsCancel = true };
var content = new Stack { Children = { accept, cancel } };
var window = new Window { Title = "Dialog", Content = content, CanClose = true };
var root = new Canvas { Children = { window } };
await using var surface = await ComponentSurface.MountAsync(
    root,
    new Size(24, 8),
    TestContext.Current.CancellationToken);

surface.ShouldHaveFocus(accept);
await surface.Keyboard.PressAsync(Code.Tab);
surface.ShouldHaveFocus(cancel);
await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
surface.ShouldHaveFocus(accept);
```

- [ ] **Step 5: Run each transient fixture and make minimal red-green
      corrections**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComboBoxSurfaceTests|*MenuSurfaceTests|*PopupSurfaceTests|*WindowSurfaceTests" \
  --timeout 60s
```

Expected: PASS.

- [ ] **Step 6: Commit transient evidence**

```bash
git add \
  tests/SharpVision.Tests/Controls/ComboBoxSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/MenuSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/PopupSurfaceTests.cs \
  tests/SharpVision.Tests/Controls/WindowSurfaceTests.cs \
  src/SharpVision/Controls/ComboBox.cs \
  src/SharpVision/Controls/Menu.cs \
  src/SharpVision/Controls/MenuItem.cs \
  src/SharpVision/Controls/Popup.cs \
  src/SharpVision/Controls/Window.cs
git commit -m "test(controls): cover transient interaction families"
```

### Task 8: Add the mixed-root behavioral journey

**Files:**

- Create:
  `tests/SharpVision.Tests/Integration/ComponentCompositionSurfaceTests.cs`

- [ ] **Step 1: Write the failing continuous sibling journey**

Construct one `Grid`/`Stack` root with Button, CheckBox, three RadioButtons,
TextInput, List, ScrollBar, and a disabled Button. Record focus and activation
events. Drive exact forward/reverse Tab order, arrows in each owner, hover
transfer, held/released pointer state, and focus transfer between siblings.

```csharp
var expectedForward = new Control[]
{
    button,
    checkBox,
    firstRadio,
    input,
    list,
    scrollBar,
};

foreach (var expected in expectedForward)
{
    await surface.Keyboard.PressAsync(Code.Tab);
    surface.ShouldHaveFocus(expected);
}

for (var index = expectedForward.Length - 2; index >= 0; index--)
{
    await surface.Keyboard.PressAsync(Code.Tab, Modifiers.Shift);
    surface.ShouldHaveFocus(expectedForward[index]);
}
```

After keyboard navigation, move to Button, press, assert exactly one pressed
semantic owner, release, assert one click, move to CheckBox, assert old hover
cleared, click, and assert focus/capture/event order plus final screen cells.

- [ ] **Step 2: Run the new integration fixture and observe the first real
      coordination failure**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComponentCompositionSurfaceTests" --timeout 60s
```

Expected: FAIL on the first missing coordination behavior, not a harness
timeout.

- [ ] **Step 3: Correct each coordination defect one at a time**

For every failure, retain the exact journey assertion, add a smaller focused
regression when the owner is ambiguous, implement the smallest product fix, and
rerun both focused and composition fixtures.

- [ ] **Step 4: Commit the mixed-root journey**

```bash
git add tests/SharpVision.Tests/Integration/ComponentCompositionSurfaceTests.cs
git commit -m "test(integration): prove mixed component coordination"
```

Commit each product correction separately with its focused control regression
before this journey-only commit.

### Task 9: Add the deep ownership and mutation journey

**Files:**

- Modify:
  `tests/SharpVision.Tests/Integration/ComponentCompositionSurfaceTests.cs`

- [ ] **Step 1: Build four-plus ownership levels with every public composition
      role**

Use this ownership chain, plus a sibling Button outside the subtree:

```text
Grid
→ GroupBox.Content
→ Stack.Children
→ Expander.Content
→ TabControl.Items
→ TabItem.Content
→ Button and TextInput
```

Add a NavigationView or List branch to include an `ItemsControl` with private
realized children.

- [ ] **Step 2: Drive nested focus, arrows, hover, press, and collapse cleanup**

```csharp
await surface.Keyboard.PressAsync(Code.Tab);
surface.ShouldHaveFocus(expander);
await surface.Keyboard.PressAsync(Code.Tab);
surface.ShouldHaveFocus(tabs);
await surface.Keyboard.PressAsync(Code.Tab);
surface.ShouldHaveFocus(deepButton);

await surface.Pointer.MoveToAsync(deepButton);
root.IsPointerOver.ShouldBeTrue();
group.IsPointerOver.ShouldBeTrue();
expander.IsPointerOver.ShouldBeTrue();
deepButton.IsPointerDirectlyOver.ShouldBeTrue();

await surface.Pointer.PressAsync();
await surface.UpdateAsync(() => expander.IsExpanded = false, "collapse focused pressed subtree");
deepButton.IsPressed.ShouldBeFalse();
surface.ShouldHaveCapture(null);
surface.ShouldHaveFocus(null);
```

Re-expand and prove no stale hover/pressed state, exact reverse exit to the
external sibling, selected-page exclusion, and final cells.

- [ ] **Step 3: Run and fix deep-composition regressions test-first**

Run the Task 8 command. Expected: PASS for both sibling and nested journeys.

- [ ] **Step 4: Mark composition evidence and commit**

Add `ComponentBehaviorEvidence` attributes for every ownership role actually
present in the journey, then run the catalog test and commit:

```bash
git add \
  tests/SharpVision.Tests/Integration/ComponentCompositionSurfaceTests.cs \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs
git commit -m "test(integration): prove deep component composition"
```

### Task 10: Close documentation, showcase, catalog, and repository gates

**Files:**

- Modify: `docs/testing/controls-integration.md`
- Modify: affected files under `docs/controls/`
- Modify: affected showcase panes and screen tests only when product behavior
  changed
- Modify: `tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs`

- [ ] **Step 1: Update the normative mounted-surface contract**

Document the capability/evidence catalog, separate key press/release, pointer
leave, focus/capture assertions, mixed-root journey, deep ownership journey, and
transient proof. Link exact control sections rather than duplicating their
behavior.

- [ ] **Step 2: Run the complete catalog audit**

Before running it, add the final evidence aggregation assertion. Reflect public
test methods from every requirement fixture, combine matching
`ComponentBehaviorEvidenceAttribute` values by control type, subtract them from
the required flags, and report every remainder as `ControlName: Flags`.

```csharp
[Fact]
public void Evidence_WhenEveryFixtureIsComplete_CoversEveryRequiredBehavior()
{
    var missing = new List<string>();

    foreach (var pair in _requirements)
    {
        var actual = pair.Value.Fixture
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .SelectMany(method => method.GetCustomAttributes<ComponentBehaviorEvidenceAttribute>())
            .Where(attribute => attribute.ControlType == pair.Key)
            .Aggregate(
                ComponentBehavior.None,
                (current, attribute) => current | attribute.Behaviors);
        var remainder = pair.Value.Behaviors & ~actual;

        if (remainder != ComponentBehavior.None)
        {
            missing.Add($"{pair.Key.Name}: {remainder}");
        }
    }

    missing.ShouldBeEmpty();
}
```

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComponentSurfaceCoverageTests" --timeout 60s
```

Expected: PASS with 31 current concrete controls, zero deferrals, valid
applicability pairs, and zero missing evidence flags.

- [ ] **Step 3: Run all mounted and composition tests**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*SurfaceTests|*ComponentCompositionSurfaceTests" --timeout 60s
```

Expected: PASS with zero failures and no timeouts.

- [ ] **Step 4: Run affected showcase tests**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --timeout 60s
```

Expected: PASS. Update screenshots/content assertions only for intentional
documented behavior changes.

- [ ] **Step 5: Run repository quality gates freshly**

```bash
make format
make lint
make build
make test
```

Expected: every command exits zero; zero build warnings/errors; configured
minimum test counts satisfied; Markdown and links clean.

- [ ] **Step 6: Audit the objective against evidence**

Confirm from current files and fresh output:

```text
all concrete controls -> catalog + named mounted evidence
mouse hover -> positive or explicit exclusion per control
focus -> mouse, Tab, Shift+Tab, cleanup
arrow navigation -> exact owner policies without focus escape
pressed/unpressed -> held, release, outside, unavailable, capture loss
mixed root -> one continuous sibling journey
nested parents/children -> four-plus levels and every ownership role
transient families -> open, containment, dismissal, cleanup, restoration
```

- [ ] **Step 7: Commit the completed contract and verification state**

```bash
git add \
  docs/testing/controls-integration.md \
  docs/controls/display/progress-bar.md \
  docs/controls/display/separator.md \
  docs/controls/layout/expander.md \
  docs/controls/collections/tab-control.md \
  docs/controls/input/combo-box.md \
  docs/controls/menus/menu.md \
  docs/controls/windows/popup.md \
  docs/controls/windows/window.md \
  tests/SharpVision.Tests/Support/ComponentSurfaceCoverageTests.cs
git commit -m "test(controls): complete mounted behavior coverage"
```

Stage only intentional task files. Do not stage unrelated user work.
