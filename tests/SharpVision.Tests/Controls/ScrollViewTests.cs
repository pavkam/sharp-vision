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
        var view = new ScrollView();
        var first = new ProbeControl();
        var second = new ProbeControl();

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
        var view = new ScrollView
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
        var view = new ScrollView
        {
            Content = new ProbeControl(new Size(1, 4)),
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        var size = new Size(3, 3);
        new Engine().Layout(view, size);
        using var frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("▲");
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▼");
    }

    /// <summary>Verifies private horizontal viewport chrome renders polished Unicode arrows, track, and thumb cells.</summary>
    [Fact]
    public void Render_WhenHorizontalChromeIsAutomatic_UsesUnicodeScrollBarGlyphs()
    {
        var view = new ScrollView
        {
            Content = new ProbeControl(new Size(4, 1)),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Hidden,
        };
        var size = new Size(3, 3);
        new Engine().Layout(view, size);
        using var frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("◀");
        FrameOracle.Get(frame, new Point(1, 2)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("▶");
    }

    /// <summary>Verifies passive viewport track cells use a shaded glyph that remains visually distinct from the thumb.</summary>
    [Fact]
    public void Render_WhenVerticalChromeHasUnoccupiedTrack_UsesShadedTrackGlyph()
    {
        var view = new ScrollView
        {
            Content = new ProbeControl(new Size(1, 100)),
            HorizontalBarVisibility = ScrollBarVisibility.Hidden,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        var size = new Size(3, 6);
        new Engine().Layout(view, size);
        using var frame = new Frame(size);

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 1)).ShouldBe("▓");
        FrameOracle.Get(frame, new Point(2, 2)).ShouldBe("░");
    }

    /// <summary>Verifies exact fit does not show automatic bars while Always reserves both axes.</summary>
    [Fact]
    public void Layout_WhenPoliciesDiffer_UsesExactFitAndAlwaysReservation()
    {
        var view = new ScrollView
        {
            Content = new ProbeControl(new Size(5, 3)),
            HorizontalBarVisibility = ScrollBarVisibility.Auto,
            VerticalBarVisibility = ScrollBarVisibility.Auto,
        };
        var engine = new Engine();

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
        var view = new ScrollView
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
        var view = Hidden(new ProbeControl(new Size(20, 10)));
        new Engine().Layout(view, new Size(5, 3));
        var changes = new List<ScrollChangedEventArgs>();
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
        var view = Hidden(new ProbeControl(new Size(20, 10)));
        var engine = new Engine();
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
        var content = new ProbeControl(new Size(8, 1)) { Content = "ABCDEFGH".AsMemory() };
        var view = Hidden(content);
        view.Bounds = new Rect(0, 0, 4, 1);
        new Engine().Layout(view, new Size(4, 1));
        _ = view.ScrollBy(2, 0);
        new Engine().Layout(view, new Size(4, 1));
        using var frame = new Frame(new Size(4, 1));

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
        var content = new ProbeContainer();
        var target = new ProbeControl { Bounds = new Rect(8, 4, 2, 1) };
        content.Children.Add(target);
        content.Width = Length.Cells(12);
        content.Height = Length.Cells(8);
        var view = Hidden(content);
        new Engine().Layout(view, new Size(5, 3));

        view.BringIntoView(target).ShouldBeTrue();

        view.HorizontalOffset.ShouldBe(5);
        view.VerticalOffset.ShouldBe(2);
    }

    /// <summary>Verifies hidden bars preserve programmatic scrolling and consume no cells.</summary>
    [Fact]
    public void Layout_WhenBarsAreHidden_PreservesFullViewportAndScrollableOffsets()
    {
        var view = new ScrollView
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
        var text = new SharpVision.Controls.Text("one two three")
        {
            Wrapping = SharpVision.Text.Wrapping.Word,
        };
        var view = new ScrollView
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
        var view = Hidden(new ProbeControl(new Size(20, 20)));
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
        var scrollCodes = new HashSet<Code>
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
        var view = Hidden(new ProbeControl(new Size(20, 20)));
        new Engine().Layout(view, new Size(5, 4));

        foreach (var code in Enum.GetValues<Code>().Where(code => !scrollCodes.Contains(code)))
        {
            var eventArgs = new KeyEventArgs(new Stroke(
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
        var view = new ScrollView
        {
            Content = new ProbeControl(new Size(20, 10)),
            HorizontalBarVisibility = ScrollBarVisibility.Always,
            VerticalBarVisibility = ScrollBarVisibility.Always,
        };
        new Engine().Layout(view, new Size(8, 4));
        var horizontal = view.HitTest(new Point(2, 3)).ShouldBeOfType<ScrollBar>();

        horizontal.ScrollBy(5, Cause.Pointer).ShouldBeTrue();

        view.HorizontalOffset.ShouldBe(5);
    }

    /// <summary>Verifies unused wheel delta continues through the nearest scrollable ancestors.</summary>
    [Fact]
    public void Dispatch_WhenNestedViewReachesBoundary_PropagatesRemainingWheelDelta()
    {
        var leaf = new ProbeControl(new Size(5, 20));
        var inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        var outer = Hidden(inner);
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
        var view = Hidden(new ProbeControl(new Size(20, 10)));
        var engine = new Engine();
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
        var content = new ProbeControl(new Size(3, 1)) { Content = "界A".AsMemory() };
        var view = Hidden(content);
        new Engine().Layout(view, new Size(2, 1));
        _ = view.ScrollBy(1, 0);
        new Engine().Layout(view, new Size(2, 1));
        using var frame = new Frame(new Size(2, 1));

        view.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        frame.GetCell(default).IsContinuation.ShouldBeFalse();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("A");
    }

    /// <summary>Verifies disposal releases public content and private composed bars exactly once.</summary>
    [Fact]
    public void Dispose_WhenViewportOwnsContent_ReleasesCompleteComposedTree()
    {
        var content = new ProbeControl();
        var view = new ScrollView { Content = content };

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
