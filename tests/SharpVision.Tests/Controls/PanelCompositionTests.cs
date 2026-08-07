// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using LayoutDock = Dock;
using LayoutExpander = Expander;
using LayoutGrid = Grid;
using LayoutGroupBox = GroupBox;
using LayoutStack = Stack;

/// <summary>Verifies the same declarative shapes across panels agree where the contract requires
/// it, and diverge only where documented - a cross-panel comparison that would have caught
/// earlier percentage-resolution and single-child-fill discrepancies directly, extended to
/// Expander and GroupBox, which share Grid's own ResolvedAxes.Both single-child arrange
/// contract.</summary>
public sealed class PanelCompositionTests
{
    /// <summary>Verifies an Expander's single content child always fills its slot regardless of
    /// Width or alignment, matching Grid's own documented ResolvedAxes.Both contract - the same
    /// gap that applies to Expander/GroupBox, which share ResolvedAxes.Both.</summary>
    [Fact]
    public void Expander_WhenContentSetsWidthAndAlignment_StillFillsTheContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // Expander itself defaults to Left/Top alignment, so as an unstretched root it would
        // shrink-wrap to its own measured content width - which is itself influenced by
        // content's explicit Width during measure, even though arrange's resolved axes ignore
        // it - creating a circular illusion that Width "worked". Stretching Expander itself to
        // the full given size removes that confound and isolates the arrange-time behavior.
        var expander = new LayoutExpander
        {
            Expanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(expander, new Size(10, 3));

        content.Bounds.Width.ShouldBe(10 - expander.ActualStyle.ContentIndent);
    }

    /// <summary>Verifies MaxWidth on an Expander's content still caps the otherwise-filled slot,
    /// mirroring Grid's own Width-ignored-but-MaxWidth-honored asymmetry exactly.</summary>
    [Fact]
    public void Expander_WhenContentSetsMaxWidth_CapsTheFilledContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1)) { MaxWidth = 3 };
        var expander = new LayoutExpander
        {
            Expanded = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(expander, new Size(10, 3));

        content.Bounds.Width.ShouldBe(3);
    }

    /// <summary>Verifies a GroupBox's single content child always fills its slot regardless of
    /// Width or alignment, matching Grid's own documented ResolvedAxes.Both contract.</summary>
    [Fact]
    public void GroupBox_WhenContentSetsWidthAndAlignment_StillFillsTheContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(3),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        // GroupBox itself defaults to Left/Top alignment, so as an unstretched root it would
        // shrink-wrap to its own measured content width - itself influenced by content's
        // explicit Width during measure - creating the same circular illusion Expander's own
        // test above must avoid. Stretching GroupBox itself removes that confound.
        var groupBox = new LayoutGroupBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(groupBox, new Size(10, 3));

        // The theme-resolved default border reserves one cell on each side (2 total); content
        // fills the remaining 8 regardless of its own Width or alignment.
        content.Bounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies MaxWidth on a GroupBox's content still caps the otherwise-filled slot.</summary>
    [Fact]
    public void GroupBox_WhenContentSetsMaxWidth_CapsTheFilledContentSlot()
    {
        var content = new ProbeControl(new Size(2, 1)) { MaxWidth = 3 };
        var groupBox = new LayoutGroupBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = content
        };

        new LayoutEngine().Layout(groupBox, new Size(10, 3));

        content.Bounds.Width.ShouldBe(3);
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
        var stack = new LayoutStack
        {
            Spacing = 0,
            Children = { stackFixed, stackPercent }
        };

        var gridFixed = new ProbeControl(new Size(4, 3));
        var gridPercent = new ProbeControl(new Size(4, 1));
        var grid = new LayoutGrid
        {
            RowSpacing = 0,
            Rows = { Track.Cells(3), Track.Percent(50) },
            Children = { gridFixed, gridPercent }
        };
        LayoutGrid.SetRow(gridPercent, 1);

        new LayoutEngine().Layout(stack, new Size(4, 10));
        new LayoutEngine().Layout(grid, new Size(4, 10));

        // Both resolve the 50% track against the complete final axis (10), not the smaller
        // remainder left after the fixed sibling (10 - 3 = 7): 50% of 10 is 5, not 3 or 4.
        stackPercent.Bounds.Height.ShouldBe(5);
        gridPercent.Bounds.Height.ShouldBe(5);
        stackPercent.Bounds.Height.ShouldBe(gridPercent.Bounds.Height);
    }

    /// <summary>Verifies a Dock cascading Percent edges through three consumed sides resolves each
    /// against the remaining space at the moment it is consumed, not the original total, for a
    /// three-or-more-level cascade.</summary>
    [Fact]
    public void Dock_WhenThreePercentEdgesCascade_EachResolvesAgainstItsOwnRemainder()
    {
        var left = new ProbeControl(new Size(1, 1));
        var top = new ProbeControl(new Size(1, 1));
        var right = new ProbeControl(new Size(1, 1));
        var fill = new ProbeControl(new Size(1, 1));
        var dock = new LayoutDock
        {
            LastChildFills = true,
            Children = { left, top, right, fill }
        };
        LayoutDock.SetSide(left, DockSide.Left);
        LayoutDock.SetSide(top, DockSide.Top);
        LayoutDock.SetSide(right, DockSide.Right);
        left.Width = Length.Percent(50);
        top.Height = Length.Percent(50);
        right.Width = Length.Percent(50);

        new LayoutEngine().Layout(dock, new Size(20, 20));

        // left consumes 50% of the full 20-wide axis: 10, leaving [10, 20) horizontally.
        left.Bounds.Width.ShouldBe(10);

        // top consumes 50% of the full 20-tall axis: 10 (top/bottom docking uses the full width
        // at the time it is consumed, but its own Percent is a vertical length resolving against
        // the vertical axis, unaffected by left's horizontal consumption).
        top.Bounds.Height.ShouldBe(10);

        // right consumes 50% of its own remaining horizontal space at the time it is docked -
        // the 10 cells left after `left` already consumed its own 50% - not 50% of the original
        // 20, so right gets 5, not 10.
        right.Bounds.Width.ShouldBe(5);
    }
}
