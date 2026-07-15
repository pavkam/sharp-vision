# Showcase UX Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correct the collected SharpVision control defects and rebuild the
showcase into clear, stable, executable documentation.

**Architecture:** Production behavior is corrected before specimens depend on
it: semantic type styles replace global underline/checked overlays, owned
scrollbars resolve one themed policy, ComboBox observes outside input while
open, Table renders one translated scroll surface, and terminal Canvas gains
clipped integer geometry. The showcase then adopts a fixed page header,
formatted section hierarchy, restrained emoji, and application-shaped stages.

**Tech Stack:** .NET 10, C# 14, SharpVision mutable controls and semantic cell
Canvas, xUnit v3, Shouldly, Microsoft Testing Platform, Markdown/Prettier.

---

## File map

- `src/SharpVision/Styling/ThemeBuilder.cs`: type-specific visual states and
  canonical scrollbar recipe.
- `src/SharpVision/Controls/`: scrollbar ownership, ComboBox/List interaction,
  and Table geometry.
- `src/SharpVision.Terminal/Rendering/Canvas.cs`: line, ellipse, and circle
  rasterization.
- `src/SharpVision.Showcase/Doc.cs` and `Gallery.cs`: fixed page shell,
  hierarchy, and footer.
- `src/SharpVision.Showcase/Panes/`: corrected public-API specimens.
- `tests/`: exact state, geometry, routing, screen, and responsive proof.
- `docs/`: normative behavior and testing contracts.

### Task 1: Replace global focus and checked overlays

**Files:**

- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Modify: `tests/SharpVision.Tests/Styling/StandardThemeTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ButtonTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/CheckBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/RadioButtonTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ScrollBarTests.cs`

- [ ] **Step 1: Write failing resolution and exact-cell tests**

```csharp
[Fact]
public void Dark_WhenControlIsFocused_DoesNotApplyUnderline()
{
    var control = new ProbeControl();
    ThemeTestSupport.ApplyTheme(control, Themes.Dark);

    var attributes = ThemeTestSupport.Resolve(
        control,
        Control.AttributesProperty,
        State.Focused);
    (attributes & TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
}

[Fact]
public void Dark_WhenButtonIsFocused_UsesAccentBorder()
{
    var button = new Button();
    ThemeTestSupport.ApplyTheme(button, Themes.Dark);

    ThemeTestSupport.Resolve(button, Control.BorderColorProperty, State.Focused)
        .ShouldBe(Color.Indexed(14));
}
```

Render focused Button and ScrollBar cells and require `Underline.None`. Render
checked CheckBox and RadioButton over a known background and require every cell
after the mark to preserve that background.

- [ ] **Step 2: Run the selected tests and verify red**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*StandardThemeTests|*ButtonTests|*CheckBoxTests|*RadioButtonTests|*ScrollBarTests" \
  --timeout 60s
```

Expected: failures expose underline focus and opaque checked backgrounds.

- [ ] **Step 3: Implement type-specific standard styles**

Remove focused underline and checked foreground/background from
`BuildBaseStyle()`. Register styles for Button, ScrollBar, CheckBox, and
RadioButton:

```csharp
theme.SetStyle(BuildButtonStyle());
theme.SetStyle(BuildScrollBarStyle());
theme.SetStyle(BuildCheckBoxStyle());
theme.SetStyle(BuildRadioButtonStyle());
```

Button uses Accent border while focused/pressed; ScrollBar uses Accent
foreground while focused; CheckBox and RadioButton use Accent foreground while
checked with no background overlay. Disabled continues resolving Muted.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: every selected test passes with zero warnings.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Styling/ThemeBuilder.cs tests/SharpVision.Tests
git commit -m "fix(styling): use semantic control focus states"
```

### Task 2: Make scrollbar presentation theme-owned

**Files:**

