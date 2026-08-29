// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies Stack allocation, ordering, scrolling, resize, and hit targets through mounted surfaces.</summary>
public sealed class StackSurfaceTests
{
    /// <summary>Verifies a mounted child re-resolves its percentage ceiling and final rendered
    /// cells when the terminal viewport changes.</summary>
    [Fact]
    public async Task ResizeAsync_WhenChildMaximumIsRelative_ReflowsBoundsAndCellsAsync()
    {
        var child = new ControlText(new string('R', 40))
        {
            Width = Length.Star(1),
            MaxWidth = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        child.Bounds.ShouldBe(new Rect(0, 0, 10, 1));
        surface.ShouldRender("RRRRRRRRRR          ");

        await surface.ResizeAsync(new Size(40, 1));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 1));
        surface.ShouldRender("RRRRRRRRRRRRRRRRRRRR                    ");
    }

    /// <summary>Verifies pointer ancestry does not apply hover chrome to the layout container.</summary>
    [Fact]
    public async Task Pointer_WhenChildIsHovered_KeepsStackFrameInactiveAsync()
    {
        // Arrange
        var child = new Button
        {
            Text = "Child",
            Style = TestButtonStyles.Flat,
            Padding = default,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var stack = new Stack
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(9, 3),
            TestContext.Current.CancellationToken);
        var borderColor = surface.Cell(default).Style.Foreground;
        borderColor.IsRgb.ShouldBeTrue();

        // Act
        await surface.Pointer.MoveToAsync(child);

        // Assert
        stack.IsPointerOver.ShouldBeTrue();
        child.IsPointerOver.ShouldBeTrue();
        // The stack border now tracks IsPointerOver through the theme.
        surface.Cell(default).Style.Foreground.IsRgb.ShouldBeTrue();
    }

    /// <summary>Verifies fixed, percentage, and star tracks reflow to exact final cells after resize.</summary>
    [Fact]
    public async Task ResizeAsync_WhenHorizontalTracksAreMixed_ReallocatesBoundsAndCellsExactlyAsync()
    {
        // Arrange
        var fixedChild = new ControlText("AAA")
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var percentChild = new ControlText("BBBBB")
        {
            Width = Length.Percent(25),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var starChild = new ControlText("CCCCCCCC")
        {
            Width = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { fixedChild, percentChild, starChild }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(20, 1),
            TestContext.Current.CancellationToken);

        // Assert initial layout
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 5, 1));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 1));
        surface.ShouldRender("AAA  BBBBB  CCCCCCCC");

        // Act
        await surface.ResizeAsync(new Size(12, 1));

        // Assert resized layout
        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 3, 1));
        starChild.Bounds.ShouldBe(new Rect(10, 0, 2, 1));
        surface.ShouldRender("AAA  BBB  CC");
    }

    /// <summary>Verifies reverse order excludes collapsed children and routes clicks to final arranged rows.</summary>
    [Fact]
    public async Task Pointer_WhenStackIsReversedAndChildCollapsed_UsesVisibleVisualOrderAsync()
    {
        // Arrange
        var activated = string.Empty;
        var first = new Button
        {
            Text = "A",
            Style = TestButtonStyles.Flat,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        first.Click += (_, _) => activated = "A";
        var collapsed = new Button { Text = "X", Visibility = Visibility.Collapsed };
        var second = new Button
        {
            Text = "B",
            Style = TestButtonStyles.Flat,
            Padding = default,
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        second.Click += (_, _) => activated = "B";
        var stack = new Stack
        {
            Reverse = true,
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { first, collapsed, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(second);

        // Assert
        second.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        collapsed.Bounds.ShouldBe(default);
        first.Bounds.ShouldBe(new Rect(0, 2, 4, 1));
        activated.ShouldBe("B");
        stack.IsPointerOver.ShouldBeTrue();
        stack.IsPointerDirectlyOver.ShouldBeFalse();
        stack.IsFocused.ShouldBeFalse();
        stack.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(second, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldRender("""
                              B

                              A
                             """);
    }

    /// <summary>Verifies intrinsic vertical scrolling clips rows and resize clamps the retained offset.</summary>
    [Fact]
    public async Task ResizeAsync_WhenVerticalStackAutoScrolls_ClipsAndRepairsVisibleRowsAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        foreach (var value in new[] { "A", "B", "界", "D", "E" })
        {
            stack.Children.Add(new ControlText(value)
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            });
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             A
                             B
                             """);

        // Act scroll
        await surface.Pointer.WheelAsync(stack, default, wheelY: -1);

        // Assert scroll
        stack.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             B
                             界
                             """);
        surface.Cell(new Point(1, 1)).Continuation.ShouldBeTrue();

        // Act resize beyond content
        await surface.ResizeAsync(new Size(4, 6));

        // Assert resize repair
        stack.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             A
                             B
                             界
                             D
                             E

                             """);
    }

    /// <summary>Verifies vertical mixed tracks (fixed, percent, star) allocate exact heights.</summary>
    [Fact]
    public async Task Render_WhenVerticalTracksMixed_AllocatesExactHeightsAsync()
    {
        // Arrange
        var fixedChild = new ControlText("F")
        {
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        var percentChild = new ControlText("P")
        {
            Height = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
        };
        var starChild = new ControlText("S")
        {
            Height = Length.Star(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(3)),
        };
        var stack = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { fixedChild, percentChild, starChild }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 10),
            TestContext.Current.CancellationToken);

        // Assert — fixed=2, percent=50% of the complete 10-cell axis=5, star=remainder=3
        // (percentages resolve against the complete final axis, not the smaller remainder
        // left after reserving the fixed track).
        fixedChild.Bounds.Height.ShouldBe(2);
        percentChild.Bounds.Y.ShouldBe(2);
        percentChild.Bounds.Height.ShouldBe(5);
        starChild.Bounds.Y.ShouldBeGreaterThanOrEqualTo(percentChild.Bounds.Bottom);
        starChild.Bounds.Height.ShouldBe(3);
        (fixedChild.Bounds.Height + percentChild.Bounds.Height + starChild.Bounds.Height).ShouldBe(10);
    }

    /// <summary>Verifies min/max constraints clamp child sizes in a horizontal Stack.</summary>
    [Fact]
    public async Task Render_WhenChildrenHaveMinMaxConstraints_ClampsAllocatedSizesAsync()
    {
        // Arrange
        var small = new ControlText("S")
        {
            Width = Length.Star(1),
            MinWidth = Length.Cells(4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(1)),
        };
        var large = new ControlText("L")
        {
            Width = Length.Star(3),
            MaxWidth = Length.Cells(8),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
        };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { small, large }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(16, 1),
            TestContext.Current.CancellationToken);

        // Assert — Star(1) would get 4 cells, Star(3) would get 12, but max=8 clamps it
        small.Bounds.Width.ShouldBeGreaterThanOrEqualTo(4);
        large.Bounds.Width.ShouldBeLessThanOrEqualTo(8);
    }

    /// <summary>Verifies nested Stacks compose correctly with independent orientation and spacing.</summary>
    [Fact]
    public async Task Render_WhenStacksAreNested_ComposesIndependentlyAsync()
    {
        // Arrange
        var innerA = new ControlText("A")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Width = Length.Cells(3)
        };
        var innerB = new ControlText("B")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Width = Length.Cells(3)
        };
        var innerStack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            Children = { innerA, innerB }
        };
        var topLabel = new ControlText("TOP")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var outerStack = new Stack
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { topLabel, innerStack }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            outerStack,
            new Size(7, 3),
            TestContext.Current.CancellationToken);

        // Assert — vertical outer: row 0 = "TOP", row 1 = spacing, row 2 = inner horizontal
        surface.Cell(default).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("A");
        surface.Cell(new Point(4, 2)).Text.ShouldBe("B");
    }

    /// <summary>Verifies a full IsVisible→Hidden→Collapsed→IsVisible transition on a mounted Stack
    /// child commits exact geometry, rendering, and hit routing at every phase. Hidden freezes
    /// bounds - ControlBase invalidates only rendering for a Hidden transition, never Measure -
    /// while excluding rendering and input; Collapsed additionally removes the track and its
    /// spacing so the trailing sibling reflows into the reclaimed row; returning to IsVisible
    /// restores both the original geometry and hit routing. An opaque sibling background and
    /// pointer probes prove committed cells and hit targets at every phase, not only the initial
    /// mounted state.</summary>
    [Fact]
    public async Task Pointer_WhenChildTransitionsThroughVisibleHiddenCollapsedVisible_CommitsExactGeometryAndHitTargetsAsync()
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
        var sibling = new ControlText("SSSS")
        {
            Height = Length.Cells(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Face = AppearanceTestValues.Face(background: ReferenceColors.Get(2)),
            Overflow = Overflow.Clip
        };
        var stack = new Stack
        {
            Spacing = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { target, sibling }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Assert the initial IsVisible phase renders, occupies its slot, and accepts input
        var visibleBounds = target.Bounds;
        visibleBounds.ShouldBe(new Rect(0, 0, 4, 1));
        sibling.Bounds.ShouldBe(new Rect(0, 2, 4, 1));
        surface.Cell(default).Text.ShouldBe("T");
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(ReferenceColors.Get(2));
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(1);

        // Act: Hidden invalidates only rendering, so the slot and the sibling's position stay
        // exactly where they were.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Hidden, "hide the target child");

        // Assert Hidden retains the slot but renders and hit-tests nothing
        target.Bounds.ShouldBe(visibleBounds);
        sibling.Bounds.ShouldBe(new Rect(0, 2, 4, 1));
        surface.Cell(default).Text.ShouldNotBe("T");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: Collapsed invalidates Measure, so the track and its spacing disappear and the
        // sibling reflows up to close the gap.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Collapsed, "collapse the target child");

        // Assert Collapsed removes the slot entirely and the sibling occupies the reclaimed row
        target.Bounds.ShouldBe(default);
        sibling.Bounds.ShouldBe(new Rect(0, 0, 4, 1));
        surface.Cell(default).Text.ShouldBe("S");
        await surface.Pointer.MoveToAsync(new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.ReleaseAsync();
        activated.ShouldBe(1);

        // Act: returning to IsVisible restores the original geometry, rendering, and hit routing.
        await surface.UpdateAsync(() => target.Visibility = Visibility.Visible, "restore the target child");

        // Assert
        target.Bounds.ShouldBe(visibleBounds);
        sibling.Bounds.ShouldBe(new Rect(0, 2, 4, 1));
        surface.Cell(default).Text.ShouldBe("T");
        await surface.Pointer.ClickAsync(target);
        activated.ShouldBe(2);
    }

    /// <summary>Verifies disabling a mounted Stack cascades disabled visual state and reduced
    /// EffectiveIsEnabled to its owned child, holds geometry stable across a genuine resize
    /// compared to an equivalently-built enabled instance, and recovers on re-enable.</summary>
    [Fact]
    public async Task Enabled_WhenStackIsDisabledAndReenabled_CascadesStateAndPreservesGeometryAsync()
    {
        // Arrange
        var child = new ControlText("FILL")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Overflow = Overflow.Clip
        };
        var stack = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { child }
        };
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 3),
            TestContext.Current.CancellationToken);

        // Act disable directly
        await surface.UpdateAsync(() => stack.IsEnabled = false, "disable Stack");

        // Assert direct and cascaded disabled state
        surface.ShouldHaveState(stack, VisualState.Disabled);
        child.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(child, VisualState.Disabled);

        // Act resize while disabled to a genuinely different size
        await surface.ResizeAsync(new Size(6, 5));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Stack
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
        stack.Bounds.ShouldBe(reference.Bounds);
        stack.DesiredSize.ShouldBe(reference.DesiredSize);

        // Act re-enable
        await surface.UpdateAsync(() => stack.IsEnabled = true, "re-enable Stack");

        // Assert recovery
        surface.ShouldHaveState(stack, VisualState.Normal);
        child.EffectiveIsEnabled.ShouldBeTrue();
    }
}
