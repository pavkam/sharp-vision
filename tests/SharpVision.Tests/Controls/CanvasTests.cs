// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using Panel = SharpVision.Controls.Canvas;

/// <summary>Verifies attached offset validation, positioned layout, clipping, and targeting.</summary>
public sealed class CanvasTests
{
    /// <summary>Verifies optional offset defaults and accepted length kinds.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl();

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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl(new Size(3, 2));
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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl(new Size(3, 2));
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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl(new Size(1, 1));
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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl
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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl(new Size(2, 1));
        Panel.SetLeft(child, Length.Percent(25));
        Panel.SetTop(child, Length.Percent(50));
        panel.Children.Add(child);
        Engine engine = new Engine();

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
        Panel panel = new Panel();
        ProbeControl fixedChild = new ProbeControl(new Size(3, 2));
        ProbeControl percentChild = new ProbeControl(new Size(4, 1));
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
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl
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
        Panel panel = new Panel
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipToBounds = false,
        };
        ProbeControl child = new ProbeControl
        {
            Bounds = new Rect(1, 0, 2, 1),
            Content = "界".AsMemory(),
        };
        panel.Children.Add(child);
        using Frame frame = new Frame(new Size(3, 1));

        panel.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        panel.HitTest(new Point(1, 0)).ShouldBeSameAs(child);
    }

    /// <summary>Verifies collection z-order and hit-test transparency remain deterministic.</summary>
    [Fact]
    public void HitTest_WhenChildrenOverlap_UsesLastEligibleChild()
    {
        Panel panel = new Panel { Bounds = new Rect(0, 0, 1, 1) };
        ProbeControl first = new ProbeControl { Bounds = new Rect(0, 0, 1, 1) };
        ProbeControl second = new ProbeControl
        {
            Bounds = new Rect(0, 0, 1, 1),
            IsHitTestVisible = false,
        };
        panel.Children.Add(first);
        panel.Children.Add(second);

        panel.HitTest(default).ShouldBeSameAs(first);
    }

    /// <summary>Verifies attached offset mutation invalidates measure and requires affinity.</summary>
    [Fact]
    public async Task SetLeft_WhenChildIsOwned_InvalidatesMeasureAndRequiresDispatcherAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl();
        panel.Children.Add(child);
        panel.Clear(Invalidation.All);

        Panel.SetLeft(child, Length.Cells(2));
        panel.Pending.ShouldBe(Invalidation.All);
        await dispatcher.InvokeAsync(
            () => panel.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(
            () => Panel.SetLeft(child, Length.Cells(3)));

        Panel.GetLeft(child).ShouldBe(Length.Cells(2));
    }
}
