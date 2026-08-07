// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;


/// <summary>Verifies attached offset validation, positioned layout, clipping, and targeting.</summary>
public sealed class OverlayPositionTests
{
    /// <summary>Verifies optional offset defaults and accepted length kinds.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var panel = new Overlay();
        var child = new ProbeControl();

        panel.ClipToBounds.ShouldBeTrue();
        Overlay.GetLeft(child).ShouldBeNull();
        Overlay.GetTop(child).ShouldBeNull();
        _ = Should.Throw<ArgumentNullException>(() => Overlay.GetLeft(null!));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.GetTop(null!));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.GetRight(null!));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.GetBottom(null!));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.SetLeft(null!, null));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.SetTop(null!, null));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.SetRight(null!, null));
        _ = Should.Throw<ArgumentNullException>(() => Overlay.SetBottom(null!, null));
        Overlay.GetRight(child).ShouldBeNull();
        Overlay.GetBottom(child).ShouldBeNull();
        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(child, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetTop(child, Length.Star(1)));
        Overlay.GetLeft(child).ShouldBeNull();
        Overlay.GetTop(child).ShouldBeNull();
    }

    /// <summary>Verifies leading fixed offsets position an intrinsic child.</summary>
    [Fact]
    public void Layout_WhenLeftAndTopAreSet_PositionsIntrinsicChild()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.SetTop(child, Length.Cells(1));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 3, 2));
    }

    /// <summary>Verifies trailing offsets position from the final right and bottom edges.</summary>
    [Fact]
    public void Layout_WhenRightAndBottomAreSet_PositionsFromTrailingEdges()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetRight(child, Length.Cells(2));
        Overlay.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(5, 3, 3, 2));
    }

    /// <summary>Verifies opposing offsets stretch only automatic dimensions.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSize_StretchesBetweenEdges()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1));
        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.SetRight(child, Length.Cells(3));
        Overlay.SetTop(child, Length.Cells(1));
        Overlay.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 5, 4));
    }

    /// <summary>Verifies explicit size keeps the leading offset when both offsets exist.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndExplicitSize_UsesLeadingOffsetAndSize()
    {
        var panel = new Overlay();
        var child = new ProbeControl { Width = Length.Cells(4), Height = Length.Cells(2) };
        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.SetRight(child, Length.Cells(3));
        Overlay.SetTop(child, Length.Cells(1));
        Overlay.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 4, 2));
    }

    /// <summary>Verifies percentage offsets defer in measure and resolve after resize.</summary>
    [Fact]
    public void Layout_WhenOffsetsArePercent_RepositionsAgainstFinalSize()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(2, 1));
        Overlay.SetLeft(child, Length.Percent(25));
        Overlay.SetTop(child, Length.Percent(50));
        panel.Children.Add(child);
        var engine = new LayoutEngine();

        engine.Layout(panel, new Size(8, 4));
        child.Bounds.ShouldBe(new Rect(2, 2, 2, 1));
        panel.DesiredSize.ShouldBe(new Size(2, 1));

        engine.Layout(panel, new Size(12, 6));
        child.Bounds.ShouldBe(new Rect(3, 3, 2, 1));
    }

    /// <summary>Verifies intrinsic union includes fixed offsets but defers percentages.</summary>
    [Fact]
    public void Measure_WhenOffsetsMixFixedAndPercent_ReportsFiniteIntrinsicUnion()
    {
        var panel = new Overlay();
        var fixedChild = new ProbeControl(new Size(3, 2));
        var percentChild = new ProbeControl(new Size(4, 1));
        Overlay.SetLeft(fixedChild, Length.Cells(2));
        Overlay.SetRight(fixedChild, Length.Cells(1));
        Overlay.SetLeft(percentChild, Length.Percent(50));
        panel.Children.Add(fixedChild);
        panel.Children.Add(percentChild);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        panel.DesiredSize.ShouldBe(new Size(6, 2));
    }

    /// <summary>Verifies oversized trailing placement may produce negative origins safely.</summary>
    [Fact]
    public void Layout_WhenTrailingChildIsOversized_AllowsNegativeFinalOrigin()
    {
        var panel = new Overlay();
        var child = new ProbeControl { Width = Length.Cells(8), Height = Length.Cells(5) };
        Overlay.SetRight(child, Length.Cells(1));
        Overlay.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(5, 3));

        child.Bounds.ShouldBe(new Rect(-4, -3, 8, 5));
    }

    /// <summary>Verifies trailing placement clamps an origin that exceeds signed coordinate range.</summary>
    [Fact]
    public void Layout_WhenTrailingOffsetAndExtentOverflow_ClampsFinalOrigin()
    {
        var panel = new Overlay();
        var child = new ProbeControl
        {
            Width = Length.Cells(int.MaxValue),
            Height = Length.Cells(1)
        };
        Overlay.SetRight(child, Length.Cells(int.MaxValue));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(1, 1));

        child.Bounds.ShouldBe(new Rect(int.MinValue, 0, int.MaxValue, 1));
    }

    /// <summary>Verifies opposing automatic offsets clamp a subtractive extent at zero.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsOverflow_ClampsStretchedExtentToZero()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1));
        Overlay.SetLeft(child, Length.Cells(int.MaxValue));
        Overlay.SetRight(child, Length.Cells(int.MaxValue));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(1, 1));

        child.Bounds.ShouldBe(new Rect(int.MaxValue, 0, 0, 1));
    }

    /// <summary>Verifies positioned and shared-slot children keep their independent layout contracts.</summary>
    [Fact]
    public void Arrange_WhenPositionedAndUnpositionedChildrenCoexist_UsesEachLayoutContract()
    {
        var panel = new Overlay();
        var shared = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Percent(50),
            Height = Length.Cells(2),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(1)
        };
        var positioned = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(positioned, Length.Cells(2));
        Overlay.SetTop(positioned, Length.Cells(1));
        panel.Children.Add(shared);
        panel.Children.Add(positioned);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        shared.Bounds.ShouldBe(new Rect(4, 3, 5, 2));
        positioned.Bounds.ShouldBe(new Rect(2, 1, 3, 2));
    }

    /// <summary>Verifies one positioned axis leaves the other axis on shared-slot layout.</summary>
    [Fact]
    public void Arrange_WhenOnlyOneAxisIsPositioned_UsesSharedSlotOnOtherAxis()
    {
        var panel = new Overlay();
        var horizontal = new ProbeControl(new Size(3, 1))
        {
            Height = Length.Cells(2),
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(left: 1, top: 1, right: 2, bottom: 1)
        };
        var vertical = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Percent(50),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Overlay.SetLeft(horizontal, Length.Cells(2));
        Overlay.SetTop(vertical, Length.Cells(1));
        panel.Children.Add(horizontal);
        panel.Children.Add(vertical);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        horizontal.Bounds.ShouldBe(new Rect(3, 3, 3, 2));
        vertical.Bounds.ShouldBe(new Rect(5, 1, 5, 1));
    }

    /// <summary>Verifies attached z-order remains independent from positioned geometry.</summary>
    [Fact]
    public void ZIndex_WhenPositionedChildrenOverlap_ControlsRenderAndHitOrder()
    {
        var panel = new Overlay();
        var high = new ProbeControl(new Size(1, 1)) { Content = "H".AsMemory() };
        var low = new ProbeControl(new Size(1, 1)) { Content = "L".AsMemory() };
        Overlay.SetLeft(high, Length.Cells(0));
        Overlay.SetTop(high, Length.Cells(0));
        Overlay.SetLeft(low, Length.Cells(0));
        Overlay.SetTop(low, Length.Cells(0));
        Overlay.SetZIndex(high, 10);
        Overlay.SetZIndex(low, -3);
        panel.Children.Add(high);
        panel.Children.Add(low);
        new LayoutEngine().Layout(panel, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        panel.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("H");
        panel.HitTest(default).ShouldBeSameAs(high);
    }

    /// <summary>Verifies disabled clipping allows off-panel drawing and hit testing.</summary>
    [Fact]
    public void ClipToBounds_WhenFalse_AllowsOutsideChildDrawingAndTargeting()
    {
        var panel = new Overlay { Bounds = new Rect(0, 0, 1, 1), ClipToBounds = false };
        var child = new ProbeControl { Bounds = new Rect(1, 0, 2, 1), Content = "界".AsMemory() };
        panel.Children.Add(child);
        using Frame frame = new(new Size(3, 1));

        panel.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).Continuation.ShouldBeTrue();
        panel.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies collection z-order and hit-test transparency remain deterministic.</summary>
    [Fact]
    public void HitTest_WhenChildrenOverlap_UsesLastEligibleChild()
    {
        var panel = new Overlay { Bounds = new Rect(0, 0, 1, 1) };
        var first = new ProbeControl { Bounds = new Rect(0, 0, 1, 1) };
        var second = new ProbeControl { Bounds = new Rect(0, 0, 1, 1), HitTestVisible = false };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.HitTest(default).ShouldBeSameAs(first);
    }

    /// <summary>
    /// Verifies an armed Overlay routes hit-testing through its owned bars and
    /// restricts content targeting to the viewport, mirroring the base
    /// Container.HitTest armed contract.
    /// </summary>
    [Fact]
    public void HitTest_WhenOverlayIsArmed_TargetsBarAndExcludesClippedGutterContent()
    {
        var panel = new Overlay
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always
        };
        var content = new ProbeControl { Width = Length.Cells(4), Height = Length.Cells(4) };
        panel.Children.Add(content);

        new LayoutEngine().Layout(panel, new Size(4, 4));

        // Column 3 row 1 is the reserved vertical bar's rendered track, even
        // though unclipped content also spans that cell.
        panel.HitTest(new Point(3, 1)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
        // The horizontal bar owns the shared bottom-right corner so no
        // transparent cell remains between the two generated rails.
        panel.HitTest(new Point(3, 3)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
    }

    /// <summary>Verifies position offset mutation invalidates measure, stores the value, and requires dispatcher affinity.</summary>
    [Fact]
    public async Task SetLeft_WhenChildIsOwned_InvalidatesMeasureAndRequiresDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var panel = new Overlay();
        var child = new ProbeControl();
        panel.Children.Add(child);
        panel.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.GetLeft(child).ShouldBe(Length.Cells(2));
        panel.Pending.ShouldBe(Invalidation.All);

        await dispatcher.InvokeAsync(
            () => panel.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Overlay.SetLeft(child, Length.Cells(3)));

        Overlay.GetLeft(child).ShouldBe(Length.Cells(2));
    }

    /// <summary>Verifies an attached offset survives removal and re-addition to a different Overlay.</summary>
    [Fact]
    public void SetLeft_WhenControlDetachesAndReattaches_RetainsValue()
    {
        var first = new Overlay();
        var second = new Overlay();
        var child = new ProbeControl();
        first.Children.Add(child);

        Overlay.SetLeft(child, Length.Cells(6));
        _ = first.Children.Remove(child);
        second.Children.Add(child);

        Overlay.GetLeft(child).ShouldBe(Length.Cells(6));
    }

    /// <summary>Verifies an attached offset has no meaning outside an Overlay parent.</summary>
    [Fact]
    public void SetLeft_WhenControlHasNoOverlayParent_IsIgnoredByLayout()
    {
        var stack = new Stack();
        var child = new ProbeControl(new Size(3, 2));
        stack.Children.Add(child);

        Overlay.SetLeft(child, Length.Cells(6));
        new LayoutEngine().Layout(stack, new Size(10, 4));

        child.Bounds.X.ShouldBe(0);
    }
}
