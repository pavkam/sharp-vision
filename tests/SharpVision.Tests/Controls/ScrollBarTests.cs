// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;


using SharpVision.Scrolling;
using SharpVision.Terminal.Input;


using KeyAction = Terminal.Input.Action;
using TerminalStyle = Style;

/// <summary>Verifies ScrollBar range, input, capture, geometry, and semantic rendering.</summary>
public sealed class ScrollBarTests
{
    /// <summary>Verifies invalid public assignments fail before changing any range state.</summary>
    [Fact]
    public void Properties_WhenAssignmentIsInvalid_PreservePreviousState()
    {
        ScrollBar control = new()
        {
            Maximum = 100,
            Value = 50,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 25,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Minimum = -1);
        _ = Should.Throw<ArgumentException>(() => control.Minimum = 51);
        _ = Should.Throw<ArgumentException>(() => control.Maximum = 49);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Value = 101);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ViewportSize = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SmallChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.LargeChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.ScrollBy(1, (Cause) 99));

        control.Minimum.ShouldBe(0);
        control.Maximum.ShouldBe(100);
        control.Value.ShouldBe(50);
        control.ViewportSize.ShouldBe(20);
        control.SmallChange.ShouldBe(2);
        control.LargeChange.ShouldBe(25);
        control.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies direct values throw while command changes clamp and report after commit.</summary>
    [Fact]
    public void ScrollBy_WhenDeltaExceedsRange_ClampsAndRaisesOrderedEvent()
    {
        ScrollBar control = new() { Maximum = 100, Value = 40 };
        List<string> changes = [];
        control.ValueChanged += (_, eventArgs) =>
            changes.Add($"{eventArgs.PreviousValue}>{eventArgs.Value}:{eventArgs.Cause}:{control.Value}");

        control.ScrollBy(int.MaxValue, Cause.Wheel).ShouldBeTrue();
        control.ScrollBy(1, Cause.Keyboard).ShouldBeFalse();
        control.ScrollBy(int.MinValue, Cause.Pointer).ShouldBeTrue();
        control.Value = 25;

        changes.ShouldBe([
            "40>100:Wheel:100",
            "100>0:Pointer:0",
            "0>25:Programmatic:25",
        ]);
    }

    /// <summary>Verifies range arithmetic remains saturating at the largest supported endpoint.</summary>
    [Fact]
    public void ScrollBy_WhenMaximumIsIntegerBoundary_DoesNotOverflow()
    {
        ScrollBar control = new() { Maximum = int.MaxValue, Value = int.MaxValue - 1 };

        control.ScrollBy(int.MaxValue).ShouldBeTrue();

        control.Value.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies immutable press geometry remains safe when a handler narrows the live range.</summary>
    [Fact]
    public async Task Dispatch_WhenRangeChangesDuringThumbDrag_ClampsAgainstCurrentRangeAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ScrollBar control = new()
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
            };
            control.Attach(dispatcher);
            using CaptureManager capture = new(control);
            control.ValueChanged += NarrowOnFirstChange;

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(6, 0), PointerAction.Move));
            int narrowed = control.Minimum;
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Move));

            narrowed.ShouldBeInRange(55, 56);
            control.Value.ShouldBe(narrowed);
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Release));

            void NarrowOnFirstChange(object? sender, ScrollEventArgs eventArgs)
            {
                _ = sender;
                control.ValueChanged -= NarrowOnFirstChange;
                control.Minimum = eventArgs.Value;
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies orientation controls intrinsic size and exact horizontal semantic cells.</summary>
    [Fact]
    public void Render_WhenHorizontal_WritesButtonsTrackAndExactThumbCells()
    {
        ScrollBar control = new()
        {
            Orientation = Orientation.Horizontal,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        new Engine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        control.DesiredSize.ShouldBe(new Size(3, 1));
        Cells(frame, width: 10, y: 0).ShouldBe("◀░░░▓▓░░░▶");
    }

    /// <summary>Verifies thin line chrome removes arrow buttons and retains a high-contrast draggable thumb.</summary>
    [Fact]
    public void Render_WhenThinLineChromeIsSelected_UsesCanonicalTrackAndThumb()
    {
        ScrollBar control = new()
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarStyle.Thin,
            Fill = ScrollBarFill.Line,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        new Engine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        control.DesiredSize.ShouldBe(new Size(1, 1));
        Cells(frame, width: 10, y: 0).ShouldBe("────━━────");
    }

    /// <summary>Verifies a foreground-only ScrollBar style preserves the parent surface background.</summary>
    [Fact]
    public void Render_WhenStyleHasForegroundOnly_PreservesSurfaceBackground()
    {
        ControlStyle<ScrollBar> style = ThemeTestSupport.OverlayStyle<ScrollBar>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(45))));
        ScrollBar control = new()
        {
            Bounds = new Rect(0, 0, 3, 1),
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarStyle.Thin,
            Fill = ScrollBarFill.Line,
            Style = style,
        };
        using Frame frame = new(new Size(3, 1));
        frame.Canvas.Fill(frame.Canvas.Bounds, new Rune(' '), new TerminalStyle(Color.Default, Color.Indexed(238)));

        control.Render(frame.Canvas);

        frame.GetCell(default).Style.Background.ShouldBe(Color.Indexed(238));
    }

    /// <summary>Verifies explicitly assigning a legacy glyph remains an intentional custom override.</summary>
    [Fact]
    public void Render_WhenLegacyTrackGlyphIsAssigned_UsesAssignedGlyph()
    {
        ScrollBar control = new()
        {
            Orientation = Orientation.Horizontal,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            TrackGlyph = new Rune('.'),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        new Engine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        Cells(frame, width: 10, y: 0).ShouldBe("◀...▓▓...▶");
    }

    /// <summary>Verifies vertical and horizontal keyboard mappings consume only press transitions.</summary>
    [Fact]
    public void Dispatch_WhenKeyboardCommandsArrive_AppliesOrientationAndPageMappings()
    {
        ScrollBar control = new()
        {
            Maximum = 100,
            Value = 50,
            SmallChange = 2,
            LargeChange = 20,
        };

        Key(control, Code.Up);
        Key(control, Code.Down);
        Key(control, Code.PageUp);
        Key(control, Code.PageDown);
        Key(control, Code.Home);
        Key(control, Code.End);
        Key(control, Code.Down, KeyAction.Release);
        control.Value.ShouldBe(100);

        control.Orientation = Orientation.Horizontal;
        Key(control, Code.Left);
        Key(control, Code.Right);
        control.Value.ShouldBe(100);
    }

    /// <summary>Verifies pointer buttons and track clicks apply small and large changes.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerPressesButtonsAndTrack_AppliesExpectedChangesAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ScrollBar control = new()
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
                Value = 50,
                ViewportSize = 20,
                SmallChange = 2,
                LargeChange = 20,
            };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using CaptureManager capture = new(control);

            _ = capture.Dispatch(Pointer(new Point(0, 0), PointerAction.Press));
            control.Value.ShouldBe(48);
            _ = capture.Dispatch(Pointer(new Point(11, 0), PointerAction.Press));
            control.Value.ShouldBe(50);
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            control.Value.ShouldBe(30);
            _ = capture.Dispatch(Pointer(new Point(10, 0), PointerAction.Press));
            control.Value.ShouldBe(50);
            focus.Focused.ShouldBeSameAs(control);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a cell drag uses press geometry and reaches the exact endpoint.</summary>
    [Fact]
    public async Task Dispatch_WhenThumbDragsByCells_UsesCaptureAndOriginalGeometryAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ScrollBar control = new()
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
            };
            control.Attach(dispatcher);
            using CaptureManager capture = new(control);

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(control);
            _ = capture.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));
            control.Value.ShouldBe(100);
            _ = capture.Dispatch(Pointer(new Point(10, 0), PointerAction.Release));

            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies inferred pixel input drags and disable cancels without a second commit.</summary>
    [Fact]
    public async Task Dispatch_WhenPixelThumbDragIsCancelled_ReleasesCaptureWithoutSpuriousChangeAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ScrollBar control = new()
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
            };
            control.Attach(dispatcher);
            using CaptureManager capture = new(control);
            int changes = 0;
            control.ValueChanged += (_, _) => changes++;

            _ = capture.Dispatch(Pointer(
                new Point(1, 0),
                PointerAction.Press,
                pixels: new Point(10, 5),
                inferred: true));
            _ = capture.Dispatch(Pointer(
                new Point(6, 0),
                PointerAction.Move,
                pixels: new Point(60, 5),
                inferred: true));
            int dragged = control.Value;
            control.IsEnabled = false;

            dragged.ShouldBeInRange(55, 56);
            control.Value.ShouldBe(dragged);
            changes.ShouldBe(1);
            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies wheel input follows orientation and reports its typed cause.</summary>
    [Fact]
    public void Dispatch_WhenWheelMoves_AppliesAxisSpecificSmallChange()
    {
        ScrollBar control = new()
        {
            Maximum = 100,
            Value = 50,
            SmallChange = 2,
        };
        List<Cause> causes = [];
        control.ValueChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);

        Route(control, Wheel(wheelX: 0, wheelY: 3));
        control.Value.ShouldBe(44);
        control.Orientation = Orientation.Horizontal;
        Route(control, Wheel(wheelX: -4, wheelY: 0));

        control.Value.ShouldBe(52);
        causes.ShouldBe([Cause.Wheel, Cause.Wheel]);
    }

    /// <summary>Verifies a wheel at an endpoint remains available to an enclosing overflow host.</summary>
    [Fact]
    public void Dispatch_WhenWheelCannotMoveRange_LeavesEventUnhandled()
    {
        ScrollBar control = new() { Maximum = 10, Value = 10 };
        PointerEventArgs eventArgs = new(Wheel(wheelX: 0, wheelY: -1));

        Router.Route(control, Events.Pointer, eventArgs);

        control.Value.ShouldBe(10);
        eventArgs.Handled.ShouldBeFalse();
    }

    /// <summary>Verifies terminal focus loss and detach both cancel active thumb ownership.</summary>
    [Fact]
    public async Task Dispatch_WhenCaptureBecomesUnavailable_CancelsDragWithoutChangingValueAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            ProbeContainer root = new() { Bounds = new Rect(0, 0, 20, 2) };
            ScrollBar control = new()
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
            };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using CaptureManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(control);
            capture.TerminalFocusLost();
            capture.Captured.ShouldBeNull();
            control.Value.ShouldBe(0);

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(control);
            _ = root.Children.Remove(control);

            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
            control.Value.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies tiny tracks degrade deterministically and custom glyphs remain narrow.</summary>
    [Fact]
    public void Render_WhenTrackIsTiny_DegradesWithoutEscapingBounds()
    {
        ScrollBar control = new()
        {
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Bounds = new Rect(0, 0, 1, 1),
        };
        using Frame one = new(new Size(1, 1));

        control.Render(one.Canvas);
        FrameOracle.Get(one, default).ShouldBe("▓");

        control.Bounds = new Rect(0, 0, 2, 1);
        using Frame two = new(new Size(2, 1));
        control.Render(two.Canvas);
        Cells(two, width: 2, y: 0).ShouldBe("◀▶");

        _ = Should.Throw<ArgumentException>(() => control.TrackGlyph = new Rune('界'));
        control.TrackGlyph.ShouldBe(new Rune('.'));
    }

    /// <summary>Verifies resolved focused and pressed style reaches every scrollbar cell.</summary>
    [Fact]
    public void Render_WhenBehaviorStateChanges_UsesResolvedVisualStyle()
    {
        ControlStyle<ScrollBar> style = ThemeTestSupport.OverlayStyle<ScrollBar>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(2))),
            (State.Focused, new ThemeOverlay(attributes: Attributes.Underline)),
            (State.Pressed, new ThemeOverlay(foreground: Color.Indexed(5))));
        ScrollBar control = new()
        {
            Bounds = new Rect(0, 0, 1, 3),
            Style = style,
        };
        control.SetFocused(true);
        control.SetPressed(true);
        using Frame frame = new(new Size(1, 3));

        control.Render(frame.Canvas);

        control.CanFocus.ShouldBeTrue();
        frame.GetCell(default).Style.Foreground.ShouldBe(Color.Indexed(5));
        frame.GetCell(default).Style.Attributes.ShouldBe(Attributes.Underline);
    }

    /// <summary>Verifies public event data rejects impossible values and unknown causes.</summary>
    [Fact]
    public void Constructor_WhenScrollEventDataIsInvalid_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new ScrollEventArgs(-1, 0, Cause.Programmatic));
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => new ScrollEventArgs(0, 0, (Cause) 99));
    }

    private static string Cells(Frame frame, int width, int y)
    {
        StringBuilder result = new(width);

        for (int x = 0; x < width; x++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, y)));
        }

        return result.ToString();
    }

    private static void Key(
        ScrollBar control,
        Code code,
        KeyAction action = KeyAction.Press) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                action)));

    private static Pointer Pointer(
        Point cells,
        PointerAction action,
        Point? pixels = null,
        bool inferred = false) => new(
        cells,
        pixels,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: inferred);

    private static Pointer Wheel(int wheelX, int wheelY) => new(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    private static void Route(ScrollBar control, Pointer pointer) =>
        Router.Route(control, Events.Pointer, new PointerEventArgs(pointer));
}
