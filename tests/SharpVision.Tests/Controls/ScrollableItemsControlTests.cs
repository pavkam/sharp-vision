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
}
