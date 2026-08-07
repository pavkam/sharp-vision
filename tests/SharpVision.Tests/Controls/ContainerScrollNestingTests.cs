// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies scrolling geometry, offset translation, and wheel propagation when an armed
/// (<see cref="Container.AutoScroll"/>) container nests inside another - the largest coverage gap:
/// <c>ContainerScrollTests.cs</c> is exclusively single-level, so no test anywhere
/// composed two levels of offset, extent, or clip translation before this file.</summary>
public sealed class ContainerScrollNestingTests
{
    /// <summary>Verifies each nested armed container discovers its own independent extent and
    /// viewport - the inner container's own scrolling capacity never leaks into the outer
    /// container's, and the outer's extent sums the inner's own fixed border-box height rather
    /// than the inner's unscrolled content height.</summary>
    [Fact]
    public void Extent_WhenTwoArmedContainersNest_ComposesIndependentExtents()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { new ProbeControl(new Size(4, 3)), inner }
        };

        new LayoutEngine().Layout(outer, new Size(4, 5));

        // The inner container's own extent is the leaf's full unscrolled height, independent of
        // whatever the outer container does with the inner's own arranged position.
        inner.Extent.ShouldBe(new Size(4, 6));
        inner.Viewport.ShouldBe(new Size(4, 4));

        // The outer container's extent sums the first sibling's height (3) and the inner
        // container's own fixed border-box height (4, from its explicit Height) - never the
        // inner's much larger unscrolled content height (6), which the inner alone is
        // responsible for reconciling through its own scrollbar.
        outer.Extent.ShouldBe(new Size(4, 7));
        outer.Viewport.ShouldBe(new Size(4, 5));
    }

    /// <summary>Verifies a leaf's absolute Bounds compose both levels of scroll translation: the
    /// inner container's own offset shifts the leaf, and the outer container's offset then also
    /// shifts the inner container's own arranged position, which the leaf inherits transitively.
    /// This is the exact two-level Bounds-translation composition left untested elsewhere.</summary>
    [Fact]
    public void Bounds_WhenBothContainersScroll_ComposesTranslationAcrossTwoLevels()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf }
        };
        var top = new ProbeControl(new Size(4, 3));
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { top, inner }
        };
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // Unscrolled: top occupies rows [0, 3), inner occupies rows [3, 7) of outer's own content
        // space, and leaf occupies rows [0, 6) of inner's own content space.
        (outer.Extent.Height - outer.Viewport.Height).ShouldBe(2);
        (inner.Extent.Height - inner.Viewport.Height).ShouldBe(2);

        _ = outer.ScrollBy(0, 2);
        _ = inner.ScrollBy(0, 1);
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // inner's own arranged position, shifted by outer's offset: 3 - 2 = 1.
        inner.Bounds.Y.ShouldBe(1);

        // leaf's absolute position: inner's own arranged top (1) plus leaf's position within
        // inner after inner's own offset (0 - 1 = -1): 1 + (-1) = 0.
        leaf.Bounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies a wheel delta too large for the inner container's own remaining capacity
    /// consumes only what the inner can absorb and keeps the whole record for itself - latching,
    /// not remainder-chaining within the same event, is the contract docs/concepts/scrolling.md
    /// commits to - and that only a later, separate record, once the inner is fully at
    /// its endpoint and moves nothing, reaches the enclosing armed container. This is the
    /// genuinely nested pair left untested elsewhere.</summary>
    [Fact]
    public void Wheel_WhenInnerContainerCannotConsumeTheFullDelta_KeepsTheWholeRecordThenLatchesToOuter()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4)
        };
        inner.Children.Add(leaf);
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        outer.Children.Add(new ProbeControl(new Size(4, 20)));
        outer.Children.Add(inner);
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // inner's own extent (6) less its own viewport (4) leaves only 2 cells of scroll capacity;
        // requesting 5 lines still moves inner (some, not zero), so the whole record stays there.
        (inner.Extent.Height - inner.Viewport.Height).ShouldBe(2);
        var outerOffsetBeforeWheel = outer.VerticalOffset;

        inner.RaiseWheel(0, -5);

        inner.VerticalOffset.ShouldBe(2);
        outer.VerticalOffset.ShouldBe(outerOffsetBeforeWheel);

        // inner is now fully at its endpoint - a later, identical record moves nothing there, so
        // this one reaches outer instead.
        inner.RaiseWheel(0, -5);

        inner.VerticalOffset.ShouldBe(2);
        outer.VerticalOffset.ShouldBeGreaterThan(outerOffsetBeforeWheel);
    }

    /// <summary>Verifies a horizontal wheel delta (WheelX) also composes correctly through a
    /// nested armed pair - WheelX in a nested pair was entirely untested elsewhere, distinct from
    /// the vertical-only coverage every existing wheel test exercises. Latches the same way the
    /// vertical case does: the inner container keeps a record it moves any amount for,
    /// and only a later, separate record that moves nothing there reaches outer.</summary>
    [Fact]
    public void Wheel_WhenHorizontalDeltaExceedsInnerCapacity_KeepsTheWholeRecordThenLatchesToOuter()
    {
        var leaf = new ProbeControl(new Size(20, 4));
        var inner = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            ScrollBars = ScrollBars.Horizontal,
            Width = Length.Cells(6)
        };
        inner.Children.Add(leaf);
        var outer = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            ScrollBars = ScrollBars.Horizontal
        };
        outer.Children.Add(new ProbeControl(new Size(30, 4)));
        outer.Children.Add(inner);
        new LayoutEngine().Layout(outer, new Size(6, 4));

        (inner.Extent.Width - inner.Viewport.Width).ShouldBe(14);
        var outerOffsetBeforeWheel = outer.HorizontalOffset;

        inner.RaiseWheel(20, 0);

        inner.HorizontalOffset.ShouldBe(14);
        outer.HorizontalOffset.ShouldBe(outerOffsetBeforeWheel);

        inner.RaiseWheel(20, 0);

        inner.HorizontalOffset.ShouldBe(14);
        outer.HorizontalOffset.ShouldBeGreaterThan(outerOffsetBeforeWheel);
    }

    /// <summary>Verifies the ordinary application shape - a Stack laid out inside a Grid cell,
    /// itself inside an armed Container - composes correctly: the Stack's own intrinsic content
    /// determines its DesiredSize, the Grid resolves its track from that, and the outer container
    /// scrolls the whole composed result without any level silently truncating the others.</summary>
    [Fact]
    public void Layout_WhenStackInsideGridInsideArmedContainer_ComposesIntrinsicSizeAndScrolls()
    {
        var stackChild = new ProbeControl(new Size(4, 30));
        var stack = new Stack { Children = { stackChild } };
        var grid = new Grid
        {
            Columns = { Track.Auto() },
            Children = { stack }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { grid }
        };

        new LayoutEngine().Layout(outer, new Size(4, 10));

        // The Stack's own intrinsic height (30) propagates through the Grid's Auto row/column
        // resolution up to the armed outer container's extent, unclamped and unclipped.
        outer.Extent.Height.ShouldBe(30);
        outer.Viewport.Height.ShouldBe(10);
        _ = outer.ScrollBy(0, 1000);
        outer.VerticalOffset.ShouldBe(20);
        stackChild.Bounds.Height.ShouldBe(30);
    }

    /// <summary>Verifies three-level intrinsic propagation through Grid, an armed Container, and
    /// an outer Grid - testing that intrinsic sizing survives an armed
    /// container sitting between two Grids rather than being silently clamped or lost at the
    /// boundary.</summary>
    [Fact]
    public void Layout_WhenGridInsideArmedContainerInsideAnotherGrid_PropagatesIntrinsicSizeAcrossThreeLevels()
    {
        var leaf = new ProbeControl(new Size(4, 30));
        var innerGrid = new Grid
        {
            Columns = { Track.Auto() },
            Children = { leaf }
        };
        var armed = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(5),
            Children = { innerGrid }
        };
        var outerGrid = new Grid
        {
            Rows = { Track.Auto() },
            Children = { armed }
        };

        new LayoutEngine().Layout(outerGrid, new Size(4, 20));

        // The armed Stack's own fixed border-box height (5, from its explicit Height) is what
        // the outer Grid's Auto row resolves to - the inner Grid's much larger intrinsic content
        // (30) is fully absorbed by the armed container's own scrollbar and never escapes to
        // inflate the outer Grid's row.
        outerGrid.DesiredSize.Height.ShouldBe(5);
        armed.Extent.Height.ShouldBe(30);
        armed.Viewport.Height.ShouldBe(5);
    }

    /// <summary>Verifies BringIntoView's own walk composes correctly across
    /// three nested armed levels, not merely the two the existing regression test covers.</summary>
    [Fact]
    public void BringIntoView_WhenThreeArmedContainersNest_RevealsTargetThroughEveryLevel()
    {
        var target = new ProbeControl(new Size(4, 2));
        var innermost = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { new ProbeControl(new Size(4, 10)), target }
        };
        var middle = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(6),
            Children = { new ProbeControl(new Size(4, 8)), innermost }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { new ProbeControl(new Size(4, 15)), middle }
        };
        new LayoutEngine().Layout(outer, new Size(4, 6));

        var result = outer.BringIntoView(target);

        // BringIntoView commits offset properties immediately but the resulting Arrange is
        // deferred - target.Bounds only reflects the new offsets after a real layout pass runs.
        new LayoutEngine().Layout(outer, new Size(4, 6));

        result.ShouldBeTrue();

        // Every level must have scrolled enough that target's final absolute position is fully
        // contained within outer's own viewport.
        outer.VerticalOffset.ShouldBeGreaterThan(0);
        middle.VerticalOffset.ShouldBeGreaterThan(0);
        innermost.VerticalOffset.ShouldBeGreaterThan(0);
        target.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        target.Bounds.Bottom.ShouldBeLessThanOrEqualTo(outer.Viewport.Height);
    }

    /// <summary>Verifies a Container child whose DesiredSize changes between the parent's own
    /// measure pass and its arrange-time bar-induced re-measure (Container.MeasureContent calls
    /// MeasureOverride directly during arrange without updating the base ContentExtent) still
    /// ends up arranged at its true, current, re-wrapped size rather than the first probe's
    /// stale one - this measure/arrange divergence was an entirely untested path elsewhere. Only
    /// the vertical axis is scrollable here: a vertical-only bar narrows the *bounded* horizontal
    /// axis, which a wrap-capable child must re-wrap against, unlike the Both-axes case where
    /// neither axis is ever bounded and no re-wrap can occur at all.</summary>
    [Fact]
    public void Layout_WhenBarInducedReMeasureChangesWrapCapableContentHeight_ArrangesAtTheNewHeight()
    {
        var text = new string('x', 40);
        var content = new ControlText(text) { Overflow = Overflow.Wrap };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(content);

        // At the unreserved width (10), the first, unbounded-height probe wraps 40 cells into 4
        // lines of 10 - but that height (4) already overflows a 3-cell viewport, so a vertical
        // bar is added; the bar reserves one column, narrowing width to 9, and the identical 40
        // cells now wrap into 5 lines of at most 9 - a height the first probe never saw.
        new LayoutEngine().Layout(container, new Size(10, 3));

        container.Viewport.ShouldBe(new Size(9, 3));
        container.Extent.ShouldBe(new Size(9, 5));
        content.Bounds.Width.ShouldBe(9);
        content.Bounds.Height.ShouldBe(5);
    }
}
