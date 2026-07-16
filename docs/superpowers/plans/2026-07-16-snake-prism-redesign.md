# Snake and Prism Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> superpowers:subagent-driven-development (recommended) or
> superpowers:executing-plans to implement this plan task-by-task. Steps use
> checkbox (\`- [ ]\`) syntax for tracking.

**Goal:** Ship a reusable, deterministic Prism foreground effect and use it to
give Snake a polished animated title, organized shortcut-aware HUD, and richer
board motion.

**Architecture:** Add one grapheme-safe foreground transformation to the
terminal canvas, then implement Prism as a caller-animated ContentControl over
that primitive. Refactor Snake into retained title and HUD components plus a
pure animation state, while keeping simulation ticks, visual pulses, and
terminal output in their existing layers.

**Tech Stack:** .NET 10, C# 14, SharpVision semantic Canvas, retained controls,
Microsoft Testing Platform, xUnit v3, Shouldly, Markdown/Prettier, and tmux
runtime smoke testing.

---

## Execution guard

The worktree already contains unrelated NavigationView changes. Before every
task, run git status --short, re-read every overlapping file, stage only paths
named by that task, and inspect git diff --cached --name-status before
committing. Never use git add -A.

## File map

- Terminal primitive: modify Canvas.cs, CanvasPrimitiveTests.cs,
  rendering-pipeline.md, and testing/rendering.md.
- Prism: create Prism.cs, PrismDirection.cs, PrismTests.cs, and
  controls/display/prism.md; update the documentation indexes.
- Showcase: create PrismPane.cs; update Gallery.cs, gallery tests, and showcase
  contracts.
- Snake: create SnakeAnimationState.cs, SnakeHud.cs, SnakeTitlePanel.cs, a
  README, and a dedicated tests/Snake.Tests project; modify SnakeBoard.cs,
  SnakeScreen.cs, SharpVision.slnx, and one assembly marker.
- Preserve one named type per exact-name file throughout.

## Task 1: Foreground-only canvas transformation

**Files:**

- Modify: src/SharpVision.Terminal/Rendering/Canvas.cs:242-284
- Modify:
  tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs:209-270
- Modify: docs/architecture/rendering-pipeline.md:20-45
- Modify: docs/testing/rendering.md:18-35

- [ ] **Step 1: Write failing exact-cell tests**

Add three Arrange/Act/Assert tests:

```csharp
[Fact]
public void ApplyForeground_WhenStoredOwnersAreRich_PreservesSemanticContentAndVisitsLeadsOnce()
{
    using Frame frame = new(new Size(4, 1));
    var original = new CellStyle(
        foreground: Color.Indexed(1),
        background: Color.Indexed(2),
        attributes: Attributes.Bold,
        hyperlink: "https://example.test/prism",
        underline: Underline.Curly,
        underlineColor: Color.Indexed(3));
    _ = frame.Canvas.Draw("A 界", default, original);
    List<Point> visited = [];

    frame.Canvas.ApplyForeground(
        frame.Bounds,
        point =>
        {
            visited.Add(point);
            return Color.Rgb(point.X * 20, 40, 60);
        });

    visited.ShouldBe([new Point(0, 0), new Point(1, 0), new Point(2, 0)]);
    AssertPreserved(frame.GetCell(new Point(0, 0)).Style, original, Color.Rgb(0, 40, 60));
    AssertPreserved(frame.GetCell(new Point(1, 0)).Style, original, Color.Rgb(20, 40, 60));
    AssertPreserved(frame.GetCell(new Point(2, 0)).Style, original, Color.Rgb(40, 40, 60));
    frame.GetCell(new Point(3, 0)).Style.ShouldBe(frame.GetCell(new Point(2, 0)).Style);
    frame.GetCell(new Point(3, 0)).IsContinuation.ShouldBeTrue();
}

private static void AssertPreserved(CellStyle actual, CellStyle expected, Color foreground)
{
    actual.Foreground.ShouldBe(foreground);
    actual.Background.ShouldBe(expected.Background);
    actual.Attributes.ShouldBe(expected.Attributes);
    actual.Hyperlink.ShouldBe(expected.Hyperlink);
    actual.Underline.ShouldBe(expected.Underline);
    actual.UnderlineColor.ShouldBe(expected.UnderlineColor);
}
```

The second test draws a width-two grapheme, clips away its lead, and requires
zero selector calls plus unchanged lead/continuation styles. The third passes
null after drawing one styled Rune and requires ArgumentNullException plus
unchanged frame state. A fourth selector throws on its second lead; require the
same exception identity, a valid first transformed owner, an unchanged second
owner, and valid continuation links. Include a stored-space assertion so stored
spaces transform while untouched blank cells do not.

- [ ] **Step 2: Run the focused test and verify red**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*CanvasPrimitiveTests" --timeout 60s
```

Expected: compilation fails because Canvas.ApplyForeground does not exist.

- [ ] **Step 3: Implement the minimal primitive**

Add this method beside ApplyStyle:

```csharp
public void ApplyForeground(Rect region, Func<Point, Color> selector)
{
    ArgumentNullException.ThrowIfNull(selector);
    _frame.ThrowIfDisposed();
    var target = _clip.Intersect(region).Intersect(_frame.Bounds);

    for (var y = target.Y; y < target.Bottom; y++)
    {
        var previousLead = -1;

        for (var x = target.X; x < target.Right; x++)
        {
            var index = _frame.GetIndex(new Point(x, y));
            var cell = _frame.GetCell(index);
            var leadIndex = cell.IsContinuation ? cell.LeadIndex : index;

            if (leadIndex == previousLead)
            {
                continue;
            }

            previousLead = leadIndex;
            var lead = _frame.GetCell(leadIndex);

            if (lead.Length == 0)
            {
                continue;
            }

            var leadPoint = new Point(leadIndex % _frame.Size.Width, leadIndex / _frame.Size.Width);
            var width = Math.Max(1, (int) lead.Width);
            var complete = true;

            for (var offset = 0; offset < width; offset++)
            {
                complete &= _clip.Contains(new Point(leadPoint.X + offset, leadPoint.Y));
            }

            if (!complete)
            {
                continue;
            }

            var style = lead.Style;
            var replacement = new CellStyle(
                selector(leadPoint),
                style.Background,
                style.Attributes,
                style.Hyperlink,
                style.Underline,
                style.UnderlineColor);
            _ = _frame.TrySetOwnerStyle(leadIndex, _clip, replacement);
        }
    }
}
```

Add full XML documentation: absolute-cell selector coordinates, stored-blank
behavior, wide-owner atomicity, non-retention, ArgumentNullException, and
ObjectDisposedException.

- [ ] **Step 4: Run focused tests and verify green**

Run Step 2 again. Expected: all CanvasPrimitiveTests pass.

- [ ] **Step 5: Update rendering contracts**

Document that foreground transforms preserve all non-foreground semantics, skip
untouched blanks, invoke once per complete owner, and fail the current render if
the callback throws. Add test obligations for row-major callbacks, stored
spaces, blank cells, clipping, and wide owners.

- [ ] **Step 6: Verify and commit exact paths**

```bash
git diff --check -- src/SharpVision.Terminal/Rendering/Canvas.cs \
  tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs \
  docs/architecture/rendering-pipeline.md docs/testing/rendering.md
git add src/SharpVision.Terminal/Rendering/Canvas.cs \
  tests/SharpVision.Terminal.Tests/Rendering/CanvasPrimitiveTests.cs \
  docs/architecture/rendering-pipeline.md docs/testing/rendering.md
git diff --cached --name-status
git commit -m "feat(rendering): add foreground transformation"
```

## Task 2: Prism public control

**Files:**

- Create: src/SharpVision/Controls/PrismDirection.cs
- Create: src/SharpVision/Controls/Prism.cs
- Create: tests/SharpVision.Tests/Controls/PrismTests.cs

- [ ] **Step 1: Write failing public-surface tests**

Create PrismTests.cs with defaults, validation, exact RGB, preservation,
wide-grapheme, stored-space, null-content, tiny-bounds, and layout tests. The
core exact-color test uses a bordered Prism around rich marked Text:

```csharp
[Fact]
public void Render_WhenPhaseChanges_RecolorsExactCellsWithoutChangingLayoutOrOtherStyle()
{
    var child = new ControlText(
        "<bg=4><u=curly><uc=5><link=https://example.test><b>ABC</b></link></uc></u></bg>");
    using var prism = new Prism
    {
        Content = child,
        Direction = PrismDirection.Horizontal,
        CycleLength = 6,
        BorderThickness = new Thickness(1),
        BorderColor = Color.Indexed(7),
    };
    var size = new Size(5, 3);
    var engine = new Engine();
    engine.Layout(prism, size);
    var bounds = child.Bounds;
    using Frame first = new(size);
    prism.Render(first.Canvas);

    first.GetCell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Rgb(255, 0, 0));
    first.GetCell(new Point(2, 1)).Style.Foreground.ShouldBe(Color.Rgb(255, 255, 0));
    first.GetCell(new Point(3, 1)).Style.Foreground.ShouldBe(Color.Rgb(0, 255, 0));
    first.GetCell(new Point(1, 1)).Style.Background.ShouldBe(Color.Indexed(4));
    first.GetCell(new Point(1, 1)).Style.Underline.ShouldBe(Underline.Curly);
    first.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(7));

    prism.Phase = 0.5;
    engine.Layout(prism, size);
    using Frame second = new(size);
    prism.Render(second.Canvas);

    child.Bounds.ShouldBe(bounds);
    second.GetCell(new Point(1, 1)).Style.Foreground.ShouldBe(Color.Rgb(0, 255, 255));
}
```

Validation must reject NaN, infinities, phase below zero or at least one,
non-positive cycle length, and unknown direction before PropertyChanged.

- [ ] **Step 2: Run and verify the missing-type failure**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*PrismTests" --timeout 60s
```

- [ ] **Step 3: Add PrismDirection**

```csharp
namespace SharpVision.Controls;

/// <summary>Defines the content-relative cell axis used by a Prism rainbow.</summary>
public enum PrismDirection
{
    /// <summary>Advances hue from left to right.</summary>
    Horizontal,

    /// <summary>Advances hue from top to bottom.</summary>
    Vertical,

    /// <summary>Advances hue across the sum of horizontal and vertical offsets.</summary>
    Diagonal,
}
```

Include the repository copyright header.

- [ ] **Step 4: Add Prism properties and rendering**

Prism is sealed, derives from ContentControl, caches one Func<Point, Color>
selector in its constructor, and uses three render-impact properties. Phase
defaults to 0 and validates finite [0,1); CycleLength defaults to 18 and is
positive; Direction defaults to Diagonal and is defined.

Use this post-child seam:

```csharp
internal override void RenderChildren(TerminalCanvas canvas)
{
    base.RenderChildren(canvas);
    canvas.ApplyForeground(ContentBounds, _selector);
}
```

Use this deterministic hue conversion:

```csharp
private Color SelectForeground(Point point)
{
    var content = ContentBounds;
    var x = point.X - content.X;
    var y = point.Y - content.Y;
    var coordinate = Direction switch
    {
        PrismDirection.Horizontal => x,
        PrismDirection.Vertical => y,
        PrismDirection.Diagonal => checked(x + y),
        _ => throw new UnreachableException(),
    };
    var hue = Phase + ((double) coordinate / CycleLength);
    hue -= Math.Floor(hue);
    var scaled = hue * 6d;
    var sector = (int) Math.Floor(scaled);
    var rising = (int) Math.Round(
        (scaled - sector) * byte.MaxValue,
        MidpointRounding.AwayFromZero);
    var falling = byte.MaxValue - rising;

    return sector switch
    {
        0 => Color.Rgb(byte.MaxValue, rising, 0),
        1 => Color.Rgb(falling, byte.MaxValue, 0),
        2 => Color.Rgb(0, byte.MaxValue, rising),
        3 => Color.Rgb(0, falling, byte.MaxValue),
        4 => Color.Rgb(rising, 0, byte.MaxValue),
        _ => Color.Rgb(byte.MaxValue, 0, falling),
    };
}
```

Use named regions for effect properties and rendering. Document units,
threading, render-only invalidation, validation, and exceptions.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*PrismTests" --timeout 60s
git add src/SharpVision/Controls/Prism.cs \
  src/SharpVision/Controls/PrismDirection.cs \
  tests/SharpVision.Tests/Controls/PrismTests.cs
git commit -m "feat(controls): add Prism foreground effect"
```

## Task 3: Prism normative documentation

**Files:**

- Create: docs/controls/display/prism.md
- Modify: docs/controls/index.md
- Modify: docs/index.md

- [ ] **Step 1: Write the complete control contract**

Specify purpose/inheritance, Content ownership, defaults, validation and
exceptions, cell-relative hue formula, foreground-only preservation,
normal-layer rendering, popup behavior, clipping/wide owners, caller-driven
phase animation, threading/invalidation, example, and test obligations.

Use this example:

```csharp
var title = new Prism
{
    Direction = PrismDirection.Diagonal,
    CycleLength = 18,
    Content = new FigletText(FigletCatalog.Default.Load("Small"))
    {
        Content = "SNAKE",
    },
};

title.Phase = (title.Phase + (1d / 60d)) % 1d;
```

State that elevated popup descendants render in the root popup pass and are not
recolored by the ordinary-layer Prism pass.

- [ ] **Step 2: Link the normative home**

Add Prism under Display in docs/controls/index.md and one precise link beside
FigletText in docs/index.md. Re-read both first because docs/controls/index.md
has unrelated edits.

- [ ] **Step 3: Validate and commit**

```bash
npx --no-install prettier --write docs/controls/display/prism.md \
  docs/controls/index.md docs/index.md
npx --no-install markdownlint-cli2 docs/controls/display/prism.md \
  docs/controls/index.md docs/index.md
npm run lint:links
git add docs/controls/display/prism.md docs/controls/index.md docs/index.md
git commit -m "docs(controls): specify Prism behavior"
```

## Task 4: Prism showcase page

**Files:**

- Create: src/SharpVision.Showcase/Panes/PrismPane.cs
- Modify: src/SharpVision.Showcase/Gallery.cs
- Modify: tests/SharpVision.Showcase.Tests/GalleryTests.cs
- Modify: tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs
- Modify: tests/SharpVision.Showcase.Tests/DisplayPaneTests.cs
- Modify: docs/architecture/showcase.md
- Modify: docs/testing/showcase.md

- [ ] **Step 1: Write failing gallery and phase tests**

Add Prism to the exact inventory and to the section map with Directions,
Caller-driven animation, Style preservation, and FIGlet title. Add this focused
behavior test:

```csharp
[Fact]
public void Prism_WhenPhaseButtonActivates_ChangesColorsWithoutMovingContent()
{
    using var page = new PrismPane();
    var size = new Size(100, 80);
    var engine = new Engine();
    engine.Layout(page, size);
    var prism = ControlTree.FindAll<Prism>(page)
        .Single(value => value.Direction == PrismDirection.Diagonal);
    var button = ControlTree.FindAll<Button>(page).Single(value =>
        ControlTree.Text(value).Contains("Advance phase", StringComparison.Ordinal));
    using Frame before = new(size);
    page.Render(before.Canvas);
    var bounds = prism.Bounds;
    var text = new Screen(before).Text;
    var point = new Point(prism.Content!.Bounds.X, prism.Content.Bounds.Y);

    button.PerformClick();
    engine.Layout(page, size);
    using Frame after = new(size);
    page.Render(after.Canvas);

    prism.Bounds.ShouldBe(bounds);
    new Screen(after).Text.ShouldBe(text);
    after.GetCell(point).Style.Foreground
        .ShouldNotBe(before.GetCell(point).Style.Foreground);
}
```

- [ ] **Step 2: Run and verify red**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DisplayPaneTests" --timeout 60s
```

Expected: missing PrismPane or inventory assertion failure.

- [ ] **Step 3: Create the retained page**

PrismPane derives from CompositeControl, defines Title = "Prism", constructs
once, and calls InitializeContent once. Create horizontal, vertical, and
diagonal specimens plus a live diagonal FIGlet specimen. Drive it explicitly:

```csharp
var live = new Prism
{
    Direction = PrismDirection.Diagonal,
    CycleLength = 18,
    Content = new FigletText(FigletCatalog.Default.Load("Small"))
    {
        Content = "PRISM",
    },
};
var status = new Text("Phase 0 / 60");
var advance = new Button { Content = new Text("Advance phase") };
var frame = 0;
advance.Click += (_, _) =>
{
    frame = (frame + 1) % 60;
    live.Phase = frame / 60d;
    status.Content = $"Phase {frame} / 60";
};
```

Compose four named Doc.Section blocks and include reproducible public-API source
strings.

- [ ] **Step 4: Register without overwriting concurrent work**

Insert the Prism factory alphabetically after Popup in the current Gallery
catalog. Re-read Gallery.cs immediately before editing because its
NavigationView migration belongs to the user. Update exact showcase inventory
counts/lists and testing prose from 19 to 20 pages.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests|*ShowcaseContentTests|*DisplayPaneTests" --timeout 60s
git add src/SharpVision.Showcase/Panes/PrismPane.cs \
  src/SharpVision.Showcase/Gallery.cs \
  tests/SharpVision.Showcase.Tests/GalleryTests.cs \
  tests/SharpVision.Showcase.Tests/ShowcaseContentTests.cs \
  tests/SharpVision.Showcase.Tests/DisplayPaneTests.cs \
  docs/architecture/showcase.md docs/testing/showcase.md
git diff --cached --name-status
git commit -m "feat(showcase): demonstrate Prism animation"
```

## Task 5: Snake test project and pure animation state

**Files:**

- Create: examples/Snake/AssemblyMarker.cs
- Create: examples/Snake/SnakeAnimationState.cs
- Create: tests/Snake.Tests/Snake.Tests.csproj
- Create: tests/Snake.Tests/GlobalUsings.cs
- Create: tests/Snake.Tests/SnakeAnimationStateTests.cs
- Modify: SharpVision.slnx

- [ ] **Step 1: Scaffold the test project and failing tests**

Use this project file:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Shouldly" />
    <PackageReference Include="xunit.v3" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\examples\Snake\Snake.csproj" />
  </ItemGroup>
</Project>
```

Add it under the solution tests folder. AssemblyMarker.cs grants only
InternalsVisibleTo("Snake.Tests") and declares one internal marker type.

Write these pure tests:

```csharp
[Fact]
public void Advance_WhenSixtyPulsesElapse_WrapsOneRainbowCycle()
{
    var animation = new SnakeAnimationState();

    for (var index = 0; index < 60; index++)
    {
        _ = animation.Advance();
    }

    animation.Frame.ShouldBe(0);
    animation.PrismPhase.ShouldBe(0d);
}

[Theory]
[InlineData(1)]
[InlineData(4)]
[InlineData(40)]
public void DeathWave_WhenWaveAndHoldPulsesElapse_CompletesOnce(int bodyLength)
{
    var animation = new SnakeAnimationState();
    animation.BeginDeath();

    for (var index = 0; index < 11; index++)
    {
        animation.Advance().ShouldBeFalse();
    }

    animation.VisibleDeathSegments(bodyLength).ShouldBe(bodyLength);

    for (var index = 0; index < 3; index++)
    {
        animation.Advance().ShouldBeFalse();
    }

    animation.Advance().ShouldBeTrue();
    animation.Advance().ShouldBeFalse();
}
```

- [ ] **Step 2: Run and verify the missing-state failure**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeAnimationStateTests" --timeout 60s
```

- [ ] **Step 3: Implement bounded animation state**

Create an internal sealed class with RainbowFrames = 60, DeathWaveFrames = 12,
DeathHoldFrames = 3, Frame in [0,59], and a death pulse of -1 when inactive.
Advance wraps Frame, returns true exactly once after 12 wave plus 3 hold pulses,
then clears death state. Use this complete segment calculation:

```csharp
internal int VisibleDeathSegments(int bodyLength)
{
    ArgumentOutOfRangeException.ThrowIfNegative(bodyLength);

    if (_deathPulse < 0)
    {
        return 0;
    }

    var wave = Math.Min(_deathPulse + 1, DeathWaveFrames);
    return bodyLength == 0
        ? 0
        : Math.Min(
            bodyLength,
            ((bodyLength * wave) + DeathWaveFrames - 1) / DeathWaveFrames);
}
```

Expose read-only Frame, PrismPhase, IsDeathActive, and DeathPulse. BeginDeath
resets the death pulse to zero. Document validation and state transitions.

- [ ] **Step 4: Run focused tests and commit**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeAnimationStateTests" --timeout 60s
git add SharpVision.slnx examples/Snake/AssemblyMarker.cs \
  examples/Snake/SnakeAnimationState.cs tests/Snake.Tests/Snake.Tests.csproj \
  tests/Snake.Tests/GlobalUsings.cs tests/Snake.Tests/SnakeAnimationStateTests.cs
git commit -m "test(snake): add deterministic animation state"
```

## Task 6: Retained two-row HUD

**Files:**

- Create: examples/Snake/SnakeHud.cs
- Create: tests/Snake.Tests/ControlTree.cs
- Create: tests/Snake.Tests/Screen.cs
- Create: tests/Snake.Tests/SnakeHudTests.cs

- [ ] **Step 1: Write failing HUD rendering tests**

Create a Screen frame oracle and ownership-slot ControlTree traversal, one type
per file. Test both 80 by 2 and 40 by 2:

```csharp
[Fact]
public void UpdateGame_WhenRendered_OrganizesMetricsAndShortcutsAcrossTwoRows()
{
    using var hud = new SnakeHud();
    var state = new GameState(40, 20, difficulty: 1);
    hud.UpdateGame(state, bestScore: 500);

    var screen = Render(hud, new Size(80, 2));

    screen.Text.ShouldContain("SCORE 000000");
    screen.Text.ShouldContain("LIVES ♥♥♥");
    screen.Text.ShouldContain("MEDIUM");
    screen.Text.ShouldContain("BEST 000500");
    screen.Text.ShouldContain("ARROWS / WASD");
    screen.Text.ShouldContain("P  PAUSE");
    screen.Text.ShouldContain("CTRL+Q  QUIT");
}

[Fact]
public void UpdateGame_WhenWidthIsForty_ReservesCompleteQuitShortcut()
{
    using var hud = new SnakeHud();
    hud.UpdateGame(new GameState(20, 10, difficulty: 0), bestScore: 500);

    Render(hud, new Size(40, 2)).Text.ShouldContain("CTRL+Q  QUIT");
}
```

- [ ] **Step 2: Run and verify red**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeHudTests" --timeout 60s
```

- [ ] **Step 3: Implement retained rows**

SnakeHud derives from CompositeControl. Put two one-row Dock controls in a
zero-spacing Stack. The root is exactly two cells high, opaque
ThemeColors.Surface, and horizontally padded. Add the right-docked quit Text
before the fill guidance child so Dock reserves it first:

```csharp
_quit = new Text("<error><b>CTRL+Q  QUIT</b></error>")
{
    Overflow = Overflow.Clip,
};
Dock.SetSide(_quit, Side.Right);
controls.Children.Add(_quit);
controls.Children.Add(_guidance);
```

Implement UpdateGame with validated arguments:

```csharp
internal void UpdateGame(GameState state, int bestScore)
{
    ArgumentNullException.ThrowIfNull(state);
    ArgumentOutOfRangeException.ThrowIfNegative(bestScore);
    var lives = new string('♥', Math.Max(0, state.Lives));
    var score = state.Score.ToString("000000", CultureInfo.InvariantCulture);
    var best = bestScore.ToString("000000", CultureInfo.InvariantCulture);
    _metrics.Content =
        $"<b>SCORE</b> <accent>{score}</accent>  " +
        $"<b>LIVES</b> <error>{lives}</error>  " +
        $"<b>{state.DifficultyName.ToUpperInvariant()}</b>  " +
        $"<b>BEST</b> {best}";
    _status.Content = state.IsSpeedBoosted
        ? "<info><b>⚡ BOOST</b></info>"
        : "<d>READY</d>";
    _guidance.Content = "<d>ARROWS / WASD</d>  MOVE   <d>P</d>  PAUSE";
}
```

Also implement UpdateTitle(string difficulty) and UpdatePaused(GameState state,
int bestScore). Call InitializeContent exactly once.

- [ ] **Step 4: Run focused tests and commit**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeHudTests" --timeout 60s
git add examples/Snake/SnakeHud.cs tests/Snake.Tests/ControlTree.cs \
  tests/Snake.Tests/Screen.cs tests/Snake.Tests/SnakeHudTests.cs
git commit -m "feat(snake): add shortcut-aware HUD"
```

## Task 7: Retained animated title screens

**Files:**

- Create: examples/Snake/SnakeTitlePanel.cs
- Create: tests/Snake.Tests/SnakeTitlePanelTests.cs

- [ ] **Step 1: Write failing retained-composition tests**

At 78 by 22, require SNAKE, ENTER START, 1 / 2 / 3, Q QUIT, HIGH SCORES, and the
selected difficulty. Capture the root and title Prism identities, call
ShowRecord then ShowTitle, and require both identities unchanged. Render before
and after Phase = 0.5; text and bounds remain equal while a title foreground
changes.

- [ ] **Step 2: Run and verify red**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeTitlePanelTests" --timeout 60s
```

- [ ] **Step 3: Build every title phase once**

SnakeTitlePanel derives from CompositeControl and owns one retained Overlay with
three Stack views:

1. Title: diagonal Prism around Small FigletText, subtitle, and a two-column
   Grid of action/high-score cards.
2. Record: diagonal Prism around Standard FigletText plus retained
   score/initials text.
3. Game over: red Standard FigletText plus retained final-score guidance.

Use two Track.Star(1, minimum: 20) columns with two cells spacing. Implement
ShowTitle, ShowRecord, and ShowGameOver by updating Text and setting exactly one
view Visible; never clear or add children after construction. Escape dynamic
score names through Text.Escape and format numbers invariantly.

Forward phase to both rainbow headings:

```csharp
internal double Phase
{
    get => _titlePrism.Phase;
    set
    {
        _titlePrism.Phase = value;
        _recordPrism.Phase = value;
    }
}
```

- [ ] **Step 4: Run focused tests and commit**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeTitlePanelTests" --timeout 60s
git add examples/Snake/SnakeTitlePanel.cs \
  tests/Snake.Tests/SnakeTitlePanelTests.cs
git commit -m "feat(snake): redesign retained title screens"
```

## Task 8: Dynamic board rendering

**Files:**

- Modify: examples/Snake/SnakeBoard.cs
- Create: tests/Snake.Tests/SnakeBoardTests.cs

- [ ] **Step 1: Write failing frame-transition tests**

Render the same board at animation frames 0 and 1 and prove with semantic cells:

- attract mode changes at least one dim field glyph without changing bounds;
- head foreground changes while body cells remain in the green family;
- each apple retains its semantic glyph but changes brightness;
- speed boost creates cyan accents;
- death-visible segments grow monotonically and never exceed body count;
- zero and tiny bounds never throw and preserve continuation ownership.

- [ ] **Step 2: Run and verify red**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeBoardTests" --timeout 60s
```

- [ ] **Step 3: Replace flash fields with validated render state**

Add render-impact setters for ShowBoard, ShowPaused, ShowAttractMode,
AnimationFrame, DeathVisibleSegments, and DeathPulse. Validate AnimationFrame in
[0,59], visible segments as non-negative, and death pulse in [-1,14] before
SetProperty. Retain State and DirectionChanged behavior.

- [ ] **Step 4: Implement deterministic visual helpers**

Split OnRender into named helpers for attract mode, obstacles, apples, snake,
death, boost, and pause. Use this bounded field formula:

```csharp
private void DrawAttractMode(TerminalCanvas canvas)
{
    for (var y = Bounds.Y; y < Bounds.Bottom; y++)
    {
        for (var x = Bounds.X; x < Bounds.Right; x++)
        {
            var signature = ((long) x * 17) + ((long) y * 31) + AnimationFrame;

            if (Math.Abs(signature % 47) is 0 or 1)
            {
                var color = AnimationFrame % 2 == 0
                    ? Color.Rgb(18, 58, 42)
                    : Color.Rgb(24, 72, 54);
                canvas.DrawRune(
                    new Rune('·'),
                    new Point(x, y),
                    new TerminalStyle(color, Color.Default, TerminalAttributes.Dim));
            }
        }
    }
}
```

Pulse apple RGB components by frame parity without changing AppleKind glyphs.
Compute head green as 210 + ((AnimationFrame % 4) * 15). During boost, color
every third body segment cyan using (segmentIndex + AnimationFrame) % 3 == 0.
During death, render the activated prefix as alternating red/gold ░ and the
remainder dim green until the hold ends.

- [ ] **Step 5: Run focused tests and commit**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeBoardTests" --timeout 60s
git add examples/Snake/SnakeBoard.cs tests/Snake.Tests/SnakeBoardTests.cs
git commit -m "feat(snake): animate board presentation"
```

## Task 9: Integrate retained views, loops, and global exit

**Files:**

- Modify: examples/Snake/SnakeScreen.cs
- Create: tests/Snake.Tests/FakeTerminal.cs
- Create: tests/Snake.Tests/SnakeScreenTests.cs
- Create: tests/Snake.Tests/SnakeExitTests.cs

- [ ] **Step 1: Add failing full-screen and runtime tests**

Create a bounded channel FakeTerminal in the Snake test namespace. Add tests
that:

1. Render title at 100 by 30 and find rainbow FIGlet, two cards, high scores,
   and both HUD rows.
2. Call the real AdvanceVisuals once and prove title foreground changes while
   GameState.Head does not.
3. Route Enter and prove gameplay metrics and the board appear.
4. Route P and prove the pause overlay plus P RESUME guidance.
5. Transition through every real GamePhase, send Kitty Ctrl+Q bytes, and await
   clean completion.
6. Render death, record-entry, and game-over phases and validate semantic colors
   plus every continuation owner.
7. Advance one game tick and prove the head moves independently of the current
   visual frame.
8. Complete one death wave and prove the phase transition occurs exactly once.
9. Dispose with both loops active and prove no queued callback mutates disposed
   controls.

Use the public runtime path:

```csharp
terminal.QueueResize(new Dimensions(new Size(100, 30)));
using var screen = new SnakeScreen();
await using var application = new Application(
    screen,
    terminal,
    terminal,
    TerminalOptions.Minimal);
await application.StartAsync(TestContext.Current.CancellationToken);
terminal.QueueInput(Encoding.ASCII.GetBytes("\u001b[113;5u"));
await application.Completion.WaitAsync(
    TimeSpan.FromSeconds(10),
    TestContext.Current.CancellationToken);
application.Failure.ShouldBeNull();
```

- [ ] **Step 2: Run and verify failures against the old screen**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeScreenTests|*SnakeExitTests" --timeout 60s
```

- [ ] **Step 3: Install retained HUD/title composition**

Replace the old top Text, FigletText, menu/scores boxes, and every overlay
Clear/Add sequence with SnakeHud and SnakeTitlePanel. Construct the board, title
overlay, and HUD once. Keep title z-index 10 and preserve board focus. Phase
methods update retained content and Visibility only.

- [ ] **Step 4: Separate game and visual cancellation**

Use `_gameLoopCts` and `_visualLoopCts`. Start the visual loop in `OnStarted`,
stop only the game loop for pause/death, and dispose both sources in
`OnDispose`. Use a no-catch-up loop:

```csharp
private async Task VisualLoopAsync(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(80), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        Application?.Dispatcher.Post(() =>
        {
            if (!IsDisposed)
            {
                AdvanceVisuals();
            }
        });
    }
}

internal void AdvanceVisuals()
{
    var deathComplete = _animation.Advance();
    _titlePanel.Phase = _animation.PrismPhase;
    _board.AnimationFrame = _animation.Frame;
    _board.DeathPulse = _animation.DeathPulse;
    _board.DeathVisibleSegments =
        _animation.VisibleDeathSegments(_state.Body.Count);

    if (deathComplete && _phase == GamePhase.DeathAnimation)
    {
        OnDeathAnimationComplete();
    }
}
```

TriggerDeathAnimation starts SnakeAnimationState death and stops the game loop;
it creates no task.

- [ ] **Step 5: Make Ctrl+Q global**

After press/handled validation but before the phase switch:

```csharp
if ((e.Stroke.Modifiers & Modifiers.Control) != 0 &&
    e.Stroke.Code == Code.Character &&
    e.Stroke.Character is { } character &&
    Rune.ToLowerInvariant(character) == new Rune('q'))
{
    Application?.Closed();
    e.Handled = true;
    return;
}
```

Keep plain Q title-only. Centralize production visibility/HUD changes in a
documented internal TransitionTo(GamePhase phase) used by real transition
methods and friend tests; reject undefined phases before mutation.

- [ ] **Step 6: Run focused integration tests and commit**

```bash
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj \
  --filter-class "*SnakeScreenTests|*SnakeExitTests" --timeout 60s
git add examples/Snake/SnakeScreen.cs tests/Snake.Tests/FakeTerminal.cs \
  tests/Snake.Tests/SnakeScreenTests.cs tests/Snake.Tests/SnakeExitTests.cs
git commit -m "feat(snake): integrate animated arcade presentation"
```

## Task 10: Documentation, visual proof, and full gates

**Files:**

- Create: examples/Snake/README.md
- Modify after failures: only implementation files named in Tasks 1-9

- [ ] **Step 1: Write the example guide**

Document the run command, arrows/WASD, P, 1/2/3, Enter, title Q, global Ctrl+Q,
special apples, readable gameplay palette, independent simulation/80 ms visual
clocks, and Prism's caller-driven 60-pulse phase cycle.

- [ ] **Step 2: Run all focused suites**

```bash
dotnet test --project tests/SharpVision.Terminal.Tests/SharpVision.Terminal.Tests.csproj \
  --filter-class "*CanvasPrimitiveTests" --timeout 60s
dotnet test --project tests/SharpVision.Tests/SharpVision.Tests.csproj \
  --filter-class "*PrismTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*GalleryTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*ShowcaseContentTests" --timeout 60s
dotnet test --project tests/SharpVision.Showcase.Tests/SharpVision.Showcase.Tests.csproj \
  --filter-class "*DisplayPaneTests" --timeout 60s
dotnet test --project tests/Snake.Tests/Snake.Tests.csproj --timeout 60s
```

Expected: all commands pass with non-zero discovery.

- [ ] **Step 3: Perform live tmux visual QA**

Build Release and launch Snake in a clean 100 by 30 tmux session. Poll
capture-pane until SNAKE appears; capture two title frames, send Enter and
capture gameplay, send P and capture pause, then send Kitty Ctrl+Q and require
exit. Store captures only under /tmp.

Inspect direct evidence:

- rainbow bands are spatially distinct and move without text/layout drift;
- title cards are balanced;
- metrics and controls occupy separate HUD rows;
- CTRL+Q QUIT remains complete;
- gameplay stays green/cyan/gold instead of becoming rainbow;
- apple/head frames visibly pulse;
- pause and death visuals stay inside the board.

- [ ] **Step 4: Run complete repository gates**

```bash
make format
make lint
make build
make test
```

Expected: zero warnings/errors, configured minimum tests discovered, and no
Markdown or link failures.

- [ ] **Step 5: Audit requirements and commit the guide**

Check the approved design requirement by requirement: start screen, HUD
organization, visible shortcuts, global exit, improved animation, restrained
rainbow, FIGlet, reusable Prism, docs, showcase, tests, and live proof each need
direct current-state evidence.

```bash
git add examples/Snake/README.md
git commit -m "docs(snake): document animated controls"
git status --short
git log --oneline -10
```

The final status may still show unrelated user files, but no Snake or Prism path
may remain uncommitted.
