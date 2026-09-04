// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies ColorPlane's HSV selection state, keyboard and pointer editing, and lifecycle
/// behavior directly, complementing the composition-focused coverage in ColorPickerTests and
/// ColorPickerSurfaceTests.</summary>
public sealed class ColorPlaneTests
{
    /// <summary>Verifies documented construction defaults.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var plane = new ColorPlane();

        plane.Hue.ShouldBe(0);
        plane.Saturation.ShouldBe(1);
        plane.Value.ShouldBe(1);
        plane.SelectedMarker.ShouldBeNull();
        plane.IsFocusable.ShouldBeTrue();
        plane.IsTabStop.ShouldBeTrue();
        plane.TabNavigation.ShouldBe(TabNavigation.None);
        plane.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        plane.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
    }

    /// <summary>Verifies SetSelection round-trips exact HSV coordinates and invalidates rendering.</summary>
    [Fact]
    public void SetSelection_WhenValuesChange_RoundTripsAndInvalidatesRender()
    {
        var plane = new ColorPlane();
        plane.Clear(Invalidation.All);

        plane.SetSelection(200, 0.4, 0.6);

        plane.Hue.ShouldBe(200);
        plane.Saturation.ShouldBe(0.4);
        plane.Value.ShouldBe(0.6);
        plane.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies SetSelection with the identical coordinates is a no-op that raises no
    /// invalidation and no Changed event.</summary>
    [Fact]
    public void SetSelection_WhenValuesAreUnchanged_DoesNotInvalidateOrRaiseChanged()
    {
        var plane = new ColorPlane();
        plane.SetSelection(90, 0.5, 0.5);
        plane.Clear(Invalidation.All);
        var raised = 0;
        plane.Changed += (_, _) => raised++;

        plane.SetSelection(90, 0.5, 0.5);

        plane.Pending.ShouldBe(Invalidation.None);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies SetSelection never raises Changed - only user-driven Commit does, matching
    /// its documented "without publishing input" contract.</summary>
    [Fact]
    public void SetSelection_WhenValuesChange_DoesNotRaiseChanged()
    {
        var plane = new ColorPlane();
        var raised = 0;
        plane.Changed += (_, _) => raised++;

        plane.SetSelection(45, 0.2, 0.8);

        raised.ShouldBe(0);
    }

    /// <summary>Verifies SetSelection rejects use after disposal.</summary>
    [Fact]
    public void SetSelection_WhenDisposed_Throws()
    {
        var plane = new ColorPlane();
        plane.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => plane.SetSelection(0, 0, 0));
    }

    /// <summary>Verifies Left and Right arrows adjust saturation by the fixed 0.01 step, clamping at
    /// the 0 and 1 endpoints instead of overshooting.</summary>
    [Fact]
    public void Dispatch_WhenLeftAndRightArrowsMove_AdjustsSaturationWithClamping()
    {
        var plane = new ColorPlane();
        plane.SetSelection(0, 0.5, 0.5);

        _ = Key(plane, Code.Right);
        plane.Saturation.ShouldBe(0.51, tolerance: 1e-9);

        _ = Key(plane, Code.Left);
        _ = Key(plane, Code.Left);
        plane.Saturation.ShouldBe(0.49, tolerance: 1e-9);

        plane.SetSelection(0, 0, 0.5);
        var handled = Key(plane, Code.Left);
        plane.Saturation.ShouldBe(0);
        handled.IsHandled.ShouldBeTrue();

        plane.SetSelection(0, 1, 0.5);
        _ = Key(plane, Code.Right);
        plane.Saturation.ShouldBe(1);
    }

    /// <summary>Verifies Up and Down arrows adjust value by the fixed 0.01 step, clamping at the 0
    /// and 1 endpoints.</summary>
    [Fact]
    public void Dispatch_WhenUpAndDownArrowsMove_AdjustsValueWithClamping()
    {
        var plane = new ColorPlane();
        plane.SetSelection(0, 0.5, 0.5);

        _ = Key(plane, Code.Up);
        plane.Value.ShouldBe(0.51, tolerance: 1e-9);

        _ = Key(plane, Code.Down);
        _ = Key(plane, Code.Down);
        plane.Value.ShouldBe(0.49, tolerance: 1e-9);

        plane.SetSelection(0, 0.5, 1);
        _ = Key(plane, Code.Up);
        plane.Value.ShouldBe(1);

        plane.SetSelection(0, 0.5, 0);
        _ = Key(plane, Code.Down);
        plane.Value.ShouldBe(0);
    }

    /// <summary>Verifies Home and End jump saturation directly to its minimum and maximum.</summary>
    [Fact]
    public void Dispatch_WhenHomeOrEndArrives_JumpsSaturationToEndpoint()
    {
        var plane = new ColorPlane();
        plane.SetSelection(0, 0.5, 0.5);

        _ = Key(plane, Code.Home);
        plane.Saturation.ShouldBe(0);

        _ = Key(plane, Code.End);
        plane.Saturation.ShouldBe(1);
    }

    /// <summary>Verifies a committed key edit raises Changed exactly once and marks the key event
    /// handled, while a key repeat also commits.</summary>
    [Fact]
    public void Dispatch_WhenKeyCommits_RaisesChangedAndHandlesEvent()
    {
        var plane = new ColorPlane();
        plane.SetSelection(0, 0.5, 0.5);
        var raised = 0;
        plane.Changed += (_, _) => raised++;

        var pressed = Key(plane, Code.Right);
        raised.ShouldBe(1);
        pressed.IsHandled.ShouldBeTrue();

        var repeated = Key(plane, Code.Right, KeyAction.Repeat);
        raised.ShouldBe(2);
        repeated.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies a key release is ignored - only Press and Repeat edit the selection.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsReleased_DoesNotEditSelection()
    {
        var plane = new ColorPlane();
        plane.SetSelection(0, 0.5, 0.5);

        var released = Key(plane, Code.Right, KeyAction.Release);

        plane.Saturation.ShouldBe(0.5);
        released.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies keys outside the plane's editing set remain available to routed input.</summary>
    [Fact]
    public void Dispatch_WhenKeyIsUnhandled_RaisesInheritedKeyDownWithoutConsumingIt()
    {
        var plane = new ColorPlane();
        var raised = 0;
        plane.KeyDown += (_, _) => raised++;

        var key = Key(plane, Code.F1);

        key.IsHandled.ShouldBeFalse();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies a disabled or hidden plane ignores keyboard editing entirely.</summary>
    [Theory]
    [InlineData(false, Visibility.Visible)]
    [InlineData(true, Visibility.Hidden)]
    public void Dispatch_WhenPlaneIsUnavailable_IgnoresKeyboardEditing(bool enabled, Visibility visibility)
    {
        var plane = new ColorPlane { IsEnabled = enabled, Visibility = visibility };
        plane.SetSelection(0, 0.5, 0.5);

        var key = Key(plane, Code.Right);

        plane.Saturation.ShouldBe(0.5);
        key.IsHandled.ShouldBeFalse();
    }

    /// <summary>Verifies pointer press requests focus, selects the pressed coordinate, and begins a
    /// drag that tracks subsequent movement until release ends it.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerPressesAndDrags_SelectsAndTracksUntilReleaseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var plane = new ColorPlane { Bounds = new Rect(0, 0, 11, 11) };
            plane.Attach(dispatcher);
            using PointerManager pointer = new(plane);
            var changes = 0;
            plane.Changed += (_, _) => changes++;

            _ = pointer.Dispatch(Pointer(new Point(0, 10), PointerAction.Press));

            plane.Saturation.ShouldBe(0);
            plane.Value.ShouldBe(0);
            pointer.Captured.ShouldBeSameAs(plane);
            plane.IsPressed.ShouldBeTrue();
            changes.ShouldBe(1);

            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));

            plane.Saturation.ShouldBe(1);
            plane.Value.ShouldBe(1);
            changes.ShouldBe(2);

            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Release));

            pointer.Captured.ShouldBeNull();
            plane.IsPressed.ShouldBeFalse();

            // Act - movement after release no longer tracks the drag.
            _ = pointer.Dispatch(Pointer(new Point(0, 0), PointerAction.Move));

            plane.Saturation.ShouldBe(1);
            changes.ShouldBe(2);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a pointer Leave while dragging cancels the drag exactly like Release.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerLeavesWhileDragging_CancelsDragAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var plane = new ColorPlane { Bounds = new Rect(0, 0, 11, 11) };
            plane.Attach(dispatcher);
            using PointerManager pointer = new(plane);
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Press));
            pointer.Captured.ShouldBeSameAs(plane);

            _ = pointer.Dispatch(Leave());

            pointer.Captured.ShouldBeNull();
            plane.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an auxiliary release cannot terminate an active primary color-plane drag.</summary>
    [Fact]
    public async Task Dispatch_WhenSecondaryReleaseArrivesDuringPrimaryDrag_PreservesCaptureAndContinuesAsync()
    {
        // Arrange
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var plane = new ColorPlane { Bounds = new Rect(0, 0, 11, 11) };
            plane.Attach(dispatcher);
            using PointerManager pointer = new(plane);
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Press));

            // Act
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Release, Buttons.Secondary));

            // Assert
            pointer.Captured.ShouldBeSameAs(plane);
            pointer.PressOrigin.ShouldBeSameAs(plane);
            plane.IsPressed.ShouldBeTrue();

            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Move));
            _ = pointer.Dispatch(Pointer(new Point(10, 0), PointerAction.Release));
            plane.Saturation.ShouldBe(1);
            plane.Value.ShouldBe(1);
            pointer.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disabling the plane mid-drag cancels capture without a further commit.</summary>
    [Fact]
    public async Task Dispatch_WhenPlaneBecomesDisabledDuringDrag_CancelsCaptureWithoutFurtherChangeAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var plane = new ColorPlane { Bounds = new Rect(0, 0, 11, 11) };
            plane.Attach(dispatcher);
            using PointerManager pointer = new(plane);
            var changes = 0;
            plane.Changed += (_, _) => changes++;
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Press));

            plane.IsEnabled = false;

            pointer.Captured.ShouldBeNull();
            plane.IsPressed.ShouldBeFalse();
            changes.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a drag survives an ancestor re-arranging the plane at an extreme coordinate
    /// mid-gesture: the pointer's small, terminal-bounded cell position and the plane's now-extreme
    /// <see cref="ControlBase.Bounds"/> origin must combine with saturating arithmetic, not plain
    /// subtraction, or the computed saturation/value wraps around instead of clamping toward the
    /// endpoint that position implies.</summary>
    [Fact]
    public async Task Dispatch_WhenBoundsMovesToExtremeCoordinateDuringDrag_SaturatesInsteadOfWrappingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var plane = new ColorPlane { Bounds = new Rect(0, 0, 11, 11) };
            plane.Attach(dispatcher);
            using PointerManager pointer = new(plane);

            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Press));

            // Mirrors an ancestor (e.g. a deeply Right-docked panel) re-arranging this already-
            // dragging plane at the integer coordinate limit mid-gesture: ContentBounds' origin now
            // sits far from the pointer's real, terminal-bounded cell position.
            plane.Bounds = new Rect(int.MinValue, int.MinValue, 11, 11);
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Move));

            plane.Saturation.ShouldBe(1);
            plane.Value.ShouldBe(0);
            _ = pointer.Dispatch(Pointer(new Point(5, 5), PointerAction.Release));
        }, TestContext.Current.CancellationToken);
    }

    // Losing focus mid-drag cancelling capture (ColorPlane.OnFocusChanged -> DragBehavior.FocusChanged)
    // is proven end-to-end through the composed picker in
    // ColorPickerTests.Dispatch_WhenPlaneLosesFocus_CancelsPointerCaptureAsync, which lays the
    // retained plane out through its owning ColorPicker instead of duplicating that layout here.

    /// <summary>Verifies disposal clears the Changed event so late-firing subscribers never observe
    /// a post-disposal commit.</summary>
    [Fact]
    public void Dispose_WhenCalled_ClearsChangedEventSubscribers()
    {
        var plane = new ColorPlane();
        var raised = 0;
        plane.Changed += (_, _) => raised++;

        plane.Dispose();

        raised.ShouldBe(0);
    }

    /// <summary>Verifies a huge logical color plane shades only the visible canvas cells.</summary>
    [Fact]
    public void Render_WhenHugeBoundsAreClipped_CompletesForVisibleCellsOnly()
    {
        var plane = new ColorPlane { Bounds = new Rect(0, 0, int.MaxValue, int.MaxValue) };
        using var frame = new Frame(new Size(2, 2));

        Should.NotThrow(() => plane.Render(frame.Canvas));

        frame.GetCell(new Point(1, 1)).Style.Background.ShouldNotBe(Color.Default);
    }

    private static KeyEventArgs Key(ColorPlane plane, Code code, KeyAction action = KeyAction.Press)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, character: null, nativeCode: 0, Modifiers.None, action));
        _ = Router.Route(plane, Events.Key, eventArgs);
        return eventArgs;
    }

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

    private static Pointer Leave() => new(
        cells: null,
        pixels: null,
        Buttons.None,
        PointerAction.Leave,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);
}
