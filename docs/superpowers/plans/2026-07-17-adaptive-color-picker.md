# Adaptive Color Picker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship reusable Slider and capability-aware ColorPicker controls with complete mouse, keyboard, layout, rendering, documentation, showcase, and test evidence.

**Architecture:** Slider is a focused leaf control with signed integer range mapping and pointer capture. ColorPicker is a retained CompositeControl whose layout-owned true-color and palette branches consume inherited terminal capabilities and the renderer's shared palette projector.

**Tech Stack:** .NET 10, C# 14, SharpVision retained controls and semantic canvas, xUnit v3, Shouldly, Microsoft Testing Platform, Markdown quality gates.

---

## File structure

- `src/SharpVision.Terminal/Rendering/Palette.cs` — public deterministic projection and indexed RGB resolution.
- `src/SharpVision/Controls/Control.Capabilities.cs` — inherited terminal capability context.
- `src/SharpVision/Controls/Slider.cs` — range state, rendering, keyboard, wheel, and pointer capture.
- `src/SharpVision/Input/SliderValueChangedEventArgs.cs` — immutable Slider transition.
- `src/SharpVision/Controls/ColorPicker.cs` — retained composition, synchronization, normalization, and event publication.
- `src/SharpVision/Controls/ColorPlane.cs`, `ColorRamp.cs`, `ColorGrid.cs`, and `ColorSwatch.cs` — focused rendering/interaction parts.
- Matching fixtures under `tests/SharpVision.Terminal.Tests`, `tests/SharpVision.Tests`, and `tests/SharpVision.Showcase.Tests`.
- `src/SharpVision.Showcase/Panes/SliderPane.cs` and `ColorPickerPane.cs` — dedicated live documentation pages.
- `docs/controls/input/slider.md` and `color-picker.md` — normative public contracts.

### Task 1: Share terminal color projection

**Files:**
- Modify: `tests/SharpVision.Terminal.Tests/Rendering/PaletteTests.cs`
- Modify: `src/SharpVision.Terminal/Rendering/Palette.cs`

- [ ] **Step 1: Write failing public projection and resolution tests**

Add public-surface tests:

```csharp
[Theory]
[MemberData(nameof(ProjectionCases))]
public void Project_WhenDepthIsSelected_ReturnsNearestSupportedColor(
    Color source,
    ColorDepth depth,
    Color expected) =>
    Palette.Project(source, depth).ShouldBe(expected);

[Fact]
public void Resolve_WhenIndexedColorIsSupplied_ReturnsReferenceRgb() =>
    Palette.Resolve(Color.Indexed(67)).ShouldBe(Color.Rgb(95, 135, 175));
```

Extend the fixed-seed loop to require Basic16 indices below 16, Indexed256 indices below 256, and stable resolve/project round trips.

- [ ] **Step 2: Run RED**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*PaletteTests" --timeout 60s
```

Expected: compilation fails because `Palette` and `Resolve` are not public.

- [ ] **Step 3: Publish the existing implementation without duplication**

```csharp
public static class Palette
{
    public static Color Project(Color source, ColorDepth depth)
```

Keep the current `Project` method body unchanged after making the declarations
public, then add the complete new method:

```csharp
public static class Palette
{

    public static Color Resolve(Color source)
    {
        if (source.Kind != ColorKind.Indexed)
        {
            return source;
        }

        Resolve(source.Index, out var red, out var green, out var blue);
        return Color.Rgb(red, green, blue);
    }
}
```

Preserve Encoder calls to this exact type and add full XML validation docs.

- [ ] **Step 4: Run GREEN**

Run the Task 1 command. Expected: all Palette tests pass with zero warnings.

### Task 2: Inherit terminal capabilities through controls

**Files:**
- Create: `src/SharpVision/Controls/Control.Capabilities.cs`
- Modify: `src/SharpVision/Controls/Control.cs`
- Modify: `src/SharpVision/Controls/OwnedControlRegistry.cs`
- Modify: `src/SharpVision/Runtime/Application.cs`
- Create: `tests/SharpVision.Tests/Runtime/ControlCapabilitiesTests.cs`

- [ ] **Step 1: Write failing attachment and update tests**

```csharp
var profile = Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 };
var control = new CapabilityProbe();

control.Attach(dispatcher, Policy.Default, profile);

control.Depth.ShouldBe(ColorDepth.Indexed256);
control.Transitions.ShouldBe([ColorDepth.Indexed256]);
```

Also prove newly added descendants inherit the profile and runtime updates commit before `Application.CapabilitiesChanged` observers inspect the tree.

- [ ] **Step 2: Run RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*ControlCapabilitiesTests" --timeout 60s
```

Expected: compilation fails because capability context and the attachment overload do not exist.

