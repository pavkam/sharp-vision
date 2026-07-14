// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Scrolling;
using SharpVision.Terminal.Input;
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

    /// <summary>Verifies disarming AutoScroll after scrolling restores the inert state.</summary>
    [Fact]
    public void ScrollState_WhenDisarmedAfterScrolling_ResetsToInert()
    {
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(container, new Size(4, 10));
        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);

        container.AutoScroll = false;
        new Engine().Layout(container, new Size(4, 10));

        container.VerticalOffset.ShouldBe(0);
        container.Extent.ShouldBe(container.Viewport);
        container.ScrollBy(0, 5).ShouldBeFalse();
    }

    /// <summary>Verifies an armed container renders the automatic vertical bar chrome.</summary>
    [Fact]
    public void Render_WhenVerticalBarIsAutomatic_UsesScrollBarGlyphs()
    {
        LayoutProbe container = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        container.Children.Add(new ProbeControl(new Size(1, 4)));
        Size size = new(3, 3);
        new Engine().Layout(container, size);
        using Frame frame = new(size);

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▲");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▼");
    }

    /// <summary>Verifies one automatic bar can induce the other, converging with both.</summary>
    [Fact]
    public void Layout_WhenAutomaticBarInducesOther_ConvergesWithBothBars()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        container.Children.Add(new ProbeControl(new Size(5, 4)));

        new Engine().Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(5, 4));
        container.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies the Down key advances the vertical offset by LineSize.</summary>
    [Fact]
    public void OnEvent_WhenDownKey_ScrollsByLineSize()
    {
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never, LineSize = 2 };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(container, new Size(4, 10));

        container.RaiseKey(Code.Down);

        container.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies unused wheel delta propagates to the nearest armed ancestor.</summary>
    [Fact]
    public void Wheel_WhenLeafAtEnd_PropagatesToArmedAncestor()
    {
        LayoutProbe outer = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        LayoutProbe inner = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        inner.Children.Add(new ProbeControl(new Size(4, 4)));   // inner cannot scroll (fits)
        outer.Children.Add(inner);
        // outer content taller than viewport via a second tall child
        outer.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(outer, new Size(4, 10));

        inner.RaiseWheel(0, -3);   // wheel over inner, which has no room

        outer.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a disabled container does not scroll on a key that would otherwise move the offset.</summary>
    [Fact]
    public void OnEvent_WhenDisabled_DoesNotScroll()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            IsEnabled = false,
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(container, new Size(4, 10));

        container.RaiseKey(Code.Down);

        container.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a committed offset change raises ScrollChanged with the cause.</summary>
    [Fact]
    public void ScrollBy_WhenOffsetChanges_RaisesScrollChanged()
    {
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(container, new Size(4, 10));
        ScrollChangedEventArgs? captured = null;
        container.ScrollChanged += (_, e) => captured = e;

        _ = container.ScrollBy(0, 3, Cause.Keyboard);

        _ = captured.ShouldNotBeNull();
        captured.Offset.ShouldBe(new Point(0, 3));
        captured.Extent.ShouldBe(container.Extent);
        captured.Viewport.ShouldBe(container.Viewport);
        captured.Cause.ShouldBe(Cause.Keyboard);
    }

    /// <summary>Verifies a no-op ScrollBy raises no ScrollChanged event.</summary>
    [Fact]
    public void ScrollBy_WhenOffsetUnchanged_DoesNotRaiseScrollChanged()
    {
        LayoutProbe container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new Engine().Layout(container, new Size(4, 10));
        bool raised = false;
        container.ScrollChanged += (_, _) => raised = true;

        container.ScrollBy(0, 0, Cause.Keyboard).ShouldBeFalse();

        raised.ShouldBeFalse();
    }

    /// <summary>Verifies BringIntoView scrolls minimally to expose a descendant below the viewport.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantBelowViewport_ScrollsToReveal()
    {
        Stack container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        ProbeControl target = new(new Size(4, 1));
        container.Children.Add(target);
        new Engine().Layout(container, new Size(4, 10));

        bool changed = container.BringIntoView(target);

        changed.ShouldBeTrue();
        container.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies BringIntoView rejects a control that is not a descendant of this container.</summary>
    [Fact]
    public void BringIntoView_WhenNotDescendant_ThrowsArgumentException()
    {
        Stack container = new() { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        new Engine().Layout(container, new Size(4, 10));
        ProbeControl stray = new(new Size(4, 1));

        _ = Should.Throw<ArgumentException>(() => container.BringIntoView(stray));
    }
}
