// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Support;

/// <summary>Verifies AutoSize grow/shrink on a container.</summary>
public sealed class ContainerAutoSizeTests
{
    /// <summary>Verifies AutoSize shrink-wraps a stretched container to its content.</summary>
    [Fact]
    public void AutoSize_WhenStretchedSlot_SizesToContent()
    {
        LayoutProbe container = new() { AutoSize = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        container.Children.Add(new ProbeControl(new Size(5, 3)) { HorizontalAlignment = HorizontalAlignment.Left });

        new Engine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(5);
        container.Bounds.Height.ShouldBe(3);
    }

    /// <summary>Verifies GrowOnly keeps the explicit fixed width as a floor.</summary>
    [Fact]
    public void AutoSizeGrowOnly_WhenContentSmallerThanFixedWidth_KeepsFixedWidth()
    {
        LayoutProbe container = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            Width = Length.Cells(10),
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new Engine().Layout(container, new Size(40, 40));

        container.Bounds.Width.ShouldBe(10);
    }
}
