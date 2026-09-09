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

    /// <summary>Verifies a single <see cref="LayoutEngine.Layout"/> pass leaves no pending Arrange
    /// bit after a row-height change re-anchors the offset. The self-heal that used to only re-dirty
    /// <see cref="ControlBase.Pending"/> - requiring a second, idle-triggered pass to actually fix
    /// the host's children <see cref="ControlBase.Bounds"/> - no longer has anything to heal because
    /// the host is already re-arranged synchronously in the same transaction.</summary>
    [Fact]
    public void ArrangeUniformRows_WhenRowHeightChanges_LeavesNoPendingArrangeAfterOnePass()
    {
        var probe = new ScrollableItemsControlUniformRowsProbe(rowCount: 20, Length.Percent(20));
        var engine = new LayoutEngine();
        engine.Layout(probe, new Size(10, 20));
        _ = probe.ScrollBy(0, 12);

        engine.Layout(probe, new Size(10, 10));

        (probe.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }
}