- [ ] **Step 3: Implement context propagation**

```csharp
protected TerminalCapabilities Capabilities { get; private set; } =
    TerminalCapabilities.Conservative;

protected virtual void OnCapabilitiesChanged(
    TerminalCapabilities previous,
    TerminalCapabilities current)
{
}
```

Thread the immutable profile through Attach, subtree context commit, owned-child publication, detach reset, and Application profile updates. Validate nulls and publish the hook after commit.

- [ ] **Step 4: Run GREEN**

Run Task 2 plus `*CapabilityNegotiationTests`. Expected: all pass with existing publication order intact.

### Task 3: Build Slider state and rendering

**Files:**
- Create: `src/SharpVision/Input/SliderValueChangedEventArgs.cs`
- Create: `src/SharpVision/Controls/Slider.cs`
- Create: `tests/SharpVision.Tests/Controls/SliderTests.cs`

- [ ] **Step 1: Write failing range and exact-cell tests**

Cover signed endpoints, invalid setter atomicity, integer extremes, committed event order, both orientations, and 0/1/2-cell bounds:

```csharp
var slider = new Slider { Minimum = -10, Maximum = 10, Value = 0 };
List<string> changes = [];
slider.ValueChanged += (_, args) =>
    changes.Add($"{args.PreviousValue}>{args.Value}:{slider.Value}");

slider.ChangeBy(int.MaxValue).ShouldBeTrue();

slider.Value.ShouldBe(10);
changes.ShouldBe(["0>10:10"]);
```

- [ ] **Step 2: Run RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*SliderTests" --timeout 60s
```

Expected: compilation fails because Slider is missing.

- [ ] **Step 3: Implement minimal state and rail**

```csharp
public class Slider: Control
{
    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;
    public int Minimum { get; set; }
    public int Maximum { get; set; } = 100;
    public int Value { get; set; }
    public int SmallChange { get; set; } = 1;
    public int LargeChange { get; set; } = 10;
    public Orientation Orientation { get; set; }
    public bool ChangeBy(int delta)
    {
        VerifyMutable();
        var requested = (long) Value + delta;
        var next = (int) Math.Clamp(requested, Minimum, Maximum);
        return Commit(next);
    }
}
```

Use long arithmetic and cumulative endpoint-inclusive mapping. Render filled, unfilled, and thumb roles strictly inside Bounds with width-policy fallbacks.

- [ ] **Step 4: Run GREEN**

Run Task 3. Expected: range and semantic-cell tests pass.

### Task 4: Add Slider input and mounted proof

**Files:**
- Modify: `src/SharpVision/Controls/Slider.cs`
- Modify: `tests/SharpVision.Tests/Controls/SliderTests.cs`
- Create: `tests/SharpVision.Tests/Controls/SliderSurfaceTests.cs`

- [ ] **Step 1: Write failing keyboard, wheel, pointer, pixel, focus, and cancellation tests**

Drive direct PointerManager press/move/release plus raw CSI/SGR mounted input. Assert direct track selection, immutable drag geometry, capture, endpoint bubbling, no release commit, and final cells.

- [ ] **Step 2: Run RED**

Run `*SliderTests` and `*SliderSurfaceTests`. Expected: input assertions fail while Value remains unchanged.

- [ ] **Step 3: Implement input parity**

Map axis arrows and wheel to SmallChange, Page keys to LargeChange, and Home/End to endpoints on press/repeat. Primary press focuses, selects directly, captures, and drags using cell or inferred-pixel baselines. Release or cancellation clears drag without committing again.

- [ ] **Step 4: Run GREEN**

Run both Slider filters. Expected: direct and mounted tests pass.

### Task 5: Build retained ColorPicker branches

**Files:**
- Create: `src/SharpVision/Input/ColorChangedEventArgs.cs`
- Create: `src/SharpVision/Controls/ColorPlane.cs`
- Create: `src/SharpVision/Controls/ColorRamp.cs`
- Create: `src/SharpVision/Controls/ColorGrid.cs`
- Create: `src/SharpVision/Controls/ColorSwatch.cs`
- Create: `src/SharpVision/Controls/ColorPicker.cs`
- Create: `tests/SharpVision.Tests/Controls/ColorPickerTests.cs`

- [ ] **Step 1: Write failing tier and synchronization tests**

Prove permanent composition, detached RGB retention, attach projection, lossy downgrade/no resurrection, event order, branch visibility, RGB/Hue synchronization, preview, and uppercase hex cells:

```csharp
var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };

