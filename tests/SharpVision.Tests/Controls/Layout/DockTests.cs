// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;


/// <summary>Verifies deterministic edge consumption and remaining-space layout.</summary>
public sealed class DockTests
{
    /// <summary>Verifies defaults and invalid values fail before mutation.</summary>
    [ComponentUnitEvidence(typeof(Dock))]
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var panel = new Dock();
        var child = new ProbeControl();

        panel.LastChildFills.ShouldBeTrue();
        panel.Spacing.ShouldBe(0);
        Dock.GetSide(child).ShouldBe(DockSide.Left);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => panel.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Dock.SetSide(child, (DockSide) int.MaxValue));
        panel.Spacing.ShouldBe(0);
        Dock.GetSide(child).ShouldBe(DockSide.Left);
    }

    /// <summary>Verifies desired size combines consumed axes and cross-axis maxima.</summary>
    [Fact]
    public void Measure_WhenSidesCombine_ReportsCompleteIntrinsicUnion()
    {
        var panel = new Dock();
        var left = new ProbeControl(new Size(2, 4));
        var top = new ProbeControl(new Size(5, 1));
        var fill = new ProbeControl(new Size(3, 2));
        Dock.SetSide(top, DockSide.Top);
        panel.Children.Add(left);
        panel.Children.Add(top);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        panel.DesiredSize.ShouldBe(new Size(7, 4));
    }

    /// <summary>Verifies all four sides consume in order before the final fill child.</summary>
    [Fact]
    public void Layout_WhenAllSidesAreUsed_LeavesExactFinalRectangle()
    {
        var panel = new Dock();
        var left = WidthOnly(2);
        var top = HeightOnly(1);
        var right = WidthOnly(2);
        var bottom = HeightOnly(1);
        var fill = new ProbeControl();
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(right, DockSide.Right);
        Dock.SetSide(bottom, DockSide.Bottom);
        panel.Children.Add(left);
        panel.Children.Add(top);
        panel.Children.Add(right);
        panel.Children.Add(bottom);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        left.Bounds.ShouldBe(new Rect(0, 0, 2, 6));
        top.Bounds.ShouldBe(new Rect(2, 0, 8, 1));
        right.Bounds.ShouldBe(new Rect(8, 1, 2, 5));
        bottom.Bounds.ShouldBe(new Rect(2, 5, 6, 1));
        fill.Bounds.ShouldBe(new Rect(2, 1, 6, 4));
    }

    /// <summary>Verifies spacing is consumed after a docked strip and before remaining content.</summary>
    [Fact]
    public void Layout_WhenSpacingIsSet_ReservesGapAfterConsumedChild()
    {
        var panel = new Dock { Spacing = 1 };
        var left = WidthOnly(2);
        var fill = new ProbeControl();
        panel.Children.Add(left);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(8, 3));

        left.Bounds.ShouldBe(new Rect(0, 0, 2, 3));
        fill.Bounds.ShouldBe(new Rect(3, 0, 5, 3));
    }

    /// <summary>Verifies percentages resolve against each iteration's remaining axis.</summary>
    [Fact]
    public void Layout_WhenSequentialChildrenUsePercent_UsesCurrentRemainingRectangle()
    {
        var panel = new Dock();
        var first = new ProbeControl { Width = Length.Percent(50) };
        var second = new ProbeControl { Width = Length.Percent(50) };
        var fill = new ProbeControl();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(10, 1));

        first.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        second.Bounds.ShouldBe(new Rect(5, 0, 3, 1));
        fill.Bounds.ShouldBe(new Rect(8, 0, 2, 1));
    }

    /// <summary>Verifies disabling final fill applies the final child's side and requested size.</summary>
    [Fact]
    public void Layout_WhenLastChildDoesNotFill_ConsumesItsConfiguredSide()
    {
        var panel = new Dock { LastChildFills = false };
        var first = WidthOnly(2);
        var last = WidthOnly(3);
        Dock.SetSide(last, DockSide.Right);
        panel.Children.Add(first);
        panel.Children.Add(last);

        new LayoutEngine().Layout(panel, new Size(10, 2));

        first.Bounds.ShouldBe(new Rect(0, 0, 2, 2));
        last.Bounds.ShouldBe(new Rect(7, 0, 3, 2));
    }

    /// <summary>Verifies collapsed children consume no edge or spacing and cannot become fill.</summary>
    [Fact]
    public void Layout_WhenChildIsCollapsed_SkipsItsGeometryEntirely()
    {
        var panel = new Dock { Spacing = 1 };
        var collapsed = WidthOnly(4);
        collapsed.Visibility = Visibility.Collapsed;
        var fill = new ProbeControl();
        panel.Children.Add(collapsed);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(6, 2));

        collapsed.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 6, 2));
    }

    /// <summary>Verifies oversized requests saturate without negative or outside rectangles.</summary>
    [Fact]
    public void Layout_WhenChildrenOverConsume_RemainingBoundsSaturateAtZero()
    {
        var panel = new Dock();
        var first = WidthOnly(8);
        var second = WidthOnly(8);
        var fill = new ProbeControl();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(3, 1));

        first.Bounds.ShouldBe(new Rect(0, 0, 3, 1));
        second.Bounds.Width.ShouldBe(0);
        fill.Bounds.Width.ShouldBe(0);
        second.Bounds.X.ShouldBe(3);
        fill.Bounds.X.ShouldBe(3);
    }

    /// <summary>Verifies side mutation invalidates only an owning Dock and enforces affinity.</summary>
    [Fact]
    public async Task SetSide_WhenChildIsOwned_InvalidatesMeasureAndRequiresDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var panel = new Dock();
        var child = new ProbeControl();
        panel.Children.Add(child);
        panel.Clear(Invalidation.All);

        Dock.SetSide(child, DockSide.Top);
        panel.Pending.ShouldBe(Invalidation.All);
        await dispatcher.InvokeAsync(
            () => panel.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Dock.SetSide(child, DockSide.Right));

        Dock.GetSide(child).ShouldBe(DockSide.Top);
    }

    /// <summary>Verifies an empty dock produces zero desired size.</summary>
    [Fact]
    public void Layout_WhenEmpty_MeasuresToZero()
    {
        var panel = new Dock { Spacing = 5 };

        new LayoutEngine().Layout(panel, new Size(10, 10));

        panel.DesiredSize.ShouldBe(default);
    }

    /// <summary>Verifies a single fill child occupies the full bounds.</summary>
    [Fact]
    public void Layout_WhenSingleFillChild_OccupiesFullBounds()
    {
        var panel = new Dock();
        var child = new ProbeControl();
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 5));

        child.Bounds.ShouldBe(new Rect(0, 0, 10, 5));
    }

    /// <summary>Verifies Top and Bottom sides consume horizontal edges correctly.</summary>
    [Fact]
    public void Layout_WhenTopAndBottomUsed_ReservesHorizontalStrips()
    {
        var panel = new Dock();
        var top = HeightOnly(2);
        var bottom = HeightOnly(3);
        var fill = new ProbeControl();
        Dock.SetSide(top, DockSide.Top);
        Dock.SetSide(bottom, DockSide.Bottom);
        panel.Children.Add(top);
        panel.Children.Add(bottom);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(8, 10));

        top.Bounds.ShouldBe(new Rect(0, 0, 8, 2));
        bottom.Bounds.ShouldBe(new Rect(0, 7, 8, 3));
        fill.Bounds.ShouldBe(new Rect(0, 2, 8, 5));
    }

    /// <summary>Verifies spacing is consumed from remaining space after each docked child.</summary>
    [Fact]
    public void Layout_WhenMultipleSidesWithSpacing_ReservesGapAfterEachDockedChild()
    {
        var panel = new Dock { Spacing = 2 };
        var left = WidthOnly(3);
        var top = HeightOnly(2);
        Dock.SetSide(top, DockSide.Top);
        var fill = new ProbeControl();
        panel.Children.Add(left);
        panel.Children.Add(top);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        left.Bounds.ShouldBe(new Rect(0, 0, 3, 10));
        top.Bounds.X.ShouldBe(5);
        fill.Bounds.X.ShouldBe(5);
        fill.Bounds.Y.ShouldBe(4);
    }

    /// <summary>Verifies hidden children retain their dock edge slot.</summary>
    [Fact]
    public void Layout_WhenDockedChildIsHidden_RetainsEdgeSlot()
    {
        var panel = new Dock();
        var left = WidthOnly(3);
        left.Visibility = Visibility.Hidden;
        var fill = new ProbeControl();
        panel.Children.Add(left);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(10, 2));

        left.Bounds.Width.ShouldBe(3);
        fill.Bounds.X.ShouldBe(3);
    }

    /// <summary>Verifies border and padding on dock panel reserve edges before children.</summary>
    [Fact]
    public void Layout_WhenPanelHasBorderAndPadding_ChildrenArrangeInsideContentBox()
    {
        var panel = new Dock
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1, 0)
        };
        var fill = new ProbeControl();
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        fill.Bounds.X.ShouldBeGreaterThanOrEqualTo(2);
        fill.Bounds.Y.ShouldBeGreaterThanOrEqualTo(1);
        fill.Bounds.Right.ShouldBeLessThanOrEqualTo(18);
        fill.Bounds.Bottom.ShouldBeLessThanOrEqualTo(9);
    }

    /// <summary>Verifies margins on docked children are external to their border box.</summary>
    [Fact]
    public void Layout_WhenDockedChildHasMargin_MarginIsExternalToEdge()
    {
        var panel = new Dock();
        var left = new ProbeControl { Width = Length.Cells(4), Margin = new Thickness(1) };
        var fill = new ProbeControl();
        panel.Children.Add(left);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        left.Bounds.X.ShouldBe(1);
        left.Bounds.Width.ShouldBe(4);
        fill.Bounds.X.ShouldBe(6);
    }

    /// <summary>Verifies LastChildFills=false with a single child docks it to its side.</summary>
    [Fact]
    public void Layout_WhenLastChildFillsDisabledWithSingleChild_DocksToSide()
    {
        var panel = new Dock { LastChildFills = false };
        var child = WidthOnly(4);
        Dock.SetSide(child, DockSide.Right);
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 2));

        child.Bounds.ShouldBe(new Rect(6, 0, 4, 2));
    }

    /// <summary>Verifies two equal-weight Star siblings on the same side split the
    /// remaining axis proportionally instead of the first claiming it all.</summary>
    [Fact]
    public void Layout_WhenTwoEqualStarSiblingsShareASide_SplitsRemainingSpaceEvenly()
    {
        var panel = new Dock { LastChildFills = false };
        var first = new ProbeControl { Width = Length.Star(1) };
        var second = new ProbeControl { Width = Length.Star(1) };
        Dock.SetSide(first, DockSide.Left);
        Dock.SetSide(second, DockSide.Left);
        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        first.Bounds.ShouldBe(new Rect(0, 0, 50, 1));
        second.Bounds.ShouldBe(new Rect(50, 0, 50, 1));
    }

    /// <summary>Verifies unequal Star weights on the same side split remaining space proportionally.</summary>
    [Fact]
    public void Layout_WhenStarSiblingsHaveUnequalWeight_SplitsProportionally()
    {
        var panel = new Dock { LastChildFills = false };
        var single = new ProbeControl { Width = Length.Star(1) };
        var doubled = new ProbeControl { Width = Length.Star(2) };
        Dock.SetSide(single, DockSide.Left);
        Dock.SetSide(doubled, DockSide.Left);
        panel.Children.Add(single);
        panel.Children.Add(doubled);

        new LayoutEngine().Layout(panel, new Size(90, 1));

        single.Bounds.Width.ShouldBe(30);
        doubled.Bounds.Width.ShouldBe(60);
    }

    /// <summary>Verifies Star children on the horizontal axis are unaffected by an
    /// unrelated vertical-axis sibling's own Star share.</summary>
    [Fact]
    public void Layout_WhenStarSiblingsSpanDifferentAxes_EachAxisAllocatesIndependently()
    {
        var panel = new Dock { LastChildFills = false };
        var left = new ProbeControl { Width = Length.Star(1) };
        var top = new ProbeControl { Height = Length.Star(1) };
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(top, DockSide.Top);
        panel.Children.Add(left);
        panel.Children.Add(top);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        left.Bounds.Width.ShouldBe(20);
        top.Bounds.Height.ShouldBe(10);
    }

    /// <summary>Verifies a Star clipped by MaxWidth redistributes the clipped remainder to an
    /// eligible sibling instead of leaving it unused.</summary>
    [Fact]
    public void Layout_WhenStarSiblingIsClippedByMaxWidth_RedistributesRemainderToOtherStar()
    {
        var panel = new Dock { LastChildFills = false };
        var clipped = new ProbeControl { Width = Length.Star(1), MaxWidth = 10 };
        var free = new ProbeControl { Width = Length.Star(1) };
        Dock.SetSide(clipped, DockSide.Left);
        Dock.SetSide(free, DockSide.Left);
        panel.Children.Add(clipped);
        panel.Children.Add(free);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        clipped.Bounds.Width.ShouldBe(10);
        free.Bounds.Width.ShouldBe(90);
    }

    /// <summary>Verifies the same MaxWidth-clipped remainder reaches the eligible Star sibling
    /// instead of the fill child when LastChildFills is enabled.</summary>
    [Fact]
    public void Layout_WhenStarSiblingIsClippedWithLastChildFillsEnabled_RedistributesToOtherStarNotFill()
    {
        var panel = new Dock { LastChildFills = true };
        var clipped = new ProbeControl { Width = Length.Star(1), MaxWidth = 10 };
        var free = new ProbeControl { Width = Length.Star(1) };
        var fill = new ProbeControl();
        Dock.SetSide(clipped, DockSide.Left);
        Dock.SetSide(free, DockSide.Left);
        Dock.SetSide(fill, DockSide.Left);
        panel.Children.Add(clipped);
        panel.Children.Add(free);
        panel.Children.Add(fill);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        clipped.Bounds.Width.ShouldBe(10);
        free.Bounds.Width.ShouldBe(90);
        fill.Bounds.Width.ShouldBe(0);
    }

    /// <summary>Verifies a Star inflated by MinWidth reserves that minimum first and then shares
    /// the remainder by weight, rather than the second Star losing cells it was allocated.</summary>
    [Fact]
    public void Layout_WhenStarSiblingHasMinWidth_ReservesMinimumThenSplitsRemainderByWeight()
    {
        var panel = new Dock { LastChildFills = false };
        var inflated = new ProbeControl { Width = Length.Star(1), MinWidth = 80 };
        var free = new ProbeControl { Width = Length.Star(1) };
        Dock.SetSide(inflated, DockSide.Left);
        Dock.SetSide(free, DockSide.Left);
        panel.Children.Add(inflated);
        panel.Children.Add(free);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        inflated.Bounds.Width.ShouldBe(90);
        free.Bounds.Width.ShouldBe(10);
    }

    /// <summary>Verifies a Star followed by a Percent sibling resolves the Percent against the
    /// axis excluding the Star's own share, so the pair fully covers the axis instead of leaving
    /// a gap.</summary>
    [Fact]
    public void Layout_WhenStarPrecedesPercentSibling_FullyCoversTheAxis()
    {
        var panel = new Dock { LastChildFills = false };
        var star = new ProbeControl { Width = Length.Star(1) };
        var percent = new ProbeControl { Width = Length.Percent(50) };
        Dock.SetSide(star, DockSide.Left);
        Dock.SetSide(percent, DockSide.Left);
        panel.Children.Add(star);
        panel.Children.Add(percent);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        star.Bounds.Width.ShouldBe(50);
        percent.Bounds.Width.ShouldBe(50);
        percent.Bounds.Right.ShouldBe(100);
    }

    /// <summary>Verifies the declaration order between a Star and a Percent sibling does not
    /// change the resolved split, matching the Percent-precedes-Star order.</summary>
    [Fact]
    public void Layout_WhenPercentPrecedesStarSibling_ResolvesTheSameSplitAsTheReverseOrder()
    {
        var panel = new Dock { LastChildFills = false };
        var percent = new ProbeControl { Width = Length.Percent(50) };
        var star = new ProbeControl { Width = Length.Star(1) };
        Dock.SetSide(percent, DockSide.Left);
        Dock.SetSide(star, DockSide.Left);
        panel.Children.Add(percent);
        panel.Children.Add(star);

        new LayoutEngine().Layout(panel, new Size(100, 1));

        percent.Bounds.Width.ShouldBe(50);
        star.Bounds.Width.ShouldBe(50);
    }

    private static ProbeControl HeightOnly(int height) => new() { Height = Length.Cells(height) };

    private static ProbeControl WidthOnly(int width) => new() { Width = Length.Cells(width) };
}
