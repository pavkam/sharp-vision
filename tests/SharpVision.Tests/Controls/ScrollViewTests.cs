// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Text;

using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Scrolling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Rendering;
using SharpVision.Tests.Support;

using Shouldly;

using KeyAction = Terminal.Input.Action;

/// <summary>Verifies ScrollView ownership, convergent layout, offsets, clipping, and commands.</summary>
public sealed class ScrollViewTests
{
    /// <summary>Verifies content replacement is atomic and preserves managed ownership.</summary>
    [Fact]
    public void Content_WhenReplaced_TransfersParentOwnership()
    {
        ScrollView view = new ScrollView();
        ProbeControl first = new ProbeControl();
        ProbeControl second = new ProbeControl();

        view.Content = first;
        view.Content = second;

        first.Parent.ShouldBeNull();
        second.Parent.ShouldBeSameAs(view);
        view.Content.ShouldBeSameAs(second);
    }

    /// <summary>Verifies one automatic bar can consume space and induce the other.</summary>
    [Fact]
    public void Layout_WhenAutomaticBarInducesOther_ConvergesWithBothBars()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(5, 4)),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };

        new Engine().Layout(view, new Size(5, 3));

        view.Extent.ShouldBe(new Size(5, 4));
        view.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies private vertical viewport chrome renders polished Unicode arrows, track, and thumb cells.</summary>
    [Fact]
    public void Render_WhenVerticalChromeIsAutomatic_UsesUnicodeScrollBarGlyphs()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(1, 4)),
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        Size size = new Size(3, 3);
        new Engine().Layout(view, size);
        using Frame frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▲");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▼");
    }

    /// <summary>Verifies private horizontal viewport chrome renders polished Unicode arrows, track, and thumb cells.</summary>
    [Fact]
    public void Render_WhenHorizontalChromeIsAutomatic_UsesUnicodeScrollBarGlyphs()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(4, 1)),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        Size size = new Size(3, 3);
        new Engine().Layout(view, size);
        using Frame frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("◀");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▶");
    }

    /// <summary>Verifies passive viewport track cells use a shaded glyph that remains visually distinct from the thumb.</summary>
    [Fact]
    public void Render_WhenVerticalChromeHasUnoccupiedTrack_UsesShadedTrackGlyph()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(1, 100)),
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        Size size = new Size(3, 6);
        new Engine().Layout(view, size);
        using Frame frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("░");
    }

    /// <summary>Verifies exact fit does not show automatic bars while Always reserves both axes.</summary>
    [Fact]
    public void Layout_WhenPoliciesDiffer_UsesExactFitAndAlwaysReservation()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(5, 3)),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        Engine engine = new Engine();

        engine.Layout(view, new Size(5, 3));
        view.Viewport.ShouldBe(new Size(5, 3));

        view.HorizontalBarVisibility = ScrollBarVisibility.Always;
        view.VerticalBarVisibility = ScrollBarVisibility.Always;
        engine.Layout(view, new Size(5, 3));
        view.Viewport.ShouldBe(new Size(4, 2));
    }

    /// <summary>Verifies the common policy suppresses chrome without disabling the allowed overflow axis.</summary>
    [Fact]
    public void Layout_WhenScrollBarsAreVerticalAndNever_ShowsNoChromeButRetainsVerticalRange()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(8, 10)),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
        };

        new Engine().Layout(view, new Size(4, 3));

        view.Viewport.ShouldBe(new Size(4, 3));
        view.HorizontalOffset.ShouldBe(0);
        view.VerticalOffset.ShouldBe(0);
        view.ScrollBy(4, 4).ShouldBeTrue();
        view.HorizontalOffset.ShouldBe(0);
        view.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies direct offsets reject overflow while commands clamp and report after commit.</summary>
    [Fact]
    public void ScrollBy_WhenDeltaExceedsExtent_ClampsAndRaisesOneEvent()
    {
        ScrollView view = Hidden(new ProbeControl(new Size(20, 10)));
        new Engine().Layout(view, new Size(5, 3));
        List<ScrollChangedEventArgs> changes = new List<ScrollChangedEventArgs>();
        view.ScrollChanged += (_, eventArgs) => changes.Add(eventArgs);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.HorizontalOffset = 16);
        view.ScrollBy(int.MaxValue, int.MaxValue, Cause.Wheel).ShouldBeTrue();

        view.HorizontalOffset.ShouldBe(15);
        view.VerticalOffset.ShouldBe(7);
        changes.Count.ShouldBe(1);
        changes[0].PreviousOffset.ShouldBe(default);
        changes[0].Offset.ShouldBe(new Point(15, 7));
        changes[0].Cause.ShouldBe(Cause.Wheel);
    }

    /// <summary>Verifies resize clamps offsets before exposing the committed viewport.</summary>
    [Fact]
    public void Layout_WhenViewportGrows_ClampsOffsetsWithResizeCause()
    {
        ScrollView view = Hidden(new ProbeControl(new Size(20, 10)));
        Engine engine = new Engine();
        engine.Layout(view, new Size(5, 3));
        _ = view.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        view.ScrollChanged += (_, eventArgs) => change = eventArgs;

        engine.Layout(view, new Size(18, 9));

        view.HorizontalOffset.ShouldBe(2);
        view.VerticalOffset.ShouldBe(1);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(Cause.Resize);
    }

    /// <summary>Verifies arranged translation, viewport clipping, and hit testing agree.</summary>
    [Fact]
    public void Render_WhenContentIsScrolled_ClipsAndTargetsOnlyViewport()
    {
        ProbeControl content = new ProbeControl(new Size(8, 1)) { Content = "ABCDEFGH".AsMemory() };
        ScrollView view = Hidden(content);
        view.Bounds = new Rect(0, 0, 4, 1);
        new Engine().Layout(view, new Size(4, 1));
        _ = view.ScrollBy(2, 0);
        new Engine().Layout(view, new Size(4, 1));
        using Frame frame = new Frame(new Size(4, 1));

        view.Render(frame.Canvas);

        content.Bounds.X.ShouldBe(-2);
        FrameOracle.Get(frame, default).ShouldBe("C");
        view.HitTest(new Point(0, 0)).ShouldBeSameAs(content);
        view.HitTest(new Point(3, 0)).ShouldBeSameAs(content);
    }

    /// <summary>Verifies BringIntoView makes the smallest offset change for a descendant.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantIsOutsideViewport_UsesMinimalOffset()
    {
        ProbeContainer content = new ProbeContainer();
        ProbeControl target = new ProbeControl { Bounds = new Rect(8, 4, 2, 1) };
        content.Children.Add(target);
        content.Width = Length.Cells(12);
        content.Height = Length.Cells(8);
        ScrollView view = Hidden(content);
        new Engine().Layout(view, new Size(5, 3));

        view.BringIntoView(target).ShouldBeTrue();

        view.HorizontalOffset.ShouldBe(5);
        view.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies hidden bars preserve programmatic scrolling and consume no cells.</summary>
    [Fact]
    public void Layout_WhenBarsAreHidden_PreservesFullViewportAndScrollableOffsets()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(10, 10)),
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };

        new Engine().Layout(view, new Size(4, 3));
        _ = view.ScrollBy(2, 3);

        view.Viewport.ShouldBe(new Size(4, 3));
        view.HorizontalOffset.ShouldBe(2);
        view.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a hidden horizontal bar gives word-wrapping content the committed width during measurement.</summary>
    [Fact]
    public void Layout_WhenHorizontalBarIsHidden_ReflowsWordWrappedContentToViewportWidth()
    {
        SharpVision.Controls.Text text = new SharpVision.Controls.Text("one two three")
        {
            Wrapping = SharpVision.Text.Wrapping.Word,
        };
        ScrollView view = new ScrollView
        {
            Content = text,
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
            ConstrainContentToViewport = true,
        };

        new Engine().Layout(view, new Size(5, 3));

        view.Extent.ShouldBe(new Size(5, 3));
        view.Viewport.ShouldBe(new Size(5, 3));
    }

    /// <summary>Verifies wheel, arrows, pages, and endpoint keys share the typed command path.</summary>
    [Fact]
    public void Dispatch_WhenCommandsArrive_UsesLinePageAndEndpointChanges()
    {
        ScrollView view = Hidden(new ProbeControl(new Size(20, 20)));
        view.LineSize = 2;
        view.PageOverlap = 1;
        new Engine().Layout(view, new Size(5, 4));

        Route(view, new Pointer(
            cells: default,
            pixels: null,
            Buttons.None,
            PointerAction.Wheel,
            wheelX: -1,
            wheelY: -2,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false));
        Key(view, Code.Right);
        Key(view, Code.PageDown);
        Key(view, Code.End);

        view.HorizontalOffset.ShouldBe(4);
        view.VerticalOffset.ShouldBe(16);
        Key(view, Code.Home);
        view.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies every defined non-scroll key remains available to other controls.</summary>
    [Fact]
    public void Dispatch_WhenNonScrollKeyArrives_LeavesEventUnhandledWithoutThrowing()
    {
        HashSet<Code> scrollCodes = new HashSet<Code>
        {
            Code.Left,
            Code.Right,
            Code.Up,
            Code.Down,
            Code.PageUp,
            Code.PageDown,
            Code.Home,
            Code.End,
        };
        ScrollView view = Hidden(new ProbeControl(new Size(20, 20)));
        new Engine().Layout(view, new Size(5, 4));

        foreach (Code code in Enum.GetValues<Code>().Where(code => !scrollCodes.Contains(code)))
        {
            KeyEventArgs eventArgs = new KeyEventArgs(new Stroke(
                code,
                code == Code.Character ? new Rune('x') : null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press));

            Router.Route(view, Events.Key, eventArgs);

            eventArgs.Handled.ShouldBeFalse($"{code} is not a scroll command");
        }

        new Point(view.HorizontalOffset, view.VerticalOffset).ShouldBe(default);
    }

    /// <summary>Verifies composed scrollbar changes synchronize back to viewport offsets.</summary>
    [Fact]
    public void ScrollBy_WhenInternalBarChanges_SynchronizesViewportOffset()
    {
        ScrollView view = new ScrollView
        {
            Content = new ProbeControl(new Size(20, 10)),
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
        };
        new Engine().Layout(view, new Size(8, 4));
        ScrollBar horizontal = view.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();

        horizontal.ScrollBy(5, Cause.Pointer).ShouldBeTrue();

        view.HorizontalOffset.ShouldBe(5);
    }

    /// <summary>Verifies unused wheel delta continues through the nearest scrollable ancestors.</summary>
    [Fact]
    public void Dispatch_WhenNestedViewReachesBoundary_PropagatesRemainingWheelDelta()
    {
        ProbeControl leaf = new ProbeControl(new Size(5, 20));
        ScrollView inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        ScrollView outer = Hidden(inner);
        new Engine().Layout(outer, new Size(5, 4));

        Route(leaf, new Pointer(
            cells: default,
            pixels: null,
            Buttons.None,
            PointerAction.Wheel,
            wheelX: 0,
            wheelY: -20,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false));

        inner.VerticalOffset.ShouldBe(12);
        outer.VerticalOffset.ShouldBe(4);
    }

    /// <summary>Verifies content shrink clamps both offsets before its change notification.</summary>
    [Fact]
    public void Layout_WhenContentShrinks_ClampsOffsetsWithContentCause()
    {
        ScrollView view = Hidden(new ProbeControl(new Size(20, 10)));
        Engine engine = new Engine();
        engine.Layout(view, new Size(5, 3));
        _ = view.ScrollBy(100, 100);
        ScrollChangedEventArgs? change = null;
        view.ScrollChanged += (_, eventArgs) => change = eventArgs;
        view.Content = new ProbeControl(new Size(4, 2));

        engine.Layout(view, new Size(5, 3));

        view.Extent.ShouldBe(new Size(4, 2));
        new Point(view.HorizontalOffset, view.VerticalOffset).ShouldBe(default);
        _ = change.ShouldNotBeNull();
        change.Cause.ShouldBe(Cause.Content);
        change.Offset.ShouldBe(default);
    }

    /// <summary>Verifies horizontal clipping never exposes half of a two-cell grapheme.</summary>
    [Fact]
    public void Render_WhenOffsetCrossesWideRune_ClipsCompleteCellOwner()
    {
        ProbeControl content = new ProbeControl(new Size(3, 1)) { Content = "界A".AsMemory() };
        ScrollView view = Hidden(content);
        new Engine().Layout(view, new Size(2, 1));
        _ = view.ScrollBy(1, 0);
        new Engine().Layout(view, new Size(2, 1));
        using Frame frame = new Frame(new Size(2, 1));

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        frame.GetCell(default).IsContinuation.ShouldBeFalse();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("A");
    }

    /// <summary>Verifies disposal releases public content and private composed bars exactly once.</summary>
    [Fact]
    public void Dispose_WhenViewportOwnsContent_ReleasesCompleteComposedTree()
    {
        ProbeControl content = new ProbeControl();
        ScrollView view = new ScrollView { Content = content };

        view.Dispose();

        view.IsDisposed.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
    }

    private static ScrollView Hidden(Control content) => new()
    {
        Content = content,
        HorizontalBarVisibility = ScrollBarVisibility.Hidden,
        VerticalBarVisibility = ScrollBarVisibility.Hidden,
    };

    private static void Key(ScrollView view, Code code) =>
        Router.Route(
            view,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    private static void Route(ScrollView view, Pointer pointer) =>
        Router.Route(view, Events.Pointer, new PointerEventArgs(pointer));

    private static void Route(Control control, Pointer pointer) =>
        Router.Route(control, Events.Pointer, new PointerEventArgs(pointer));
}
