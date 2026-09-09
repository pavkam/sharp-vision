// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the shared scrolling contract for item controls with private overflow hosts.</summary>
public sealed class ScrollableItemsControlTests
{
    /// <summary>Verifies consumers can configure and observe a scrolling item owner without knowing
    /// which private container presents its realized controls.</summary>
    [Fact]
    public void ScrollBy_WhenUsedThroughSharedBase_UsesTheSemanticOwnerContract()
    {
        ScrollableItemsControl control = new UiListView
        {
            Items = Enumerable.Range(0, 20).Select(value => (object?) $"Item {value}").ToArray(),
            LineSize = 2,
            PageOverlap = 1,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        new LayoutEngine().Layout(control, new Size(10, 4));
        object? sender = null;
        control.ScrollChanged += (candidate, _) => sender = candidate;

        var changed = control.ScrollBy(0, 2);

        changed.ShouldBeTrue();
        control.VerticalOffset.ShouldBe(2);
        control.Extent.Height.ShouldBeGreaterThan(control.Viewport.Height);
        control.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.ThinLine);
        sender.ShouldBeSameAs(control);
    }

    /// <summary>Verifies the shared uniform-row resolve-and-remap helper keeps the same logical row
    /// (and proportional point within it) visible after a viewport change resolves a new row height,
    /// matching what <see cref="UiListView"/> and
    /// <see cref="Table"/> both rely on this shared state machine
    /// for.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenRowHeightChanges_KeepsSameLogicalRowVisible()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20));
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 20));
        _ = probe.ScrollBy(0, 12);
        probe.VerticalOffset.ShouldBe(12);

        engine.Layout(probe, new Size(10, 10));

        var expected = UniformRowHeight.RemapOffset(offset: 12, previousHeight: 4, currentHeight: 2, gap: 0);
        probe.VerticalOffset.ShouldBe(expected);
    }

    /// <summary>Verifies an offset that already sits inside the leading band passes through a
    /// row-height change unchanged, because the band's own height did not change - the header-band
    /// branch of <see cref="ScrollableItemsControl.ArrangeUniformRows"/> that a caller with no band
    /// never exercises.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenOffsetIsInsideTheLeadingBand_LeavesItUnchanged()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20))
        {
            LeadingBandHeight = 5
        };
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 25));
        _ = probe.ScrollBy(0, 3);
        probe.VerticalOffset.ShouldBe(3);

        engine.Layout(probe, new Size(10, 15));

        probe.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies an offset past the leading band remaps relative to the band: the band
    /// height is excluded from the content offset before <see cref="UniformRowHeight.RemapOffset"/>
    /// runs, and added back onto its result.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenOffsetIsPastTheLeadingBand_RemapsRelativeToTheBand()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20))
        {
            LeadingBandHeight = 5
        };
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 25));
        _ = probe.ScrollBy(0, 20);
        probe.VerticalOffset.ShouldBe(20);

        engine.Layout(probe, new Size(10, 15));

        var expectedMapped = UniformRowHeight.RemapOffset(offset: 15, previousHeight: 4, currentHeight: 2, gap: 0);
        probe.VerticalOffset.ShouldBe(5 + expectedMapped);
    }

    /// <summary>Verifies an offset that lands exactly at the leading band boundary maps to the band
    /// itself, proving the boundary belongs to the "past the band" branch of the ternary rather than
    /// falling off-by-one to either side.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenOffsetIsExactlyAtTheLeadingBandBoundary_MapsToTheBand()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20))
        {
            LeadingBandHeight = 5
        };
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 25));
        _ = probe.ScrollBy(0, 5);
        probe.VerticalOffset.ShouldBe(5);

        engine.Layout(probe, new Size(10, 15));

        probe.VerticalOffset.ShouldBe(5);
    }

    /// <summary>Verifies the host's realized children already sit at the remapped offset after
    /// exactly one <see cref="LayoutEngine.Layout"/> pass following a row-height change, proving
    /// <see cref="ScrollableItemsControl.ArrangeUniformRows"/> re-arranges the host synchronously in
    /// the same transaction instead of leaving its children at their pre-remap
    /// <see cref="ControlBase.Bounds"/> for a caller that drives only one layout pass to observe.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenRowHeightChanges_ReArrangesHostChildrenInTheSamePass()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20));
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 20));
        _ = probe.ScrollBy(0, 12);

        engine.Layout(probe, new Size(10, 10));

        var expectedOffset = UniformRowHeight.RemapOffset(offset: 12, previousHeight: 4, currentHeight: 2, gap: 0);
        probe.VerticalOffset.ShouldBe(expectedOffset);
        probe.Host.Children[0].Bounds.Y.ShouldBe(probe.Host.Bounds.Y - expectedOffset);
    }
}
