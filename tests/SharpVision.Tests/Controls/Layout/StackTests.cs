// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using Panel = Stack;

/// <summary>Verifies sequential panel measurement, allocation, ordering, and rendering.</summary>
public sealed class StackTests
{
    /// <summary>Verifies defaults and invalid setters preserve prior state.</summary>
    [ComponentUnitEvidence(typeof(Panel))]
    [Fact]
    public void Constructor_WhenCreated_HasValidatedDefaults()
    {
        var panel = new Panel();

        panel.Orientation.ShouldBe(Orientation.Vertical);
        panel.Spacing.ShouldBe(0);
        panel.Reverse.ShouldBeFalse();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => panel.Spacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => panel.Orientation = (Orientation) int.MaxValue);

        panel.Spacing.ShouldBe(0);
        panel.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies vertical automatic children use intrinsic height and width.</summary>
    [Fact]
    public void Layout_WhenVerticalChildrenAreAutomatic_ArrangesSequentialIntrinsicHeights()
    {
        var panel = new Panel { Spacing = 1 };
        var first = new ProbeControl(new Size(3, 2));
        var second = new ProbeControl(new Size(4, 1));

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(10, 6));

        first.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        second.Bounds.ShouldBe(new Rect(0, 3, 4, 1));
        panel.DesiredSize.ShouldBe(new Size(4, 4));
    }

    /// <summary>Verifies fixed, percentage, and star widths resolve exactly once.</summary>
    [Fact]
    public void Layout_WhenHorizontalLengthsAreMixed_AllocatesFinalAxisExactly()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var fixedChild = new ProbeControl { Width = Length.Cells(3) };
        var percentChild = new ProbeControl { Width = Length.Percent(25) };
        var starChild = new ProbeControl { Width = Length.Star(1) };

        panel.Children.Add(fixedChild);
        panel.Children.Add(percentChild);
        panel.Children.Add(starChild);

        new LayoutEngine().Layout(panel, new Size(20, 2));

        fixedChild.Bounds.ShouldBe(new Rect(0, 0, 3, 2));
        percentChild.Bounds.ShouldBe(new Rect(5, 0, 5, 2));
        starChild.Bounds.ShouldBe(new Rect(12, 0, 8, 2));
    }

    /// <summary>Verifies collapsed children consume neither a track nor adjacent spacing.</summary>
    [ComponentVisibilityEvidence(
        typeof(Panel),
        ComponentVisibilityEvidence.CollapsedExcludesSize | ComponentVisibilityEvidence.CollapsedRemovesSpacingOrTrack)]
    [Fact]
    public void Layout_WhenMiddleChildIsCollapsed_RemovesItsTrackAndSpacing()
    {
        var panel = new Panel { Spacing = 1 };
        var first = new ProbeControl(new Size(1, 1));
        var collapsed = new ProbeControl(new Size(1, 4)) { Visibility = Visibility.Collapsed };
        var last = new ProbeControl(new Size(1, 1));

        panel.Children.Add(first);
        panel.Children.Add(collapsed);
        panel.Children.Add(last);

        new LayoutEngine().Layout(panel, new Size(2, 5));

        first.Bounds.ShouldBe(new Rect(0, 0, 1, 1));
        collapsed.Bounds.ShouldBe(default);
        last.Bounds.ShouldBe(new Rect(0, 2, 1, 1));
        panel.DesiredSize.Height.ShouldBe(3);
    }

    /// <summary>Verifies margins remain external to child border boxes and stack spacing.</summary>
    [Fact]
    public void Layout_WhenChildrenHaveMargins_ConsumesOuterEdgesWithoutCollapsing()
    {
        var panel = new Panel { Spacing = 1 };
        var first = new ProbeControl(new Size(1, 1)) { Margin = new Thickness(1) };
        var second = new ProbeControl(new Size(1, 1));

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(5, 6));

        first.Bounds.ShouldBe(new Rect(1, 1, 1, 1));
        second.Bounds.ShouldBe(new Rect(0, 4, 1, 1));
    }

    /// <summary>Verifies reverse order changes geometry, cells, and default focus traversal.</summary>
    [Fact]
    public async Task Reverse_WhenEnabled_ReversesVisualAndNavigationOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        var panel = new Panel { Reverse = true };
        var first = new ProbeControl(new Size(1, 1)) { IsFocusable = true, Content = "A".AsMemory() };
        var second = new ProbeControl(new Size(1, 1)) { IsFocusable = true, Content = "B".AsMemory() };

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(1, 2));

        using Frame frame = new(new Size(1, 2));

        panel.Render(frame.Canvas);

        first.Bounds.Y.ShouldBe(1);
        second.Bounds.Y.ShouldBe(0);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("B");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");

        await dispatcher.InvokeAsync(() =>
        {
            panel.Attach(dispatcher);
            using FocusManager focus = new(panel);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(second);
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(first);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reverse mode flips promoted popup drawing and hit order with the ordinary stack order.</summary>
    [Fact]
    public void PopupLayer_WhenReverseChanges_FlipsDrawingAndHitPriority()
    {
        var panel = new Panel { Bounds = new Rect(0, 0, 6, 4) };
        var first = CreatePopup("A");
        var second = CreatePopup("B");

        panel.Children.Add(first);
        panel.Children.Add(second);

        ArrangePopup(first, panel.Bounds);
        ArrangePopup(second, panel.Bounds);

        var bounds = first.Content!.Bounds;
        var point = new Point(bounds.X, bounds.Y);

        using Frame forward = new(new Size(6, 4));

        panel.Render(forward.Canvas);

        FrameOracle.Get(forward, point).ShouldBe("B");
        panel.HitTest(point).ShouldBeSameAs(second.Content);

        panel.Reverse = true;
        using Frame reverse = new(new Size(6, 4));
        panel.Render(reverse.Canvas);

        FrameOracle.Get(reverse, point).ShouldBe("A");
        panel.HitTest(point).ShouldBeSameAs(first.Content);
    }

    /// <summary>Verifies resize recomputes percentage and star allocation deterministically.</summary>
    [Fact]
    public void Layout_WhenViewportChanges_ReallocatesDeferredLengths()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var percent = new ProbeControl { Width = Length.Percent(50) };
        var star = new ProbeControl { Width = Length.Star(1) };

        panel.Children.Add(percent);
        panel.Children.Add(star);

        var engine = new LayoutEngine();

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
        var panel = new Panel { Orientation = Orientation.Horizontal };
        var first = new ProbeControl { Width = Length.Cells(5) };
        var second = new ProbeControl { Width = Length.Cells(5) };

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(3, 1));

        first.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        second.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
        first.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
        second.Bounds.Right.ShouldBeLessThanOrEqualTo(3);
    }

    /// <summary>Verifies property changes request the earliest required phase.</summary>
    [Fact]
    public void PropertySetter_WhenStackPolicyChanges_InvalidatesPrecisePhase()
    {
        var panel = new Panel();
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

    /// <summary>Verifies vertical percentage heights resolve against the final axis.</summary>
    [Fact]
    public void Layout_WhenVerticalChildrenUsePercent_ResolvesAgainstFinalAxis()
    {
        var panel = new Panel { Spacing = 0 };
        var top = new ProbeControl { Height = Length.Percent(30) };
        var bottom = new ProbeControl { Height = Length.Percent(70) };

        panel.Children.Add(top);
        panel.Children.Add(bottom);

        new LayoutEngine().Layout(panel, new Size(4, 10));

        top.Bounds.ShouldBe(new Rect(0, 0, 0, 3));
        bottom.Bounds.ShouldBe(new Rect(0, 3, 0, 7));
    }

    /// <summary>Verifies a Percent child inside an AutoScroll Stack resolves against the viewport,
    /// not the scroll extent it itself contributes to - otherwise a full-sized Auto sibling
    /// raises the extent and crushes the Percent sibling toward zero.</summary>
    [Fact]
    public void Layout_WhenAutoScrollStackHasPercentChild_ResolvesAgainstViewportNotExtent()
    {
        var panel = new Panel
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Spacing = 0
        };
        var tall = new ProbeControl(new Size(4, 30));
        var percentChild = new ProbeControl(new Size(4, 1)) { Height = Length.Percent(50) };
        panel.Children.Add(tall);
        panel.Children.Add(percentChild);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        panel.Viewport.ShouldBe(new Size(10, 10));
        percentChild.Bounds.Height.ShouldBe(5);
    }

    /// <summary>Verifies a Star child along an AutoScroll stacking axis falls back to its own
    /// intrinsic request instead of dividing remaining space, since a scrolling axis has no
    /// fixed remaining space to divide.</summary>
    [Fact]
    public void Layout_WhenAutoScrollStackHasStarChild_FallsBackToIntrinsicRequest()
    {
        var panel = new Panel
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Spacing = 0
        };
        var tall = new ProbeControl(new Size(4, 30));
        var starChild = new ProbeControl(new Size(4, 2)) { Height = Length.Star(1) };
        panel.Children.Add(tall);
        panel.Children.Add(starChild);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        starChild.Bounds.Height.ShouldBe(2);
    }

    /// <summary>Verifies star children with different weights split proportionally.</summary>
    [Fact]
    public void Layout_WhenStarWeightsDiffer_DividesRemainderByWeight()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var one = new ProbeControl { Width = Length.Star(1) };
        var two = new ProbeControl { Width = Length.Star(2) };

        panel.Children.Add(one);
        panel.Children.Add(two);

        new LayoutEngine().Layout(panel, new Size(12, 1));

        one.Bounds.Width.ShouldBe(4);
        two.Bounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies an empty stack measures to zero and does not fail.</summary>
    [Fact]
    public void Layout_WhenEmpty_MeasuresToZero()
    {
        var panel = new Panel { Spacing = 5 };

        new LayoutEngine().Layout(panel, new Size(10, 10));

        panel.DesiredSize.ShouldBe(default);
    }

    /// <summary>Verifies a single child receives no spacing contribution.</summary>
    [Fact]
    public void Layout_WhenSingleChild_OmitsSpacing()
    {
        var panel = new Panel { Spacing = 3 };
        var child = new ProbeControl(new Size(2, 2));

        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(5, 5));

        panel.DesiredSize.ShouldBe(new Size(2, 2));
        child.Bounds.ShouldBe(new Rect(0, 0, 2, 2));
    }

    /// <summary>Verifies all-collapsed children produce zero desired size and no spacing.</summary>
    [Fact]
    public void Layout_WhenAllChildrenAreCollapsed_MeasuresToZero()
    {
        var panel = new Panel { Spacing = 2 };
        var first = new ProbeControl(new Size(5, 5)) { Visibility = Visibility.Collapsed };
        var second = new ProbeControl(new Size(5, 5)) { Visibility = Visibility.Collapsed };

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        panel.DesiredSize.ShouldBe(default);
        first.Bounds.ShouldBe(default);
        second.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies hidden children retain their layout slot unlike collapsed ones.</summary>
    [ComponentVisibilityEvidence(typeof(Panel), ComponentVisibilityEvidence.HiddenRetainsSlot)]
    [Fact]
    public void Layout_WhenChildIsHidden_RetainsSlotButCollapsedRemovesIt()
    {
        var panel = new Panel { Spacing = 1 };
        var visible = new ProbeControl(new Size(2, 2));
        var hidden = new ProbeControl(new Size(2, 3)) { Visibility = Visibility.Hidden };
        var last = new ProbeControl(new Size(2, 1));

        panel.Children.Add(visible);
        panel.Children.Add(hidden);
        panel.Children.Add(last);

        new LayoutEngine().Layout(panel, new Size(5, 10));

        visible.Bounds.ShouldBe(new Rect(0, 0, 2, 2));
        hidden.Bounds.Height.ShouldBe(3);
        last.Bounds.Y.ShouldBe(hidden.Bounds.Bottom + 1);
    }

    /// <summary>Verifies horizontal auto children use intrinsic width and stretch on the cross axis.</summary>
    [Fact]
    public void Layout_WhenHorizontalChildrenAreAutomatic_ArrangesSequentialIntrinsicWidths()
    {
        var panel = new Panel { Orientation = Orientation.Horizontal, Spacing = 1 };
        var first = new ProbeControl(new Size(3, 2));
        var second = new ProbeControl(new Size(4, 1));

        panel.Children.Add(first);
        panel.Children.Add(second);

        new LayoutEngine().Layout(panel, new Size(10, 5));

        first.Bounds.Width.ShouldBe(3);
        first.Bounds.X.ShouldBe(0);

        second.Bounds.Width.ShouldBe(4);
        second.Bounds.X.ShouldBe(4);

        panel.DesiredSize.Width.ShouldBe(8);
        panel.DesiredSize.Height.ShouldBe(2);
    }

    /// <summary>Verifies MinHeight and MaxHeight clamp the resolved child track.</summary>
    [Fact]
    public void Layout_WhenChildHasMinMaxConstraints_ClampsResolvedTrack()
    {
        var panel = new Panel();
        var constrained = new ProbeControl(new Size(2, 1))
        {
            Height = Length.Star(1),
            MinHeight = 3,
            MaxHeight = 5
        };

        var @fixed = new ProbeControl(new Size(2, 2));

        panel.Children.Add(constrained);
        panel.Children.Add(@fixed);

        new LayoutEngine().Layout(panel, new Size(4, 20));

        constrained.Bounds.Height.ShouldBeGreaterThanOrEqualTo(3);
        constrained.Bounds.Height.ShouldBeLessThanOrEqualTo(5);
    }

    /// <summary>Verifies horizontal percentage uses the full content axis, not the remaining space.</summary>
    [Fact]
    public void Layout_WhenHorizontalPercentFollowsFixed_UsesFullAxisNotRemainder()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var fixedChild = new ProbeControl { Width = Length.Cells(4) };
        var percentChild = new ProbeControl { Width = Length.Percent(50) };

        panel.Children.Add(fixedChild);
        panel.Children.Add(percentChild);

        new LayoutEngine().Layout(panel, new Size(20, 1));

        fixedChild.Bounds.Width.ShouldBe(4);
        percentChild.Bounds.Width.ShouldBe(10);
    }

    /// <summary>Verifies an Auto sibling keeps its intrinsic extent against a Percent(100)
    /// track under deficit, instead of being annihilated because the laundered Percent length
    /// was invisible to Percent's shrink priority.</summary>
    [Fact]
    public void Layout_WhenAutoCompetesWithFullPercentUnderDeficit_KeepsAutoIntrinsicExtent()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var header = new ProbeControl(new Size(3, 2));
        var body = new ProbeControl(new Size(3, 1)) { Height = Length.Percent(100) };

        panel.Children.Add(header);
        panel.Children.Add(body);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        // Percent is the first shrink priority, so it alone absorbs the full deficit (from its
        // requested 10 down to 8) before Auto is touched at all - the header keeps its
        // untouched intrinsic height of 2 instead of being annihilated to 0.
        header.Bounds.Height.ShouldBe(2);
        body.Bounds.Height.ShouldBe(8);
    }

    /// <summary>Verifies a fixed sibling keeps its exact extent when it follows a Percent(100)
    /// track under deficit, instead of being annihilated because Percent's shrink priority never
    /// saw the laundered Cells-typed length as a Percent track.</summary>
    [Fact]
    public void Layout_WhenFixedFollowsFullPercentUnderDeficit_KeepsFixedExactExtent()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var percentChild = new ProbeControl(new Size(3, 1)) { Height = Length.Percent(100) };
        var fixedChild = new ProbeControl(new Size(3, 1)) { Height = Length.Cells(4) };

        panel.Children.Add(percentChild);
        panel.Children.Add(fixedChild);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        fixedChild.Bounds.Height.ShouldBe(4);
        percentChild.Bounds.Height.ShouldBe(6);
    }

    /// <summary>Verifies three equal-thirds percentage tracks sum to the complete axis instead
    /// of losing a cell to independent per-track rounding with no shared cumulative edge state.</summary>
    [Fact]
    public void Layout_WhenThreeEqualPercentTracksShareAnAxis_SumToTheCompleteAxis()
    {
        var panel = new Panel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var first = new ProbeControl(new Size(3, 1)) { Height = Length.Percent(100.0 / 3) };
        var second = new ProbeControl(new Size(3, 1)) { Height = Length.Percent(100.0 / 3) };
        var third = new ProbeControl(new Size(3, 1)) { Height = Length.Percent(100.0 / 3) };

        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);

        new LayoutEngine().Layout(panel, new Size(10, 10));

        (first.Bounds.Height + second.Bounds.Height + third.Bounds.Height).ShouldBe(10);
    }

    /// <summary>Verifies spacing between children does not contribute spacing before the first or after the last.</summary>
    [Fact]
    public void Layout_WhenSpacingIsSet_DoesNotAddExtraAtEdges()
    {
        var panel = new Panel { Spacing = 2 };
        var first = new ProbeControl(new Size(1, 1));
        var second = new ProbeControl(new Size(1, 1));
        var third = new ProbeControl(new Size(1, 1));

        panel.Children.Add(first);
        panel.Children.Add(second);
        panel.Children.Add(third);

        new LayoutEngine().Layout(panel, new Size(5, 10));

        first.Bounds.Y.ShouldBe(0);
        second.Bounds.Y.ShouldBe(3);
        third.Bounds.Y.ShouldBe(6);
        panel.DesiredSize.Height.ShouldBe(7);
    }

    /// <summary>Verifies border and padding reserve edges before children receive their slot.</summary>
    [Fact]
    public void Layout_WhenPanelHasBorderAndPadding_ChildrenArrangeInsideContentBox()
    {
        var panel = new Panel
        {
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(2, 1)
        };

        var child = new ProbeControl(new Size(3, 2));
        panel.Children.Add(child);

        new LayoutEngine().Layout(panel, new Size(20, 10));

        child.Bounds.X.ShouldBeGreaterThanOrEqualTo(3);
        child.Bounds.Y.ShouldBeGreaterThanOrEqualTo(2);
    }

    private static Popup CreatePopup(string content) => new()
    {
        Content = new ProbeControl(new Size(1, 1)) { Content = content.AsMemory() },
        IsOpen = true
    };

    private static void ArrangePopup(Popup popup, Rect bounds)
    {
        popup.Measure(new Constraint(bounds.Width, bounds.Height));
        popup.Arrange(bounds, widthResolved: true, heightResolved: true);
    }

    /// <summary>Verifies the identical declarative shape - three fixed-size children, one
    /// percentage-sized child, no spacing - agrees on the resolved percentage base across Grid
    /// (a single Star column split across rows) and Stack (the same tracks along its own
    /// stacking axis) - a comparison that would have caught directly the case where Grid and
    /// Stack once disagreed on what a Percent length resolves against.</summary>
    [Fact]
    public void Layout_WhenGridAndStackShareTheIdenticalTrackShape_ResolvePercentAgainstTheSameBase()
    {
        var stackFixed = new ProbeControl(new Size(4, 3));
        var stackPercent = new ProbeControl(new Size(4, 1)) { Height = Length.Percent(50) };
        var stack = new Panel
        {
            Spacing = 0,
            Children = { stackFixed, stackPercent }
        };

        var gridFixed = new ProbeControl(new Size(4, 3));
        var gridPercent = new ProbeControl(new Size(4, 1));
        var grid = new Grid
        {
            RowSpacing = 0,
            Rows = { Track.Cells(3), Track.Percent(50) },
            Children = { gridFixed, gridPercent }
        };
        Grid.SetRow(gridPercent, 1);

        new LayoutEngine().Layout(stack, new Size(4, 10));
        new LayoutEngine().Layout(grid, new Size(4, 10));

        // Both resolve the 50% track against the complete final axis (10), not the smaller
        // remainder left after the fixed sibling (10 - 3 = 7): 50% of 10 is 5, not 3 or 4.
        stackPercent.Bounds.Height.ShouldBe(5);
        gridPercent.Bounds.Height.ShouldBe(5);
        stackPercent.Bounds.Height.ShouldBe(gridPercent.Bounds.Height);
    }

    /// <summary>Verifies disabling a detached Stack cascades EffectiveIsEnabled to an owned child
    /// and recovers on re-enable, without needing a mounted surface.</summary>
    [ComponentUnitEvidence(typeof(Panel), ComponentBehavior.Disabled)]
    [Fact]
    public void Enabled_WhenToggled_CascadesEffectiveIsEnabledToOwnedChild()
    {
        var panel = new Panel();
        var child = new ProbeControl();
        panel.Children.Add(child);

        child.EffectiveIsEnabled.ShouldBeTrue();

        panel.IsEnabled = false;

        panel.EffectiveIsEnabled.ShouldBeFalse();
        child.EffectiveIsEnabled.ShouldBeFalse();

        panel.IsEnabled = true;

        panel.EffectiveIsEnabled.ShouldBeTrue();
        child.EffectiveIsEnabled.ShouldBeTrue();
    }
}
