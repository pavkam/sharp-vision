// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies AutoSize grow/shrink on a container.</summary>
public sealed class ContainerAutoSizeTests
{
    /// <summary>Verifies AutoSize includes border and padding in the border box while preserving the content inset.</summary>
    [Fact]
    public void AutoSize_WhenContentHasPaddingAndBorder_SizesBorderBoxAndInsetsContent()
    {
        ProbeControl child = new(new Size(4, 2));
        LayoutProbe container = new()
        {
            AutoSize = true,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(2, 1, 3, 2)
        };
        container.Children.Add(child);

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.ShouldBe(new Size(11, 7));
        container.Bounds.ShouldBe(new Rect(0, 0, 11, 7));
        child.Bounds.ShouldBe(new Rect(3, 2, 4, 2));
    }

    /// <summary>Verifies AutoSize saturates a content, padding, and border sum beyond the integer range.</summary>
    [Fact]
    public void AutoSize_WhenPaddingAndBorderExceedIntegerRange_SaturatesBorderBox()
    {
        LayoutProbe container = new()
        {
            AutoSize = true,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(int.MaxValue - 2, 0, 0, 0)
        };
        container.Children.Add(new ProbeControl(new Size(1, 1)));

        container.Measure(new Constraint(width: null, height: null));

        container.DesiredSize.ShouldBe(new Size(int.MaxValue, 3));
    }

    /// <summary>Verifies AutoSize shrink-wraps a stretched container to its content.</summary>
    [Fact]
    public void AutoSize_WhenStretchedSlot_SizesToContent()
    {
        var container = new LayoutProbe { AutoSize = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        container.Children.Add(new ProbeControl(new Size(5, 3)) { HorizontalAlignment = HorizontalAlignment.Left });

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(5);
        container.Bounds.Height.ShouldBe(3);
    }

    /// <summary>Verifies GrowAndShrink shrinks to content even below an explicit fixed width.</summary>
    [Fact]
    public void AutoSizeGrowAndShrink_WhenContentSmallerThanFixedWidth_ShrinksToContent()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Width = Length.Cells(10)
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(4);
    }