- Modify: `src/SharpVision/Controls/ScrollBar.cs`
- Modify: `src/SharpVision/Controls/Container.cs`
- Modify: `src/SharpVision/Controls/TextInput.cs`
- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Modify: `tests/SharpVision.Tests/Controls/ScrollBarTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ContainerScrollTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ComboBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/TextInputTests.cs`

- [ ] **Step 1: Write failing theme/default/local precedence tests**

```csharp
[Fact]
public void ScrollBars_WhenThemeDefinesPolicy_UseResolvedChromeAndFill()
{
    var style = new ControlStyle<ScrollBar>();
    style.Set(ScrollBar.ChromeProperty, State.Normal, ScrollBarChrome.Thin);
    style.Set(ScrollBar.FillProperty, State.Normal, ScrollBarFill.Line);
    var theme = new Theme();
    theme.SetStyle(style);
    var standalone = new ScrollBar();
    ThemeTestSupport.ApplyTheme(standalone, theme);

    standalone.Chrome.ShouldBe(ScrollBarChrome.Thin);
    standalone.Fill.ShouldBe(ScrollBarFill.Line);
}
```

Repeat through Container, List, ComboBox, and TextInput owned rails. Add
explicit Full/Block local override cases.

- [ ] **Step 2: Run owner tests and verify red**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ScrollBarTests|*ContainerScrollTests|*ListTests|*ComboBoxTests|*TextInputTests" \
  --timeout 60s
```

Expected: style properties are missing or owned bars retain hard-coded values.

- [ ] **Step 3: Back rail policies with style properties**

Register:

```csharp
public static StyleProperty<ScrollBarChrome> ChromeProperty { get; } =
    StyleProperty<ScrollBarChrome>.Register<ScrollBar>(
        "chrome", ScrollBarChrome.Full, Impact.Measure);

public static StyleProperty<ScrollBarFill> FillProperty { get; } =
    StyleProperty<ScrollBarFill>.Register<ScrollBar>(
        "fill", ScrollBarFill.Block, Impact.Render);
```

Use `GetValue`/`SetValue` in ScrollBar. Register owner properties in Container
and TextInput, synchronize resolved owner values to created bars after style or
theme invalidation, and preserve explicit local precedence. List and ComboBox
continue forwarding through their owned scroll surface.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: standalone and owned bars agree, and local overrides win.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/ScrollBar.cs \
  src/SharpVision/Controls/Container.cs \
  src/SharpVision/Controls/TextInput.cs \
  src/SharpVision/Styling/ThemeBuilder.cs tests/SharpVision.Tests/Controls
git commit -m "feat(styling): theme scrollbar presentation"
```

### Task 3: Correct ComboBox chrome, hover, and dismissal

**Files:**

- Modify: `src/SharpVision/Controls/ComboBox.cs`
- Modify: `src/SharpVision/Controls/ListItem.cs`
- Modify: `src/SharpVision/Styling/ThemeBuilder.cs`
- Modify: `tests/SharpVision.Tests/Controls/ComboBoxTests.cs`
- Modify: `tests/SharpVision.Tests/Controls/ListTests.cs`

- [ ] **Step 1: Write failing render and routed-input tests**

```csharp
[Fact]
public async Task Dispatch_WhenOpenComboReceivesOutsidePress_ClosesWithoutCommitAsync()
{
    await using var dispatcher = Dispatcher.Start();
    await dispatcher.InvokeAsync(() =>
    {
        var box = new ComboBox { Items = ["A", "B"], SelectedIndex = 0 };
        var outside = new Button { Content = new ControlText("Outside") };
        var root = new Stack { Children = { box, outside } };
        new Engine().Layout(root, new Size(20, 6));
        root.Attach(dispatcher);
        using FocusManager focus = new(root);
        using CaptureManager capture = new(root);
        box.IsOpen = true;
        new Engine().Layout(root, new Size(20, 6));

        var point = new Point(outside.Bounds.X, outside.Bounds.Y);
        _ = capture.Dispatch(Pointer(point, PointerAction.Press));

        box.IsOpen.ShouldBeFalse();
        box.SelectedIndex.ShouldBe(0);
        focus.Focused.ShouldBeSameAs(outside);
    }, TestContext.Current.CancellationToken);
}
```

