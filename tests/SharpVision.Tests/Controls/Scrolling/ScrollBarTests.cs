// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies ScrollBar range, input, capture, geometry, and semantic rendering.</summary>
public sealed class ScrollBarTests
{
    /// <summary>Verifies local style ownership overrides Theme fallback and clearing restores it.</summary>
    [ComponentUnitEvidence(typeof(ScrollBar))]
    [Fact]
    public void Style_WhenThemeAndLocalValuesChange_UsesDocumentedPrecedence()
    {
        var theme = CreateTheme(ScrollBarStyle.ThinLine);
        var control = new ScrollBar();

        control.SetTheme(theme);
        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(ScrollBarStyle.Default);

        control.Style = ScrollBarStyle.FullBlock;
        control.ActualStyle.ShouldBe(ScrollBarStyle.FullBlock);

        control.Style = null;
        control.ActualStyle.ShouldBe(ScrollBarStyle.Default);
    }

    /// <summary>Verifies every ScrollBar-declared property starts at its documented default.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var control = new ScrollBar();

        control.Style.ShouldBeNull();
        control.Minimum.ShouldBe(0);
        control.Maximum.ShouldBe(100);
        control.Value.ShouldBe(0);
        control.ViewportSize.ShouldBe(0);
        control.SmallChange.ShouldBe(1);
        control.LargeChange.ShouldBe(10);
        control.Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies disposing the scroll bar prevents every declared property from mutating.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        var control = new ScrollBar();