picker.Attach(
    dispatcher,
    Policy.Default,
    Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

picker.Value.ShouldBe(Color.Indexed(67));
```

- [ ] **Step 2: Run RED**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*ColorPickerTests" --timeout 60s
```

Expected: compilation fails because ColorPicker and its parts are missing.

- [ ] **Step 3: Implement composition and one commit path**

Construct all Grid/Stack/Dock/Overlay branches once and initialize exactly one root. Centralize updates:

```csharp
private bool Commit(Color requested)
{
    var normalized = Normalize(requested, Capabilities.ColorDepth);

    if (_value == normalized)
    {
        return false;
    }

    var previous = _value;
    _value = normalized;
    SynchronizeParts();
    ValueChanged?.Invoke(this, new ColorChangedEventArgs(previous, normalized));
    return true;
}
```

Use explicit HSV/RGB helpers, Palette.Resolve/Project, and Visibility changes only.

- [ ] **Step 4: Run GREEN**

Run Task 5. Expected: construction, tier, event, and synchronization tests pass.

### Task 6: Add ColorPicker interaction and surface proof

**Files:**
- Modify: `src/SharpVision/Controls/ColorPlane.cs`
- Modify: `src/SharpVision/Controls/ColorGrid.cs`
- Modify: `src/SharpVision/Controls/ColorPicker.cs`
- Modify: `tests/SharpVision.Tests/Controls/ColorPickerTests.cs`
- Create: `tests/SharpVision.Tests/Controls/ColorPickerSurfaceTests.cs`

- [ ] **Step 1: Write failing interaction and randomized containment tests**

Cover plane arrows/drag, palette row-column/Home-End navigation, hue/RGB sliders, capability updates, disabled state, focus, zero/tiny/resize containment, selected markers, raw input, and fixed-seed active-tier membership.

- [ ] **Step 2: Run RED**

Run `*ColorPickerTests` and `*ColorPickerSurfaceTests`. Expected: input and containment assertions fail.

- [ ] **Step 3: Implement coordinate and keyboard mapping**

Use committed local bounds and cumulative endpoint-inclusive rounding. ColorPlane and ColorGrid capture primary presses and update during drag. Clamp arrows at edges, Home/End at endpoints, and send every selection through ColorPicker.Commit.

- [ ] **Step 4: Run GREEN**

Run both ColorPicker filters. Expected: direct and mounted tests pass.

### Task 7: Add dedicated showcases and remove the Canvas specimen

**Files:**
- Delete: `src/SharpVision.Showcase/Panes/CanvasColorGridSample.cs`
- Modify: `src/SharpVision.Showcase/Panes/CanvasPane.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Create: `src/SharpVision.Showcase/Panes/SliderPane.cs`
- Create: `src/SharpVision.Showcase/Panes/ColorPickerPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/SliderPaneTests.cs`
- Create: `tests/SharpVision.Showcase.Tests/ColorPickerPaneTests.cs`

- [ ] **Step 1: Write failing catalog, content, render, and live-event tests**

Require both pages, absence of the old sample, keyboard instructions, live value labels after interaction, and representative color/rail cells.

- [ ] **Step 2: Run RED**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*SliderPaneTests|*ColorPickerPaneTests|*GalleryTests|*ShowcaseContentTests|*CanvasPaneTests" --timeout 60s
```

Expected: new page assertions fail.

- [ ] **Step 3: Implement the pages**

Use Doc.Page/Section/Example and ordinary retained layout. Register alphabetically, add interactive labels, and remove only the Canvas palette section/sample.

- [ ] **Step 4: Run GREEN**

Run Task 7. Expected: affected showcase tests pass.

### Task 8: Document and verify

**Files:**
- Create: `docs/controls/input/slider.md`
- Create: `docs/controls/input/color-picker.md`
- Modify: `docs/controls/index.md`
- Modify: `docs/architecture/showcase.md`
- Modify: `docs/concepts/input-routing.md`
- Modify: `docs/concepts/layout.md`
- Modify: `docs/testing/controls-integration.md`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Write complete contracts**

Document inheritance, defaults, validation, event ordering, capability normalization, focus/input/capture, layout, visual states, examples, and test obligations with precise section links.

- [ ] **Step 2: Run focused checks**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj --filter-class "*PaletteTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj --filter-class "*Slider*Tests|*ColorPicker*Tests|*ControlCapabilitiesTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj --filter-class "*SliderPaneTests|*ColorPickerPaneTests|*GalleryTests|*ShowcaseContentTests|*CanvasPaneTests" --timeout 60s
npm run test:docs
```

Expected: every focused test and documentation check passes.

- [ ] **Step 3: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings, zero errors, configured test minimum met, and no Markdown/link failures.

- [ ] **Step 4: Review the final diff**

Run `git diff --check`, inspect only intentional files, preserve unrelated dirty work, and report any baseline failure with its exact command and output.
