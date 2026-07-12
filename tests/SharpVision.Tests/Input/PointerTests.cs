using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Styling;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

namespace SharpVision.Tests.Input;

/// <summary>Verifies hit testing, local coordinates, capture, and pointer state cleanup.</summary>
public sealed class PointerTests
{
    /// <summary>Verifies pixel-only input cannot hit the top-left control.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHasNoCells_DoesNotFabricateHitAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 5) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            var pointer = new Pointer(
                null,
                new Point(5, 5),
                Buttons.None,
                PointerAction.Move,
                0,
                0,
                Modifiers.None,
                true,
                false);

            manager.Dispatch(pointer).ShouldBeNull();
            manager.Hovered.ShouldBeNull();
            child.IsHovered.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capture receives pixel-only input with unavailable local cells.</summary>
    [Fact]
    public async Task Dispatch_WhenPixelOnlyPointerIsCaptured_RoutesWithoutLocalCellsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 5) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            Point? local = default;
            var routed = false;
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    local = eventArgs.LocalCells;
                    routed = true;
                }
            });
            manager.Capture(child).ShouldBeTrue();
            var pointer = new Pointer(
                null,
                new Point(15, 8),
                Buttons.Primary,
                PointerAction.Move,
                0,
                0,
                Modifiers.None,
                true,
                false);

            manager.Dispatch(pointer).ShouldBeSameAs(child);
            routed.ShouldBeTrue();
            local.ShouldBeNull();
            manager.Hovered.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies reverse child order, parent clipping, and disabled exclusion.</summary>
    [Fact]
    public void HitTest_WhenChildrenOverlap_ReturnsHighestEligibleClippedControl()
    {
        var root = new ProbeContainer { Bounds = new Rect(0, 0, 10, 6) };
        var lower = new ProbeControl { Bounds = new Rect(1, 1, 8, 4) };
        var higher = new ProbeControl { Bounds = new Rect(2, 1, 8, 4) };
        root.Children.Add(lower);
        root.Children.Add(higher);

        root.HitTest(new Point(3, 2)).ShouldBeSameAs(higher);
        higher.IsEnabled = false;
        root.HitTest(new Point(3, 2)).ShouldBeSameAs(lower);
        root.HitTest(new Point(11, 2)).ShouldBeNull();
    }

    /// <summary>Verifies each route element observes pointer coordinates in its own bounds.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsChild_ProvidesLocalCoordinatesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(4, 3, 8, 4) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            var observed = new List<(Control Sender, Point? Local)>();
            _ = root.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    observed.Add(((Control) sender!, eventArgs.LocalCells));
                }
            });
            _ = child.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    observed.Add(((Control) sender!, eventArgs.LocalCells));
                }
            });

            manager.Dispatch(CreatePointer(new Point(6, 5), PointerAction.Move))
                .ShouldBeSameAs(child);

            observed.ShouldBe([
                (child, new Point(2, 2)),
                (root, new Point(6, 5)),
            ]);
            manager.Hovered.ShouldBeSameAs(child);
            child.IsHovered.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary click focuses any eligible focusable hit target.</summary>
    [Fact]
    public async Task Dispatch_WhenPrimaryPointerPressesFocusableControl_FocusesItAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl
            {
                Bounds = new Rect(4, 3, 8, 4),
                CanFocus = true,
                Style = FocusStyle(),
            };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new CaptureManager(root);

            capture.Dispatch(CreatePointer(new Point(6, 5), PointerAction.Press))
                .ShouldBeSameAs(child);

            focus.Focused.ShouldBeSameAs(child);
            child.IsFocused.ShouldBeTrue();
            child.Appearance.Background.ShouldBe(Color.Indexed(11));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capture overrides hit testing until explicit release.</summary>
    [Fact]
    public async Task Capture_WhenActive_TakesPrecedenceUntilReleasedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 10, 10) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);

            manager.Capture(first).ShouldBeTrue();
            manager.Dispatch(CreatePointer(new Point(15, 5), PointerAction.Move))
                .ShouldBeSameAs(first);
            manager.Release();
            manager.Dispatch(CreatePointer(new Point(15, 5), PointerAction.Move))
                .ShouldBeSameAs(second);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capture retains routed delivery while hover follows the physical pointer target.</summary>
    [Fact]
    public async Task Dispatch_WhenCaptureIsActive_HoversPhysicalTargetAndRoutesToCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbePressable { Bounds = new Rect(0, 0, 10, 10) };
            var second = new ProbePressable { Bounds = new Rect(10, 0, 10, 10) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            var routed = new List<Control>();
            _ = first.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    routed.Add((Control) sender!);
                }
            });

            manager.Capture(first).ShouldBeTrue();
            manager.Dispatch(CreatePointer(new Point(15, 5), PointerAction.Move))
                .ShouldBeSameAs(first);

            manager.Captured.ShouldBeSameAs(first);
            manager.Hovered.ShouldBeSameAs(second);
            first.IsHovered.ShouldBeFalse();
            second.IsHovered.ShouldBeTrue();
            routed.ShouldBe([first]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detach and terminal focus loss cancel capture exactly once.</summary>
    [Fact]
    public async Task Capture_WhenStateBecomesInvalid_ReleasesWithReasonAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            var reasons = new List<ReleaseReason>();
            manager.Cancelled += (_, eventArgs) => reasons.Add(eventArgs.Reason);
            manager.Capture(child).ShouldBeTrue();

            _ = root.Children.Remove(child);

            manager.Captured.ShouldBeNull();
            reasons.ShouldBe([ReleaseReason.Detached]);
            root.Children.Add(child);
            manager.Capture(child).ShouldBeTrue();
            manager.TerminalFocusLost();
            manager.TerminalFocusLost();
            reasons.ShouldBe([ReleaseReason.Detached, ReleaseReason.TerminalFocusLost]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disabled and hidden capture targets cancel with precise reasons.</summary>
    [Fact]
    public async Task Capture_WhenTargetDisablesOrHides_CancelsImmediatelyAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);
            var reasons = new List<ReleaseReason>();
            manager.Cancelled += (_, eventArgs) => reasons.Add(eventArgs.Reason);
            manager.Capture(child).ShouldBeTrue();

            child.IsEnabled = false;
            child.IsEnabled = true;
            manager.Capture(child).ShouldBeTrue();
            child.Visibility = SharpVision.Layout.Visibility.Hidden;

            manager.Captured.ShouldBeNull();
            reasons.ShouldBe([ReleaseReason.Disabled, ReleaseReason.Hidden]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies root disposal clears state and manager ownership without leaks.</summary>
    [Fact]
    public async Task Dispose_WhenRootOwnsManagers_SeversAllReferencesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 10),
                CanFocus = true,
            };
            root.Children.Add(child);
            root.Attach(dispatcher);
            var focus = new FocusManager(root);
            var capture = new CaptureManager(root);
            focus.Focus(child).ShouldBeTrue();
            capture.Capture(child).ShouldBeTrue();

            root.Dispose();

            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
            root.FocusOwner.ShouldBeNull();
            root.CaptureOwner.ShouldBeNull();
            _ = Should.Throw<ObjectDisposedException>(() => focus.Focus(null));
            _ = Should.Throw<ObjectDisposedException>(capture.Release);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hover transitions and press cancellation clear visual state.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerMovesPressesAndLeaves_UpdatesVisualStatesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new CaptureManager(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            child.IsHovered.ShouldBeTrue();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            child.IsPressed.ShouldBeTrue();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            child.IsPressed.ShouldBeFalse();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            manager.TerminalFocusLost();
            child.IsHovered.ShouldBeFalse();
            child.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static Pointer CreatePointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static Style FocusStyle()
    {
        var style = new Style();
        style.Set(State.Normal, new Appearance(background: Color.Indexed(10)));
        style.Set(State.Focused, new Appearance(background: Color.Indexed(11)));
        return style;
    }
}