Add outside wheel, inside press, field toggle, and disposal cleanup cases. Add
exact field Surface/border cells. Add List pointer-move proof that hover fills a
row without changing selection.

- [ ] **Step 2: Run ComboBox/List tests and verify red**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*ComboBoxTests|*ListTests" --timeout 60s
```

Expected: field lacks shared chrome, hover is unchanged, and outside input
leaves the popup open.

- [ ] **Step 3: Implement the behavior**

Call `RenderChrome(canvas)` before ComboBox label/arrow drawing. Standard
ComboBox style uses Opaque Surface fill and focused Accent border. While open,
register an `Events.Pointer` handler on the attached root with
`handledEventsToo: true`; during Preview, close for a primary press or wheel
outside both `Bounds` and `_popup.SurfaceBounds`. Dispose the registration on
close, root change, disable, detach, and disposal. Restore field focus only when
focus was inside `_list`.

Give List owner styles Hovered and Selected row backgrounds; selection remains
stronger and hover never commits it.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: all ComboBox and List tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/ComboBox.cs \
  src/SharpVision/Controls/ListItem.cs src/SharpVision/Styling/ThemeBuilder.cs \
  tests/SharpVision.Tests/Controls/ComboBoxTests.cs \
  tests/SharpVision.Tests/Controls/ListTests.cs
git commit -m "fix(controls): dismiss and style combo boxes consistently"
```

### Task 4: Correct Table alignment and scrolling

**Files:**

- Modify: `src/SharpVision/Controls/Table.cs`
- Modify: `tests/SharpVision.Tests/Controls/TableTests.cs`

- [ ] **Step 1: Write failing layout, render, hit-test, and assertion tests**

```csharp
[Fact]
public void Layout_WhenCellUsesIntrinsicAlignment_KeepsMeasuredBounds()
{
    var option = new CheckBox
    {
        Content = new ControlText("Include integration tests"),
        VerticalAlignment = VerticalAlignment.Top,
    };
    var table = new Table();
    table.Columns.Add(TableColumn.Fixed("Action", 16));
    table.Columns.Add(TableColumn.Fill("Configuration"));
    table.Rows.Add(new TableRow([
        new Button { Content = new ControlText("Run checks") },
        option,
    ]));

    new Engine().Layout(table, new Size(48, 8));

    option.Bounds.Width.ShouldBe(option.DesiredSize.Width);
    option.Bounds.Height.ShouldBe(option.DesiredSize.Height);
}
```

Add explicit Stretch, two-axis translated header/grid/cell, viewport clipping,
scrollbar z-order, translated HitTest, and real owned-scrollbar interaction
cases. The interaction must move the content origin negative in Debug.

- [ ] **Step 2: Run Table tests and verify red**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*TableTests" --timeout 60s
```

Expected: cells fill slots, chrome stays unshifted, and negative origins reach
the assertion.

- [ ] **Step 3: Implement one translated content rectangle**

Store the Rect passed to `ArrangeOverride`. Arrange each cell with
`cell.Arrange(CellPadding.Deflate(slot))` so alignment resolves normally. Move
header/grid drawing into `RenderContent`, draw from the stored content origin,
then call `base.RenderContent(canvas)`; Container supplies the viewport clip and
renders bars afterward.

Split arithmetic:

```csharp
private static int AddExtent(int left, int right) =>
    (int) Math.Min(int.MaxValue, (long) left + right);

private static int Advance(int origin, int extent) =>
    (int) Math.Clamp((long) origin + extent, int.MinValue, int.MaxValue);
```

Assert both inputs non-negative in `AddExtent`; assert only `extent` in
`Advance`.

- [ ] **Step 4: Run Table and container scrolling tests**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*TableTests|*ContainerScrollTests" --timeout 60s
```

