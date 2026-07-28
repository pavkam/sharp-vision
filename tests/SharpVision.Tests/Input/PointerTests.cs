// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies hit testing, local coordinates, capture, and pointer state cleanup.</summary>
public sealed class PointerTests
{
    /// <summary>Verifies removing focus eligibility releases focus without cancelling independent pointer capture.</summary>
    [Fact]
    public async Task CanFocus_WhenFocusedControlIsCaptured_ReleasesFocusAndPreservesCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 5), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            capture.Capture(child).ShouldBeTrue();
            child.Focusable = false;

            focus.Focused.ShouldBeNull();
            child.IsFocused.ShouldBeFalse();
            capture.Captured.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

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
            using PointerManager manager = new(root);
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
            child.IsPointerOver.ShouldBeFalse();
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
            using PointerManager manager = new(root);
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
            var child = new ProbeControl { Bounds = new Rect(4, 3, 8, 4), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);
            List<(Control Sender, Point? Local)> observed = [];
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
                (root, new Point(6, 5))
            ]);
            manager.Hovered.ShouldBeSameAs(child);
            child.IsPointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a non-interactive hit target routes input but is never hovered.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsNonInteractiveControl_DoesNotHoverAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move))
                .ShouldBeSameAs(child);

            manager.Hovered.ShouldBeSameAs(child);
            child.IsPointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hover records the physical leaf while ancestors receive subtree membership.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsChildOfInteractiveAncestor_HoversAncestorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 12, 8), Focusable = true };
            var child = new ProbeControl { Bounds = new Rect(2, 2, 6, 4) };
            ancestor.Children.Add(child);
            root.Children.Add(ancestor);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            manager.Dispatch(CreatePointer(new Point(4, 3), PointerAction.Move))
                .ShouldBeSameAs(child);

            manager.Hovered.ShouldBeSameAs(child);
            ancestor.IsPointerOver.ShouldBeTrue();
            child.IsPointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies non-Container owned ancestry resolves hover and releases scoped pointer state on removal.</summary>
    [Fact]
    public async Task Dispatch_WhenNonContainerOwnedSubtreeIsRemoved_ClearsHoverAndCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new TraversalOwner { Bounds = new Rect(0, 0, 20, 10) };
            var middle = new TraversalOwner { Bounds = new Rect(0, 0, 12, 8), Focusable = true };
            var leaf = new ProbeControl { Bounds = new Rect(2, 2, 6, 4) };
            middle.AddNormal(leaf);
            root.AddNormal(middle);
            root.Attach(dispatcher);
            using var capture = new PointerManager(root);

            capture.Dispatch(CreatePointer(new Point(4, 3), PointerAction.Move)).ShouldBeSameAs(leaf);
            capture.Hovered.ShouldBeSameAs(leaf);
            capture.Capture(leaf).ShouldBeTrue();

            root.RemoveNormal(middle).ShouldBeTrue();

            capture.Hovered.ShouldBeNull();
            capture.Captured.ShouldBeNull();
            middle.IsPointerOver.ShouldBeFalse();
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
            var child = new ProbeControl { Bounds = new Rect(4, 3, 8, 4), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            capture.Dispatch(CreatePointer(new Point(6, 5), PointerAction.Press))
                .ShouldBeSameAs(child);

            focus.Focused.ShouldBeSameAs(child);
            child.IsFocused.ShouldBeTrue();
            child.Face.Background.ShouldBe(ThemeColor.Control);
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
            using PointerManager manager = new(root);

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
            using PointerManager manager = new(root);
            List<Control> routed = [];
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
            first.IsPointerOver.ShouldBeFalse();
            second.IsPointerOver.ShouldBeTrue();
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
            using PointerManager manager = new(root);
            List<PointerCaptureLossReason> reasons = [];
            child.LostPointerCapture += (_, eventArgs) => reasons.Add(eventArgs.Reason);
            manager.Capture(child).ShouldBeTrue();

            _ = root.Children.Remove(child);

            manager.Captured.ShouldBeNull();
            reasons.ShouldBe([PointerCaptureLossReason.Unavailable]);
            root.Children.Add(child);
            manager.Capture(child).ShouldBeTrue();
            manager.TerminalFocusLost();
            manager.TerminalFocusLost();
            reasons.ShouldBe([PointerCaptureLossReason.Unavailable, PointerCaptureLossReason.TerminalFocusLost]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the protected release seam cannot release another control's capture.</summary>
    [Fact]
    public async Task ReleasePointerCapture_WhenAnotherControlOwnsCapture_PreservesOwnerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var owner = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            var other = new ProbeControl { Bounds = new Rect(10, 0, 10, 10) };
            root.Children.Add(owner);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            owner.CaptureProbePointer().ShouldBeTrue();

            other.ReleaseProbePointer();

            manager.Captured.ShouldBeSameAs(owner);
            owner.ProbeHasPointerCapture.ShouldBeTrue();
            other.ProbeHasPointerCapture.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies implicit cancellation clears all pointer state before the protected hook runs.</summary>
    [Fact]
    public async Task Capture_WhenImplicitlyCancelled_ClearsStateBeforeControlHookAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            child.CaptureProbePointer().ShouldBeTrue();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));

            _ = root.Children.Remove(child);

            manager.Captured.ShouldBeNull();
            manager.Hovered.ShouldBeNull();
            manager.PressOrigin.ShouldBeNull();
            child.PointerCaptureCancellationCalls.ShouldBe(1);
            child.PointerCaptureCancellationReason.ShouldBe(PointerCaptureLossReason.Unavailable);
            child.PointerStateWasClearDuringCancellation.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cancellation callbacks cannot reacquire capture before detachment commits.</summary>
    [Fact]
    public async Task Capture_WhenCancellationHookRequestsRecapture_RejectsRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 10),
                RecaptureDuringPointerCancellation = true
            };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            child.CaptureProbePointer().ShouldBeTrue();

            _ = root.Children.Remove(child);

            child.RecaptureResult.ShouldBe(false);
            child.Parent.ShouldBeNull();
            manager.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies state-change callbacks cannot reacquire capture while cancellation is publishing.</summary>
    [Fact]
    public async Task Press_WhenClearingCallbackRequestsCapture_RejectsRequestAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            child.RecaptureWhenPressedClears = true;

            _ = root.Children.Remove(child);

            child.PressedClearRecaptureResult.ShouldBeNull();
            child.Parent.ShouldBeNull();
            manager.Captured.ShouldBeNull();
            manager.PressOrigin.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies press cleanup without capture does not publish a capture callback.</summary>
    [Fact]
    public async Task Press_WhenImplicitlyCancelledWithoutCapture_DoesNotCallCaptureHookAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));

            child.IsEnabled = false;

            manager.PressOrigin.ShouldBeNull();
            child.PointerCaptureCancellationCalls.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing capture hook cannot strand a disposed child in its owner.</summary>
    [Fact]
    public async Task Dispose_WhenCapturedChildHookThrows_CompletesTreeAndManagerCleanupAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 10),
                ThrowOnPointerCaptureCancellation = true
            };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            child.CaptureProbePointer().ShouldBeTrue();
            var cancellations = 0;
            child.LostPointerCapture += (_, _) => cancellations++;

            _ = Should.Throw<InvalidOperationException>(child.Dispose);

            child.IsDisposed.ShouldBeTrue();
            child.Parent.ShouldBeNull();
            root.Children.ShouldBeEmpty();
            manager.Captured.ShouldBeNull();
            cancellations.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing capture hook cannot prevent root-manager and control cleanup.</summary>
    [Fact]
    public async Task Dispose_WhenCapturedRootHookThrows_CompletesManagerAndControlCleanupAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), ThrowOnPointerCaptureCancellation = true };
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            root.CaptureProbePointer().ShouldBeTrue();
            var cancellations = 0;
            root.LostPointerCapture += (_, _) => cancellations++;

            _ = Should.Throw<InvalidOperationException>(root.Dispose);

            root.IsDisposed.ShouldBeTrue();
            root.CaptureOwner.ShouldBeNull();
            manager.Captured.ShouldBeNull();
            cancellations.ShouldBe(1);
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
            using PointerManager manager = new(root);
            List<PointerCaptureLossReason> reasons = [];
            child.LostPointerCapture += (_, eventArgs) => reasons.Add(eventArgs.Reason);
            manager.Capture(child).ShouldBeTrue();

            child.IsEnabled = false;
            child.IsEnabled = true;
            manager.Capture(child).ShouldBeTrue();
            child.Visibility = Visibility.Hidden;

            manager.Captured.ShouldBeNull();
            reasons.ShouldBe([PointerCaptureLossReason.Unavailable, PointerCaptureLossReason.Unavailable]);
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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            var focus = new FocusManager(root);
            var capture = new PointerManager(root);
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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            child.IsPointerOver.ShouldBeTrue();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            child.IsPressed.ShouldBeFalse();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            child.IsPressed.ShouldBeFalse();
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            manager.TerminalFocusLost();
            child.IsPointerOver.ShouldBeFalse();
            child.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies routed click counts accumulate only for one target, button, cell, and deadline.</summary>
    [Fact]
    public async Task Dispatch_WhenPressesRepeatAndDiverge_ReportsDeterministicClickCountsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 8, 8), Focusable = true };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 8, 8), Focusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            var clock = new ManualTimeProvider();
            using PointerManager manager = new(root, clock);
            List<int> observed = [];
            _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    observed.Add(eventArgs.ClickCount);
                }
            });

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            clock.Advance(TimeSpan.FromMilliseconds(200));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            clock.Advance(TimeSpan.FromMilliseconds(501));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(3, 2), PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(12, 2), PointerAction.Press));

            observed.ShouldBe([1, 0, 2, 0, 1, 1, 1]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a primary press publishes its stable delivery target before routed handlers run.</summary>
    [Fact]
    public async Task Dispatch_WhenPrimaryPressTargetsControl_PublishesActivationBeforeRoutingAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(1, 1, 8, 4) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            List<Control?> activated = [];
            using var manager = new PointerManager(
                root,
                null,
                target =>
                {
                    activated.Add(target);
                    return null;
                });
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == Phase.Bubble)
                {
                    activated.ShouldHaveSingleItem().ShouldBeSameAs(child);
                }
            });

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));

            activated.ShouldHaveSingleItem().ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies only primary cell presses publish application activation targets.</summary>
    [Fact]
    public async Task Dispatch_WhenRecordDoesNotQualify_DoesNotPublishActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeControl { Bounds = new Rect(0, 0, 20, 10) };
            root.Attach(dispatcher);
            List<Control?> activated = [];
            using var manager = new PointerManager(
                root,
                null,
                target =>
                {
                    activated.Add(target);
                    return null;
                });

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));
            _ = manager.Dispatch(new Pointer(
                new Point(2, 2),
                pixels: null,
                Buttons.Secondary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));
            _ = manager.Dispatch(new Pointer(
                new Point(2, 2),
                pixels: null,
                Buttons.None,
                PointerAction.Wheel,
                wheelX: 0,
                wheelY: 1,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));

            activated.ShouldBeEmpty();

            _ = manager.Dispatch(CreatePointer(new Point(25, 15), PointerAction.Press));

            activated.ShouldHaveSingleItem().ShouldBeNull();
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
}