        control.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => control.Style = ScrollBarStyle.ThinLine);
        _ = Should.Throw<ObjectDisposedException>(() => control.Maximum = 50);
        _ = Should.Throw<ObjectDisposedException>(() => control.Minimum = 0);
        _ = Should.Throw<ObjectDisposedException>(() => control.Value = 1);
        _ = Should.Throw<ObjectDisposedException>(() => control.ViewportSize = 1);
        _ = Should.Throw<ObjectDisposedException>(() => control.SmallChange = 2);
        _ = Should.Throw<ObjectDisposedException>(() => control.LargeChange = 2);
        _ = Should.Throw<ObjectDisposedException>(() => control.Orientation = Orientation.Horizontal);
        _ = Should.Throw<ObjectDisposedException>(() => control.ScrollBy(1));
    }

    /// <summary>Verifies every declared setter requires dispatcher affinity once attached.</summary>
    [Fact]
    public async Task PropertySetter_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ScrollBar();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Style = ScrollBarStyle.ThinLine);
        _ = Should.Throw<InvalidOperationException>(() => control.Maximum = 50);
        _ = Should.Throw<InvalidOperationException>(() => control.Minimum = 0);
        _ = Should.Throw<InvalidOperationException>(() => control.Value = 1);
        _ = Should.Throw<InvalidOperationException>(() => control.ViewportSize = 1);
        _ = Should.Throw<InvalidOperationException>(() => control.SmallChange = 2);
        _ = Should.Throw<InvalidOperationException>(() => control.LargeChange = 2);
        _ = Should.Throw<InvalidOperationException>(() => control.Orientation = Orientation.Horizontal);
        _ = Should.Throw<InvalidOperationException>(() => control.ScrollBy(1));

        control.Value.ShouldBe(0);
    }

    /// <summary>Verifies invalid public assignments fail before changing any range state.</summary>
    [Fact]
    public void Properties_WhenAssignmentIsInvalid_PreservePreviousState()
    {
        var control = new ScrollBar
        {
            Maximum = 100,
            Value = 50,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 25
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Minimum = -1);
        _ = Should.Throw<ArgumentException>(() => control.Minimum = 51);
        _ = Should.Throw<ArgumentException>(() => control.Maximum = 49);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Value = 101);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ViewportSize = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.SmallChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.LargeChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Orientation = (Orientation) 99);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.ScrollBy(1, (ScrollCause) 99));

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
        var control = new ScrollBar { Maximum = 100, Value = 40 };
        List<string> changes = [];
        control.ValueChanged += (_, eventArgs) =>
            changes.Add($"{eventArgs.PreviousValue}>{eventArgs.Value}:{eventArgs.Cause}:{control.Value}");

        control.ScrollBy(int.MaxValue, ScrollCause.Wheel).ShouldBeTrue();
        control.ScrollBy(1, ScrollCause.Keyboard).ShouldBeFalse();
        control.ScrollBy(int.MinValue, ScrollCause.Pointer).ShouldBeTrue();
        control.Value = 25;

        changes.ShouldBe([
            "40>100:Wheel:100",
            "100>0:Pointer:0",
            "0>25:Programmatic:25"
        ]);
    }

    /// <summary>Verifies range arithmetic remains saturating at the largest supported endpoint.</summary>
    [Fact]
    public void ScrollBy_WhenMaximumIsIntegerBoundary_DoesNotOverflow()
    {
        var control = new ScrollBar { Maximum = int.MaxValue, Value = int.MaxValue - 1 };

        control.ScrollBy(int.MaxValue).ShouldBeTrue();

        control.Value.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies immutable press geometry remains safe when a handler narrows the live range.</summary>
    [Fact]
    public async Task Dispatch_WhenRangeChangesDuringThumbDrag_ClampsAgainstCurrentRangeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            control.Attach(dispatcher);
            using PointerManager capture = new(control);
            control.ValueChanged += NarrowOnFirstChange;

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(6, 0), PointerAction.Move));
            var narrowed = control.Minimum;
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Move));

            narrowed.ShouldBeInRange(55, 56);
            control.Value.ShouldBe(narrowed);
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Release));
            return;

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
        var control = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        control.DesiredSize.ShouldBe(new Size(3, 1));
        Cells(frame, width: 10, y: 0).ShouldBe("◀░░░▓▓░░░▶");
    }

    /// <summary>Verifies thin line chrome removes arrow buttons and retains a high-contrast draggable thumb.</summary>
    [Fact]
    public void Render_WhenThinLineChromeIsSelected_UsesCanonicalTrackAndThumb()
    {
        var control = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.ThinLine,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        control.DesiredSize.ShouldBe(new Size(1, 1));
        Cells(frame, width: 10, y: 0).ShouldBe("────━━────");
    }

    /// <summary>Verifies an explicitly assigned glyph remains an intentional local override.</summary>
    [Fact]
    public void Render_WhenLegacyTrackGlyphIsAssigned_UsesAssignedGlyph()
    {
        var control = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            Style = WithBlockGlyphs(track: new Rune('.')),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(control, new Size(10, 1));
        using Frame frame = new(new Size(10, 1));

        control.Render(frame.Canvas);

        Cells(frame, width: 10, y: 0).ShouldBe("◀...▓▓...▶");
    }

    /// <summary>Verifies vertical and horizontal keyboard mappings consume only press transitions.</summary>
    [Fact]
    public void Dispatch_WhenKeyboardCommandsArrive_AppliesOrientationAndPageMappings()
    {
        var control = new ScrollBar { Maximum = 100, Value = 50, SmallChange = 2, LargeChange = 20 };

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

    /// <summary>Verifies keys outside the scrollbar command set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var control = new ScrollBar();
        var raised = 0;
        control.KeyDown += (_, _) => raised++;
        var key = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        _ = Router.Route(control, Events.Key, key);

        // Assert
        key.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies pointer buttons and track clicks apply small and large changes.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerPressesButtonsAndTrack_AppliesExpectedChangesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100,
                Value = 50,
                ViewportSize = 20,
                SmallChange = 2,
                LargeChange = 20
            };
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager capture = new(control);

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
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            control.Attach(dispatcher);
            using PointerManager capture = new(control);

            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            capture.Captured.ShouldBeSameAs(control);
            _ = capture.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));
            control.Value.ShouldBe(100);
            _ = capture.Dispatch(Pointer(new Point(10, 0), PointerAction.Release));

            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a resize mid-drag is reflected immediately: the track length used to
    /// convert the pointer position back to a value is re-read from live geometry on every move
    /// instead of the length captured once when the drag began, so widening the bar mid-drag
    /// changes how far the same absolute cell position scrolls.</summary>
    [Fact]
    public async Task Dispatch_WhenBoundsResizeDuringThumbDrag_UsesLiveTrackLengthAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var narrow = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            narrow.Attach(dispatcher);
            using PointerManager narrowCapture = new(narrow);

            _ = narrowCapture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            _ = narrowCapture.Dispatch(Pointer(new Point(6, 0), PointerAction.Move));
            var narrowValue = narrow.Value;
            _ = narrowCapture.Dispatch(Pointer(new Point(6, 0), PointerAction.Release));

            var resized = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            resized.Attach(dispatcher);
            using PointerManager resizedCapture = new(resized);

            _ = resizedCapture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            resized.Bounds = new Rect(0, 0, 22, 1);
            _ = resizedCapture.Dispatch(Pointer(new Point(6, 0), PointerAction.Move));
            var widenedValue = resized.Value;

            // The same absolute pointer move (press at cell 1, move to cell 6) must scroll less
            // once the track is widened mid-drag, because the same five-cell displacement now
            // covers a smaller fraction of the (now longer) track. A stale track length captured
            // at press time would instead reproduce the narrow bar's value unchanged.
            widenedValue.ShouldBeLessThan(narrowValue);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies inferred pixel input drags and disable cancels without a second commit.</summary>
    [ComponentUnitEvidence(typeof(ScrollBar), ComponentBehavior.Disabled)]
    [Fact]
    public async Task Dispatch_WhenPixelThumbDragIsCancelled_ReleasesCaptureWithoutSpuriousChangeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            control.Attach(dispatcher);
            using PointerManager capture = new(control);
            var changes = 0;
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
            var dragged = control.Value;
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
        var control = new ScrollBar { Maximum = 100, Value = 50, SmallChange = 2 };
        List<ScrollCause> causes = [];
        control.ValueChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);

        Route(control, Wheel(wheelX: 0, wheelY: 3));
        control.Value.ShouldBe(44);
        control.Orientation = Orientation.Horizontal;
        Route(control, Wheel(wheelX: -4, wheelY: 0));

        control.Value.ShouldBe(36);
        causes.ShouldBe([ScrollCause.Wheel, ScrollCause.Wheel]);
    }

    /// <summary>Verifies a wheel at an endpoint remains available to an enclosing overflow host.</summary>
    [Fact]
    public void Dispatch_WhenWheelCannotMoveRange_LeavesEventUnhandled()
    {
        var control = new ScrollBar { Maximum = 10, Value = 10 };
        var eventArgs = new PointerEventArgs(Wheel(wheelX: 0, wheelY: -1));

        _ = Router.Route(control, Events.Pointer, eventArgs);

        control.Value.ShouldBe(10);
        eventArgs.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies terminal focus loss and detach both cancel active thumb ownership.</summary>
    [Fact]
    public async Task Dispatch_WhenCaptureBecomesUnavailable_CancelsDragWithoutChangingValueAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 2) };
            var control = new ScrollBar
            {
                Bounds = new Rect(0, 0, 12, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 100
            };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using PointerManager capture = new(root);

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
        var control = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Bounds = new Rect(0, 0, 1, 1)
        };
        using Frame one = new(new Size(1, 1));

        control.Render(one.Canvas);
        FrameOracle.Get(one, default).ShouldBe("▓");

        control.Bounds = new Rect(0, 0, 2, 1);
        using Frame two = new(new Size(2, 1));
        control.Render(two.Canvas);
        Cells(two, width: 2, y: 0).ShouldBe("◀▶");

        _ = Should.Throw<ArgumentException>(() =>
            control.Style = WithBlockGlyphs(track: new Rune('界')));
        control.ActualStyle.Glyphs.BlockTrack.ShouldBe(new Rune('░'));
    }

    /// <summary>Verifies public event data rejects impossible values and unknown causes.</summary>
    [Fact]
    public void Constructor_WhenScrollEventDataIsInvalid_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollEventArgs(-1, 0, ScrollCause.Programmatic));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new ScrollEventArgs(0, 0, (ScrollCause) 99));
    }

    private static string Cells(Frame frame, int width, int y)
    {
        var result = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, y)));
        }

        return result.ToString();
    }

    private static ScrollBarStyle WithBlockGlyphs(
        Rune? decrement = null,
        Rune? increment = null,
        Rune? track = null,
        Rune? thumb = null)
    {
        var baseline = ScrollBarStyle.Default;
        var glyphs = baseline.Glyphs;
        var replacement = new ScrollBarGlyphs(
            glyphs.VerticalDecrement,
            glyphs.VerticalIncrement,
            decrement ?? glyphs.HorizontalDecrement,
            increment ?? glyphs.HorizontalIncrement,
            track ?? glyphs.BlockTrack,
            thumb ?? glyphs.BlockThumb,
            glyphs.HorizontalLineTrack,
            glyphs.HorizontalLineThumb,
            glyphs.VerticalLineTrack,
            glyphs.VerticalLineThumb);

        return baseline with { Glyphs = replacement };
    }

    private static Theme CreateTheme(ScrollBarStyle _)
    {
        var theme = new Theme();

        theme.Freeze();
        return theme;
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
