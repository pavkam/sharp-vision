// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Grid track geometry, spans, padding, resize, cells, and hit targets through mounted surfaces.</summary>
public sealed class GridSurfaceTests
{
    /// <summary>Verifies mixed columns and deterministic remainder reflow commit exact bounds and cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenColumnsMixKinds_RecomputesExactTrackGeometryAsync()
    {
        // Arrange
        var fixedChild = new ControlText("AAA") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var percentChild = new ControlText("BBBBB")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var automaticChild = new ControlText("界") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var starChild = new ControlText("CCCCCCCC")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Grid.SetColumn(percentChild, 1);
        Grid.SetColumn(automaticChild, 2);
        Grid.SetColumn(starChild, 3);
        var grid = new Grid
        {
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Columns.Add(Track.Cells(3));
        grid.Columns.Add(Track.Percent(25));
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Star(1));
        grid.Children.Add(fixedChild);
        grid.Children.Add(percentChild);
        grid.Children.Add(automaticChild);
        grid.Children.Add(starChild);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Assert initial geometry
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        automaticChild.Bounds.ShouldBe(new Rect(9, 0, 2, 1));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 1));
        surface.ShouldRender("AAA BBBB 界 CCCCCCCC");
        surface.Cell(new Point(10, 0)).Continuation.ShouldBeTrue();

        // Act
        await surface.ResizeAsync(new Size(13, 1));

        // Assert resized geometry
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(4, 0, 3, 1));
        automaticChild.Bounds.ShouldBe(new Rect(8, 0, 2, 1));
        starChild.Bounds.ShouldBe(new Rect(11, 0, 2, 1));
        surface.ShouldRender("AAA BBB 界 CC");
        surface.Cell(new Point(9, 0)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies spanning, padding, collapsed exclusion, and pointer routing use final arranged slots.</summary>
    [ComponentBehaviorEvidence(
        typeof(Grid),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Composition)]
    [Fact]
    public async Task Pointer_WhenGridUsesSpanPaddingAndCollapsedChild_HitsCommittedCellAsync()
    {
        // Arrange
        var activated = false;
        var header = new ControlText("HEADER")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        Grid.SetColumnSpan(header, 2);
        var left = new ControlText("LEFT") { HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(left, 1);
        var right = new Button
        {
            Text = "R",
            Style = TestButtonStyles.Flat,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(right, 1);
        Grid.SetColumn(right, 1);
        right.Click += (_, _) => activated = true;
        var collapsed = new ControlText("SHOULD NOT AFFECT TRACKS") { Visibility = Visibility.Collapsed };
        Grid.SetRow(collapsed, 1);
        Grid.SetColumnSpan(collapsed, 2);
        var grid = new Grid
        {
            Padding = new Thickness(1),
            RowSpacing = 1,
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Rows.Add(Track.Cells(1));
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        grid.Children.Add(header);
        grid.Children.Add(left);
        grid.Children.Add(right);
        grid.Children.Add(collapsed);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(right);

        // Assert
        header.Bounds.ShouldBe(new Rect(1, 1, 8, 1));
        left.Bounds.ShouldBe(new Rect(1, 3, 4, 1));
        right.Bounds.ShouldBe(new Rect(6, 3, 3, 1));
        collapsed.Bounds.ShouldBe(default);
        activated.ShouldBeTrue();
        grid.IsPointerOver.ShouldBeTrue();
        grid.IsPointerDirectlyOver.ShouldBeFalse();
        grid.IsFocused.ShouldBeFalse();
        grid.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(right, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""

                              HEADER

                              LEFT  R

                             """);
    }

    /// <summary>Verifies a full IsVisible→Hidden→Collapsed→IsVisible transition on a mounted Grid's
    /// Auto-tracked child commits exact geometry, rendering, and hit routing at every phase.
    /// Hidden freezes the track - ControlBase invalidates only rendering for a Hidden transition,
    /// never Measure - while excluding rendering and input; Collapsed additionally shrinks the
    /// Auto row to zero (without deleting the declared track) so the Star row below reclaims the
    /// space; returning to IsVisible restores both the original track geometry and hit routing. An
    /// opaque fill background and pointer probes prove committed cells and hit targets at every
    /// phase, not only the initial mounted state.</summary>
    [ComponentVisibilityEvidence(
        typeof(Grid),
        ComponentVisibilityEvidence.TransitionInvalidatesCorrectly |
        ComponentVisibilityEvidence.MountedTransitionCommittedGeometry |
        ComponentVisibilityEvidence.MountedTransitionHitTargets)]
    [Fact]
    public async Task Pointer_WhenTrackedChildTransitionsThroughVisibleHiddenCollapsedVisible_CommitsExactGeometryAndHitTargetsAsync()
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
        var fill = new ControlText("SSSS\nSSSS")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
            Overflow = Overflow.Clip
        };
        Grid.SetRow(fill, 1);
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Rows.Add(Track.Auto());
        grid.Rows.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(1));
        grid.Children.Add(target);
        grid.Children.Add(fill);
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert the initial IsVisible phase renders, occupies its track, and accepts input
        var visibleBounds = target.Bounds;
        visibleBounds.ShouldBe(new Rect(0, 0, 4, 1));
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldBe("T");
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(ReferenceColors.Get(2));
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(1);

        // Act: Hidden invalidates only rendering, so the Auto track and the fill's position stay
        // exactly where they were.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Hidden, "hide the tracked child");

        // Assert Hidden retains the track but renders and hit-tests nothing
        target.Bounds.ShouldBe(visibleBounds);
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldNotBe("T");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: Collapsed invalidates Measure, so the Auto row shrinks to zero (the declared track
        // survives, only its content contribution disappears) and the Star row reclaims the space.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Collapsed, "collapse the tracked child");

        // Assert Collapsed removes the track's contribution entirely and fill occupies the row
        target.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 4, 3));
        surface.Cell(default).Text.ShouldBe("S");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: returning to IsVisible restores the original track, rendering, and hit routing.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Visible, "restore the tracked child");

        // Assert
        target.Bounds.ShouldBe(visibleBounds);
        fill.Bounds.ShouldBe(new Rect(0, 1, 4, 2));
        surface.Cell(default).Text.ShouldBe("T");
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(2);
    }

    /// <summary>Verifies row spanning occupies multiple rows including internal spacing.</summary>
    [Fact]
    public async Task Render_WhenChildSpansRows_OccupiesMultipleRowsWithSpacingAsync()
    {
        // Arrange
        var spanning = new ControlText("S")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        Grid.SetRowSpan(spanning, 2);
        var topRight = new ControlText("T") { HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetColumn(topRight, 1);
        var bottomRight = new ControlText("B") { HorizontalAlignment = HorizontalAlignment.Stretch };
        Grid.SetRow(bottomRight, 1);
        Grid.SetColumn(bottomRight, 1);
        var grid = new Grid
        {
            RowSpacing = 1,
            ColumnSpacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Rows.Add(Track.Cells(1));
        grid.Rows.Add(Track.Cells(1));
        grid.Columns.Add(Track.Cells(3));
        grid.Columns.Add(Track.Star(1));
        grid.Children.Add(spanning);
        grid.Children.Add(topRight);
        grid.Children.Add(bottomRight);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(8, 3),
            TestContext.Current.CancellationToken);

        // Assert — spanning child covers rows 0 through 2 (including the 1-cell gap)
        spanning.Bounds.ShouldBe(new Rect(0, 0, 3, 3));
        topRight.Bounds.ShouldBe(new Rect(4, 0, 4, 1));
        bottomRight.Bounds.ShouldBe(new Rect(4, 2, 4, 1));
        surface.Cell(default).Text.ShouldBe("S");
        surface.Cell(new Point(4, 0)).Text.ShouldBe("T");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("B");
    }

    /// <summary>Verifies weighted star columns distribute space proportionally.</summary>
    [Fact]
    public async Task Render_WhenStarColumnsHaveDifferentWeights_DistributesProportionallyAsync()
    {
        // Arrange
        var one = new ControlText("1")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        var two = new ControlText("2")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
        };
        Grid.SetColumn(two, 1);
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        grid.Columns.Add(Track.Star(1));
        grid.Columns.Add(Track.Star(2));
        grid.Children.Add(one);
        grid.Children.Add(two);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(9, 1),
            TestContext.Current.CancellationToken);

        // Assert — Star(1) gets 3 cells, Star(2) gets 6 cells
        one.Bounds.Width.ShouldBe(3);
        two.Bounds.Width.ShouldBe(6);
        surface.Cell(default).Text.ShouldBe("1");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("2");
    }

    /// <summary>Verifies auto-sized column measures content then applies overflow on the result.</summary>
    [Fact]
    public async Task Render_WhenAutoColumnContainsOverflowText_SizesToContentAsync()
    {
        // Arrange
        var label = new ControlText("Name") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var field = new ControlText("Very long value that overflows")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Ellipsis
        };
        Grid.SetColumn(field, 1);
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1
        };
        grid.Columns.Add(Track.Auto());
        grid.Columns.Add(Track.Star(1));
        grid.Children.Add(label);
        grid.Children.Add(field);

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(16, 1),
            TestContext.Current.CancellationToken);

        // Assert — auto column is 4 cells ("Name"), star gets the rest
        label.Bounds.Width.ShouldBe(4);
        field.Bounds.X.ShouldBe(5);
        surface.Cell(default).Text.ShouldBe("N");
        surface.Cell(new Point(3, 0)).Text.ShouldBe("e");
        surface.Cell(new Point(5, 0)).Text.ShouldBe("V");
    }

    /// <summary>Verifies disabling a mounted Grid cascades disabled visual state and reduced
    /// EffectiveIsEnabled to its owned child, holds geometry stable across a genuine resize
    /// compared to an equivalently-built enabled instance, and recovers on re-enable.</summary>
    [ComponentBehaviorEvidence(typeof(Grid), ComponentBehavior.Disabled)]
    [Fact]
    public async Task Enabled_WhenGridIsDisabledAndReenabled_CascadesStateAndPreservesGeometryAsync()
    {
        // Arrange
        var child = new ControlText("FILL")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            grid,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act disable directly
        await surface.UpdateAsync(() => grid.IsEnabled = false, "disable Grid");

        // Assert direct and cascaded disabled state
        surface.ShouldHaveState(grid, VisualState.Disabled);
        child.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(child, VisualState.Disabled);

        // Act resize while disabled to a genuinely different size
        await surface.ResizeAsync(new Size(6, 5));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Grid
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
        grid.Bounds.ShouldBe(reference.Bounds);
        grid.DesiredSize.ShouldBe(reference.DesiredSize);

        // Act re-enable
        await surface.UpdateAsync(() => grid.IsEnabled = true, "re-enable Grid");

        // Assert recovery
        surface.ShouldHaveState(grid, VisualState.Normal);
        child.EffectiveIsEnabled.ShouldBeTrue();
    }
}
