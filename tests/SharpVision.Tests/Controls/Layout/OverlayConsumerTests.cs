// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;


/// <summary>Verifies attached offset validation, positioned layout, clipping, and targeting.</summary>
public sealed class OverlayConsumerTests
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

    /// <summary>Verifies opposing offsets stretch a proportional dimension between them.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSize_StretchesBetweenEdges()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1), Height = Length.Star(1) };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(5));
        Overlay.SetTop(child, Length.Cells(5));
        Overlay.SetBottom(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 40));

        child.Bounds.ShouldBe(new Rect(5, 5, 30, 30));
    }

    /// <summary>Verifies a stretched proportional width below MinWidth clamps up to MinWidth.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeBelowMinWidth_ClampsToMinWidth()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1), MinWidth = Length.Cells(20) };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(30));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(5, 0, 20, 10));
    }

    /// <summary>Verifies a positioned child resolves a percentage ceiling from the Overlay's
    /// complete content axis once, then re-resolves from the resized axis.</summary>
    [Fact]
    public void Layout_WhenPositionedChildMaximumIsRelative_UsesOverlayExtentWithoutDoubleClamping()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Star(1),
            MaxWidth = Length.Percent(50)
        };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(5));
        panel.Children.Add(child);
        var engine = new LayoutEngine();

        engine.Layout(panel, new Size(40, 2));
        child.Bounds.ShouldBe(new Rect(5, 0, 20, 2));

        engine.Layout(panel, new Size(80, 2));
        child.Bounds.ShouldBe(new Rect(5, 0, 40, 2));
    }

    /// <summary>Verifies a margined child stretched below MinWidth clamps its content box to
    /// MinWidth rather than landing short by the margin, matching how the single-offset Star path
    /// already clamps the pre-margin extent before adding margin back for the outer slot.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeBelowMinWidthWithMargin_ClampsContentToMinWidth()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Star(1),
            MinWidth = Length.Cells(20),
            Margin = new Thickness(2, 0, 2, 0)
        };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(30));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(7, 0, 20, 10));
    }

    /// <summary>Verifies a margined child stretched by opposing offsets with no Min/Max clamp in
    /// effect stays fully inside the two offset marks, deflating margin from the offset-to-offset
    /// extent rather than adding it on top - the far edge in this case is a hard boundary, unlike
    /// the single-offset Star path where only one edge is anchored.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeWithMarginAndNoClamp_KeepsChildInsideOffsets()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1))
        {
            Width = Length.Star(1),
            Height = Length.Star(1),
            Margin = new Thickness(2)
        };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(5));
        Overlay.SetTop(child, Length.Cells(5));
        Overlay.SetBottom(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 40));

        child.Bounds.ShouldBe(new Rect(7, 7, 26, 26));
    }

    /// <summary>Verifies a stretched proportional width above MaxWidth clamps down to MaxWidth.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeAboveMaxWidth_ClampsToMaxWidth()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1), MaxWidth = Length.Cells(10) };
        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.SetRight(child, Length.Cells(2));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(2, 0, 10, 10));
    }

    /// <summary>Verifies a stretched proportional height below MinHeight clamps up to MinHeight.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeBelowMinHeight_ClampsToMinHeight()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Height = Length.Star(1), MinHeight = Length.Cells(20) };
        Overlay.SetTop(child, Length.Cells(5));
        Overlay.SetBottom(child, Length.Cells(30));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 5, 1, 20));
    }

    /// <summary>Verifies a stretched proportional height above MaxHeight clamps down to MaxHeight.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndStarSizeAboveMaxHeight_ClampsToMaxHeight()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Height = Length.Star(1), MaxHeight = Length.Cells(10) };
        Overlay.SetTop(child, Length.Cells(2));
        Overlay.SetBottom(child, Length.Cells(2));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 2, 1, 10));
    }

    /// <summary>Verifies a stretched automatic width below MinWidth clamps up to MinWidth.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSizeBelowMinWidth_ClampsToMinWidth()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { MinWidth = Length.Cells(20) };
        Overlay.SetLeft(child, Length.Cells(5));
        Overlay.SetRight(child, Length.Cells(30));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(5, 0, 20, 10));
    }

    /// <summary>Verifies a stretched automatic width above MaxWidth clamps down to MaxWidth.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSizeAboveMaxWidth_ClampsToMaxWidth()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { MaxWidth = Length.Cells(10) };
        Overlay.SetLeft(child, Length.Cells(2));
        Overlay.SetRight(child, Length.Cells(2));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(2, 0, 10, 10));
    }

    /// <summary>Verifies a stretched automatic height below MinHeight clamps up to MinHeight.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSizeBelowMinHeight_ClampsToMinHeight()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { MinHeight = Length.Cells(20) };
        Overlay.SetTop(child, Length.Cells(5));
        Overlay.SetBottom(child, Length.Cells(30));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 5, 1, 20));
    }

    /// <summary>Verifies a stretched automatic height above MaxHeight clamps down to MaxHeight.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSizeAboveMaxHeight_ClampsToMaxHeight()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { MaxHeight = Length.Cells(10) };
        Overlay.SetTop(child, Length.Cells(2));
        Overlay.SetBottom(child, Length.Cells(2));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 2, 1, 10));
    }

    /// <summary>Verifies a lone leading offset stretches a proportional dimension to the far edge.</summary>
    [Fact]
    public void Layout_WhenLeftOffsetAndStarSize_StretchesToFarEdge()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        Overlay.SetLeft(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(5, 0, 35, 10));
    }

    /// <summary>Verifies a lone trailing offset stretches a proportional dimension to the far edge.</summary>
    [Fact]
    public void Layout_WhenRightOffsetAndStarSize_StretchesToFarEdge()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        Overlay.SetRight(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 35, 10));
    }

    /// <summary>Verifies a lone top offset stretches a proportional dimension to the far edge.</summary>
    [Fact]
    public void Layout_WhenTopOffsetAndStarSize_StretchesToFarEdge()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Height = Length.Star(1) };
        Overlay.SetTop(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 5, 1, 35));
    }

    /// <summary>Verifies a lone bottom offset stretches a proportional dimension to the far edge.</summary>
    [Fact]
    public void Layout_WhenBottomOffsetAndStarSize_StretchesToFarEdge()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Height = Length.Star(1) };
        Overlay.SetBottom(child, Length.Cells(5));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 40));

        child.Bounds.ShouldBe(new Rect(0, 0, 1, 35));
    }

    /// <summary>Verifies an unpositioned proportional child fills the shared slot, unaffected by offset resolution.</summary>
    [Fact]
    public void Layout_WhenNoOffsetsAndStarSize_FillsSharedSlot()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(3, 2)) { Width = Length.Star(1), Height = Length.Star(1) };
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(0, 0, 10, 6));
    }

    /// <summary>Verifies a single offset exceeding the axis clamps a proportional extent at zero.</summary>
    [Fact]
    public void Layout_WhenSingleOffsetOverflowsAxis_ClampsStarExtentToZero()
    {
        var panel = new Overlay();
        var child = new ProbeControl(new Size(1, 1)) { Width = Length.Star(1) };
        Overlay.SetLeft(child, Length.Cells(50));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(40, 1));

        child.Bounds.ShouldBe(new Rect(50, 0, 0, 1));
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
        var second = new ProbeControl { Bounds = new Rect(0, 0, 1, 1), IsHitTestVisible = false };
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

    /// <summary>Verifies attached positions default to null.</summary>
    [Fact]
    public void Position_WhenDefaulted_IsNull()
    {
        var control = new ProbeControl();

        Overlay.GetLeft(control).ShouldBeNull();
        Overlay.GetTop(control).ShouldBeNull();
        Overlay.GetRight(control).ShouldBeNull();
        Overlay.GetBottom(control).ShouldBeNull();
    }

    /// <summary>Verifies cells and percent values are accepted.</summary>
    [Fact]
    public void Position_WhenCellsOrPercent_Accepted()
    {
        var control = new ProbeControl();
        Overlay.SetLeft(control, Length.Cells(5));
        Overlay.SetTop(control, Length.Percent(50));
        Overlay.SetRight(control, Length.Cells(3));

        Overlay.GetLeft(control).ShouldBe(Length.Cells(5));
        Overlay.GetTop(control).ShouldBe(Length.Percent(50));
        Overlay.GetRight(control).ShouldBe(Length.Cells(3));

        Overlay.SetBottom(control, null);
        Overlay.GetBottom(control).ShouldBeNull();
    }

    /// <summary>Verifies Auto values are rejected on all four attached positions.</summary>
    [Fact]
    public void Position_WhenAuto_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetTop(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetRight(control, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetBottom(control, Length.Auto));
    }

    /// <summary>Verifies Star values are rejected on all four attached positions.</summary>
    [Fact]
    public void Position_WhenStar_Throws()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Star(1)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetTop(control, Length.Star(2)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetRight(control, Length.Star(1)));
        _ = Should.Throw<ArgumentException>(() => Overlay.SetBottom(control, Length.Star(1)));
    }

    /// <summary>Verifies rejected values do not mutate the attached value.</summary>
    [Fact]
    public void Position_WhenRejected_PreservesOldValue()
    {
        var control = new ProbeControl();
        Overlay.SetLeft(control, Length.Cells(10));

        _ = Should.Throw<ArgumentException>(() => Overlay.SetLeft(control, Length.Auto));

        Overlay.GetLeft(control).ShouldBe(Length.Cells(10));
    }

    /// <summary>Verifies changing an attached position invalidates the parent Overlay's measure pass.</summary>
    [Fact]
    public void Position_WhenChanged_InvalidatesParentMeasure()
    {
        var overlay = new Overlay();
        var child = new ProbeControl();
        overlay.Children.Add(child);
        overlay.Clear(Invalidation.All);

        Overlay.SetLeft(child, Length.Cells(1));

        overlay.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies setting the same value is a no-op.</summary>
    [Fact]
    public void Position_WhenSameValue_DoesNotInvalidate()
    {
        var overlay = new Overlay();
        var child = new ProbeControl();
        overlay.Children.Add(child);
        Overlay.SetLeft(child, Length.Cells(5));
        overlay.Clear(Invalidation.All);

        Overlay.SetLeft(child, Length.Cells(5));

        overlay.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies position requires dispatcher affinity when attached.</summary>
    [Fact]
    public async Task Position_WhenAttached_RequiresDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeContainer();
        var child = new ProbeControl();
        root.Children.Add(child);

        await dispatcher.InvokeAsync(
            () => root.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Overlay.SetLeft(child, Length.Cells(5)));
    }

    /// <summary>Verifies Overlay layout reads position from attached storage.</summary>
    [Fact]
    public void OverlayLayout_WhenControlHasPosition_ArrangesCorrectly()
    {
        var canvas = new Overlay();
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(child, Length.Cells(4));
        Overlay.SetTop(child, Length.Cells(2));
        canvas.Children.Add(child);

        new LayoutEngine().Layout(canvas, new Size(12, 8));

        child.Bounds.ShouldBe(new Rect(4, 2, 3, 2));
    }
}