Expected: all tests pass without assertion output.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision/Controls/Table.cs \
  tests/SharpVision.Tests/Controls/TableTests.cs
git commit -m "fix(table): align and scroll cells with chrome"
```

### Task 5: Add arbitrary Canvas geometry

**Files:**

- Modify: `src/SharpVision.Terminal/Rendering/Canvas.cs`
- Modify: `tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs`

- [ ] **Step 1: Write failing public API tests**

```csharp
[Theory]
[InlineData(0, 0, 5, 3)]
[InlineData(5, 3, 0, 0)]
[InlineData(0, 3, 5, 0)]
public void DrawLine_WhenEndpointsVary_RasterizesBothEndpoints(
    int x1, int y1, int x2, int y2)
{
    using Frame frame = new(new Size(6, 4));

    frame.Canvas.DrawLine(new Point(x1, y1), new Point(x2, y2), new Rune('*'));

    FrameOracle.Get(frame, new Point(x1, y1)).ShouldBe("*");
    FrameOracle.Get(frame, new Point(x2, y2)).ShouldBe("*");
}
```

Add exact octant, radius 0/1/3, odd/even ellipse, clipped, one-cell-axis,
repeatability, invalid Rune, and negative radius tests. Invalid inputs must
leave the frame unchanged.

- [ ] **Step 2: Run Canvas primitive tests and verify red**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*CanvasPrimitiveTests" --timeout 60s
```

Expected: `DrawLine`, `DrawEllipse`, and `DrawCircle` are missing.

- [ ] **Step 3: Implement allocation-free integer rasterization**

Add the exact public signatures from the design. Validate the Rune once. Use
signed `long` intermediates for Bresenham line and midpoint ellipse error terms,
then call `DrawRune` only for points in the effective clip. Degenerate ellipses
draw their line or point. `DrawCircle` is cell-coordinate geometry and rejects
negative radius before mutation.

- [ ] **Step 4: Run terminal rendering tests**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*CanvasPrimitiveTests|*CanvasTests|*LineTests" --timeout 60s
```

Expected: all selected tests pass with exact cells.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Terminal/Rendering/Canvas.cs \
  tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs
git commit -m "feat(rendering): draw arbitrary cell geometry"
```

### Task 6: Build the fixed documentation shell and footer

**Files:**

- Modify: `src/SharpVision.Showcase/Doc.cs`
- Modify: `src/SharpVision.Showcase/Gallery.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/GalleryRenderingTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`

- [ ] **Step 1: Write failing shell and hierarchy tests**

Capture the page header bounds, route wheel input to the body, and require the
header to remain fixed while a body section moves. Assert one emoji per section,
Accent section text, bold example text, dim descriptions, and Info source
labels. Arrange the footer at normal and constrained heights and require Theme,
picker, separator, Quit, and Ctrl+C hint rectangles not to overlap.

```csharp
var headerBefore = FindText(gallery.Content, "Button").Bounds;
DispatchWheel(pageBody, -3);
new Engine().Layout(gallery, size);
FindText(gallery.Content, "Button").Bounds.ShouldBe(headerBefore);
```

- [ ] **Step 2: Run shell tests and verify red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests|*GalleryRenderingTests|*ShowcaseContentTests" \
  --timeout 60s
```

Expected: the page header scrolls, markup hierarchy is shallow, and the footer
is vertically fragmented.

- [ ] **Step 3: Implement the two-region page and aligned footer**

Change `Doc.Page` to return a Dock: dock an opaque Surface header with bottom
border at Top, and place a padded vertical Stack with AutoScroll Vertical and a
hidden horizontal bar in the remaining slot. Make Gallery's page host
non-scrolling; each fresh page body starts at offset zero.

Change the Section signature to:

```csharp
internal static Control Section(
    string icon,
    string heading,
    string description,
    params Control[] examples)
