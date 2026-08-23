// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Dock edge consumption, final fill, resize, cells, and hit targets through mounted surfaces.</summary>
public sealed class DockSurfaceTests
{
    /// <summary>Verifies a simple dock with children on all four sides renders the expected layout.</summary>
    [Fact]
    public async Task Render_WhenChildrenDockAllFourSides_DrawsExpectedLayoutAsync()
    {
        // Arrange
        var top = new ControlText("TTTTTT")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var bottom = new ControlText("BBBBBB")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var left = new ControlText("L\nL\nL") { Width = Length.Cells(1), Overflow = Overflow.Clip };
        var right = new ControlText("R\nR\nR") { Width = Length.Cells(1), Overflow = Overflow.Clip };
        var fill = new ControlText("XXXX\nXXXX\nXXXX")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(bottom, DockSide.Bottom);
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(right, DockSide.Right);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, bottom, left, right, fill }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(6, 5),
            TestContext.Current.CancellationToken);

        // Assert layout with top and bottom consuming full width first
        surface.ShouldRender("""
                             TTTTTT
                             LXXXXR
                             LXXXXR
                             LXXXXR
                             BBBBBB
                             """);
    }

    /// <summary>Verifies disabling LastChildFills renders the last child at its docked side instead of filling.</summary>
    [Fact]
    public async Task Render_WhenLastChildFillsIsDisabled_DocksLastChildToItsSideAsync()
    {
        // Arrange
        var left = new ControlText("LL\nLL\nLL") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var right = new ControlText("RR\nRR\nRR") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(right, DockSide.Right);
        var dock = new Dock
        {
            LastChildFills = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { left, right }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert last child docks to its side with gap in the middle
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 3));
        right.Bounds.ShouldBe(new Rect(6, 0, 2, 3));
        surface.ShouldRender("""
                             LL    RR
                             LL    RR
                             LL    RR
                             """);
    }

    /// <summary>Verifies all four edges consume in order and leave one exact clickable fill rectangle.</summary>
    [Fact]
    public async Task ResizeAsync_WhenEverySideAndFillArePresent_ReflowsExactRegionsAsync()
    {
        // Arrange
        var clicked = false;
        var left = new ControlText("LL\nLL\nLL\nLL\nLL\nLL") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var top = new ControlText("TTTTTTTT")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var right = new ControlText("RR\nRR\nRR\nRR\nRR") { Width = Length.Cells(2), Overflow = Overflow.Clip };
        var bottom = new ControlText("BBBBBB")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var fill = new Button
        {
            Text = "FFFFFF\nFFFFFF\nFFFFFF\nFFFFFF",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        fill.TextControl!.Overflow = Overflow.Clip;
        fill.Click += (_, _) => clicked = true;
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(right, DockSide.Right);
        Dock.SetSide(bottom, DockSide.Bottom);
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children =
            {
                left,
                top,
                right,
                bottom,
                fill
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(10, 6),
            TestContext.Current.CancellationToken);

        // Act initial hit
        await surface.Pointer.ClickAsync(fill);

        // Assert initial geometry
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 6));
        top.Bounds.ShouldBe(new Rect(2, 0, 8, 1));
        right.Bounds.ShouldBe(new Rect(8, 1, 2, 5));
        bottom.Bounds.ShouldBe(new Rect(2, 5, 6, 1));
        fill.Bounds.ShouldBe(new Rect(2, 1, 6, 4));
        clicked.ShouldBeTrue();
        dock.IsPointerOver.ShouldBeTrue();
        dock.IsPointerDirectlyOver.ShouldBeFalse();
        dock.IsFocused.ShouldBeFalse();
        dock.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(fill, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""
                             LLTTTTTTTT
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLFFFFFFRR
                             LLBBBBBBRR
                             """);

        // Act resize
        await surface.ResizeAsync(new Size(8, 4));

        // Assert resized geometry
        left.Bounds.ShouldBe(new Rect(0, 0, 2, 4));
        top.Bounds.ShouldBe(new Rect(2, 0, 6, 1));
        right.Bounds.ShouldBe(new Rect(6, 1, 2, 3));
        bottom.Bounds.ShouldBe(new Rect(2, 3, 4, 1));
        fill.Bounds.ShouldBe(new Rect(2, 1, 4, 2));
        surface.ShouldRender("""
                             LLTTTTTT
                             LLFFFFRR
                             LLFFFFRR
                             LLBBBBRR
                             """);
    }

    /// <summary>Verifies two children docked to the same side consume space in insertion order.</summary>
    [Fact]
    public async Task Render_WhenTwoChildrenDockToTop_ConsumesInInsertionOrderAsync()
    {
        // Arrange
        var first = new ControlText("111")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(first, DockSide.Top);
        var second = new ControlText("222")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Dock.SetSide(second, DockSide.Top);
        var fill = new ControlText("FFF")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { first, second, fill }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 4),
            TestContext.Current.CancellationToken);

        // Assert — first top gets row 0, second top gets row 1, fill gets rows 2-3
        first.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        second.Bounds.ShouldBe(new Rect(0, 1, 4, 1));
        fill.Bounds.Y.ShouldBe(2);
        surface.Cell(default).Text.ShouldBe("1");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("2");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("F");
    }

    /// <summary>Verifies a wrapped Text docked after a leading Star spacer stays visible instead
    /// of measuring to a zero-width box, since the Star sibling no longer freezes the Auto
    /// sibling's DesiredSize at zero during measure.</summary>
    [Fact]
    public async Task Render_WhenTextFollowsLeadingStarSpacer_RendersVisibleContentAsync()
    {
        // Arrange
        var spacer = new ControlText { Width = Length.Star(1) };
        var label = new ControlText("Hi");
        Dock.SetSide(spacer, DockSide.Left);
        Dock.SetSide(label, DockSide.Left);
        var dock = new Dock
        {
            LastChildFills = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { spacer, label }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        // Assert initial geometry gives the label its intrinsic width, not zero
        label.Bounds.ShouldBe(new Rect(7, 0, 2, 1));
        surface.Cell(new Point(7, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(8, 0)).Text.ShouldBe("i");

        // Act resize to a genuinely different size to force arrange to rerun
        await surface.ResizeAsync(new Size(12, 1));

        // Assert the label still reports and renders its intrinsic width after reflow
        label.Bounds.ShouldBe(new Rect(10, 0, 2, 1));
        surface.Cell(new Point(10, 0)).Text.ShouldBe("H");
        surface.Cell(new Point(11, 0)).Text.ShouldBe("i");
    }

    /// <summary>Verifies collapsed children do not consume dock edge space.</summary>
    [Fact]
    public async Task Render_WhenDockChildIsCollapsed_DoesNotConsumeSpaceAsync()
    {
        // Arrange
        var top = new ControlText("TOP")
        {
            Height = Length.Cells(1),
            Visibility = Visibility.Collapsed,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Dock.SetSide(top, DockSide.Top);
        var fill = new ControlText("FILL")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { top, fill }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert — collapsed top doesn't consume space, fill starts at row 0
        top.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 4, 3));
        surface.Cell(default).Text.ShouldBe("F");
    }

    /// <summary>Verifies a full IsVisible→Hidden→Collapsed→IsVisible transition on a mounted Dock's
    /// top-docked child commits exact geometry, rendering, and hit routing at every phase. Hidden
    /// freezes the docked edge - ControlBase invalidates only rendering for a Hidden transition,
    /// never Measure - while excluding rendering and input; Collapsed additionally releases the
    /// edge entirely so the fill child reclaims the row; returning to IsVisible restores both the
    /// original edge geometry and hit routing. An opaque fill background and pointer probes prove
    /// committed cells and hit targets at every phase, not only the initial mounted state.</summary>
    [Fact]
    public async Task Pointer_WhenDockedChildTransitionsThroughVisibleHiddenCollapsedVisible_CommitsExactGeometryAndHitTargetsAsync()
    {
        // Arrange
        var activated = 0;
        var target = new Button
        {
            Text = "TTTT",
            Style = TestButtonStyles.FlatWithPadding(default),
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        target.Click += (_, _) => activated++;
        Dock.SetSide(target, DockSide.Top);
        var fill = new ControlText("SSSS\nSSSS")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { target, fill }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert the initial IsVisible phase renders, occupies its edge, and accepts input
        var visibleBounds = target.Bounds;
        visibleBounds.ShouldBe(new Rect(0, 0, 4, 1));
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldBe("T");
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(ReferenceColors.Get(2));
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(1);

        // Act: Hidden invalidates only rendering, so the docked edge and the fill's position stay
        // exactly where they were.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Hidden, "hide the docked child");

        // Assert Hidden retains the edge but renders and hit-tests nothing
        target.Bounds.ShouldBe(visibleBounds);
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldNotBe("T");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: Collapsed invalidates Measure, so the edge disappears and fill reclaims the row.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Collapsed, "collapse the docked child");

        // Assert Collapsed removes the edge entirely and fill occupies the reclaimed row
        target.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 4, 3));
        surface.Cell(default).Text.ShouldBe("S");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: returning to IsVisible restores the original edge, rendering, and hit routing.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Visible, "restore the docked child");

        // Assert
        target.Bounds.ShouldBe(visibleBounds);
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldBe("T");
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(2);
    }

    /// <summary>Verifies disabling a mounted Dock cascades disabled visual state and reduced
    /// EffectiveIsEnabled to its owned child, holds geometry stable across a genuine resize
    /// compared to an equivalently-built enabled instance, and recovers on re-enable.</summary>
    [Fact]
    public async Task Enabled_WhenDockIsDisabledAndReenabled_CascadesStateAndPreservesGeometryAsync()
    {
        // Arrange
        var child = new ControlText("FILL")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var dock = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            dock,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act disable directly
        await surface.UpdateAsync(() => dock.IsEnabled = false, "disable Dock");

        // Assert direct and cascaded disabled state
        surface.ShouldHaveState(dock, VisualState.Disabled);
        child.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(child, VisualState.Disabled);

        // Act resize while disabled to a genuinely different size
        await surface.ResizeAsync(new Size(6, 5));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children =
            {
                new ControlText("FILL")
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Overflow = Overflow.Clip
                }
            }
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(6, 5),
            TestContext.Current.CancellationToken);
        dock.Bounds.ShouldBe(reference.Bounds);
        dock.DesiredSize.ShouldBe(reference.DesiredSize);

        // Act re-enable
        await surface.UpdateAsync(() => dock.IsEnabled = true, "re-enable Dock");

        // Assert recovery
        surface.ShouldHaveState(dock, VisualState.Normal);
        child.EffectiveIsEnabled.ShouldBeTrue();
    }
}
