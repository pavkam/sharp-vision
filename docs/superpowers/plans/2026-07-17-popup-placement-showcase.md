# Popup Placement Showcase Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the ambiguous fake-editor popup placement specimen with a
framed, bordered directional diagram centered on an `⚓ Anchor`.

**Architecture:** Keep the existing retained `PopupPane` composition and
ordinary `Button`/`Popup` behavior. Change only the placement specimen's
presentation, then prove its retained properties, activation behavior, and
rendered cells through a dedicated showcase test.

**Tech Stack:** .NET 10, C# 14, SharpVision retained controls, xUnit v3,
Shouldly, Microsoft Testing Platform.

---

## File map

- Create `tests/SharpVision.Showcase.Tests/PopupPlacementPaneTests.cs` for the
  focused visual and behavioral regression.
- Modify `src/SharpVision.Showcase/Panes/PopupPane.cs` to build the framed
  placement diagram.
- Modify `docs/architecture/showcase.md` to describe the intentional placement
  specimen.

### Task 1: Lock the visual and behavioral contract

**Files:**

- Create: `tests/SharpVision.Showcase.Tests/PopupPlacementPaneTests.cs`

- [ ] **Step 1: Write the failing test**

Create one `PopupPlacementPaneTests` type with a test named
`Render_WhenPlacementDiagramBuilds_ShowsFramedAnchorAndDirectionControls`. Build
and lay out `PopupPane`, then locate the buttons whose text is `⚓ Anchor`,
`↑ Above`, `← Left`, `Right →`, and `↓ Below`. Assert each has
`BorderThickness == new Thickness(1)`, `BorderGlyphs == Glyphs.Rounded`, and
`BorderColor == ThemeColor.From(ColorRole.Border)`. Activate `Right →`, assert
the placement popup is open with `PopupPlacement.Right`, lay out again, render,
and assert the screen contains `⚓ Anchor`, `Placement preview`, and
`Requested side: Right`.

```csharp
var right = buttons.Single(value => Content(value) == "Right →");
right.PerformClick();
popup.IsOpen.ShouldBeTrue();
popup.Placement.ShouldBe(PopupPlacement.Right);
screen.Text.ShouldContain("Placement preview");

static string? Content(Button button) =>
    button.Content is ControlText text ? text.Content : null;
```

- [ ] **Step 2: Run the test and confirm the expected failure**

Run:

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*PopupPlacementPaneTests" --timeout 60s
```

Expected: FAIL because the current specimen uses `Preview anchor`, plain
direction labels, and buttons without the explicit shared border contract.

- [ ] **Step 3: Commit the red test**

```bash
git add tests/SharpVision.Showcase.Tests/PopupPlacementPaneTests.cs
git commit -m "test: specify popup placement diagram"
```

### Task 2: Build the framed anchor diagram

**Files:**

- Modify: `src/SharpVision.Showcase/Panes/PopupPane.cs`
- Test: `tests/SharpVision.Showcase.Tests/PopupPlacementPaneTests.cs`

- [ ] **Step 1: Add explicit placement-button chrome**

Change `PlacementButton` so every directional control uses rounded intrinsic
chrome and semantic surface colors:

```csharp
var button = new Button
{
    Content = new Text(label),
    Background = ColorRole.Surface,
    BorderThickness = new Thickness(1),
    BorderGlyphs = Glyphs.Rounded,
    BorderColor = ColorRole.Border,
    Padding = new Thickness(1, 0),
};
```

- [ ] **Step 2: Replace the fake editor placement stage**

Use `⚓ Anchor` for the center button and apply the same rounded border
contract. Use `↑ Above`, `← Left`, `Right →`, and `↓ Below` for the direction
buttons. Keep the cross layout, add a horizontal `Separator` above the status,
and replace the editor backdrop with the heading `Popup placement diagram`. Keep
the popup anchored to the center button and retain the `Placement preview`
content.

```csharp
var placementAnchor = PlacementControl("⚓ Anchor");
var above = PlacementButton("↑ Above", PopupPlacement.Above, placementPopup, placementStatus);
var left = PlacementButton("← Left", PopupPlacement.Left, placementPopup, placementStatus);
var right = PlacementButton("Right →", PopupPlacement.Right, placementPopup, placementStatus);
var below = PlacementButton("↓ Below", PopupPlacement.Below, placementPopup, placementStatus);

static Button PlacementControl(string label) => new()
{
    Content = new Text(label),
    Background = ColorRole.Surface,
    BorderThickness = new Thickness(1),
    BorderGlyphs = Glyphs.Rounded,
    BorderColor = ColorRole.Border,
    Padding = new Thickness(1, 0),
};
```

- [ ] **Step 3: Run the focused test**

Run the Task 1 command again.

Expected: PASS with one discovered test and zero failures.

- [ ] **Step 4: Run existing popup and gallery interaction coverage**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*PopupPlacementPaneTests" "*GalleryInteractionTests" --timeout 60s
```

Expected: PASS with zero failures.

- [ ] **Step 5: Commit the implementation**

```bash
git add src/SharpVision.Showcase/Panes/PopupPane.cs \
  tests/SharpVision.Showcase.Tests/PopupPlacementPaneTests.cs
git commit -m "feat: clarify popup placement showcase"
```

### Task 3: Align documentation and complete verification

**Files:**

- Modify: `docs/architecture/showcase.md`

- [ ] **Step 1: Document the placement diagram**

Add a concise sentence explaining that the Popup page uses a rounded semantic
surface, an `⚓ Anchor`, bordered directional controls, and a separated status
row so placement behavior remains legible across themes.

- [ ] **Step 2: Run repository gates**

```bash
make format
make lint
make build
make test
```

Expected: all four commands exit successfully, the build reports zero warnings
and errors, and every discovered test passes.

- [ ] **Step 3: Commit the documentation**

```bash
git add docs/architecture/showcase.md
git commit -m "docs: explain popup placement diagram"
```
