// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies intrinsic Container scrolling geometry, offsets, clipping, and chrome.</summary>
public sealed class ContainerTests
{
    /// <summary>Verifies scrolling getters reject access after disposal instead of returning stale
    /// retained values contrary to their documented lifetime contract.</summary>
    [Fact]
    public void ScrollingProperties_WhenContainerIsDisposed_GettersThrow()
    {
        // Arrange
        var container = new LayoutProbe
        {
            LineSize = 2,
            PageOverlap = 1,
            ShowScrollBars = ShowScrollBars.Always
        };
        container.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => _ = container.HorizontalOffset);
        _ = Should.Throw<ObjectDisposedException>(() => _ = container.VerticalOffset);
        _ = Should.Throw<ObjectDisposedException>(() => _ = container.LineSize);
        _ = Should.Throw<ObjectDisposedException>(() => _ = container.PageOverlap);
        _ = Should.Throw<ObjectDisposedException>(() => _ = container.ShowScrollBars);
    }

    /// <summary>Verifies reentrant AutoScroll policy wins both arming directions, including the
    /// generated scrollbar ownership transaction.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoScroll_WhenPropertyObserverReversesValue_AppliesFinalPolicy(bool requested)
    {
        var container = new LayoutProbe
        {
            AutoScroll = !requested,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        container.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Container.AutoScroll) &&
                container.AutoScroll == requested)
            {
                container.AutoScroll = !requested;
            }
        };

        container.AutoScroll = requested;

        container.AutoScroll.ShouldBe(!requested);
        OwnedTree.FindAll<ScrollBar>(container).Count.ShouldBe(requested ? 0 : 2);
    }

    /// <summary>Verifies every common scrollbar policy applies the final reentrant value to both
    /// independent axis properties.</summary>
    [Theory]
    [InlineData(ShowScrollBars.Never, ShowScrollBars.Always)]
    [InlineData(ShowScrollBars.WhenNeeded, ShowScrollBars.Never)]
    [InlineData(ShowScrollBars.Always, ShowScrollBars.WhenNeeded)]
    public void ShowScrollBars_WhenPropertyObserverChangesPolicy_AppliesFinalPolicy(
        ShowScrollBars requested,
        ShowScrollBars replacement)
    {
        var container = new LayoutProbe();

        if (container.ShowScrollBars == requested)
        {
            container.ShowScrollBars = replacement;
        }

        container.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Container.ShowScrollBars) &&
                container.ShowScrollBars == requested)
            {
                container.ShowScrollBars = replacement;
            }
        };

        container.ShowScrollBars = requested;

        var expected = replacement switch
        {
            ShowScrollBars.Never => ScrollBarVisibility.Hidden,
            ShowScrollBars.WhenNeeded => ScrollBarVisibility.Auto,
            ShowScrollBars.Always => ScrollBarVisibility.Always,
            _ => throw new UnreachableException()
        };
        container.ShowScrollBars.ShouldBe(replacement);
        container.HorizontalBarVisibility.ShouldBe(expected);
        container.VerticalBarVisibility.ShouldBe(expected);
    }

    /// <summary>Verifies an unarmed container reports an inert scroll state and clips overflow.</summary>
    [Fact]
    public void ScrollState_WhenNotArmed_IsInert()
    {
        var container = new LayoutProbe();
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

        container.AutoScroll.ShouldBeFalse();
        container.Extent.ShouldBe(container.Viewport);
        container.VerticalOffset.ShouldBe(0);
        container.ScrollBy(0, 5).ShouldBeFalse();
    }

    /// <summary>Verifies an armed vertical container discovers the natural extent and clamps offsets.</summary>
    [Fact]
    public void Extent_WhenArmedVertical_IsNaturalContentHeight()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

        container.Extent.Height.ShouldBe(40);
        container.Viewport.Height.ShouldBe(10);
        container.ScrollBy(0, 1000).ShouldBeTrue();
        container.VerticalOffset.ShouldBe(30);
    }

    /// <summary>Verifies the child is translated by the vertical offset during arrange.</summary>
    [Fact]
    public void Arrange_WhenScrolled_TranslatesChildByOffset()
    {
        var child = new ProbeControl(new Size(4, 40));
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        new LayoutEngine().Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 6);
        new LayoutEngine().Layout(container, new Size(4, 10));

        child.Bounds.Y.ShouldBe(-6);
    }

    /// <summary>Verifies disarming AutoScroll after scrolling restores the inert state.</summary>
    [Fact]
    public void ScrollState_WhenDisarmedAfterScrolling_ResetsToInert()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);

        container.AutoScroll = false;
        new LayoutEngine().Layout(container, new Size(4, 10));

        container.VerticalOffset.ShouldBe(0);
        container.Extent.ShouldBe(container.Viewport);
        container.ScrollBy(0, 5).ShouldBeFalse();
    }

    /// <summary>Verifies disarming AutoScroll on a scrolled container raises ScrollChanged and
    /// resynchronizes the generated ScrollBar parts instead of silently zeroing the internal
    /// offset fields.</summary>
    [Fact]
    public void AutoScroll_WhenDisarmedAfterScrolling_RaisesScrollChangedAndResynchronizesBars()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(31);
        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        ScrollChangedEventArgs? captured = null;
        container.ScrollChanged += (_, e) => captured = e;

        container.AutoScroll = false;

        container.VerticalOffset.ShouldBe(0);
        var change = captured.ShouldNotBeNull();
        change.PreviousOffset.ShouldBe(new Point(0, 31));
        change.Offset.ShouldBe(new Point(0, 0));
        bar.Value.ShouldBe(0);
    }

    /// <summary>
    /// Verifies generated scrollbar chrome never changes public tab traversal,
    /// including while a previously visible scrolling container is disarmed.
    /// </summary>
    [Fact]
    public void AutoScroll_WhenDisarmedAfterBarsWereVisible_CollapsesBothBars()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)) { IsFocusable = true });
        var navigationCount = container.NavigationCount;
        new LayoutEngine().Layout(container, new Size(4, 10));
        container.NavigationCount.ShouldBe(navigationCount);

        container.AutoScroll = false;

        container.NavigationCount.ShouldBe(navigationCount);
    }

    /// <summary>
    /// Verifies generated scrollbar chrome remains private to an armed Stack's
    /// interaction model and cannot become an extra public tab stop.
    /// </summary>
    [Fact]
    public async Task MoveNext_WhenStackIsArmed_SkipsPrivateGeneratedBarsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var panel = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        var child = new ProbeControl(new Size(4, 40)) { IsFocusable = true };
        panel.Children.Add(child);

        await dispatcher.InvokeAsync(() =>
        {
            panel.Attach(dispatcher);
            new LayoutEngine().Layout(panel, new Size(4, 10));
            using FocusManager focus = new(panel);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(child);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an armed container renders the automatic vertical bar chrome.</summary>
    [Fact]
    public void Render_WhenVerticalBarIsAutomatic_UsesScrollBarGlyphs()
    {
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(new ProbeControl(new Size(1, 4)));
        var size = new Size(3, 3);
        new LayoutEngine().Layout(container, size);
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
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(new ProbeControl(new Size(5, 4)));

        new LayoutEngine().Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(5, 4));
        container.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies the Down key advances the vertical offset by LineSize.</summary>
    [Fact]
    public void OnEvent_WhenDownKey_ScrollsByLineSize()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never, LineSize = 2 };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        container.RaiseKey(Code.Down);

        container.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies unused wheel delta propagates to the nearest armed ancestor.</summary>
    [Fact]
    public void Wheel_WhenLeafAtEnd_PropagatesToArmedAncestor()
    {
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        var inner = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        inner.Children.Add(new ProbeControl(new Size(4, 4))); // inner cannot scroll (fits)
        outer.Children.Add(inner);
        // outer content taller than viewport via a second tall child
        outer.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(outer, new Size(4, 10));

        inner.RaiseWheel(0, -3); // wheel over inner, which has no room

        outer.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a leaf that moves any amount - even less than the full requested delta -
    /// keeps the whole wheel record for itself instead of handing the unconsumed part outward
    /// within the same event: latching, not remainder-chaining, is the documented contract
    /// (docs/concepts/scrolling.md). Only a later, separate wheel record - once the leaf is fully
    /// at its endpoint and moves nothing - reaches the ancestor.</summary>
    [Fact]
    public void Wheel_WhenLeafPartiallyConsumesTheDelta_DoesNotAlsoScrollTheAncestor()
    {
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        var inner = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            LineSize = 3,
            Height = Length.Cells(5)
        };
        inner.Children.Add(new ProbeControl(new Size(4, 10))); // inner has 5 cells of room (10 - 5)
        outer.Children.Add(inner);
        // outer content taller than viewport via a second tall child
        outer.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(outer, new Size(4, 10));

        inner.RaiseWheel(0, -2); // 2 notches * LineSize 3 = 6 cells requested, only 5 available

        inner.VerticalOffset.ShouldBe(5);
        outer.VerticalOffset.ShouldBe(0);

        // A later, identical record now moves nothing at inner (already at its endpoint), so it
        // reaches outer instead - this is the "next unchanged wheel event" scrolling.md describes.
        inner.RaiseWheel(0, -2);

        inner.VerticalOffset.ShouldBe(5);
        outer.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies an unconsumed keyboard scroll command propagates to the nearest armed
    /// ancestor exactly like wheel input, instead of dead-ending and marking the key handled
    /// anyway.</summary>
    [Fact]
    public void Key_WhenLeafAtEnd_PropagatesToArmedAncestor()
    {
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        var inner = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        inner.Children.Add(new ProbeControl(new Size(4, 4))); // inner cannot scroll (fits)
        outer.Children.Add(inner);
        // outer content taller than viewport via a second tall child
        outer.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(outer, new Size(4, 10));

        inner.RaiseKey(Code.Down);

        outer.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies a key that cannot move any offset - on an axis this container does not
    /// scroll, with no armed ancestor to hand off to - is left unhandled instead of being consumed
    /// for nothing.</summary>
    [Fact]
    public void Key_WhenAxisCannotScrollAndNoAncestor_LeavesEventUnhandled()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        var result = Router.Route(
            container,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.Right, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press)));

        result.IsHandled.ShouldBeFalse();
        container.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>Verifies PageUp/PageDown and Home/End drive the horizontal offset on a
    /// horizontal-only container instead of being consumed for no effect - the vertical-only
    /// mapping left it with no fast-travel key at all.</summary>
    [Fact]
    public void Key_WhenHorizontalOnlyContainer_PageAndEndpointKeysMoveHorizontalOffset()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            ShowScrollBars = ShowScrollBars.Never,
            PageOverlap = 1
        };
        container.Children.Add(new ProbeControl(new Size(20, 4)));
        new LayoutEngine().Layout(container, new Size(5, 4));

        container.RaiseKey(Code.PageDown);

        container.HorizontalOffset.ShouldBe(4); // Viewport.Width(5) - PageOverlap(1)
        container.VerticalOffset.ShouldBe(0);

        container.RaiseKey(Code.End);

        container.HorizontalOffset.ShouldBe(15); // Extent.Width(20) - Viewport.Width(5)

        container.RaiseKey(Code.Home);

        container.HorizontalOffset.ShouldBe(0);
    }

    /// <summary>
    /// Verifies PageDown still advances by at least one cell when PageOverlap is configured at or
    /// above the viewport extent. PageOverlap has no configured upper bound tying it to the
    /// viewport (only non-negativity is validated), so an overlap this large previously computed
    /// a page step of exactly zero, silently turning PageUp/PageDown into permanent no-ops for
    /// that axis instead of degrading to a smaller step.
    /// </summary>
    [Fact]
    public void Key_WhenPageOverlapIsAtLeastTheViewportExtent_StillAdvancesByAtLeastOneCell()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            PageOverlap = 100
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        var result = Router.Route(
            container,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.PageDown, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press)));

        result.IsHandled.ShouldBeTrue();
        container.VerticalOffset.ShouldBe(1);
    }

    /// <summary>
    /// Verifies PageDown still advances by at least one cell when the page-axis Viewport extent
    /// is itself exactly zero, not just when PageOverlap consumes it. An always-visible horizontal
    /// scrollbar claims the container's only available row here, so Viewport.Height computes to
    /// Math.Max(0, 1 - 1) = 0 even though content is several rows tall - previously computing a
    /// page step of exactly zero and silently turning PageUp/PageDown into permanent no-ops for
    /// that axis instead of degrading to a smaller step.
    /// </summary>
    [Fact]
    public void Key_WhenViewportExtentIsZero_StillAdvancesByAtLeastOneCell()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 1));

        var result = Router.Route(
            container,
            Events.Key,
            new KeyEventArgs(new Stroke(Code.PageDown, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press)));

        result.IsHandled.ShouldBeTrue();
        container.VerticalOffset.ShouldBe(1);
    }

    /// <summary>Verifies a generated vertical scrollbar's SmallChange mirrors the owning
    /// container's LineSize instead of being left at the ScrollBar's own default.</summary>
    [Fact]
    public void Synchronize_WhenBarsAreGenerated_SetsSmallChangeToLineSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            LineSize = 3
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.SmallChange.ShouldBe(3);
    }

    /// <summary>Verifies a generated vertical scrollbar's LargeChange mirrors the same page-step
    /// computation (viewport extent minus PageOverlap) already used for keyboard PageUp/PageDown,
    /// rather than the raw viewport extent - so a scrollbar-driven page click keeps the same
    /// overlap as the keyboard equivalent.</summary>
    [Fact]
    public void Synchronize_WhenBarsAreGenerated_SetsLargeChangeToPageStepOfViewport()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            PageOverlap = 1
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.LargeChange.ShouldBe(9); // Viewport.Height(10) - PageOverlap(1)
    }

    /// <summary>
    /// Verifies a generated vertical scrollbar's LargeChange is clamped to at least one cell when
    /// PageOverlap is configured at or above the viewport extent, mirroring the clamp already
    /// verified for keyboard PageUp/PageDown in
    /// Key_WhenPageOverlapIsAtLeastTheViewportExtent_StillAdvancesByAtLeastOneCell above - a
    /// scrollbar-driven page click must not silently become a no-op either.
    /// </summary>
    [Fact]
    public void Synchronize_WhenPageOverlapExceedsViewport_ClampsGeneratedBarLargeChangeToAtLeastOneCell()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            PageOverlap = 100
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.LargeChange.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a generated vertical scrollbar's LargeChange is clamped to at least one cell when
    /// the page-axis Viewport extent itself is exactly zero, mirroring the clamp already verified
    /// for keyboard PageUp/PageDown in Key_WhenViewportExtentIsZero_StillAdvancesByAtLeastOneCell
    /// above - a scrollbar-driven page click must not silently become a no-op either. The vertical
    /// bar is located via the owned control tree rather than HitTest because a zero-height
    /// Viewport also collapses the generated vertical bar's own on-screen rectangle to zero cells,
    /// leaving it unreachable by a point hit-test even though it still exists and is synchronized.
    /// </summary>
    [Fact]
    public void Synchronize_WhenViewportExtentIsZero_ClampsGeneratedBarLargeChangeToAtLeastOneCell()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 1));

        var bar = OwnedTree.FindAll<ScrollBar>(container)
            .Single(candidate => candidate.Orientation == Orientation.Vertical);
        bar.LargeChange.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a generated bar's SmallChange still reflects a changed LineSize after a
    /// subsequent, genuinely different-size layout pass. LineSize's setter already pushes the new
    /// value onto the generated bar immediately (see
    /// LineSize_WhenChangedAfterLayout_UpdatesGeneratedBarSmallChangeWithoutRelayout); this test
    /// pins that a later relayout - which independently resynchronizes the bar via
    /// ArrangeOverlays -> Synchronize - reconfirms the same value instead of reverting it.
    /// </summary>
    [Fact]
    public void Synchronize_WhenLineSizeChangesAndRelayoutOccurs_ReconfirmsGeneratedBarSmallChange()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            LineSize = 2
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.SmallChange.ShouldBe(2);

        container.LineSize = 5;
        new LayoutEngine().Layout(container, new Size(4, 11));

        bar.SmallChange.ShouldBe(5);
    }

    /// <summary>Verifies a generated bar's SmallChange picks up a changed LineSize immediately, at
    /// setter time, without requiring any subsequent Layout pass.</summary>
    [Fact]
    public void LineSize_WhenChangedAfterLayout_UpdatesGeneratedBarSmallChangeWithoutRelayout()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            LineSize = 2
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.SmallChange.ShouldBe(2);

        container.LineSize = 5;

        bar.SmallChange.ShouldBe(5);
    }

    /// <summary>Verifies a generated bar's LargeChange picks up a changed PageOverlap immediately,
    /// at setter time, without requiring any subsequent Layout pass.</summary>
    [Fact]
    public void PageOverlap_WhenChangedAfterLayout_UpdatesGeneratedBarLargeChangeWithoutRelayout()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            PageOverlap = 1
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var bar = container.HitTest(new Point(3, 4)).ShouldBeOfType<ScrollBar>();
        bar.LargeChange.ShouldBe(9); // Viewport.Height(10) - PageOverlap(1)

        container.PageOverlap = 3;

        bar.LargeChange.ShouldBe(7); // Viewport.Height(10) - PageOverlap(3)
    }

    /// <summary>Verifies scroll ancestry crosses a non-Container owner but selects only armed Container ancestors.</summary>
    [Fact]
    public void Wheel_WhenNonContainerBridgesArmedContainers_PropagatesToActualContainer()
    {
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        var bridge = new TraversalOwner();
        var inner = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        var content = new ProbeControl(new Size(4, 4));
        inner.Children.Add(content);
        bridge.AddNormal(inner);
        outer.Children.Add(bridge);
        outer.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(outer, new Size(4, 10));

        inner.RaiseWheel(0, -3);

        inner.VerticalOffset.ShouldBe(0);
        outer.VerticalOffset.ShouldBeGreaterThan(0);

        // LayoutProbe's naive union arrange stretches every child - including content - to the
        // full (4, 40) content slot, so content is 40 cells tall inside outer's 10-cell viewport
        // and can never be fully contained regardless of offset. BringIntoView now reports that
        // honestly instead of returning true merely because some offset changed.
        outer.BringIntoView(content).ShouldBeFalse();
    }

    /// <summary>Verifies a disabled container does not scroll on a key that would otherwise move the offset.</summary>
    [Fact]
    public void OnEvent_WhenDisabled_DoesNotScroll()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            IsEnabled = false
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        container.RaiseKey(Code.Down);

        container.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies a committed offset change raises ScrollChanged with the cause.</summary>
    [Fact]
    public void ScrollBy_WhenOffsetChanges_RaisesScrollChanged()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        ScrollChangedEventArgs? captured = null;
        container.ScrollChanged += (_, e) => captured = e;

        _ = container.ScrollBy(0, 3, ScrollCause.Keyboard);

        _ = captured.ShouldNotBeNull();
        captured.Offset.ShouldBe(new Point(0, 3));
        captured.Extent.ShouldBe(container.Extent);
        captured.Viewport.ShouldBe(container.Viewport);
        captured.Cause.ShouldBe(ScrollCause.Keyboard);
    }

    /// <summary>Verifies a no-op ScrollBy raises no ScrollChanged event.</summary>
    [Fact]
    public void ScrollBy_WhenOffsetUnchanged_DoesNotRaiseScrollChanged()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var raised = false;
        container.ScrollChanged += (_, _) => raised = true;

        container.ScrollBy(0, 0, ScrollCause.Keyboard).ShouldBeFalse();

        raised.ShouldBeFalse();
    }

    /// <summary>Verifies reentry from an earlier ScrollChanged subscriber prevents later subscribers
    /// from receiving the obsolete outer transition.</summary>
    [Fact]
    public void ScrollChanged_WhenSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var observed = new List<(Point EventOffset, Point LiveOffset)>();
        container.ScrollChanged += (_, eventArgs) =>
        {
            if (eventArgs.Offset == new Point(0, 3))
            {
                _ = container.ScrollBy(0, 1, ScrollCause.Keyboard);
            }
        };
        container.ScrollChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Offset, new Point(container.HorizontalOffset, container.VerticalOffset)));

        // Act
        _ = container.ScrollBy(0, 3, ScrollCause.Keyboard);

        // Assert
        observed.ShouldBe([(new Point(0, 4), new Point(0, 4))]);
    }

    /// <summary>Verifies BringIntoView scrolls minimally to expose a descendant below the viewport.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantBelowViewport_ScrollsToReveal()
    {
        var container = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        var target = new ProbeControl(new Size(4, 1));
        container.Children.Add(target);
        new LayoutEngine().Layout(container, new Size(4, 10));

        var changed = container.BringIntoView(target);

        changed.ShouldBeTrue();
        container.VerticalOffset.ShouldBeGreaterThan(0);
    }

    /// <summary>Verifies BringIntoView walks through an intervening armed container, scrolling it
    /// too, instead of saturating only the receiver's own offset and returning a false "true"
    /// while the descendant stays outside the visible rows.</summary>
    [Fact]
    public void BringIntoView_WhenAnIntermediateArmedContainerSitsBetween_ScrollsBothAndFullyReveals()
    {
        var inner = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4)
        };
        var target = new ProbeControl(new Size(4, 2));
        inner.Children.Add(new ProbeControl(new Size(4, 6)));
        inner.Children.Add(target);
        var outer = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        outer.Children.Add(new ProbeControl(new Size(4, 20)));
        outer.Children.Add(inner);
        new LayoutEngine().Layout(outer, new Size(4, 10));

        var result = outer.BringIntoView(target);

        result.ShouldBeTrue();
        inner.VerticalOffset.ShouldBe(4);
        outer.VerticalOffset.ShouldBe(14);
    }

    /// <summary>Verifies BringIntoView reports false when the receiver's own extent boundary
    /// still leaves the descendant outside the viewport after scrolling every intervening armed
    /// container as far as it can.</summary>
    [Fact]
    public void BringIntoView_WhenExtentBoundaryStillExcludesTheDescendant_ReturnsFalse()
    {
        var container = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        var target = new ProbeControl(new Size(4, 40));
        container.Children.Add(target);
        new LayoutEngine().Layout(container, new Size(4, 10));

        // target is 40 cells tall, far larger than the 10-cell viewport, so no offset can ever
        // fully contain it.
        container.BringIntoView(target).ShouldBeFalse();
        var revealedOffset = container.VerticalOffset;
        new LayoutEngine().Layout(container, new Size(4, 10));

        container.BringIntoView(target).ShouldBeFalse();
        container.VerticalOffset.ShouldBe(revealedOffset);
    }

    /// <summary>Verifies BringIntoView rejects a control that is not a descendant of this container.</summary>
    [Fact]
    public void BringIntoView_WhenNotDescendant_ThrowsArgumentException()
    {
        var container = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        new LayoutEngine().Layout(container, new Size(4, 10));
        var stray = new ProbeControl(new Size(4, 1));

        _ = Should.Throw<ArgumentException>(() => container.BringIntoView(stray));
    }

    /// <summary>Verifies BringIntoView rejects a null descendant before any offset changes.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantIsNull_ThrowsArgumentNullException()
    {
        var container = new Stack { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 20)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        _ = Should.Throw<ArgumentNullException>(() => container.BringIntoView(null!));

        container.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies ScrollBy rejects an undefined cause before any offset changes.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        new LayoutEngine().Layout(container, new Size(4, 10));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => container.ScrollBy(0, 1, (ScrollCause) int.MaxValue));

        container.VerticalOffset.ShouldBe(0);
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
        var container = new LayoutProbe();
        container.Children.Add(new ProbeControl(new Size(4, 40)));
        var navigationCountBeforeArming = container.NavigationCount;

        container.AutoScroll = true;

        // Generated bars are framework-private and therefore cannot change the
        // container's public sequential-navigation participants.
        container.NavigationCount.ShouldBe(navigationCountBeforeArming);
    }

    /// <summary>
    /// Verifies one Layout pass on a freshly armed container leaves no residual
    /// Measure invalidation, proving arrange no longer re-dirties this
    /// container's own measure phase by creating the scrollbar chrome.
    /// </summary>
    [Fact]
    public void Layout_WhenContainerArmsForTheFirstTime_ConvergesInOnePass()
    {
        var container = new LayoutProbe { AutoScroll = true };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(4, 10));

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
        var leaf = new ProbeControl(new Size(4, 20));
        var inner = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { inner }
        };
        new LayoutEngine().Layout(outer, new Size(4, 4));

        _ = inner.ScrollBy(0, 10);
        new LayoutEngine().Layout(outer, new Size(4, 4));

        inner.VerticalOffset.ShouldBe(10);
        leaf.Bounds.Y.ShouldBe(-10);
    }

    /// <summary>Verifies an armed container renders the automatic horizontal bar chrome.</summary>
    [Fact]
    public void Render_WhenHorizontalBarIsAutomatic_UsesScrollBarGlyphs()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            HorizontalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(new ProbeControl(new Size(4, 1)));
        var size = new Size(3, 3);
        new LayoutEngine().Layout(container, size);
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
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(new ProbeControl(new Size(1, 100)));
        var size = new Size(3, 6);
        new LayoutEngine().Layout(container, size);
        using Frame frame = new(size);

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("░");
    }

    /// <summary>Verifies exact fit does not show automatic bars while Always reserves both axes regardless of overflow.</summary>
    [Fact]
    public void Layout_WhenPoliciesDiffer_UsesExactFitAndAlwaysReservation()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(new ProbeControl(new Size(5, 3)));
        var engine = new LayoutEngine();

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
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never
        };
        container.Children.Add(new ProbeControl(new Size(8, 10)));

        new LayoutEngine().Layout(container, new Size(4, 3));

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
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        new LayoutEngine().Layout(container, new Size(5, 3));
        List<ScrollChangedEventArgs> changes = [];
        container.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => container.HorizontalOffset = 16);
        container.ScrollBy(int.MaxValue, int.MaxValue, ScrollCause.Wheel).ShouldBeTrue();

        container.HorizontalOffset.ShouldBe(15);
        container.VerticalOffset.ShouldBe(7);
        changes.Count.ShouldBe(1);
        changes[0].PreviousOffset.ShouldBe(default);
        changes[0].Offset.ShouldBe(new Point(15, 7));
        changes[0].Cause.ShouldBe(ScrollCause.Wheel);
    }

    /// <summary>Verifies a growing viewport clamps offsets before exposing the committed geometry.</summary>
    [Fact]
    public void Layout_WhenViewportGrows_ClampsOffsetsWithResizeCause()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(5, 3));
        _ = container.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        container.ScrollChanged += (_, eventArgs) => change = eventArgs;

        engine.Layout(container, new Size(18, 9));

        container.HorizontalOffset.ShouldBe(2);
        container.VerticalOffset.ShouldBe(1);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(ScrollCause.Resize);
    }

    /// <summary>Verifies arranged translation, viewport clipping, and hit testing agree once content is scrolled.</summary>
    [Fact]
    public void Render_WhenContentIsScrolled_ClipsAndTargetsOnlyViewport()
    {
        var content = new ProbeControl(new Size(8, 1)) { Content = "ABCDEFGH".AsMemory() };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(content);
        new LayoutEngine().Layout(container, new Size(4, 1));
        _ = container.ScrollBy(2, 0);
        new LayoutEngine().Layout(container, new Size(4, 1));
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
        var text = new ControlText("one two three") { Overflow = Overflow.Wrap };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(text);

        new LayoutEngine().Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(5, 3));
        container.Viewport.ShouldBe(new Size(5, 3));
    }

    /// <summary>Verifies word-wrapping content is measured at the width the reserved always-visible
    /// vertical bar actually leaves it, not the full padded width - otherwise the extra row the
    /// narrower width forces is never laid out at all, and no VerticalOffset can reveal it.</summary>
    [Fact]
    public void Layout_WhenVerticalBarIsAlwaysVisible_MeasuresWordWrappedContentAtViewportWidth()
    {
        var text = new ControlText("aaaaaaaaa bb") { Overflow = Overflow.Wrap };
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(text);

        new LayoutEngine().Layout(container, new Size(12, 4));

        // The viewport is 11 cells wide (12 minus the always-reserved bar column). The 12-character
        // content ("aaaaaaaaa bb") fits exactly on one row at the full 12-cell width but not at 11,
        // so it must wrap onto two rows there. Measuring against the unclamped 12-cell width (the
        // bug) fits it on one row.
        container.Viewport.Width.ShouldBe(11);
        container.Extent.Height.ShouldBe(2);
        text.DesiredSize.Height.ShouldBe(2);
    }

    /// <summary>Verifies an automatically-added vertical bar re-measures word-wrapped content at the
    /// narrower reserved width instead of comparing the unreserved extent against the reduced
    /// viewport: the reserved column is what makes the content tall enough to need the bar in the
    /// first place, so the probe must feed its own reservation back into the content it measures.</summary>
    [Fact]
    public void Layout_WhenVerticalBarIsAutoInduced_ReflowsWordWrappedContentBeforeCommittingExtent()
    {
        var text = new ControlText("one two three") { Overflow = Overflow.Wrap };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(text);

        // At width 5 the unreserved content wraps to 3 lines, which overflows the 2-row viewport
        // and adds the bar. Once added, the reservation narrows the width to 4, which wraps the
        // content to 4 lines instead of 3 - the extent that should actually be committed.
        new LayoutEngine().Layout(container, new Size(5, 2));

        container.Extent.ShouldBe(new Size(4, 4));
        container.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies wheel, arrows, pages, and endpoint keys share the typed command path.</summary>
    [Fact]
    public void OnEvent_WhenCommandsArrive_UsesLinePageAndEndpointChanges()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            LineSize = 2,
            PageOverlap = 1
        };
        container.Children.Add(new ProbeControl(new Size(20, 20)));
        new LayoutEngine().Layout(container, new Size(5, 4));

        container.RaiseWheel(1, -2);
        container.RaiseKey(Code.Right);
        container.RaiseKey(Code.PageDown);
        container.RaiseKey(Code.End);

        container.HorizontalOffset.ShouldBe(4);
        container.VerticalOffset.ShouldBe(16);
        container.RaiseKey(Code.Home);
        container.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies wheel tilt/scroll direction agrees with the equivalent arrow key on both
    /// axes: tilting right moves the content exactly like pressing Right, and scrolling down moves
    /// it exactly like pressing Down.</summary>
    [Fact]
    public void OnEvent_WhenWheelIsUsed_MatchesTheEquivalentArrowKeyDirection()
    {
        var wheelContainer = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            LineSize = 2
        };
        wheelContainer.Children.Add(new ProbeControl(new Size(20, 20)));
        new LayoutEngine().Layout(wheelContainer, new Size(5, 4));

        var keyContainer = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            LineSize = 2
        };
        keyContainer.Children.Add(new ProbeControl(new Size(20, 20)));
        new LayoutEngine().Layout(keyContainer, new Size(5, 4));

        wheelContainer.RaiseWheel(1, -1);
        keyContainer.RaiseKey(Code.Right);
        keyContainer.RaiseKey(Code.Down);

        wheelContainer.HorizontalOffset.ShouldBe(keyContainer.HorizontalOffset);
        wheelContainer.VerticalOffset.ShouldBe(keyContainer.VerticalOffset);
    }

    /// <summary>
    /// Verifies content shrink clamps both offsets before its change notification,
    /// reporting <see cref="ScrollCause.Content"/> because the content extent (not the
    /// viewport) changed.
    /// </summary>
    [Fact]
    public void Layout_WhenContentShrinks_ClampsOffsets()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(5, 3));
        _ = container.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        container.ScrollChanged += (_, eventArgs) => change = eventArgs;
        container.Children[0] = new ProbeControl(new Size(4, 2));

        engine.Layout(container, new Size(5, 3));

        container.Extent.ShouldBe(new Size(4, 2));
        new Point(container.HorizontalOffset, container.VerticalOffset).ShouldBe(default);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(ScrollCause.Content);
        change.Offset.ShouldBe(default);
    }

    /// <summary>Verifies horizontal clipping never exposes half of a two-cell grapheme.</summary>
    [Fact]
    public void Render_WhenOffsetCrossesWideRune_ClipsCompleteCellOwner()
    {
        var content = new ProbeControl(new Size(3, 1)) { Content = "界A".AsMemory() };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden
        };
        container.Children.Add(content);
        new LayoutEngine().Layout(container, new Size(2, 1));
        _ = container.ScrollBy(1, 0);
        new LayoutEngine().Layout(container, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        container.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        frame.GetCell(default).Continuation.ShouldBeFalse();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("A");
    }

    /// <summary>Verifies disposal releases the child content and the owned composed bar chrome exactly once.</summary>
    [Fact]
    public void Dispose_WhenArmed_ReleasesCompleteComposedTree()
    {
        var content = new ProbeControl();
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always
        };
        container.Children.Add(content);
        new LayoutEngine().Layout(container, new Size(8, 4));
        var horizontal = container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();
        var vertical = container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>();

        container.Dispose();

        container.IsDisposed.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
        horizontal.IsDisposed.ShouldBeTrue();
        vertical.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies generated bars inherit Theme styles without local copies or reconstruction.</summary>
    [Fact]
    public void Theme_WhenScrollBarStyleChanges_UpdatesExistingGeneratedBars()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.Always
        };
        container.Children.Add(new ProbeControl(new Size(20, 10)));
        var thinTheme = CreateTheme(ScrollBarStyle.ThinLine);
        var fullTheme = CreateTheme(ScrollBarStyle.FullBlock);
        container.PropagateTheme(thinTheme);
        new LayoutEngine().Layout(container, new Size(8, 4));
        var horizontal = container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();
        var vertical = container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>();

        container.ScrollBarStyle.ShouldBeNull();
        container.ActualScrollBarStyle.ShouldBe(ScrollBarStyle.Default);
        horizontal.Style.ShouldBeNull();
        vertical.Style.ShouldBeNull();
        horizontal.Theme.ShouldBeSameAs(thinTheme);
        vertical.Theme.ShouldBeSameAs(thinTheme);
        horizontal.ActualStyle.ShouldBe(ScrollBarStyle.Default);
        vertical.ActualStyle.ShouldBe(ScrollBarStyle.Default);

        container.PropagateTheme(fullTheme);
        new LayoutEngine().Layout(container, new Size(8, 4));

        container.HitTest(new Point(2, 3)).ShouldBeSameAs(horizontal);
        container.HitTest(new Point(7, 2)).ShouldBeSameAs(vertical);
        horizontal.ActualStyle.ShouldBe(ScrollBarStyle.Default);
        vertical.ActualStyle.ShouldBe(ScrollBarStyle.Default);
    }

    private static Theme CreateTheme(ScrollBarStyle _)
    {
        var theme = new Theme();

        theme.Freeze();
        return theme;
    }

    /// <summary>Verifies each nested armed container discovers its own independent extent and
    /// viewport - the inner container's own scrolling capacity never leaks into the outer
    /// container's, and the outer's extent sums the inner's own fixed border-box height rather
    /// than the inner's unscrolled content height.</summary>
    [Fact]
    public void Extent_WhenTwoArmedContainersNest_ComposesIndependentExtents()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { new ProbeControl(new Size(4, 3)), inner }
        };

        new LayoutEngine().Layout(outer, new Size(4, 5));

        // The inner container's own extent is the leaf's full unscrolled height, independent of
        // whatever the outer container does with the inner's own arranged position.
        inner.Extent.ShouldBe(new Size(4, 6));
        inner.Viewport.ShouldBe(new Size(4, 4));

        // The outer container's extent sums the first sibling's height (3) and the inner
        // container's own fixed border-box height (4, from its explicit Height) - never the
        // inner's much larger unscrolled content height (6), which the inner alone is
        // responsible for reconciling through its own scrollbar.
        outer.Extent.ShouldBe(new Size(4, 7));
        outer.Viewport.ShouldBe(new Size(4, 5));
    }

    /// <summary>Verifies a leaf's absolute Bounds compose both levels of scroll translation: the
    /// inner container's own offset shifts the leaf, and the outer container's offset then also
    /// shifts the inner container's own arranged position, which the leaf inherits transitively.
    /// This is the exact two-level Bounds-translation composition left untested elsewhere.</summary>
    [Fact]
    public void Bounds_WhenBothContainersScroll_ComposesTranslationAcrossTwoLevels()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { leaf }
        };
        var top = new ProbeControl(new Size(4, 3));
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { top, inner }
        };
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // Unscrolled: top occupies rows [0, 3), inner occupies rows [3, 7) of outer's own content
        // space, and leaf occupies rows [0, 6) of inner's own content space.
        (outer.Extent.Height - outer.Viewport.Height).ShouldBe(2);
        (inner.Extent.Height - inner.Viewport.Height).ShouldBe(2);

        _ = outer.ScrollBy(0, 2);
        _ = inner.ScrollBy(0, 1);
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // inner's own arranged position, shifted by outer's offset: 3 - 2 = 1.
        inner.Bounds.Y.ShouldBe(1);

        // leaf's absolute position: inner's own arranged top (1) plus leaf's position within
        // inner after inner's own offset (0 - 1 = -1): 1 + (-1) = 0.
        leaf.Bounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies a wheel delta too large for the inner container's own remaining capacity
    /// consumes only what the inner can absorb and keeps the whole record for itself - latching,
    /// not remainder-chaining within the same event, is the contract docs/concepts/scrolling.md
    /// commits to - and that only a later, separate record, once the inner is fully at
    /// its endpoint and moves nothing, reaches the enclosing armed container. This is the
    /// genuinely nested pair left untested elsewhere.</summary>
    [Fact]
    public void Wheel_WhenInnerContainerCannotConsumeTheFullDelta_KeepsTheWholeRecordThenLatchesToOuter()
    {
        var leaf = new ProbeControl(new Size(4, 6));
        var inner = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4)
        };
        inner.Children.Add(leaf);
        var outer = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        outer.Children.Add(new ProbeControl(new Size(4, 20)));
        outer.Children.Add(inner);
        new LayoutEngine().Layout(outer, new Size(4, 5));

        // inner's own extent (6) less its own viewport (4) leaves only 2 cells of scroll capacity;
        // requesting 5 lines still moves inner (some, not zero), so the whole record stays there.
        (inner.Extent.Height - inner.Viewport.Height).ShouldBe(2);
        var outerOffsetBeforeWheel = outer.VerticalOffset;

        inner.RaiseWheel(0, -5);

        inner.VerticalOffset.ShouldBe(2);
        outer.VerticalOffset.ShouldBe(outerOffsetBeforeWheel);

        // inner is now fully at its endpoint - a later, identical record moves nothing there, so
        // this one reaches outer instead.
        inner.RaiseWheel(0, -5);

        inner.VerticalOffset.ShouldBe(2);
        outer.VerticalOffset.ShouldBeGreaterThan(outerOffsetBeforeWheel);
    }

    /// <summary>Verifies a horizontal wheel delta (WheelX) also composes correctly through a
    /// nested armed pair - WheelX in a nested pair was entirely untested elsewhere, distinct from
    /// the vertical-only coverage every existing wheel test exercises. Latches the same way the
    /// vertical case does: the inner container keeps a record it moves any amount for,
    /// and only a later, separate record that moves nothing there reaches outer.</summary>
    [Fact]
    public void Wheel_WhenHorizontalDeltaExceedsInnerCapacity_KeepsTheWholeRecordThenLatchesToOuter()
    {
        var leaf = new ProbeControl(new Size(20, 4));
        var inner = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            ScrollBars = ScrollBars.Horizontal,
            Width = Length.Cells(6)
        };
        inner.Children.Add(leaf);
        var outer = new LayoutProbe
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            ScrollBars = ScrollBars.Horizontal
        };
        outer.Children.Add(new ProbeControl(new Size(30, 4)));
        outer.Children.Add(inner);
        new LayoutEngine().Layout(outer, new Size(6, 4));

        (inner.Extent.Width - inner.Viewport.Width).ShouldBe(14);
        var outerOffsetBeforeWheel = outer.HorizontalOffset;

        inner.RaiseWheel(20, 0);

        inner.HorizontalOffset.ShouldBe(14);
        outer.HorizontalOffset.ShouldBe(outerOffsetBeforeWheel);

        inner.RaiseWheel(20, 0);

        inner.HorizontalOffset.ShouldBe(14);
        outer.HorizontalOffset.ShouldBeGreaterThan(outerOffsetBeforeWheel);
    }

    /// <summary>Verifies the ordinary application shape - a Stack laid out inside a Grid cell,
    /// itself inside an armed Container - composes correctly: the Stack's own intrinsic content
    /// determines its DesiredSize, the Grid resolves its track from that, and the outer container
    /// scrolls the whole composed result without any level silently truncating the others.</summary>
    [Fact]
    public void Layout_WhenStackInsideGridInsideArmedContainer_ComposesIntrinsicSizeAndScrolls()
    {
        var stackChild = new ProbeControl(new Size(4, 30));
        var stack = new Stack { Children = { stackChild } };
        var grid = new Grid
        {
            Columns = { Track.Auto() },
            Children = { stack }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { grid }
        };

        new LayoutEngine().Layout(outer, new Size(4, 10));

        // The Stack's own intrinsic height (30) propagates through the Grid's Auto row/column
        // resolution up to the armed outer container's extent, unclamped and unclipped.
        outer.Extent.Height.ShouldBe(30);
        outer.Viewport.Height.ShouldBe(10);
        _ = outer.ScrollBy(0, 1000);
        outer.VerticalOffset.ShouldBe(20);
        stackChild.Bounds.Height.ShouldBe(30);
    }

    /// <summary>Verifies three-level intrinsic propagation through Grid, an armed Container, and
    /// an outer Grid - testing that intrinsic sizing survives an armed
    /// container sitting between two Grids rather than being silently clamped or lost at the
    /// boundary.</summary>
    [Fact]
    public void Layout_WhenGridInsideArmedContainerInsideAnotherGrid_PropagatesIntrinsicSizeAcrossThreeLevels()
    {
        var leaf = new ProbeControl(new Size(4, 30));
        var innerGrid = new Grid
        {
            Columns = { Track.Auto() },
            Children = { leaf }
        };
        var armed = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(5),
            Children = { innerGrid }
        };
        var outerGrid = new Grid
        {
            Rows = { Track.Auto() },
            Children = { armed }
        };

        new LayoutEngine().Layout(outerGrid, new Size(4, 20));

        // The armed Stack's own fixed border-box height (5, from its explicit Height) is what
        // the outer Grid's Auto row resolves to - the inner Grid's much larger intrinsic content
        // (30) is fully absorbed by the armed container's own scrollbar and never escapes to
        // inflate the outer Grid's row.
        outerGrid.DesiredSize.Height.ShouldBe(5);
        armed.Extent.Height.ShouldBe(30);
        armed.Viewport.Height.ShouldBe(5);
    }

    /// <summary>Verifies BringIntoView's own walk composes correctly across
    /// three nested armed levels, not merely the two the existing regression test covers.</summary>
    [Fact]
    public void BringIntoView_WhenThreeArmedContainersNest_RevealsTargetThroughEveryLevel()
    {
        var target = new ProbeControl(new Size(4, 2));
        var innermost = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(4),
            Children = { new ProbeControl(new Size(4, 10)), target }
        };
        var middle = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Height = Length.Cells(6),
            Children = { new ProbeControl(new Size(4, 8)), innermost }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            Children = { new ProbeControl(new Size(4, 15)), middle }
        };
        new LayoutEngine().Layout(outer, new Size(4, 6));

        var result = outer.BringIntoView(target);

        // BringIntoView commits offset properties immediately but the resulting Arrange is
        // deferred - target.Bounds only reflects the new offsets after a real layout pass runs.
        new LayoutEngine().Layout(outer, new Size(4, 6));

        result.ShouldBeTrue();

        // Every level must have scrolled enough that target's final absolute position is fully
        // contained within outer's own viewport.
        outer.VerticalOffset.ShouldBeGreaterThan(0);
        middle.VerticalOffset.ShouldBeGreaterThan(0);
        innermost.VerticalOffset.ShouldBeGreaterThan(0);
        target.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0);
        target.Bounds.Bottom.ShouldBeLessThanOrEqualTo(outer.Viewport.Height);
    }

    /// <summary>Verifies a Container child whose DesiredSize changes between the parent's own
    /// measure pass and its arrange-time bar-induced re-measure (Container.MeasureContent calls
    /// MeasureOverride directly during arrange without updating the base ContentExtent) still
    /// ends up arranged at its true, current, re-wrapped size rather than the first probe's
    /// stale one - this measure/arrange divergence was an entirely untested path elsewhere. Only
    /// the vertical axis is scrollable here: a vertical-only bar narrows the *bounded* horizontal
    /// axis, which a wrap-capable child must re-wrap against, unlike the Both-axes case where
    /// neither axis is ever bounded and no re-wrap can occur at all.</summary>
    [Fact]
    public void Layout_WhenBarInducedReMeasureChangesWrapCapableContentHeight_ArrangesAtTheNewHeight()
    {
        var text = new string('x', 40);
        var content = new ControlText(text) { Overflow = Overflow.Wrap };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(content);

        // At the unreserved width (10), the first, unbounded-height probe wraps 40 cells into 4
        // lines of 10 - but that height (4) already overflows a 3-cell viewport, so a vertical
        // bar is added; the bar reserves one column, narrowing width to 9, and the identical 40
        // cells now wrap into 5 lines of at most 9 - a height the first probe never saw.
        new LayoutEngine().Layout(container, new Size(10, 3));

        container.Viewport.ShouldBe(new Size(9, 3));
        container.Extent.ShouldBe(new Size(9, 5));
        content.Bounds.Width.ShouldBe(9);
        content.Bounds.Height.ShouldBe(5);
    }

    /// <summary>Verifies only the inner container renders a scrollbar when only its own content
    /// overflows - the outer container's total content fits exactly, so it adds no chrome of its
    /// own - and content past the inner container's own viewport edge never renders anywhere,
    /// including into the outer container's own unrelated rows.</summary>
    [Fact]
    public void Render_WhenOnlyInnerContentOverflows_InnerAloneShowsScrollbarAndClipsAtItsOwnEdge()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var c = new ProbeControl(new Size(1, 1)) { Content = "C".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(2),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b, c }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner }
        };

        // outer's own total content (1 + inner's own fixed height 2 = 3) exactly fills a 3-row
        // viewport, so outer needs no bar of its own; inner's own content (a, b, c = 3 rows)
        // overflows its own fixed 2-row height, so inner alone needs one.
        new LayoutEngine().Layout(outer, new Size(2, 3));
        using Frame frame = new(new Size(2, 3));

        outer.Render(frame.Canvas);

        // Row 0: top's own content in column 0; column 1 is outer's own unreserved gutter next
        // to a control that never claimed it - blank, not scrollbar chrome or leaked content.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe(string.Empty);
        outer.HitTest(new Point(1, 0)).ShouldNotBeOfType<ScrollBar>();

        // Rows 1-2: inner's own translated 2-row viewport shows only 'A' and 'B' - 'C' is past
        // inner's own edge and must never render anywhere in the frame, not even into outer's
        // own row 0 gutter or beyond the frame's own bounds.
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("C");
            }
        }

        // Inner's own scrollbar column, translated by outer's arrangement (top's own height, 1):
        // absolute rows 1-2 hit-test as inner's own vertical bar.
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies the mirror image of the previous test: the inner container's own content
    /// fits its own bounds exactly (no inner bar at all), but the outer container's combined
    /// content overflows its own viewport, so only the outer container shows a scrollbar - and
    /// hit-testing the translated inner block never reports a bar that does not exist.</summary>
    [Fact]
    public void Render_WhenOnlyOuterContentOverflows_OuterAloneShowsScrollbarAndInnerHasNone()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var bottom = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(1),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner, bottom }
        };

        // inner's own content (a, b = 2 rows) exactly fills its own fixed 2-row height - no
        // overflow, no bar. outer's own total content (top 1 + inner 2 + bottom 1 = 4) overflows
        // a 3-row viewport, so outer alone needs a bar; the reserved column narrows outer's own
        // content width by one, but inner's own explicit Width(1) is unaffected by that.
        new LayoutEngine().Layout(outer, new Size(2, 3));
        using Frame frame = new(new Size(2, 3));

        outer.Render(frame.Canvas);

        // Unscrolled: row 0 shows top, rows 1-2 show inner's own full a/b (nothing clipped,
        // since inner needs no scrolling of its own).
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        // 'bottom' (outer's 4th row of content) is past outer's own 3-row viewport and must not
        // render anywhere in the unscrolled frame.
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("X");
            }
        }

        // outer's own bar occupies the reserved column across every row; inner's own translated
        // block (rows 1-2, column 0 only) hit-tests as content, not as any bar of its own - the
        // only bar anywhere is outer's.
        outer.HitTest(new Point(1, 0)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(0, 1)).ShouldNotBeOfType<ScrollBar>();
    }

    /// <summary>Verifies both containers show independent scrollbars simultaneously when both
    /// overflow, each at its own correctly translated position, and scrolling one does not
    /// disturb the other's own offset, chrome, or the content clip boundary between them. Three
    /// columns are provisioned deliberately: one for content, one for inner's own bar, and one
    /// for outer's own bar - two columns would starve inner's explicit Width down to a single
    /// column once outer's own bar reservation narrows the cross-axis slot inner is arranged
    /// within, leaving no room for inner's own content and bar to coexist.</summary>
    [Fact]
    public void Render_WhenBothContainersOverflow_EachShowsItsOwnIndependentScrollbar()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var c = new ProbeControl(new Size(1, 1)) { Content = "C".AsMemory() };
        var bottom = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(2),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b, c }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner, bottom }
        };

        // inner's own content (3 rows) overflows its own 2-row height. outer's own total content
        // (top 1 + inner 2 + bottom 1 = 4) overflows a 3-row viewport too - both need their own
        // independent bar, each claiming its own column out of the 3 provisioned.
        new LayoutEngine().Layout(outer, new Size(3, 3));
        using Frame frame = new(new Size(3, 3));

        outer.Render(frame.Canvas);

        // Unscrolled: row 0 is top, rows 1-2 are inner's own translated viewport showing 'A'/'B'
        // (its own 'C' clipped); 'bottom' is past outer's own viewport and invisible.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("C");
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("X");
            }
        }

        // Column 1 hit-tests as inner's own bar for its own translated rows (1-2), and column 2
        // hit-tests as outer's own bar for every row - the two reservations sit in adjacent but
        // distinct columns and never collide or clip each other's chrome.
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 0)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);

        // row 0 at column 1 is outer's own gutter next to 'top', which never claimed that
        // column - not a bar cell, since inner's own bar has no rows there to occupy.
        outer.HitTest(new Point(1, 0)).ShouldNotBeOfType<ScrollBar>();

        // Scrolling inner alone must not touch outer's own offset or bring 'bottom' into view.
        // ScrollBy commits the offset property immediately, but the resulting Arrange is
        // deferred - a real layout pass must run before the new offset is reflected in Bounds.
        _ = inner.ScrollBy(0, 1);
        outer.VerticalOffset.ShouldBe(0);
        new LayoutEngine().Layout(outer, new Size(3, 3));
        using Frame afterInnerScroll = new(new Size(3, 3));
        outer.Render(afterInnerScroll.Canvas);

        FrameOracle.Get(afterInnerScroll, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(afterInnerScroll, new Point(0, 1)).ShouldBe("B");
        FrameOracle.Get(afterInnerScroll, new Point(0, 2)).ShouldBe("C");
    }

    /// <summary>Verifies an inner container with both horizontal and vertical bars - the shared
    /// bottom-right corner cell that the horizontal bar owns (see the scrollbar-corner fix) -
    /// still renders correctly once nested inside an outer container that itself scrolls
    /// vertically and has already been scrolled, so the inner block's own corner is translated
    /// away from the origin rather than sitting at a fixed screen position.</summary>
    [Fact]
    public void Render_WhenInnerHasBothBarsAndOuterIsScrolled_CornerCellTranslatesWithInnerBlock()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var leaf = new ProbeControl(new Size(6, 6));
        var inner = new LayoutProbe
        {
            Width = Length.Cells(4),
            Height = Length.Cells(4),
            AutoScroll = true,
            ScrollBars = ScrollBars.Both
        };
        inner.Children.Add(leaf);
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner }
        };

        new LayoutEngine().Layout(outer, new Size(5, 4));
        _ = outer.ScrollBy(0, 1);
        new LayoutEngine().Layout(outer, new Size(5, 4));
        using Frame frame = new(new Size(5, 4));

        outer.Render(frame.Canvas);

        // outer's own extent (1 + 4 = 5) less its own viewport (4) leaves exactly one line of
        // scroll capacity - after scrolling by 1, inner's own block occupies absolute rows
        // [0, 4) instead of [1, 5), with 'top' scrolled fully out of view.
        var innerAbsoluteBounds = inner.Bounds;
        innerAbsoluteBounds.Y.ShouldBe(0);

        // inner's own bottom-right corner, translated to absolute coordinates, hit-tests as the
        // horizontal bar - the documented shared-corner owner - not the vertical one, and not a
        // stray content cell.
        var corner = new Point(innerAbsoluteBounds.Right - 1, innerAbsoluteBounds.Bottom - 1);
        outer.HitTest(corner).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Horizontal);
    }

    /// <summary>Verifies AutoSizeMode rejects an undefined value before committing it, leaving the
    /// documented default in place - the growth/shrink policy every AutoSize-driven measure decision
    /// below depends on being one of the two defined values.</summary>
    [Fact]
    public void AutoSizeMode_WhenValueIsUndefined_ThrowsBeforeMutation()
    {
        var container = new LayoutProbe();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => container.AutoSizeMode = (AutoSizeMode) 99);

        container.AutoSizeMode.ShouldBe(AutoSizeMode.GrowAndShrink);
    }

    /// <summary>Verifies HorizontalBarVisibility and VerticalBarVisibility each reject an undefined
    /// value before committing it, matching every other chrome-reservation enum on this control.</summary>
    [Fact]
    public void BarVisibility_WhenValueIsUndefined_ThrowsBeforeMutation()
    {
        var container = new LayoutProbe();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            container.HorizontalBarVisibility = (ScrollBarVisibility) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            container.VerticalBarVisibility = (ScrollBarVisibility) 99);

        container.HorizontalBarVisibility.ShouldBe(ScrollBarVisibility.Auto);
        container.VerticalBarVisibility.ShouldBe(ScrollBarVisibility.Auto);
    }

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
            MaxWidth = Length.Cells(12),
            MaxHeight = Length.Cells(4),
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

    /// <summary>Verifies a responsive maximum participates in the width-dependent remeasure and
    /// scrolling transaction rather than clipping content measured at its natural width.</summary>
    [Fact]
    public void AutoSize_WhenPercentageMaximumClampsWrappingContent_RemeasuresAndScrolls()
    {
        var text = new string('x', 49);
        var container = new Stack
        {
            AutoSize = true,
            AutoScroll = true,
            ShowScrollBars = ShowScrollBars.Never,
            MaxWidth = Length.Percent(50),
            MaxHeight = Length.Percent(20),
            Children = { new ControlText(text) { Overflow = Overflow.Wrap } }
        };

        new LayoutEngine().Layout(container, new Size(24, 20));

        container.Bounds.ShouldBe(new Rect(0, 0, 12, 4));
        container.Extent.ShouldBe(new Size(12, 5));
        container.Viewport.ShouldBe(new Size(12, 4));
        container.ScrollBy(0, 1).ShouldBeTrue();
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
            MaxWidth = Length.Cells(12),
            Children = { new ControlText(text) { Overflow = Overflow.Wrap } }
        };
        var leafText = new ControlText(text) { Overflow = Overflow.Wrap, MaxWidth = Length.Cells(12) };
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
        var container = new LayoutProbe { AutoSize = true, MinWidth = Length.Cells(10) };
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
            MaxWidth = Length.Cells(8)
        };
        container.Children.Add(new ProbeControl(new Size(4, 2)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.Width.ShouldBe(8);
        container.Bounds.Width.ShouldBe(8);
    }

    /// <summary>Verifies AutoSize re-clamps the desired size to MaxWidth after growing it by one
    /// cell to reserve a vertical scrollbar - the un-clamped grown value would report a size one
    /// cell past the container's own hard cap, even though Arrange always re-clamps Bounds to that
    /// same cap regardless of what Measure reported.</summary>
    [Fact]
    public void AutoSize_WhenVerticalScrollbarReservationExceedsMaxWidth_ClampsDesiredSizeToMaxWidth()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoScroll = true,
            MaxWidth = Length.Cells(5),
            MaxHeight = Length.Cells(4)
        };
        container.Children.Add(new ProbeControl(new Size(5, 10)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.ShouldBe(new Size(5, 4));
        container.Bounds.Width.ShouldBe(5);
    }

    /// <summary>Verifies the symmetric horizontal-scrollbar reservation re-clamps the grown desired
    /// height to MaxHeight instead of reporting one cell past the container's own hard cap.</summary>
    [Fact]
    public void AutoSize_WhenHorizontalScrollbarReservationExceedsMaxHeight_ClampsDesiredSizeToMaxHeight()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoScroll = true,
            ScrollBars = ScrollBars.Horizontal,
            MaxWidth = Length.Cells(4),
            MaxHeight = Length.Cells(5)
        };
        container.Children.Add(new ProbeControl(new Size(10, 5)));

        new LayoutEngine().Layout(container, new Size(40, 40));

        container.DesiredSize.ShouldBe(new Size(4, 5));
    }

    /// <summary>Verifies OnMeasuredDesired catches a vertical scrollbar need that only exists
    /// because the horizontal bar's row narrows the height viewport - the independent, pre-fix
    /// check compares vertical overflow against the unreserved viewport and misses it entirely.
    /// The offered width (5) is narrower than the content's natural width (10), forcing a
    /// horizontal bar independently of any induction. The content's height (6) is deliberately
    /// equal to the offered height (40 is never binding, so result.Height mirrors content height
    /// exactly at 6), so extent.Height(6) > result.Height(6) is false and the pre-fix code would
    /// never flag vertical overflow. Only once the horizontal bar's one-row reservation narrows
    /// the height viewport to 5 does extent.Height(6) > 5 become true, inducing the vertical bar.
    /// Width growing from 5 to 6 (MinWidth/MaxWidth are left unbounded, so nothing masks the
    /// growth) is the only way to observe the fix, since the induced vertical bar's own
    /// reservation lands on the width axis.</summary>
    [Fact]
    public void AutoScroll_WhenHorizontalBarReservationInducesVerticalOverflow_ReservesBothBars()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both
        };
        container.Children.Add(new ProbeControl(new Size(10, 6)));

        new LayoutEngine().Layout(container, new Size(5, 40));

        container.DesiredSize.ShouldBe(new Size(6, 7));
    }

    /// <summary>Verifies a Vertical Stack's own Auto cross-axis sizing - which reads a child's
    /// DesiredSize directly with no Max reclamp of its own - matches the child's actual arranged
    /// width instead of over-allocating by the one cell the unclamped scrollbar-reservation growth
    /// used to leave behind as a visible dead gap.</summary>
    [Fact]
    public void Stack_WhenAutoSizeChildScrollbarReservationClampsAtMaxWidth_CrossAxisMatchesChildBounds()
    {
        var container = new LayoutProbe
        {
            AutoSize = true,
            AutoScroll = true,
            MaxWidth = Length.Cells(5),
            MaxHeight = Length.Cells(4)
        };
        container.Children.Add(new ProbeControl(new Size(5, 10)));
        var stack = new Stack { AutoSize = true, Children = { container } };

        new LayoutEngine().Layout(stack, new Size(40, 40));

        stack.DesiredSize.Width.ShouldBe(container.Bounds.Width);
        stack.Bounds.Width.ShouldBe(container.Bounds.Width);
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

    private const int _caseCount = 10_000;
    private const int _seed = 0x005C_701E;

    /// <summary>Verifies viewport and both framework bars remain inside border and padding.</summary>
    [Fact]
    public void Layout_WhenBorderPaddingAndBothBarsArePresent_ContainsViewportAndBars()
    {
        var child = new ProbeControl(new Size(20, 10));
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        container.Children.Add(child);

        new LayoutEngine().Layout(container, new Size(10, 6));

        container.Viewport.ShouldBe(new Size(5, 1));
        child.Bounds.X.ShouldBe(2);
        child.Bounds.Y.ShouldBe(2);
        container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
        container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies shadow overflow neither changes scroll geometry nor escapes the committed viewport.</summary>
    [Fact]
    public void Render_WhenChildShadowIsVisible_KeepsExtentNeutralAndClipsToViewport()
    {
        var child = new LayoutProbe
        {
            Shadow = AppearanceTestValues.Shadow(visible: true, mode: ShadowMode.BlockGlyph, offset: new Point(1, 1), glyph: new Rune('▓')),
            Children = { new ProbeControl(new Size(3, 2)) }
        };
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            Children = { child }
        };
        new LayoutEngine().Layout(container, new Size(3, 2));
        using Frame frame = new(new Size(4, 3));

        container.Render(frame.Canvas);

        container.Extent.ShouldBe(new Size(3, 2));
        container.Viewport.ShouldBe(new Size(3, 2));
        frame.GetCell(new Point(3, 1)).ShouldBe(CellInfo.Blank);
        frame.GetCell(new Point(2, 2)).ShouldBe(CellInfo.Blank);
    }

    /// <summary>Verifies randomized viewports and policies stabilize in one repeated layout.</summary>
    [Fact]
    public void Layout_WhenCasesAreRandomized_PreservesStableContainedGeometry()
    {
        var random = new Random(_seed);
        var engine = new LayoutEngine();
        var container = new LayoutProbe { AutoScroll = true, ScrollBars = ScrollBars.Both };
        container.Children.Add(new ProbeControl(new Size(50, 30)));

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var size = new Size(random.Next(0, 80), random.Next(0, 50));
            container.HorizontalBarVisibility = RandomScrollBarVisibility(random);
            container.VerticalBarVisibility = RandomScrollBarVisibility(random);
            engine.Layout(container, size);
            var first = container.Viewport;
            engine.Layout(container, size);
            var context = $"seed=0x{_seed:X8}, case={sample}, size={size}";

            container.Viewport.ShouldBe(first, context);
            container.Viewport.Width.ShouldBeInRange(0, size.Width, context);
            container.Viewport.Height.ShouldBeInRange(0, size.Height, context);
            container.HorizontalOffset.ShouldBeInRange(
                0,
                Math.Max(0, container.Extent.Width - container.Viewport.Width),
                context);
            container.VerticalOffset.ShouldBeInRange(
                0,
                Math.Max(0, container.Extent.Height - container.Viewport.Height),
                context);
        }
    }

    /// <summary>Verifies an unpadded armed container reserves a vertical bar cell in DesiredSize
    /// when content overflows the border-box height.</summary>
    [Fact]
    public void Measure_WhenUnpaddedContentOverflowsHeight_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Height = Length.Cells(6)
        };
        container.Children.Add(new ProbeControl(new Size(4, 7)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        container.DesiredSize.ShouldBe(new Size(5, 6));
    }

    /// <summary>Verifies a padded armed container with an identical content-box height still
    /// reserves the vertical bar cell in DesiredSize: comparing the content-box extent against a
    /// border-box result under-detects overflow by exactly the padding and border inset.</summary>
    [Fact]
    public void Measure_WhenPaddedContentOverflowsContentBoxHeight_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Height = Length.Cells(10),
            Padding = new Thickness(0, 2, 0, 2)
        };
        container.Children.Add(new ProbeControl(new Size(4, 7)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        // The content box is 10 - 4 = 6 cells tall, identical to the unpadded case above, so the
        // bar cell must be reserved here too. Before the fix, DesiredSize.Width stayed 4 because
        // ContentExtent.Height (7) was compared against the border-box result.Height (10).
        container.DesiredSize.ShouldBe(new Size(5, 10));
    }

    /// <summary>Verifies an <see cref="Container.AutoSize"/> container with an explicit
    /// <see cref="LengthKind.Cells"/> Width - overridden to content size under
    /// <see cref="AutoSizeMode.GrowAndShrink"/> - still reserves the vertical bar cell.</summary>
    [Fact]
    public void Measure_WhenAutoSizeOverridesAnExplicitWidth_ReservesVerticalBarCellInDesiredSize()
    {
        var container = new LayoutProbe
        {
            AutoScroll = true,
            AutoSize = true,
            ScrollBars = ScrollBars.Vertical,
            Width = Length.Cells(99),
            MaxHeight = Length.Cells(5)
        };
        container.Children.Add(new ProbeControl(new Size(4, 40)));

        new LayoutEngine().Layout(container, new Size(20, 20));

        // GrowAndShrink ignores the explicit Cells(99) and content-sizes to width 4, but its mere
        // presence must not suppress bar-cell reservation just because Width.Kind != Auto.
        container.DesiredSize.ShouldBe(new Size(5, 5));
    }

    /// <summary>Verifies a padded and bordered armed container with Auto-visibility bars (not
    /// Always, the only policy the existing padded+bordered test above exercises) still contains
    /// the discovered viewport and both automatically-added bars entirely inside the
    /// border-and-padding-deflated content box.</summary>
    [Fact]
    public void Layout_WhenBorderPaddingAndAutoBarsArePresent_ContainsViewportAndBars()
    {
        var child = new ProbeControl(new Size(20, 10));
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
            Border = AppearanceTestValues.Border(BorderSide.All),
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        container.Children.Add(child);

        new LayoutEngine().Layout(container, new Size(10, 6));

        // Content (20x10) overflows the 6x2 content box (10 - 2 border - 2 padding on each axis)
        // on both axes, so both Auto bars are added - identical geometry to the Always case
        // above, proving Auto-triggered addition reserves the same cells Always does.
        container.Viewport.ShouldBe(new Size(5, 1));
        child.Bounds.X.ShouldBe(2);
        child.Bounds.Y.ShouldBe(2);
        container.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Horizontal);
        container.HitTest(new Point(7, 2)).ShouldBeOfType<ScrollBar>()
            .Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies content that shrinks while scrolled near the end of its previous, larger
    /// extent re-clamps the offset to the new, smaller maximum instead of leaving it stranded
    /// past the current extent - an out-of-range offset that a naive re-layout could otherwise
    /// commit verbatim.</summary>
    [Fact]
    public void Layout_WhenContentShrinksWhileScrolledNearTheEnd_ReClampsOffsetToNewMaximum()
    {
        var child = new ProbeControl(new Size(4, 40));
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);

        _ = container.Children.Remove(child);
        var shrunkChild = new ProbeControl(new Size(4, 12));
        container.Children.Add(shrunkChild);
        engine.Layout(container, new Size(4, 10));

        // The new extent (12) less the unchanged viewport (10) leaves a maximum of 2 - the stale
        // offset (30) must be clamped down to that, not left pointing past the new extent.
        container.Extent.Height.ShouldBe(12);
        container.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies that when content shrinks while scrolled near the end of its previous
    /// extent, the offset clamp Container's own Arrange pass performs does not strand a stray
    /// Arrange bit on Pending once the pass returns. That clamp runs inside this container's own
    /// arrange - reacting to the geometry Arrange just recomputed - so its effect is already
    /// folded into the in-flight pass; leaving the bit set would otherwise force one redundant
    /// Measure/Arrange traversal on the very next resize or idle check.</summary>
    [Fact]
    public void Layout_WhenContentShrinksWhileScrolled_LeavesNoStrayArrangeBit()
    {
        var child = new ProbeControl(new Size(4, 40));
        var container = new LayoutProbe { AutoScroll = true, ShowScrollBars = ShowScrollBars.Never };
        container.Children.Add(child);
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(4, 10));

        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);

        _ = container.Children.Remove(child);
        var shrunkChild = new ProbeControl(new Size(4, 12));
        container.Children.Add(shrunkChild);
        engine.Layout(container, new Size(4, 10));

        // Render legitimately stays pending - Layout never renders - but Arrange must not: the
        // clamp above already ran as part of this very pass, so nothing should demand another.
        (container.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies collapsing the child that was driving an armed, auto-barred container's
    /// oversized extent shrinks the extent, re-clamps a stale scrolled offset to the new maximum,
    /// retracts the auto vertical scrollbar chrome once content no longer overflows, and clears
    /// the cells the retracted bar's column used to occupy - the shared Container contract every
    /// custom scrolling host, including ListView, must honor for a Collapsed child. Note:
    /// <see cref="ScrollBarVisibility"/> here names the bar's own chrome mode, an unrelated enum
    /// from the child's Layout.<see cref="Visibility"/> under test.</summary>
    [Fact]
    public void Layout_WhenOversizedChildInAnAutoBarredContainerCollapses_ShrinksExtentReclampsOffsetAndRetractsTheBar()
    {
        var big = new ProbeControl(new Size(4, 40));
        var small = new ProbeControl(new Size(4, 5));
        var container = new LayoutProbe
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            VerticalBarVisibility = ScrollBarVisibility.Auto
        };
        container.Children.Add(big);
        container.Children.Add(small);
        var engine = new LayoutEngine();
        engine.Layout(container, new Size(4, 10));

        container.Extent.Height.ShouldBe(40);
        container.Viewport.Width.ShouldBe(3);
        _ = container.ScrollBy(0, 1000);
        container.VerticalOffset.ShouldBe(30);
        using var beforeFrame = new Frame(new Size(4, 10));
        container.Render(beforeFrame.Canvas);
        FrameOracle.Get(beforeFrame, new Point(3, 0)).ShouldBe("▲");

        big.Visibility = Visibility.Collapsed;
        engine.Layout(container, new Size(4, 10));

        // The extent shrinks to the one remaining visible child, well within the viewport.
        container.Extent.Height.ShouldBe(5);
        // The stale offset (30) re-clamps to the new maximum (0, since content now fits).
        container.VerticalOffset.ShouldBe(0);
        // The auto bar retracts because content no longer overflows, reclaiming its column.
        container.Viewport.Width.ShouldBe(4);
        using var afterFrame = new Frame(new Size(4, 10));
        container.Render(afterFrame.Canvas);
        FrameOracle.Get(afterFrame, new Point(3, 0)).ShouldNotBe("▲");
    }

    /// <summary>Verifies the sealed owner pipeline clears skipped collapsed roots at multiple
    /// ownership depths, so a custom container does not need a panel-local cleanup recipe.</summary>
    [Fact]
    public void Arrange_WhenSkippedChildrenCollapseAtNestedOwnershipDepths_ClearsEachRootBounds()
    {
        var root = new ProbeContainer();
        var branch = new ProbeContainer();
        var leaf = new ProbeControl();
        root.Children.Add(branch);
        branch.Children.Add(leaf);
        root.ArrangeOwned(branch, new Rect(1, 1, 8, 4), ResolvedAxes.Both);
        branch.ArrangeOwned(leaf, new Rect(2, 2, 3, 1), ResolvedAxes.Both);

        leaf.Visibility = Visibility.Collapsed;
        new LayoutEngine().Layout(branch, new Size(8, 4));

        leaf.Bounds.ShouldBe(default);

        branch.Visibility = Visibility.Collapsed;
        new LayoutEngine().Layout(root, new Size(10, 6));

        branch.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies collapsed-child cleanup commits only after the owner's arrange callback
    /// succeeds, while a failed transaction remains pending and clears the stale bounds on retry.</summary>
    [Fact]
    public void Arrange_WhenOwnerCallbackFails_PreservesCollapsedBoundsUntilSuccessfulRetry()
    {
        var owner = new ProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.ArrangeOwned(child, new Rect(1, 1, 3, 2), ResolvedAxes.Both);
        child.Visibility = Visibility.Collapsed;
        owner.Arranging = _ => throw new InvalidOperationException("Arrange failed.");
        var engine = new LayoutEngine();

        _ = Should.Throw<InvalidOperationException>(() => engine.Layout(owner, new Size(8, 4)));

        child.Bounds.ShouldBe(new Rect(1, 1, 3, 2));
        (owner.Pending & Invalidation.Arrange).ShouldBe(Invalidation.Arrange);

        owner.Arranging = null;
        engine.Layout(owner, new Size(8, 4));

        child.Bounds.ShouldBe(default);
        (owner.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    private static ScrollBarVisibility RandomScrollBarVisibility(Random random) =>
        (ScrollBarVisibility) random.Next(0, 3);

    /// <summary>Verifies child layout seams accept only direct ownership and defined axis flags.</summary>
    [Fact]
    public void ChildLayout_WhenCandidateIsNotDirectOrAxesAreUnknown_RejectsBeforeTransaction()
    {
        var owner = new ProbeContainer();
        var child = new ProbeControl(new Size(3, 2));
        var foreign = new ProbeControl(new Size(5, 4));
        owner.Children.Add(child);

        owner.MeasureOwned(child, new Constraint(10, 5)).ShouldBe(new Size(3, 2));
        _ = Should.Throw<ArgumentNullException>(() =>
            owner.MeasureOwned(null!, new Constraint(10, 5)));
        _ = Should.Throw<ArgumentException>(() =>
            owner.MeasureOwned(foreign, new Constraint(10, 5)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            owner.ArrangeOwned(foreign, new Rect(0, 0, 5, 4), (ResolvedAxes) 8));
        _ = Should.Throw<ArgumentException>(() =>
            owner.ArrangeOwned(foreign, new Rect(0, 0, 5, 4), ResolvedAxes.Both));

        owner.ArrangeOwned(child, new Rect(1, 1, 3, 2), ResolvedAxes.Both);
        child.Bounds.ShouldBe(new Rect(1, 1, 3, 2));
        foreign.DesiredSize.ShouldBe(default);
        foreign.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies the skipped child's own pending Arrange bit survives the second pass
    /// (it was never cleared, since it was never arranged) and an ancestor above the arranging
    /// parent picks up a fresh pending Arrange request - the propagation the child's own
    /// Invalidate call could not deliver because it was swallowed. A sibling the panel does
    /// arrange is unaffected: its pending bit clears normally.</summary>
    [Fact]
    public void Arrange_WhenAnArrangingParentMeasuresButSkipsAChild_RecordsAPendingArrangeOnAnAncestor()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var target = new ProbeControl();
        var sibling = new ProbeControl();
        inner.Children.Add(target);
        inner.Children.Add(sibling);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));

        // Second pass: the panel now skips target, and its own remeasure of target uses a
        // constraint that genuinely differs from what settled during the first pass - like
        // Grid's finite-track remeasure resolving to a width its own initial measure never saw -
        // so target's trailing Invalidate(Arrange) is a fresh announcement, not a repeat of one
        // already recorded. Re-dirtying inner directly is what makes this pass reach Arrange at
        // all: nothing else changed since the settled first pass.
        inner.SkippedChild = target;
        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));

        (target.Pending & Invalidation.Arrange).ShouldBe(Invalidation.Arrange);
        (sibling.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (root.Pending & Invalidation.Arrange).ShouldBe(Invalidation.Arrange);
    }

    /// <summary>Verifies the self-healed ancestor pending is not inert bookkeeping: because
    /// <see cref="LayoutEngine.Layout"/> re-walks from the root every call, and Arrange only
    /// short-circuits when its own pending Arrange bit is clear, the root's recovered pending
    /// bit is exactly what lets a third pass reach back down and arrange the previously-skipped
    /// child once the parent stops skipping it.</summary>
    [Fact]
    public void Arrange_WhenALaterPassStopsSkippingTheChild_ArrangesItUsingTheRecoveredPending()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var target = new ProbeControl();
        inner.Children.Add(target);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));
        var settledBounds = target.Bounds;

        inner.SkippedChild = target;
        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));
        target.Bounds.ShouldBe(settledBounds, "a skipped child's bounds do not move on their own");

        inner.SkippedChild = null;
        engine.Layout(root, new Size(10, 4));

        target.Bounds.ShouldBe(new Rect(0, 0, 10, 4));
        (target.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    /// <summary>Control case: a panel that arranges every child it measures - the contract every
    /// shipped container honors - never needs the self-heal. Even though the second pass's
    /// remeasure genuinely differs from the first (the same condition that triggers the swallow
    /// in the tests above), arranging the child right afterward clears its pending Arrange bit
    /// before this panel's own Arrange finishes, so no ancestor is left holding one.</summary>
    [Fact]
    public void Arrange_WhenAnArrangingParentArrangesEveryMeasuredChild_LeavesNoPendingArrangeBehind()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var first = new ProbeControl();
        var second = new ProbeControl();
        inner.Children.Add(first);
        inner.Children.Add(second);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));

        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));

        (first.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (second.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (inner.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (root.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    /// <summary>Measures every child, then arranges every child except
    /// <see cref="SkippedChild"/>, which is left null in the common case so the panel behaves
    /// like an ordinary container that honors the contract <see
    /// cref="ControlBase.Invalidate(Invalidation)"/> trusts.</summary>
    private sealed class ArrangeSkippingPanel: Container
    {
        /// <summary>Fills its assigned slot regardless of children's intrinsic size, so the probe
        /// children below - none of which report real content - still land at deterministic,
        /// non-zero bounds.</summary>
        internal ArrangeSkippingPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }

        /// <summary>Gets or sets the one child measured during Arrange but deliberately not
        /// arranged.</summary>
        internal ControlBase? SkippedChild { get; set; }

        /// <summary>Gets or sets the width Arrange hands each child's remeasure, overriding the
        /// assigned bounds width. Real panels naturally remeasure against a width their own
        /// initial pass never saw - Grid's resolved finite-track width, for one; this test tree
        /// has no such source of difference on its own, so a later pass sets this explicitly to
        /// produce one.</summary>
        internal int? ArrangeRemeasureWidth { get; set; }

        protected override Size MeasureOverride(Constraint constraint)
        {
            var size = default(Size);

            foreach (var child in Children)
            {
                var childSize = MeasureChild(child, constraint);
                size = new Size(
                    Math.Max(size.Width, childSize.Width),
                    Math.Max(size.Height, childSize.Height));
            }

            return size;
        }

        protected override void ArrangeOverride(Rect bounds)
        {
            var remeasureConstraint = new Constraint(ArrangeRemeasureWidth ?? bounds.Width, bounds.Height);

            foreach (var child in Children)
            {
                _ = MeasureChild(child, remeasureConstraint);

                if (!ReferenceEquals(child, SkippedChild))
                {
                    ArrangeChild(child, bounds, ResolvedAxes.Both);
                }
            }
        }
    }

    /// <summary>Verifies nested two-axis bars, pixel dragging, focus reveal, Unicode cells, and resize.</summary>
    [Fact]
    public async Task Input_WhenNestedAutomaticViewsUsePixelMouse_PreservesExactOffsetsAndThumbsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(8, 5), new Size(80, 50)));
        var content = new Overlay { Width = Length.Cells(14), Height = Length.Cells(9) };
        var label = new ControlText("界Z");
        var target = new Button { Text = "Go", Width = Length.Cells(2), Height = Length.Cells(1) };
        Overlay.SetLeft(target, Length.Cells(12));
        Overlay.SetTop(target, Length.Cells(8));
        content.Children.Add(label);
        content.Children.Add(target);
        var inner = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Both,
            Width = Length.Cells(12),
            Height = Length.Cells(7),
            Children = { content }
        };
        var outer = new Stack { AutoScroll = true, ScrollBars = ScrollBars.Both, Children = { inner } };
        await using Application application = new(
            outer,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Coordinates = MouseCoordinates.Pixel });
        await application.StartAsync(TestContext.Current.CancellationToken);

        outer.Viewport.ShouldBe(new Size(7, 4));
        inner.Viewport.ShouldBe(new Size(11, 6));
        terminal.QueueInput("\u001b[<0;16;46M\u001b[<32;56;46M\u001b[<0;56;46m"u8);
        await WaitUntilAsync(
            () => outer.HorizontalOffset == 5 && application.Capture.Captured is null,
            application,
            "pixel thumb drag",
            TestContext.Current.CancellationToken);

        var rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            outer.HorizontalOffset = 0;
            inner.HorizontalOffset = 0;
            outer.VerticalOffset = 0;
            inner.VerticalOffset = 0;
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        var horizontal = string.Concat(Enumerable.Repeat("\u001b[<67;16;16M", 10));
        var vertical = string.Concat(Enumerable.Repeat("\u001b[<65;16;16M", 10));
        terminal.QueueInput(Encoding.ASCII.GetBytes(horizontal + vertical));
        await WaitUntilAsync(
            () => inner.HorizontalOffset == 3 && outer.HorizontalOffset == 5 &&
                  inner.VerticalOffset == 3 && outer.VerticalOffset == 3,
            application,
            "nested two-axis wheel remainder",
            TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame frame = new(application.Size);
            outer.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(5, 4)).ShouldBe("▓");
            FrameOracle.Get(frame, new Point(7, 2)).ShouldBe("▓");
            frame.GetCell(new Point(0, 0)).Continuation.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
        rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            inner.HorizontalOffset = 0;
            inner.VerticalOffset = 0;
            outer.HorizontalOffset = 0;
            outer.VerticalOffset = 0;
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        rendered = NextFrame(application);
        await application.Dispatcher.InvokeAsync(() =>
        {
            // outer.BringIntoView walks through the intervening armed inner container on its
            // own, so a caller no longer pre-reveals through inner separately.
            application.Focus.Focus(target).ShouldBeTrue();
            outer.BringIntoView(target).ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        inner.HorizontalOffset.ShouldBe(3);
        inner.VerticalOffset.ShouldBe(3);

        // The minimal offset that fully exposes target once inner has settled at (3, 3) is
        // (4, 2), not the larger (5, 3) the pre-fix walk produced by mixing target's Bounds -
        // already translated by inner's own offset - directly into outer's own logical math.
        outer.HorizontalOffset.ShouldBe(4);
        outer.VerticalOffset.ShouldBe(2);
        application.Focus.Focused.ShouldBeSameAs(target);

        rendered = NextFrame(application);
        terminal.QueueResize(new Dimensions(new Size(16, 11), new Size(160, 110)));
        await rendered.WaitAsync(TestContext.Current.CancellationToken);
        outer.HorizontalOffset.ShouldBe(0);
        outer.VerticalOffset.ShouldBe(0);
        outer.Viewport.ShouldBe(new Size(16, 11));
        await application.Dispatcher.InvokeAsync(() =>
        {
            using Frame frame = new(application.Size);
            outer.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(15, 10)).ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies SGR wheel bytes, nested remainder, resize clamping, and final offsets.</summary>
    [Fact]
    public async Task Input_WhenNestedViewsReceiveWheel_ConsumesRemainderAndClampsAfterResizeAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(5, 4), new Size(50, 40)));
        var leaf = new ControlText(string.Join('\n', Enumerable.Range(0, 20))) { Width = Length.Cells(5) };
        var inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        var outer = Hidden(inner);
        await using Application application = new(outer, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        outer.ScrollChanged += (_, _) =>
        {
            if (outer.VerticalOffset == 4)
            {
                _ = reached.TrySetResult();
            }
        };
        var wheel = string.Concat(Enumerable.Repeat("\u001b[<65;1;1M", 20));

        terminal.QueueInput(Encoding.ASCII.GetBytes(wheel));
        await reached.Task.WaitAsync(TestContext.Current.CancellationToken);

        inner.VerticalOffset.ShouldBe(12);
        outer.VerticalOffset.ShouldBe(4);
        var resized = new Size(5, 8);
        terminal.QueueResize(new Dimensions(resized, new Size(50, 80)));
        await WaitUntilAsync(
            () => application.Size == resized && outer.VerticalOffset == 0,
            application,
            "nested resize clamp",
            TestContext.Current.CancellationToken);
        outer.VerticalOffset.ShouldBe(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    private static Stack Hidden(ControlBase content) => new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        ShowScrollBars = ShowScrollBars.Never,
        Children = { content }
    };

    private static Task NextFrame(Application application)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += Complete;
        return completion.Task;

        void Complete(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            application.FrameRendered -= Complete;
            _ = completion.TrySetResult();
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> predicate,
        Application application,
        string operation,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            if (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            {
                return;
            }

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
        }

        (await application.Dispatcher.InvokeAsync(predicate, cancellationToken))
            .ShouldBeTrue($"Timed out waiting for {operation}.");
    }
}
