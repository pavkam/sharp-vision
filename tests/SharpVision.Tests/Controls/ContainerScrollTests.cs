// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Scrolling;
using SharpVision.Terminal.Input;
using SharpVision.Tests.Support;

using ControlText = SharpVision.Controls.Text;
using Wrapping = SharpVision.Text.Wrapping;

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

    /// <summary>
    /// Verifies arming AutoScroll creates the scrollbar chrome immediately from the
    /// property setter rather than lazily the first time this container arranges.
    /// Lazy creation added children to this container mid-arrange, which
    /// invalidates this container's own measure phase; a nested armed container
    /// doing the same on every frame could prevent the tree from ever settling.
    /// </summary>
    [Fact]
    public void AutoScroll_WhenArmed_CreatesBarsBeforeAnyLayoutPass()
    {
        LayoutProbe container = new();
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        int navigationCountBeforeArming = container.NavigationCount;

        container.AutoScroll = true;

        // NavigationCount folds in the owned scrollbar chrome once created
        // (see Container.NavigationCount), so its growth proves the bars now
        // exist before Layout ever ran once for this container.
        container.NavigationCount.ShouldBe(navigationCountBeforeArming + 2);
    }

    /// <summary>
    /// Verifies one Layout pass on a freshly armed container leaves no residual
    /// Measure invalidation, proving arrange no longer re-dirties this
    /// container's own measure phase by creating the scrollbar chrome.
    /// </summary>
    [Fact]
    public void Layout_WhenContainerArmsForTheFirstTime_ConvergesInOnePass()
    {
        LayoutProbe container = new() { AutoScroll = true };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new Engine().Layout(container, new Size(4, 10));

        // Render legitimately stays pending — Layout never renders — but a
        // second Measure/Arrange pass would mean the first one left the tree
        // dirty, which is exactly what lazy bar creation during arrange did.
        (container.Pending & (Invalidation.Measure | Invalidation.Arrange))
            .ShouldBe(Invalidation.None);
    }

    /// <summary>
    /// Verifies a Stack armed as scrolled content inside another armed Stack
    /// arranges correctly at a negative origin once scrolled past zero, rather
    /// than hanging. ResolveContentSlot legitimately shifts a scrolled
    /// container's content slot below its own top-left corner; Stack's
    /// internal arrange-origin accumulator must tolerate that without
    /// asserting.
    /// </summary>
    [Fact]
    public void Layout_WhenNestedArmedStacksScrollBeyondZero_ArrangesNegativeOriginContent()
    {
        ProbeControl leaf = new(new Size(4, 20));
        Stack inner = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf },
        };
        Stack outer = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { inner },
        };
        new Engine().Layout(outer, new Size(4, 4));

        _ = inner.ScrollBy(0, 10);
        new Engine().Layout(outer, new Size(4, 4));

        inner.VerticalOffset.ShouldBe(10);
        leaf.Bounds.Y.ShouldBe(-10);
    }

    /// <summary>Verifies an armed container renders the automatic horizontal bar chrome.</summary>
    [Fact]
    public void Render_WhenHorizontalBarIsAutomatic_UsesScrollBarGlyphs()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
        };
        container.Children.Add(new ProbeControl(new Size(4, 1)));
        Size size = new(3, 3);
        new Engine().Layout(container, size);
        using Frame frame = new(size);

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("◀");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▶");
    }

    /// <summary>Verifies passive viewport track cells use a shaded glyph that remains visually distinct from the thumb.</summary>
    [Fact]
    public void Render_WhenVerticalChromeHasUnoccupiedTrack_UsesShadedTrackGlyph()
    {
        LayoutProbe container = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        container.Children.Add(new ProbeControl(new Size(1, 100)));
        Size size = new(3, 6);
        new Engine().Layout(container, size);
        using Frame frame = new(size);

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("░");
    }

    /// <summary>Verifies exact fit does not show automatic bars while Always reserves both axes regardless of overflow.</summary>
    [Fact]
    public void Layout_WhenPoliciesDiffer_UsesExactFitAndAlwaysReservation()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        container.Children.Add(new ProbeControl(new Size(5, 3)));
        Engine engine = new();

        engine.Layout(container, new Size(5, 3));
        container.Viewport.ShouldBe(new Size(5, 3));

        container.HorizontalBarVisibility = ScrollBarVisibility.Always;
        container.VerticalBarVisibility = ScrollBarVisibility.Always;
        engine.Layout(container, new Size(5, 3));
        container.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies the common Never policy suppresses chrome without disabling the enabled overflow axis.</summary>
    [Fact]
    public void Layout_WhenScrollBarsAreVerticalAndNever_ShowsNoChromeButRetainsVerticalRange()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
        };
        container.Children.Add(new ProbeControl(new Size(8, 10)));

        new Engine().Layout(container, new Size(4, 3));

        container.Viewport.ShouldBe(new Size(4, 3));
        container.HorizontalOffset.ShouldBe(0);
        container.VerticalOffset.ShouldBe(0);
        container.ScrollBy(4, 4).ShouldBeTrue();
        container.HorizontalOffset.ShouldBe(0);
        container.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies a ScrollBy delta exceeding the extent clamps to the endpoint and raises exactly one event.</summary>
    [Fact]
    public void ScrollBy_WhenDeltaExceedsExtent_ClampsAndRaisesOneEvent()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        new Engine().Layout(container, new Size(5, 3));
        List<ScrollChangedEventArgs> changes = [];
        container.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => container.HorizontalOffset = 16);
        container.ScrollBy(int.MaxValue, int.MaxValue, Cause.Wheel).ShouldBeTrue();

        container.HorizontalOffset.ShouldBe(15);
        container.VerticalOffset.ShouldBe(7);
        changes.Count.ShouldBe(1);
        changes[0].PreviousOffset.ShouldBe(default);
        changes[0].Offset.ShouldBe(new Point(15, 7));
        changes[0].Cause.ShouldBe(Cause.Wheel);
    }

    /// <summary>Verifies a growing viewport clamps offsets before exposing the committed geometry.</summary>
    [Fact]
    public void Layout_WhenViewportGrows_ClampsOffsetsWithResizeCause()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        Engine engine = new();
        engine.Layout(container, new Size(5, 3));
        _ = container.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        container.ScrollChanged += (_, eventArgs) => change = eventArgs;

        engine.Layout(container, new Size(18, 9));

        container.HorizontalOffset.ShouldBe(2);
        container.VerticalOffset.ShouldBe(1);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(Cause.Resize);
    }

    /// <summary>Verifies arranged translation, viewport clipping, and hit testing agree once content is scrolled.</summary>
    [Fact]
    public void Render_WhenContentIsScrolled_ClipsAndTargetsOnlyViewport()
    {
        ProbeControl content = new(new Size(8, 1)) { Content = "ABCDEFGH".AsMemory() };
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(content);
        new Engine().Layout(container, new Size(4, 1));
        _ = container.ScrollBy(2, 0);
        new Engine().Layout(container, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        container.Render(frame.Canvas);

        content.Bounds.X.ShouldBe(-2);
        FrameOracle.Get(frame, default).ShouldBe("C");
        container.HitTest(new Point(0, 0)).ShouldBeSameAs(content);
        container.HitTest(new Point(3, 0)).ShouldBeSameAs(content);
    }

    /// <summary>Verifies a hidden horizontal bar gives word-wrapping content the committed viewport width during measurement.</summary>
    [Fact]
    public void Layout_WhenHorizontalBarIsHidden_ReflowsWordWrappedContentToViewportWidth()
    {
        ControlText text = new("one two three") { Wrapping = Wrapping.Word };
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(text);

        new Engine().Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(5, 3));
        container.Viewport.ShouldBe(new Size(5, 3));
    }

    /// <summary>Verifies wheel, arrows, pages, and endpoint keys share the typed command path.</summary>
    [Fact]
    public void OnEvent_WhenCommandsArrive_UsesLinePageAndEndpointChanges()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            LineSize = 2,
            PageOverlap = 1,
        };
        container.Children.Add(new ProbeControl(new Size(20, 20)));
        new Engine().Layout(container, new Size(5, 4));

        container.RaiseWheel(-1, -2);
        container.RaiseKey(Code.Right);
        container.RaiseKey(Code.PageDown);
        container.RaiseKey(Code.End);

        container.HorizontalOffset.ShouldBe(4);
        container.VerticalOffset.ShouldBe(16);
        container.RaiseKey(Code.Home);
        container.VerticalOffset.ShouldBe(0);
    }

    /// <summary>
    /// Verifies content shrink clamps both offsets before its change notification,
    /// reporting <see cref="Cause.Content"/> because the content extent (not the
    /// viewport) changed.
    /// </summary>
    [Fact]
    public void Layout_WhenContentShrinks_ClampsOffsets()
    {
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        Engine engine = new();
        engine.Layout(container, new Size(5, 3));
        _ = container.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        container.ScrollChanged += (_, eventArgs) => change = eventArgs;
        container.Children[0] = new ProbeControl(new Size(4, 2));

        engine.Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(4, 2));
        new Point(container.HorizontalOffset, container.VerticalOffset).ShouldBe(default);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(Cause.Content);
        change.Offset.ShouldBe(default);
    }

    /// <summary>Verifies horizontal clipping never exposes half of a two-cell grapheme.</summary>
    [Fact]
    public void Render_WhenOffsetCrossesWideRune_ClipsCompleteCellOwner()
    {
        ProbeControl content = new(new Size(3, 1)) { Content = "界A".AsMemory() };
        LayoutProbe container = new()
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        container.Children.Add(content);
        new Engine().Layout(container, new Size(2, 1));
        _ = container.ScrollBy(1, 0);
        new Engine().Layout(container, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        frame.GetCell(default).IsContinuation.ShouldBeFalse();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("A");
    }

    /// <summary>Verifies disposal releases the child content and the owned composed bar chrome exactly once.</summary>
    [Fact]
    public void Dispose_WhenArmed_ReleasesCompleteComposedTree()
    {
        ProbeControl content = new();
        LayoutProbe container = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
        };
        container.Children.Add(content);
        new Engine().Layout(container, new Size(8, 4));
        ScrollBar horizontal = container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();
        ScrollBar vertical = container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>();

        container.Dispose();

        container.IsDisposed.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
        horizontal.IsDisposed.ShouldBeTrue();
        vertical.IsDisposed.ShouldBeTrue();
    }
}