    /// <summary>Verifies GrowOnly keeps the explicit fixed width as a floor when content is smaller.</summary>
    [Fact]
    public void AutoSizeGrowOnly_WhenContentSmallerThanFixedWidth_KeepsFixedWidth()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = Length.Cells(10)
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(10);
    }

    /// <summary>Verifies AutoSize grows past an explicit fixed width when content is larger.</summary>
    [Fact]
    public void AutoSize_WhenContentLargerThanFixedWidth_GrowsToContent()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = Length.Cells(10)
        };
        container.Children.Add(new ProbeControl(new Size(20, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(20);
    }

    /// <summary>Verifies AutoSize with MaxWidth/MaxHeight re-measures wrap-capable content at the
    /// clamped width instead of trusting the unbounded, unwrapped first pass - the identical
    /// geometry expressed as an explicit Width/Height Stack wraps and scrolls correctly, so the
    /// AutoSize equivalent must match it exactly rather than reporting a taller unwrapped extent
    /// that AutoScroll can never reach.</summary>
    [Fact]
    public void AutoSize_WhenMaxWidthClampsWrapCapableContent_MatchesDeterminateEquivalent()
    {
        var text = new string('x', 49);
        var autoSize = new Stack
        {
            AutoSize = true,
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            MaxWidth = 12,
            MaxHeight = 4,
            Children = { new ControlText(text) { Overflow = Overflow.Wrap } }
        };
        var determinate = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Width = Length.Cells(12),
            Height = Length.Cells(4),
            Children = { new ControlText(text) { Overflow = Overflow.Wrap } }
        };

        new LayoutEngine().Layout(autoSize, new Size(40, 40));
        new LayoutEngine().Layout(determinate, new Size(40, 40));

        autoSize.DesiredSize.ShouldBe(determinate.DesiredSize);
        autoSize.Extent.ShouldBe(determinate.Extent);
        autoSize.Viewport.ShouldBe(determinate.Viewport);
        autoSize.ScrollBy(0, 1).ShouldBe(determinate.ScrollBy(0, 1));
    }

    /// <summary>Verifies a MaxWidth on an AutoSize container re-measures wrap-capable content at
    /// the clamped width - without any AutoScroll involved - matching the identical geometry
    /// expressed as the same MaxWidth on the leaf content directly instead of reporting the
    /// leaf's unwrapped, single-line natural size.</summary>
    [Fact]
    public void AutoSize_WhenMaxWidthClampsWrapCapableContentWithoutAutoScroll_MatchesMaxOnLeaf()
    {
        var text = new string('x', 49);
        var maxOnContainer = new Stack
        {
            AutoSize = true,
            MaxWidth = 12,
            Children = { new ControlText(text) { Overflow = Overflow.Wrap } }
        };
        var leafText = new ControlText(text) { Overflow = Overflow.Wrap, MaxWidth = 12 };
        var maxOnLeaf = new Stack
        {
            AutoSize = true,
            Children = { leafText }
        };

        new LayoutEngine().Layout(maxOnContainer, new Size(40, 40));
        new LayoutEngine().Layout(maxOnLeaf, new Size(40, 40));

        maxOnContainer.Bounds.ShouldBe(maxOnLeaf.Bounds);
        maxOnContainer.Bounds.ShouldBe(new Rect(0, 0, 12, 5));
    }

    /// <summary>Verifies MinWidth raises an AutoSize container above content smaller than it,
    /// symmetric with the existing MaxWidth coverage above.</summary>
    [Fact]
    public void AutoSize_WhenContentIsSmallerThanMinWidth_GrowsToTheMinimum()
    {
        var container = new LayoutProbe { AutoSize = true, MinWidth = 10 };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.Width.ShouldBe(10);
        container.Bounds.Width.ShouldBe(10);
    }

    /// <summary>Verifies GrowOnly's explicit-Cells floor never overrides MaxWidth when the two
    /// conflict - the cap always wins over the floor, matching Math.Clamp's own precedence, not
    /// the other way around.</summary>
    [Fact]
    public void AutoSizeGrowOnly_WhenFloorExceedsMaxWidth_MaxWidthWins()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = Length.Cells(10),
            MaxWidth = 8
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.Width.ShouldBe(8);
        container.Bounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies an AutoSize container placed in a slot smaller than its natural content
    /// still reports the full, unclamped natural DesiredSize - only Min/Max clamp DesiredSize
    /// itself - while its committed Bounds separately shrink-wrap to whatever the parent's slot
    /// actually grants, through the ordinary ShrinkWrapsWidth/Height arrange-time path shared
    /// with every other shrink-wrapping control.</summary>
    [Fact]
    public void AutoSize_WhenGrantedSlotIsSmallerThanDesire_ClampsBoundsButNotDesiredSize()
    {
        var container = new LayoutProbe { AutoSize = true };
        container.Children.Add(new ProbeControl(new Size(20, 5)));
        var host = new LayoutProbe { HorizontalAlignment = HorizontalAlignment.Stretch };
        host.Children.Add(container);

        new LayoutEngine().Layout(host, new Size(6, 10));

        container.DesiredSize.ShouldBe(new Size(20, 5));
        container.Bounds.Width.ShouldBe(6);
    }

    /// <summary>Verifies AutoSize includes the complete border-and-padding content inset.</summary>
    [Fact]
    public void AutoSize_WhenBorderAndPaddingAreSet_IncludesCompleteContentInset()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1)
        };
        container.Children.Add(new ProbeControl(new Size(5, 3)) { HorizontalAlignment = HorizontalAlignment.Left });

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.Bounds.ShouldBe(new Rect(0, 0, 9, 7));
    }
}