```

Render section titles as `<accent><b>icon heading</b></accent>`, descriptions
dim, example titles bold, and source labels Info. Replace the sidebar footer
Stacks with a two-column Grid inside a top-separated Dock.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: all shell/content tests pass at normal and constrained sizes.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Showcase/Doc.cs src/SharpVision.Showcase/Gallery.cs \
  tests/SharpVision.Showcase.Tests
git commit -m "feat(showcase): pin page headers and clarify hierarchy"
```

### Task 7: Repair interactive-control specimens

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/ButtonPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/CheckBoxPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ComboBoxPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/RadioButtonPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/InputPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/SelectionPaneTests.cs`

- [ ] **Step 1: Write failing behavior and screen tests**

Require a patterned Button shadow stage, autosave trigger/count log, and flat
Button bounds that do not change on press. Require CheckBox background
preservation, default/bordered ComboBox fields with hover, and same-name
cross-container RadioButtons that remain exclusive.

```csharp
var before = flat.Bounds;
DispatchPrimaryPress(flat);
flat.Bounds.ShouldBe(before);
```

- [ ] **Step 2: Run input/selection showcase tests and verify red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*InputPaneTests|*SelectionPaneTests" --timeout 60s
```

Expected: old abstract specimens and incorrect RadioButton group fail.

- [ ] **Step 3: Rebuild the four panes through public APIs**

Use an Overlay with patterned Surface content beneath composite/block/flat
Buttons. Replace Programmatic target with Save draft, Simulate autosave, and an
invocation count. Keep CheckBox matrices compact and mark-only. Add a bordered
ComboBox, outside-dismissal guidance, and canonical long-list rail. Put actual
`quality` group members in different cards and show the selected value.

Pass one explicit emoji to every `Doc.Section` in these files.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Showcase/Panes/ButtonPane.cs \
  src/SharpVision.Showcase/Panes/CheckBoxPane.cs \
  src/SharpVision.Showcase/Panes/ComboBoxPane.cs \
  src/SharpVision.Showcase/Panes/RadioButtonPane.cs \
  tests/SharpVision.Showcase.Tests
git commit -m "fix(showcase): clarify interactive control states"
```

### Task 8: Repair Canvas, Grid, and Table specimens

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/CanvasPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/CanvasSample.cs`
- Modify: `src/SharpVision.Showcase/Panes/CanvasShadeSample.cs`
- Create: `src/SharpVision.Showcase/Panes/CanvasGeometrySample.cs`
- Modify: `src/SharpVision.Showcase/Panes/GridPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/TablePane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/CanvasPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/LayoutPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/DataPaneTests.cs`

- [ ] **Step 1: Write failing exact-cell and geometry tests**

Require every CanvasShadeSample corner, diagonal/circle/ellipse cells, non-empty
Grid auto/star interiors with 2:1 extents, intrinsic top-left Table CheckBox
bounds, and all shortcut rows with no horizontal scrollbar.

```csharp
FrameOracle.Get(frame, new Point(shade.Bounds.Right - 1, shade.Bounds.Y))
    .ShouldNotBeEmpty();
interactiveOption.Bounds.Width.ShouldBe(interactiveOption.DesiredSize.Width);
interactiveOption.Bounds.Height.ShouldBe(interactiveOption.DesiredSize.Height);
```

- [ ] **Step 2: Run Canvas/layout/data tests and verify red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*CanvasPaneTests|*LayoutPaneTests|*DataPaneTests" --timeout 60s
```

Expected: border overwrite, clipped Grid, stretched CheckBox, and headerless
scrollbar fail.

- [ ] **Step 3: Rebuild the specimens**

Reflow CanvasShadeSample labels inside the border. Add CanvasGeometrySample
using only public `DrawLine`, `DrawCircle`, and `DrawEllipse`. Increase Grid's
proportional stage height and use visible Surface-backed regions with committed
ratio labels. Top-align Table's CheckBox, replace headerless content with short
formatted shortcut rows, and add a corrected two-axis scroll specimen. Pass
explicit section emoji in these panes.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: all selected tests pass with exact cells.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Showcase/Panes tests/SharpVision.Showcase.Tests
git commit -m "fix(showcase): repair drawing and data specimens"
```

