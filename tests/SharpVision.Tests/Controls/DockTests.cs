// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using Panel = Dock;

/// <summary>Verifies deterministic edge consumption and remaining-space layout.</summary>
public sealed class DockTests
{
    /// <summary>Verifies defaults and invalid values fail before mutation.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl();

        panel.LastChildFills.ShouldBeTrue();
        panel.Spacing.ShouldBe(0);
        Panel.GetSide(child).ShouldBe(Side.Left);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => panel.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => Panel.SetSide(child, (Side) int.MaxValue));
        panel.Spacing.ShouldBe(0);
        Panel.GetSide(child).ShouldBe(Side.Left);
    }

    /// <summary>Verifies desired size combines consumed axes and cross-axis maxima.</summary>
    [Fact]
    public void Measure_WhenSidesCombine_ReportsCompleteIntrinsicUnion()
    {
        Panel panel = new Panel();
        ProbeControl left = new ProbeControl(new Size(2, 4));
        ProbeControl top = new ProbeControl(new Size(5, 1));
        ProbeControl fill = new ProbeControl(new Size(3, 2));
        Panel.SetSide(top, Side.Top);
        panel.Children.Add(left);
        panel.Children.Add(top);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(20, 10));

        panel.DesiredSize.ShouldBe(new Size(7, 4));
    }

    /// <summary>Verifies all four sides consume in order before the final fill child.</summary>
    [Fact]
    public void Layout_WhenAllSidesAreUsed_LeavesExactFinalRectangle()
    {
        Panel panel = new Panel();
        ProbeControl left = WidthOnly(2);
        ProbeControl top = HeightOnly(1);
        ProbeControl right = WidthOnly(2);
        ProbeControl bottom = HeightOnly(1);
        ProbeControl fill = new ProbeControl();
        Panel.SetSide(left, Side.Left);
        Panel.SetSide(top, Side.Top);
        Panel.SetSide(right, Side.Right);
        Panel.SetSide(bottom, Side.Bottom);
        panel.Children.Add(left);
        panel.Children.Add(top);
        panel.Children.Add(right);
        panel.Children.Add(bottom);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(10, 6));

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
        Panel panel = new Panel { Spacing = 1 };
        ProbeControl left = WidthOnly(2);
        ProbeControl fill = new ProbeControl();
        panel.Children.Add(left);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(8, 3));

        left.Bounds.ShouldBe(new Rect(0, 0, 2, 3));
        fill.Bounds.ShouldBe(new Rect(3, 0, 5, 3));
    }

    /// <summary>Verifies percentages resolve against each iteration's remaining axis.</summary>
    [Fact]
    public void Layout_WhenSequentialChildrenUsePercent_UsesCurrentRemainingRectangle()
    {
        Panel panel = new Panel();
        ProbeControl first = new ProbeControl { Width = Length.Percent(50) };
        ProbeControl second = new ProbeControl { Width = Length.Percent(50) };
        ProbeControl fill = new ProbeControl();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(10, 1));

        first.Bounds.ShouldBe(new Rect(0, 0, 5, 1));
        second.Bounds.ShouldBe(new Rect(5, 0, 3, 1));
        fill.Bounds.ShouldBe(new Rect(8, 0, 2, 1));
    }

    /// <summary>Verifies disabling final fill applies the final child's side and requested size.</summary>
    [Fact]
    public void Layout_WhenLastChildDoesNotFill_ConsumesItsConfiguredSide()
    {
        Panel panel = new Panel { LastChildFills = false };
        ProbeControl first = WidthOnly(2);
        ProbeControl last = WidthOnly(3);
        Panel.SetSide(last, Side.Right);
        panel.Children.Add(first);
        panel.Children.Add(last);

        new Engine().Layout(panel, new Size(10, 2));

        first.Bounds.ShouldBe(new Rect(0, 0, 2, 2));
        last.Bounds.ShouldBe(new Rect(7, 0, 3, 2));
    }

    /// <summary>Verifies collapsed children consume no edge or spacing and cannot become fill.</summary>
    [Fact]
    public void Layout_WhenChildIsCollapsed_SkipsItsGeometryEntirely()
    {
        Panel panel = new Panel { Spacing = 1 };
        ProbeControl collapsed = WidthOnly(4);
        collapsed.Visibility = Visibility.Collapsed;
        ProbeControl fill = new ProbeControl();
        panel.Children.Add(collapsed);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(6, 2));

        collapsed.Bounds.ShouldBe(default);
        fill.Bounds.ShouldBe(new Rect(0, 0, 6, 2));
    }

    /// <summary>Verifies oversized requests saturate without negative or outside rectangles.</summary>
    [Fact]
    public void Layout_WhenChildrenOverConsume_RemainingBoundsSaturateAtZero()
    {
        Panel panel = new Panel();
        ProbeControl first = WidthOnly(8);
        ProbeControl second = WidthOnly(8);
        ProbeControl fill = new ProbeControl();
        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(fill);

        new Engine().Layout(panel, new Size(3, 1));

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
        await using Dispatcher dispatcher = Dispatcher.Start();
        Panel panel = new Panel();
        ProbeControl child = new ProbeControl();
        panel.Children.Add(child);
        panel.Clear(Invalidation.All);

        Panel.SetSide(child, Side.Top);
        panel.Pending.ShouldBe(Invalidation.All);
        await dispatcher.InvokeAsync(
            () => panel.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => Panel.SetSide(child, Side.Right));

        Panel.GetSide(child).ShouldBe(Side.Top);
    }

    private static ProbeControl HeightOnly(int height) => new() { Height = Length.Cells(height) };

    private static ProbeControl WidthOnly(int width) => new() { Width = Length.Cells(width) };
}
