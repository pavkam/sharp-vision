// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Support;

/// <summary>Verifies intrinsic Container scrolling geometry, offsets, clipping, and chrome.</summary>
public sealed class ContainerScrollTests
{
    /// <summary>Verifies an unarmed container reports an inert scroll state and clips overflow.</summary>
    [Fact]
    public void ScrollState_WhenNotArmed_IsInert()
    {
        LayoutProbe container = new();
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new Engine().Layout(container, new Size(4, 10));

        container.AutoScroll.ShouldBeFalse();
        container.Extent.ShouldBe(container.Viewport);
        container.VerticalOffset.ShouldBe(0);
        container.ScrollBy(0, 5).ShouldBeFalse();
    }

    /// <summary>Verifies an armed vertical container discovers the natural extent and clamps offsets.</summary>
    [Fact]
    public void Extent_WhenArmedVertical_IsNaturalContentHeight()
    {
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new Engine().Layout(container, new Size(4, 10));

        container.Extent.Height.ShouldBe(40);
        container.Viewport.Height.ShouldBe(10);
        container.ScrollBy(0, 1000).ShouldBeTrue();
        container.VerticalOffset.ShouldBe(30);
    }

    /// <summary>Verifies the child is translated by the vertical offset during arrange.</summary>
    [Fact]
    public void Arrange_WhenScrolled_TranslatesChildByOffset()
    {
        ProbeControl child = new(new Size(4, 40));
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        new Engine().Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 6);
        new Engine().Layout(container, new Size(4, 10));

        child.Bounds.Y.ShouldBe(-6);
    }
}
