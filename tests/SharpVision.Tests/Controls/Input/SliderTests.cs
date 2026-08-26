// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies Slider range state, semantic geometry, and direct input behavior.</summary>
public sealed class SliderTests
{
    /// <summary>Verifies a newer value committed by PropertyChanged owns the typed event stream.</summary>
    [Fact]
    public void Value_WhenPropertyObserverCommitsNewerValue_SuppressesStaleTypedEvent()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100 };
        var observations = new List<(int EventValue, int LiveValue)>();
        slider.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(Slider.Value) && slider.Value == 10)
            {
                slider.Value = 20;
            }
        };
        slider.ValueChanged += (_, eventArgs) => observations.Add((eventArgs.Value, slider.Value));

        slider.Value = 10;

        observations.ShouldBe([(20, 20)]);
    }

    /// <summary>Verifies both endpoint setters restore the value invariant after a throwing observer.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Bounds_WhenPropertyObserverThrows_StillClampValue(bool minimum)
    {
        // Arrange
        var slider = new Slider { Value = minimum ? 10 : 90 };
        var propertyName = minimum ? nameof(Slider.Minimum) : nameof(Slider.Maximum);
        slider.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                throw new InvalidOperationException("observer failure");
            }
        };

        // Act
        _ = Should.Throw<InvalidOperationException>(() =>
        {
            if (minimum)
            {
                slider.Minimum = 20;
            }
            else
            {
                slider.Maximum = 80;
            }
        });

        // Assert
        slider.Value.ShouldBe(minimum ? 20 : 80);
    }

    /// <summary>Verifies documented range, presentation, alignment, and interaction defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        // Arrange and act
        var theme = new Theme();
        theme.Freeze();
        var slider = new Slider();
        slider.SetTheme(theme);

        // Assert
        slider.Minimum.ShouldBe(0);
        slider.Maximum.ShouldBe(100);
        slider.Value.ShouldBe(0);
        slider.SmallChange.ShouldBe(1);
        slider.LargeChange.ShouldBe(10);
        slider.Orientation.ShouldBe(Orientation.Horizontal);
        slider.Style.ShouldBeNull();
        slider.ActualStyle.ShouldBe(SliderStyle.Default);
        slider.CanFocus.ShouldBeTrue();
        slider.IsHitTestVisible.ShouldBeTrue();
    }

    /// <summary>Verifies invalid assignments fail before changing signed range state.</summary>
    [Fact]
    public void Properties_WhenAssignmentIsInvalid_PreservePreviousState()
    {
        var slider = new Slider
        {
            Minimum = -20,
            Maximum = 20,
            Value = 5,
            SmallChange = 2,
            LargeChange = 8
        };

        _ = Should.Throw<ArgumentException>(() => slider.Minimum = 21);
        _ = Should.Throw<ArgumentException>(() => slider.Maximum = -21);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => slider.Value = 21);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => slider.SmallChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => slider.LargeChange = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => slider.Orientation = (Orientation) 99);

        slider.Minimum.ShouldBe(-20);
        slider.Maximum.ShouldBe(20);
        slider.Value.ShouldBe(5);
        slider.SmallChange.ShouldBe(2);
        slider.LargeChange.ShouldBe(8);
        slider.Orientation.ShouldBe(Orientation.Horizontal);
    }

    /// <summary>Verifies raising Minimum above the current Value commits without throwing and
    /// auto-clamps Value to the new endpoint, raising ValueChanged - the documented contract only
    /// throws on an inverting assignment, never on one that merely excludes Value.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveValue_ClampsValueAndRaisesValueChanged()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 10 };
        SliderValueChangedEventArgs? change = null;
        slider.ValueChanged += (_, args) => change = args;

        _ = Should.NotThrow(() => slider.Minimum = 50);

        slider.Minimum.ShouldBe(50);
        slider.Value.ShouldBe(50);
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(10);
        raised.Value.ShouldBe(50);
    }

    /// <summary>Verifies lowering Maximum below the current Value commits without throwing and
    /// auto-clamps Value to the new endpoint, raising ValueChanged.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowValue_ClampsValueAndRaisesValueChanged()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 90 };
        SliderValueChangedEventArgs? change = null;
        slider.ValueChanged += (_, args) => change = args;

        _ = Should.NotThrow(() => slider.Maximum = 40);

        slider.Maximum.ShouldBe(40);
        slider.Value.ShouldBe(40);
        var raised = change.ShouldNotBeNull();
        raised.PreviousValue.ShouldBe(90);
        raised.Value.ShouldBe(40);
    }

    /// <summary>Verifies command arithmetic saturates, clamps, and publishes after commit.</summary>
    [Fact]
    public void ChangeBy_WhenDeltaExceedsSignedRange_ClampsAndRaisesOrderedEvent()
    {
        var slider = new Slider { Minimum = -10, Maximum = 10 };
        List<string> changes = [];
        slider.ValueChanged += (_, eventArgs) =>
            changes.Add($"{eventArgs.PreviousValue}>{eventArgs.Value}:{slider.Value}");

        slider.ChangeBy(int.MaxValue).ShouldBeTrue();
        slider.ChangeBy(1).ShouldBeFalse();
        slider.ChangeBy(int.MinValue).ShouldBeTrue();

        slider.Value.ShouldBe(-10);
        changes.ShouldBe(["0>10:10", "10>-10:-10"]);
    }

    /// <summary>Verifies an extreme signed range maps without overflow.</summary>
    [Fact]
    public void ChangeBy_WhenEndpointsUseIntegerBoundaries_DoesNotOverflow()
    {
        var slider = new Slider { Minimum = int.MinValue, Maximum = int.MaxValue, Value = int.MaxValue - 1 };

        slider.ChangeBy(int.MaxValue).ShouldBeTrue();

        slider.Value.ShouldBe(int.MaxValue);
    }

    /// <summary>Verifies horizontal rendering distinguishes filled, thumb, and remaining cells.</summary>
    [Fact]
    public void Render_WhenHorizontal_WritesExactRailCells()
    {
        var slider = new Slider { Maximum = 100, Value = 50, HorizontalAlignment = HorizontalAlignment.Stretch };
        new LayoutEngine().Layout(slider, new Size(9, 1));
        using Frame frame = new(new Size(9, 1));

        slider.Render(frame.Canvas);

        slider.DesiredSize.ShouldBe(new Size(5, 1));
        Cells(frame, 9, y: 0).ShouldBe("━━━━◆────");
    }

    /// <summary>Verifies vertical minimum is at the bottom and maximum is at the top.</summary>
    [Fact]
    public void Render_WhenVertical_MapsMinimumToBottom()
    {
        var slider = new Slider
        {
            Orientation = Orientation.Vertical,
            Maximum = 10,
            Value = 0,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        new LayoutEngine().Layout(slider, new Size(1, 5));
        using Frame frame = new(new Size(1, 5));

        slider.Render(frame.Canvas);

        slider.DesiredSize.ShouldBe(new Size(1, 5));
        Column(frame, 5, x: 0).ShouldBe("││││◆");
    }

    /// <summary>Verifies zero and one-cell allocations remain contained.</summary>
    [Fact]
    public void Render_WhenBoundsAreTiny_DoesNotEscape()
    {
        var slider = new Slider { Bounds = default };
        using Frame empty = new(new Size(1, 1));

        Should.NotThrow(() => slider.Render(empty.Canvas));

        slider.Bounds = new Rect(0, 0, 1, 1);
        slider.Value = slider.Maximum;
        using Frame one = new(new Size(1, 1));
        slider.Render(one.Canvas);
        FrameOracle.Get(one, default).ShouldBe("◆");
    }

    /// <summary>Verifies orientation-specific keys and paging edit the same signed range.</summary>
    [Fact]
    public void Dispatch_WhenKeyboardCommandsArrive_AppliesSliderMappings()
    {
        var slider = new Slider { Minimum = -10, Maximum = 10, SmallChange = 2, LargeChange = 5 };

        Key(slider, Code.Right);
        Key(slider, Code.PageUp);
        Key(slider, Code.PageDown);
        Key(slider, Code.Home);
        Key(slider, Code.End);
        Key(slider, Code.Left, KeyAction.Release);

        slider.Value.ShouldBe(10);

        slider.Orientation = Orientation.Vertical;
        Key(slider, Code.Down);
        Key(slider, Code.Up);
        slider.Value.ShouldBe(10);
    }

    /// <summary>Verifies keys outside the slider command set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        // Arrange
        var slider = new Slider();
        var raised = 0;
        slider.KeyDown += (_, _) => raised++;
        var key = new KeyEventArgs(new Stroke(
            Code.F1,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));

        // Act
        _ = Router.Route(slider, Events.Key, key);

        // Assert
        key.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies pointer press selects directly and captured movement reaches endpoints.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerSelectsAndDrags_UsesCaptureAndDirectMappingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var slider = new Slider { Bounds = new Rect(0, 0, 11, 1), Maximum = 100 };
            slider.Attach(dispatcher);
            using PointerManager pointer = new(slider);

            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press));

            slider.Value.ShouldBe(50);
            pointer.Captured.ShouldBeSameAs(slider);
            slider.IsPressed.ShouldBeTrue();

            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));
            slider.Value.ShouldBe(100);
            _ = pointer.Dispatch(Pointer(new Point(0, 0), PointerAction.Release));

            slider.Value.ShouldBe(100);
            pointer.Captured.ShouldBeNull();
            slider.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a focus callback may dispose the pressed slider without the pointer path
    /// committing a value or starting capture afterward.</summary>
    [Fact]
    public async Task Dispatch_WhenGotFocusDisposesSlider_StopsPointerContinuationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var slider = new Slider { Bounds = new Rect(0, 0, 11, 1), Maximum = 100 };
            slider.Attach(dispatcher);
            using FocusManager focus = new(slider);
            using PointerManager pointer = new(slider);
            slider.GotFocus += (_, _) => slider.Dispose();

            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press));

            slider.IsDisposed.ShouldBeTrue();
            slider.Value.ShouldBe(0);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing IsFocused observer cannot skip Slider's mandatory blur hook,
    /// which cancels its active press and pointer capture.</summary>
    [Fact]
    public async Task Focus_WhenBlurPropertyObserverThrows_StillCancelsSliderDragAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            var slider = new Slider { Bounds = new Rect(0, 0, 11, 1), Maximum = 100 };
            var next = new Button { Text = "Next" };
            root.Children.Add(slider);
            root.Children.Add(next);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager pointer = new(root);
            focus.Focus(slider).ShouldBeTrue();
            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press));
            slider.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName == nameof(ControlBase.IsFocused) && !slider.IsFocused)
                {
                    throw new InvalidOperationException("blur observer failed");
                }
            };

            _ = Should.Throw<InvalidOperationException>(() => focus.Focus(next));

            slider.IsPressed.ShouldBeFalse();
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an auxiliary release cannot terminate an active primary slider drag.</summary>
    [Fact]
    public async Task Dispatch_WhenSecondaryReleaseArrivesDuringPrimaryDrag_PreservesCaptureAndContinuesAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var slider = new Slider { Bounds = new Rect(0, 0, 11, 1), Maximum = 100 };
            slider.Attach(dispatcher);
            using PointerManager pointer = new(slider);
            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press));

            // Act
            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Release, Buttons.Secondary));

            // Assert
            pointer.Captured.ShouldBeSameAs(slider);
            pointer.PressOrigin.ShouldBeSameAs(slider);
            slider.IsPressed.ShouldBeTrue();

            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Release));
            slider.Value.ShouldBe(100);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies wheel changes commit while endpoint no-ops remain unhandled.</summary>
    [Fact]
    public void Dispatch_WhenWheelMoves_ConsumesOnlyChangedValues()
    {
        var slider = new Slider { Maximum = 10, Value = 5, SmallChange = 2 };
        var changed = new PointerEventArgs(Wheel(wheelY: 1));

        _ = Router.Route(slider, Events.Pointer, changed);

        slider.Value.ShouldBe(7);
        changed.IsHandled.ShouldBeTrue();

        slider.Value = 10;
        var pinned = new PointerEventArgs(Wheel(wheelY: 1));
        _ = Router.Route(slider, Events.Pointer, pinned);

        slider.Value.ShouldBe(10);
        pinned.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies disable cancels a drag without committing a second value.</summary>
    [Fact]
    public async Task Dispatch_WhenSliderBecomesDisabled_CancelsCaptureWithoutChangingValueAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var slider = new Slider { Bounds = new Rect(0, 0, 11, 1), Maximum = 100 };
            slider.Attach(dispatcher);
            using PointerManager pointer = new(slider);
            var changes = 0;
            slider.ValueChanged += (_, _) => changes++;

            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press));
            slider.IsEnabled = false;

            slider.Value.ShouldBe(50);
            changes.ShouldBe(1);
            pointer.Captured.ShouldBeNull();
            slider.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies raising Minimum above Value auto-clamps Value upward.</summary>
    [Fact]
    public void Minimum_WhenRaisedAboveValue_ClampsValueToNewMinimum()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 20 };

        slider.Minimum = 50;

        slider.Minimum.ShouldBe(50);
        slider.Value.ShouldBe(50);
    }

    /// <summary>Verifies lowering Maximum below Value auto-clamps Value downward.</summary>
    [Fact]
    public void Maximum_WhenLoweredBelowValue_ClampsValueToNewMaximum()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 80 };

        slider.Maximum = 50;

        slider.Maximum.ShouldBe(50);
        slider.Value.ShouldBe(50);
    }

    /// <summary>Verifies Minimum still rejects values that exceed Maximum.</summary>
    [Fact]
    public void Minimum_WhenExceedsMaximum_Throws()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100 };

        _ = Should.Throw<ArgumentException>(() => slider.Minimum = 101);

        slider.Minimum.ShouldBe(0);
    }

    /// <summary>Verifies Maximum still rejects values below Minimum.</summary>
    [Fact]
    public void Maximum_WhenBelowMinimum_Throws()
    {
        var slider = new Slider { Minimum = 10, Maximum = 100 };

        _ = Should.Throw<ArgumentException>(() => slider.Maximum = 9);

        slider.Maximum.ShouldBe(100);
    }

    /// <summary>Verifies lowering Maximum below Value raises ValueChanged with clamped transition.</summary>
    [Fact]
    public void Maximum_WhenClampsValue_RaisesValueChanged()
    {
        // Arrange
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 80 };
        var raised = false;
        slider.ValueChanged += (_, eventArgs) =>
        {
            eventArgs.PreviousValue.ShouldBe(80);
            eventArgs.Value.ShouldBe(40);
            raised = true;
        };

        // Act
        slider.Maximum = 40;

        // Assert
        raised.ShouldBeTrue();
    }

    /// <summary>Verifies assigning a Style built from a transparent part color throws and leaves
    /// the previous local Style untouched (colors now live on SliderStyle, not Slider).</summary>
    [Fact]
    public void Style_WhenAssignedStyleHasTransparentPartColor_ThrowsBeforeMutation()
    {
        // Arrange
        var slider = new Slider();
        var baseline = SliderStyle.Default;

        // Act and assert
        _ = Should.Throw<ArgumentException>(() => slider.Style = new SliderStyle(
            baseline.Face,
            baseline.Border,
            baseline.Shadow,
            Color.Transparent,
            baseline.TrackColor,
            baseline.ThumbColor,
            baseline.Glyphs));
        slider.Style.ShouldBeNull();
    }

    /// <summary>Verifies a valid Style assignment round-trips through Style and ActualStyle, and
    /// that a color-only change to an already-assigned Style invalidates rendering only, matching
    /// SliderStyle.Definition's documented render-only color-diff contract.</summary>
    [Fact]
    public void Style_WhenAssignedValidStyle_RoundTripsAndInvalidatesRenderOnColorChange()
    {
        // Arrange
        var theme = new Theme();
        theme.Freeze();
        var slider = new Slider();
        slider.SetTheme(theme);
        var custom = SliderStyle.Default with { FillColor = Color.Rgb(10, 20, 30) };

        // Act
        slider.Style = custom;

        // Assert
        slider.Style.ShouldBe(custom);
        slider.ActualStyle.ShouldBe(custom);

        // Act - a color-only change
        slider.Clear(Invalidation.All);
        slider.Style = custom with { TrackColor = Color.Rgb(40, 50, 60) };

        // Assert
        slider.Pending.ShouldBe(Invalidation.Render);

        // Act - clearing restores theme ownership
        slider.Style = null;

        // Assert
        slider.Style.ShouldBeNull();
        slider.ActualStyle.ShouldBe(SliderStyle.Default);
    }

    /// <summary>Verifies disposing the slider prevents further mutation.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        // Arrange
        var slider = new Slider();

        // Act
        slider.Dispose();

        // Assert
        _ = Should.Throw<ObjectDisposedException>(() => slider.Value = 50);
    }

    /// <summary>Verifies ChangeBy rejects use after disposal, matching its documented
    /// ObjectDisposedException contract independently of the Value setter.</summary>
    [Fact]
    public void ChangeBy_WhenDisposed_Throws()
    {
        // Arrange
        var slider = new Slider();
        slider.Dispose();

        // Act and assert
        _ = Should.Throw<ObjectDisposedException>(() => slider.ChangeBy(1));
    }

    /// <summary>Verifies Orientation invalidates measurement, matching its documented layout-axis
    /// swap contract.</summary>
    [Fact]
    public void Orientation_WhenChanged_InvalidatesMeasure()
    {
        // Arrange
        var slider = new Slider();
        slider.Clear(Invalidation.All);

        // Act
        slider.Orientation = Orientation.Vertical;

        // Assert
        slider.Orientation.ShouldBe(Orientation.Vertical);
        slider.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a non-clamping Minimum or Maximum change still invalidates rendering,
    /// matching the endpoint markers' own documented render-only impact.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MinimumOrMaximum_WhenChangedWithoutClampingValue_InvalidatesRenderOnly(bool changeMinimum)
    {
        // Arrange
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        slider.Clear(Invalidation.All);

        // Act
        if (changeMinimum)
        {
            slider.Minimum = 10;
        }
        else
        {
            slider.Maximum = 90;
        }

        // Assert
        slider.Value.ShouldBe(50);
        slider.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies auto-clamping raises ValueChanged when Value is adjusted.</summary>
    [Fact]
    public void Minimum_WhenClampsValue_RaisesValueChanged()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 30 };
        var raised = false;
        slider.ValueChanged += (_, eventArgs) =>
        {
            eventArgs.PreviousValue.ShouldBe(30);
            eventArgs.Value.ShouldBe(60);
            raised = true;
        };

        slider.Minimum = 60;

        raised.ShouldBeTrue();
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

    private static string Column(Frame frame, int height, int x)
    {
        var result = new StringBuilder(height);

        for (var y = 0; y < height; y++)
        {
            _ = result.Append(FrameOracle.Get(frame, new Point(x, y)));
        }

        return result.ToString();
    }

    private static void Key(
        Slider slider,
        Code code,
        KeyAction action = KeyAction.Press) =>
        Router.Route(
            slider,
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
        Buttons buttons = Buttons.Primary) => new(
        cells,
        pixels: null,
        buttons,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static Pointer Wheel(int wheelY) => new(
        cells: default,
        pixels: null,
        Buttons.None,
        PointerAction.Wheel,
        wheelX: 0,
        wheelY,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);
}
