// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using Panel = Stack;

/// <summary>Verifies sequential panel measurement, allocation, ordering, and rendering.</summary>
public sealed class StackTests
{
    /// <summary>Verifies defaults and invalid setters preserve prior state.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        Panel panel = new Panel();

        panel.Orientation.ShouldBe(Orientation.Vertical);
        panel.Spacing.ShouldBe(0);
        panel.Reverse.ShouldBeFalse();
        _ = Should.Throw<ArgumentOutOfRangeException>(() => panel.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => panel.Orientation = (Orientation) int.MaxValue);
        panel.Spacing.ShouldBe(0);
        panel.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies vertical automatic children use intrinsic height and width.</summary>
    [Fact]
    public void Layout_WhenVerticalChildrenAreAutomatic_ArrangesSequentialIntrinsicHeights()
    {
        Panel panel = new Panel { Spacing = 1 };
        ProbeControl first = new ProbeControl(new Size(3, 2));
        ProbeControl second = new ProbeControl(new Size(4, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);

        new Engine().Layout(panel, new Size(10, 6));

        first.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        second.Bounds.ShouldBe(new Rect(0, 3, 4, 1));
        panel.DesiredSize.ShouldBe(new Size(4, 4));
    }

    /// <summary>Verifies fixed, percentage, and star widths resolve exactly once.</summary>
    [Fact]
    public void Layout_WhenHorizontalLengthsAreMixed_AllocatesFinalAxisExactly()
    {
        Panel panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ProbeControl fixedChild = new ProbeControl { Width = Length.Cells(3) };
        ProbeControl percentChild = new ProbeControl { Width = Length.Percent(25) };
        ProbeControl starChild = new ProbeControl { Width = Length.Star(1) };
        panel.Children.Add(fixedChild);
        panel.Children.Add(percentChild);
        panel.Children.Add(starChild);

        new Engine().Layout(panel, new Size(20, 2));

        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 5, 2));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 2));
    }

    /// <summary>Verifies collapsed children consume neither a track nor adjacent spacing.</summary>
    [Fact]
    public void Layout_WhenMiddleChildIsCollapsed_RemovesItsTrackAndSpacing()
    {
        Panel panel = new Panel { Spacing = 1 };
        ProbeControl first = new ProbeControl(new Size(1, 1));
        ProbeControl collapsed = new ProbeControl(new Size(1, 4))
        {
            Visibility = Visibility.Collapsed,
        };
        ProbeControl last = new ProbeControl(new Size(1, 1));
        panel.Children.Add(first);
        panel.Children.Add(collapsed);
        panel.Children.Add(last);

        new Engine().Layout(panel, new Size(2, 5));

        first.Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        collapsed.Bounds.ShouldBe(default);
        last.Bounds.ShouldBe(new Rect(0, 2, 1, 1));
        panel.DesiredSize.Height.ShouldBe(3);
    }

    /// <summary>Verifies margins remain external to child border boxes and stack spacing.</summary>
    [Fact]
    public void Layout_WhenChildrenHaveMargins_ConsumesOuterEdgesWithoutCollapsing()
    {
        Panel panel = new Panel { Spacing = 1 };
        ProbeControl first = new ProbeControl(new Size(1, 1)) { Margin = new Thickness(1) };
        ProbeControl second = new ProbeControl(new Size(1, 1));
        panel.Children.Add(first);
        panel.Children.Add(second);

        new Engine().Layout(panel, new Size(5, 6));

        first.Bounds.ShouldBe(new Rect(1, 1, 1, 1));
        second.Bounds.ShouldBe(new Rect(0, 4, 1, 1));
    }

    /// <summary>Verifies reverse order changes geometry, cells, and default focus traversal.</summary>
    [Fact]
    public async Task Reverse_WhenEnabled_ReversesVisualAndNavigationOrderAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        Panel panel = new Panel { Reverse = true };
        ProbeControl first = new ProbeControl(new Size(1, 1))
        {
            CanFocus = true,
            Content = "A".AsMemory(),
        };
        ProbeControl second = new ProbeControl(new Size(1, 1))
        {
            CanFocus = true,
            Content = "B".AsMemory(),
        };
        panel.Children.Add(first);
        panel.Children.Add(second);
        new Engine().Layout(panel, new Size(1, 2));
        using Frame frame = new Frame(new Size(1, 2));
        panel.Render(frame.Canvas);

        first.Bounds.Y.ShouldBe(1);
        second.Bounds.Y.ShouldBe(0);
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("B");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");

        await dispatcher.InvokeAsync(() =>
        {
            panel.Attach(dispatcher);
            using FocusManager focus = new FocusManager(panel);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies resize recomputes percentage and star allocation deterministically.</summary>
    [Fact]
    public void Layout_WhenViewportChanges_ReallocatesDeferredLengths()
    {
        Panel panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        ProbeControl percent = new ProbeControl { Width = Length.Percent(50) };
        ProbeControl star = new ProbeControl { Width = Length.Star(1) };
        panel.Children.Add(percent);
        panel.Children.Add(star);
        Engine engine = new Engine();

        engine.Layout(panel, new Size(9, 1));
        percent.Bounds.Width.ShouldBe(5);
        star.Bounds.Width.ShouldBe(4);

        engine.Layout(panel, new Size(13, 1));
        percent.Bounds.Width.ShouldBe(7);
        star.Bounds.Width.ShouldBe(6);
    }

    /// <summary>Verifies over-requested fixed tracks stay within a tiny final axis.</summary>
    [Fact]
    public void Layout_WhenFixedTracksOverflowTinyBounds_ContainsEveryChild()
    {
        Panel panel = new Panel { Orientation = Orientation.Horizontal };
        ProbeControl first = new ProbeControl { Width = Length.Cells(5) };
        ProbeControl second = new ProbeControl { Width = Length.Cells(5) };
        panel.Children.Add(first);
        panel.Children.Add(second);

        new Engine().Layout(panel, new Size(3, 1));

        first.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        second.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        first.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
        second.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>Verifies property changes request the earliest required phase.</summary>
    [Fact]
    public void PropertySetter_WhenStackPolicyChanges_InvalidatesPrecisePhase()
    {
        Panel panel = new Panel();
        panel.Clear(Invalidation.All);

        panel.Spacing = 1;
        panel.Pending.ShouldBe(Invalidation.All);
        panel.Clear(Invalidation.All);
        panel.Reverse = true;
        panel.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
        panel.Clear(Invalidation.All);
        panel.Orientation = Orientation.Horizontal;
        panel.Pending.ShouldBe(Invalidation.All);
    }
}