### Task 9: Repair floating, theme, and remaining section specimens

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/PopupPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/WindowPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ThemingPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/DockPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/FigletTextPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ListPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/MenuPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/OverlayPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/ScrollBarPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/StackPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/TextPane.cs`
- Modify: `src/SharpVision.Showcase/Panes/TextInputPane.cs`
- Modify: `tests/SharpVision.Showcase.Tests/LayerPaneTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ThemeGalleryTests.cs`
- Modify: `tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs`

- [ ] **Step 1: Write failing floating-stage, placement, and theme tests**

Require Popup and Window frames to overlap populated background cells while
remaining above them. Drive four placement Buttons and require the same Popup to
move around one anchor. Require baseline and semantic type-styled Buttons with
no shadow and no raw `Glyphs {` text. Require exactly one emoji prefix on every
major section and none on example titles.

- [ ] **Step 2: Run layer/theme/content tests and verify red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*LayerPaneTests|*ThemeGalleryTests|*ShowcaseContentTests" \
  --timeout 60s
```

Expected: empty inline stages, separate placement demos, noisy theme readout,
and missing icons fail.

- [ ] **Step 3: Rebuild the specimens**

Place Popup/Window over toolbar/content/status backgrounds in Overlay or Canvas.
Replace PlacementDemo with one central anchor, one Popup, four side Buttons, and
a requested-side label. Use semantic Accent/Surface in the type style, no
shadow, a baseline sibling, and `Background: Accent · Border: Heavy`. Add one
explicit emoji to every remaining `Doc.Section`; keep example titles plain.

- [ ] **Step 4: Run Step 2 and verify green**

Expected: all selected tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/SharpVision.Showcase/Panes tests/SharpVision.Showcase.Tests
git commit -m "fix(showcase): make layers and hierarchy legible"
```

### Task 10: Align documentation and verify the repository

**Files:**

- Modify: `docs/concepts/styling.md`
- Modify: `docs/concepts/scrolling.md`
- Modify: `docs/controls/input/combo-box.md`
- Modify: `docs/controls/layout/table.md`
- Modify: `docs/architecture/rendering-pipeline.md`
- Modify: `docs/architecture/showcase.md`
- Modify: `docs/testing/showcase.md`

- [ ] **Step 1: Update each owning contract**

Document type-specific visual states, theme/local scrollbar precedence, ComboBox
outside dismissal, Table alignment/unified scrolling, Canvas geometry
validation/clipping/aspect ratio, and the fixed showcase hierarchy. Keep each
rule in its owning document and link from showcase/testing pages.

- [ ] **Step 2: Run focused product suites**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*CanvasPrimitiveTests|*CanvasTests|*LineTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*StandardThemeTests|*ButtonTests|*CheckBoxTests|*RadioButtonTests|*ScrollBarTests|*ContainerScrollTests|*ComboBoxTests|*ListTests|*TableTests|*TextInputTests" \
  --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --timeout 60s
```

Expected: every command exits 0 with no failed tests or warnings.

- [ ] **Step 3: Run documentation gates**

```bash
npm run format
npm run lint:markdown
npm run lint:links
npm run test:docs
```

Expected: Markdown is formatted, links/anchors are valid, and Node tests pass.

- [ ] **Step 4: Run repository quality gates**

```bash
make format
make lint
make build
make test
```

Expected: all exit 0; build warnings/errors are zero; discovered tests meet the
configured minimum; Markdown and links pass.

- [ ] **Step 5: Audit the remediation trace**

Read the approved remediation spec top to bottom. For every traceability row,
record the production test, showcase test, exact rendered-cell assertion, or
owning contract that proves it. Missing evidence returns to its owning task.

- [ ] **Step 6: Commit the final documented state**

```bash
git add docs src tests
git commit -m "docs: align showcase UX contracts"
git status --short
```

Expected: commit succeeds and status is empty.
