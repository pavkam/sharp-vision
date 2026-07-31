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
