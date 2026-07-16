// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



using Panel = SharpVision.Controls.Canvas;

/// <summary>Verifies attached offset validation, positioned layout, clipping, and targeting.</summary>
public sealed class CanvasTests
{
    /// <summary>Verifies optional offset defaults and accepted length kinds.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var panel = new Panel();
        var child = new ProbeControl();

        panel.ClipToBounds.ShouldBeTrue();
        Panel.GetLeft(child).ShouldBeNull();
        Panel.GetTop(child).ShouldBeNull();
        Panel.GetRight(child).ShouldBeNull();
        Panel.GetBottom(child).ShouldBeNull();
        _ = Should.Throw<ArgumentException>(() => Panel.SetLeft(child, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => Panel.SetTop(child, Length.Star(1)));
        Panel.GetLeft(child).ShouldBeNull();
        Panel.GetTop(child).ShouldBeNull();
    }

    /// <summary>Verifies leading fixed offsets position an intrinsic child.</summary>
    [Fact]
    public void Layout_WhenLeftAndTopAreSet_PositionsIntrinsicChild()
    {
        var panel = new Panel();
        var child = new ProbeControl(new Size(3, 2));
        Panel.SetLeft(child, Length.Cells(2));
        Panel.SetTop(child, Length.Cells(1));
        panel.Children.Add(child);

        new Engine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 3, 2));
    }

    /// <summary>Verifies trailing offsets position from the final right and bottom edges.</summary>
    [Fact]
    public void Layout_WhenRightAndBottomAreSet_PositionsFromTrailingEdges()
    {
        var panel = new Panel();
        var child = new ProbeControl(new Size(3, 2));
        Panel.SetRight(child, Length.Cells(2));
        Panel.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new Engine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(5, 3, 3, 2));
    }

    /// <summary>Verifies opposing offsets stretch only automatic dimensions.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndAutoSize_StretchesBetweenEdges()
    {
        var panel = new Panel();
        var child = new ProbeControl(new Size(1, 1));
        Panel.SetLeft(child, Length.Cells(2));
        Panel.SetRight(child, Length.Cells(3));
        Panel.SetTop(child, Length.Cells(1));
        Panel.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new Engine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 5, 4));
    }

    /// <summary>Verifies explicit size keeps the leading offset when both offsets exist.</summary>
    [Fact]
    public void Layout_WhenOpposingOffsetsAndExplicitSize_UsesLeadingOffsetAndSize()
    {
        var panel = new Panel();
        var child = new ProbeControl()
        {
            Width = Length.Cells(4),
            Height = Length.Cells(2),
        };
        Panel.SetLeft(child, Length.Cells(2));
        Panel.SetRight(child, Length.Cells(3));
        Panel.SetTop(child, Length.Cells(1));
        Panel.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new Engine().Layout(panel, new Size(10, 6));

        child.Bounds.ShouldBe(new Rect(2, 1, 4, 2));
    }

    /// <summary>Verifies percentage offsets defer in measure and resolve after resize.</summary>
    [Fact]
    public void Layout_WhenOffsetsArePercent_RepositionsAgainstFinalSize()
    {
        var panel = new Panel();
        var child = new ProbeControl(new Size(2, 1));
        Panel.SetLeft(child, Length.Percent(25));
        Panel.SetTop(child, Length.Percent(50));
        panel.Children.Add(child);
        var engine = new Engine();

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
        var panel = new Panel();
        var fixedChild = new ProbeControl(new Size(3, 2));
        var percentChild = new ProbeControl(new Size(4, 1));
        Panel.SetLeft(fixedChild, Length.Cells(2));
        Panel.SetRight(fixedChild, Length.Cells(1));
        Panel.SetLeft(percentChild, Length.Percent(50));
        panel.Children.Add(fixedChild);
        panel.Children.Add(percentChild);

        new Engine().Layout(panel, new Size(20, 10));

        panel.DesiredSize.ShouldBe(new Size(6, 2));
    }

    /// <summary>Verifies oversized trailing placement may produce negative origins safely.</summary>
    [Fact]
    public void Layout_WhenTrailingChildIsOversized_AllowsNegativeFinalOrigin()
    {
        var panel = new Panel();
        var child = new ProbeControl()
        {
            Width = Length.Cells(8),
            Height = Length.Cells(5),
        };
        Panel.SetRight(child, Length.Cells(1));
        Panel.SetBottom(child, Length.Cells(1));
        panel.Children.Add(child);

        new Engine().Layout(panel, new Size(5, 3));

        child.Bounds.ShouldBe(new Rect(-4, -3, 8, 5));
    }

    /// <summary>Verifies disabled clipping allows off-panel drawing and hit testing.</summary>
    [Fact]
    public void ClipToBounds_WhenFalse_AllowsOutsideChildDrawingAndTargeting()
    {
        var panel = new Panel()
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipToBounds = false,
        };
        var child = new ProbeControl()
        {
            Bounds = new Rect(1, 0, 2, 1),
            Content = "界".AsMemory(),
        };
        panel.Children.Add(child);
        using Frame frame = new(new Size(3, 1));

        panel.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        panel.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies collection z-order and hit-test transparency remain deterministic.</summary>
    [Fact]
    public void HitTest_WhenChildrenOverlap_UsesLastEligibleChild()
    {
        var panel = new Panel() { Bounds = new Rect(0, 0, 1, 1) };
        var first = new ProbeControl() { Bounds = new Rect(0, 0, 1, 1) };
        var second = new ProbeControl()
        {
            Bounds = new Rect(0, 0, 1, 1),
            IsHitTestVisible = false,
        };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.HitTest(default).ShouldBeSameAs(first);
    }

    /// <summary>
    /// Verifies an armed Canvas routes hit-testing through its owned bars and
    /// restricts content targeting to the viewport, mirroring the base
    /// Container.HitTest armed contract. Canvas.HitTest is an independent
    /// override (for conditional ClipToBounds) that used to never check
    /// _bars/_viewportBounds at all, so its scrollbars could not be clicked
    /// and clipped gutter content was spuriously hittable.
    /// </summary>
    [Fact]
    public void HitTest_WhenCanvasIsArmed_TargetsBarAndExcludesClippedGutterContent()
    {
        var panel = new Panel()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
        };
        var content = new ProbeControl() { Width = Length.Cells(4), Height = Length.Cells(4) };
        panel.Children.Add(content);

        new Engine().Layout(panel, new Size(4, 4));

        // Column 3 row 1 is the reserved vertical bar's rendered track, even
        // though unclipped content also spans that cell.
        panel.HitTest(new Point(3, 1)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
        // Row 3 column 3 is the dead corner covered by neither bar, and lies
        // outside the viewport, so it must not spuriously hit clipped content.
        panel.HitTest(new Point(3, 3)).ShouldBeSameAs(panel);
    }

    /// <summary>Verifies position offset mutation stores the value and requires dispatcher affinity.</summary>
    [Fact]
    public async Task SetLeft_WhenChildIsOwned_StoresValueAndRequiresDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var panel = new Panel();
        var child = new ProbeControl();
        panel.Children.Add(child);

        Panel.SetLeft(child, Length.Cells(2));
        child.Left.ShouldBe(Length.Cells(2));
        Panel.GetLeft(child).ShouldBe(Length.Cells(2));

        await dispatcher.InvokeAsync(
            () => panel.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(
            () => Panel.SetLeft(child, Length.Cells(3)));

        Panel.GetLeft(child).ShouldBe(Length.Cells(2));
    }
}
