// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

using BindingFlags = System.Reflection.BindingFlags;

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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 5), IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            capture.Capture(child).ShouldBeTrue();
            child.IsFocusable = false;

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

    /// <summary>Verifies the oldest surviving raw press remains the public origin while two
    /// different buttons are released in either order.</summary>
    [Theory]
    [InlineData(Buttons.Primary, Buttons.Secondary)]
    [InlineData(Buttons.Secondary, Buttons.Primary)]
    public async Task Dispatch_WhenButtonPressesOverlap_PreservesOldestSurvivingOriginAsync(
        Buttons firstButton,
        Buttons secondButton)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 8, 8) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), firstButton, PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(12, 2), secondButton, PointerAction.Press));

            manager.PressOrigin.ShouldBeSameAs(first);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), firstButton, PointerAction.Release));

            manager.PressOrigin.ShouldBeSameAs(second);

            _ = manager.Dispatch(CreatePointer(new Point(12, 2), secondButton, PointerAction.Release));

            manager.PressOrigin.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies releasing a newer auxiliary press leaves the older primary origin live.</summary>
    [Fact]
    public async Task Dispatch_WhenNewerAuxiliaryPressReleases_PreservesPrimaryOriginAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 8, 8) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), Buttons.Primary, PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(12, 2), Buttons.Middle, PointerAction.Press));

            _ = manager.Dispatch(CreatePointer(new Point(12, 2), Buttons.Middle, PointerAction.Release));

            manager.PressOrigin.ShouldBeSameAs(first);
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
                if (eventArgs.Phase == RoutingPhase.Bubble)
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
            var child = new ProbeControl { Bounds = new Rect(4, 3, 8, 4), IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);
            List<(ControlBase Sender, Point? Local)> observed = [];
            _ = root.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    observed.Add(((ControlBase) sender!, eventArgs.LocalCells));
                }
            });
            _ = child.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    observed.Add(((ControlBase) sender!, eventArgs.LocalCells));
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
            var ancestor = new ProbeContainer { Bounds = new Rect(0, 0, 12, 8), IsFocusable = true };
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
            var middle = new TraversalOwner { Bounds = new Rect(0, 0, 12, 8), IsFocusable = true };
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
            var child = new ProbeControl { Bounds = new Rect(4, 3, 8, 4), IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            capture.Dispatch(CreatePointer(new Point(6, 5), PointerAction.Press))
                .ShouldBeSameAs(child);

            focus.Focused.ShouldBeSameAs(child);
            child.IsFocused.ShouldBeTrue();
            child.Face.Background.ShouldBe(SemanticColor.Control);
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
            List<ControlBase> routed = [];
            _ = first.AddHandler(Events.Pointer, (sender, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routed.Add((ControlBase) sender!);
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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), IsFocusable = true };
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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), IsFocusable = true };
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

    /// <summary>Verifies a throwing capture hook cannot suppress the public loss event on explicit release.</summary>
    [Fact]
    public async Task Release_WhenCapturedHookThrows_PublishesLossDespiteFailureAsync()
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
            manager.Capture(child).ShouldBeTrue();
            var cancellations = 0;
            child.LostPointerCapture += (_, _) => cancellations++;

            _ = Should.Throw<InvalidOperationException>(manager.Release);

            manager.Captured.ShouldBeNull();
            cancellations.ShouldBe(1);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing capture hook cannot suppress the public loss event on the protected release seam.</summary>
    [Fact]
    public async Task ReleasePointerCapture_WhenOwningControlHookThrows_PublishesLossDespiteFailureAsync()
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

            _ = Should.Throw<InvalidOperationException>(child.ReleaseProbePointer);

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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), IsFocusable = true };
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

    /// <summary>Verifies a throwing hover callback during disposal still fully severs manager
    /// ownership, so the root is left free for a replacement manager afterward.</summary>
    [Fact]
    public async Task Dispose_WhenHoverCallbackThrows_StillSeversRootOwnershipAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 10),
                ThrowOnPointerOverChanged = true
            };
            root.Children.Add(child);
            root.Attach(dispatcher);
            var manager = new PointerManager(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            manager.Hovered.ShouldBeSameAs(child);

            _ = Should.Throw<InvalidOperationException>(manager.Dispose);

            root.CaptureOwner.ShouldBeNull();
            using var replacement = new PointerManager(root);
            replacement.Hovered.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies terminal-focus loss also forgets the last known pointer cell, so a later
    /// hover reconciliation - e.g. from a layout pass - does not resurrect the hover it just
    /// cancelled.</summary>
    [Fact]
    public async Task TerminalFocusLost_WhenHoverIsReconciledAfterward_DoesNotResurrectHoverAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            manager.Hovered.ShouldBeSameAs(child);

            manager.TerminalFocusLost();
            manager.Hovered.ShouldBeNull();

            manager.ReconcileHover();

            manager.Hovered.ShouldBeNull();
            child.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disabling a hovered leaf clears only its own hover instead of collapsing
    /// the whole path, restoring hover on its still-eligible ancestors.</summary>
    [Fact]
    public async Task Unavailable_WhenHoveredControlDisables_PreservesAncestorHoverAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var panel = new ProbeContainer { Bounds = new Rect(0, 0, 12, 8) };
            var button = new ProbeControl { Bounds = new Rect(2, 2, 6, 4) };
            panel.Children.Add(button);
            root.Children.Add(panel);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(4, 3), PointerAction.Move));

            root.IsPointerOver.ShouldBeTrue();
            panel.IsPointerOver.ShouldBeTrue();
            button.IsPointerOver.ShouldBeTrue();

            button.IsEnabled = false;

            button.IsPointerOver.ShouldBeFalse();
            panel.IsPointerOver.ShouldBeTrue();
            root.IsPointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies hiding a hovered leaf clears only its own hover instead of collapsing the
    /// whole path, restoring hover on its still-eligible ancestors.</summary>
    [Fact]
    public async Task Unavailable_WhenHoveredControlHides_PreservesAncestorHoverAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var panel = new ProbeContainer { Bounds = new Rect(0, 0, 12, 8) };
            var button = new ProbeControl { Bounds = new Rect(2, 2, 6, 4) };
            panel.Children.Add(button);
            root.Children.Add(panel);
            root.Attach(dispatcher);
            using PointerManager manager = new(root);

            _ = manager.Dispatch(CreatePointer(new Point(4, 3), PointerAction.Move));

            root.IsPointerOver.ShouldBeTrue();
            panel.IsPointerOver.ShouldBeTrue();
            button.IsPointerOver.ShouldBeTrue();

            button.Visibility = Visibility.Hidden;

            button.IsPointerOver.ShouldBeFalse();
            panel.IsPointerOver.ShouldBeTrue();
            root.IsPointerOver.ShouldBeTrue();
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
            var child = new ProbeControl { Bounds = new Rect(0, 0, 10, 10), IsFocusable = true };
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
            var first = new ProbeControl { Bounds = new Rect(0, 0, 8, 8), IsFocusable = true };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 8, 8), IsFocusable = true };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            var clock = new ManualTimeProvider();
            using PointerManager manager = new(root, clock);
            List<int> observed = [];
            _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
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

    /// <summary>Verifies a completed click's target is released once it leaves the tree.
    /// PressOrigin is already null once a click completes, so Cancel's clearPressed branch never
    /// runs for it, and only a later press elsewhere or manager disposal previously cleared the
    /// reference - leaving a detached or disposed clicked control referenced by
    /// _lastClickTarget until then. Reads the private field directly rather than
    /// asserting via GC/WeakReference, since an unrelated pre-existing retention elsewhere in
    /// PointerManager (a reused hover-path scratch buffer that is not cleared between calls)
    /// would otherwise keep the control reachable regardless of this fix and produce a false
    /// negative.</summary>
    [Fact]
    public async Task Dispatch_WhenClickedControlIsRemoved_ClearsLastClickTargetAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            var field = typeof(PointerManager).GetField(
                "_lastClickTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Press));
            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Release));

            field.GetValue(manager).ShouldBeSameAs(child);

            _ = root.Children.Remove(child);

            field.GetValue(manager).ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the reused hover-path exit scratch buffer does not retain a control past
    /// the reconciliation that processed it - it was cleared only at the next call's own
    /// top-of-method reset, so a control the hover moved away from stayed referenced until
    /// another pointer move happened anywhere in the tree, indefinitely if none ever did.</summary>
    [Fact]
    public async Task Dispatch_WhenHoverMovesAway_ClearsExitScratchBufferAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var manager = new PointerManager(root);
            var field = typeof(PointerManager).GetField(
                "_exitBuffer", BindingFlags.NonPublic | BindingFlags.Instance)!;

            _ = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));
            manager.Hovered.ShouldBeSameAs(child);

            _ = manager.Dispatch(new Pointer(
                cells: null,
                pixels: new Point(50, 50),
                Buttons.None,
                PointerAction.Move,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: true,
                isCellPositionInferred: false));

            manager.Hovered.ShouldBeNull();
            ((List<ControlBase>) field.GetValue(manager)!).ShouldBeEmpty();
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
            List<ControlBase?> activated = [];
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
                if (eventArgs.Phase == RoutingPhase.Bubble)
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
            List<ControlBase?> activated = [];
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

    private static Pointer CreatePointer(Point cells, Buttons buttons, PointerAction action) => new(
        cells,
        pixels: null,
        buttons,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    /// <summary>Verifies a wheel scroll that moves content under a pointer left stationary at the
    /// wheeled cell repaints hover onto the control now at that cell, not the one that used to be
    /// there.</summary>
    [Fact]
    public async Task PointerOverFace_WhenWheelScrollMovesContentUnderTheCursor_RepaintsTheNewlyHoveredControlAsync()
    {
        // Arrange
        var stack = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var buttons = new Button[4];

        for (var index = 0; index < buttons.Length; index++)
        {
            buttons[index] = new Button(index.ToString(CultureInfo.InvariantCulture))
            {
                Height = Length.Cells(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            stack.Children.Add(buttons[index]);
        }

        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(stack, new Point(0, 0));
        surface.ShouldHaveState(buttons[0], VisualState.IsPointerOver);

        // Act - wheel down at the same cell the pointer already occupies
        await surface.Pointer.WheelAsync(stack, new Point(0, 0), wheelY: -1);

        // Assert - the button now under the stationary cell is hovered, not the stale one
        stack.VerticalOffset.ShouldBe(1);
        surface.ShouldHaveState(buttons[0], VisualState.Normal);
        surface.ShouldHaveState(buttons[1], VisualState.IsPointerOver);
    }

    /// <summary>Verifies a terminal resize that removes the row at the pointer's cell clears hover
    /// instead of leaving it pointing at a control no longer under the cursor.</summary>
    [Fact]
    public async Task Hovered_WhenTerminalResizesUnderThePointer_ClearsWhenNoControlRemainsAtTheCellAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(4, 2), new Size(40, 20)));
        var spacer = new ProbeControl { Height = Length.Cells(1) };
        var button = new Button("Go") { Height = Length.Cells(1), Width = Length.Cells(4) };
        var root = new Stack
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { spacer, button }
        };
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput(Encoding.ASCII.GetBytes("[<35;1;2M"));
        await WaitUntilAsync(
            () => ReferenceEquals(application.Capture.Hovered, button),
            application,
            "initial hover",
            TestContext.Current.CancellationToken);

        // Act - shrink the terminal so the row the pointer sits on no longer exists
        terminal.QueueResize(new Dimensions(new Size(4, 1), new Size(40, 10)));
        await WaitUntilAsync(
            () => application.Capture.Hovered is null,
            application,
            "hover clears after resize",
            TestContext.Current.CancellationToken);

        // Assert
        await application.Dispatcher.InvokeAsync(
            () => application.Capture.Hovered.ShouldBeNull(),
            TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);
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

    /// <summary>Verifies the direct target and its ancestors receive distinct pointer state.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerHitsChild_SetsDirectAndAncestorPointerStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(2, 2, 8, 4) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var capture = new PointerManager(root);

            _ = capture.Dispatch(new Pointer(new Point(3, 3), null, Buttons.None, PointerAction.Move, 0, 0,
                Modifiers.None, true, false));

            root.IsPointerOver.ShouldBeTrue();
            root.IsPointerDirectlyOver.ShouldBeFalse();
            child.IsPointerOver.ShouldBeTrue();
            child.IsPointerDirectlyOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capture transfer clears the former owner before publishing its direct loss event.</summary>
    [Fact]
    public async Task Capture_WhenTransferred_PublishesFormerOwnerLossAfterStateClearsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 5, 5) };
            var second = new ProbeControl { Bounds = new Rect(6, 0, 5, 5) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var pointer = new PointerManager(root);
            PointerCaptureLossReason? reason = null;
            var ownerWasClear = false;
            first.LostPointerCapture += (_, eventArgs) =>
            {
                reason = eventArgs.Reason;
                ownerWasClear = !first.HasPointerCapture && !second.HasPointerCapture;
            };

            first.CaptureProbePointer().ShouldBeTrue();
            second.CaptureProbePointer().ShouldBeTrue();

            reason.ShouldBe(PointerCaptureLossReason.Transferred);
            ownerWasClear.ShouldBeTrue();
            second.HasPointerCapture.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal from the former owner's capture-loss callback remains terminal
    /// and cannot let the outer transfer install a contradictory ghost capture.</summary>
    [Fact]
    public async Task Capture_WhenFormerOwnerDisposesManager_DoesNotResurrectCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            var second = new ProbeControl { Bounds = new Rect(10, 0, 8, 8) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            var manager = new PointerManager(root);
            manager.Capture(first).ShouldBeTrue();
            first.LostPointerCapture += (_, _) => manager.Dispose();

            // Act
            var captured = manager.Capture(second);

            // Assert
            captured.ShouldBeFalse();
            manager.Captured.ShouldBeNull();
            root.CaptureOwner.ShouldBeNull();
            second.ProbeHasPointerCapture.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal from hover-entry publication aborts the in-flight dispatch before
    /// routed pointer delivery and does not report the now-ownerless target.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerEntryDisposesManager_DoesNotRoutePointerAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 8, 8) };
            root.Children.Add(child);
            root.Attach(dispatcher);
            var manager = new PointerManager(root);
            var routed = 0;
            child.PointerEntered += (_, _) => manager.Dispose();
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routed++;
                }
            });

            // Act
            var target = manager.Dispatch(CreatePointer(new Point(2, 2), PointerAction.Move));

            // Assert
            target.ShouldBeNull();
            routed.ShouldBe(0);
            root.CaptureOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal from the application activation callback stops a primary press
    /// before focus and routed pointer delivery continue against the released manager.</summary>
    [Fact]
    public async Task Dispatch_WhenActivationCallbackDisposesManager_DoesNotFocusOrRouteAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 8, 8), IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            PointerManager manager = null!;
            manager = new PointerManager(
                root,
                timeProvider: null,
                activationTarget: target =>
                {
                    manager.Dispose();
                    return target;
                });
            var routed = 0;
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routed++;
                }
            });

            // Act
            var target = manager.Dispatch(
                CreatePointer(new Point(2, 2), Buttons.Primary, PointerAction.Press));

            // Assert
            target.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            routed.ShouldBe(0);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposal from pointer-driven focus publication stops the primary press
    /// before routed pointer delivery continues against the released manager.</summary>
    [Fact]
    public async Task Dispatch_WhenFocusChangingDisposesManager_DoesNotRouteAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            // Arrange
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var child = new ProbeControl { Bounds = new Rect(0, 0, 8, 8), IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            var manager = new PointerManager(root);
            var routed = 0;
            focus.Changing += (_, _) => manager.Dispose();
            _ = child.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble)
                {
                    routed++;
                }
            });

            // Act
            var target = manager.Dispatch(
                CreatePointer(new Point(2, 2), Buttons.Primary, PointerAction.Press));

            // Assert
            target.ShouldBeNull();
            routed.ShouldBe(0);
            root.CaptureOwner.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies sibling movement does not publish a spurious exit and re-entry on their shared ancestor.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerMovesBetweenSiblings_PreservesSharedAncestorPointerStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 10) };
            var first = new ProbeControl { Bounds = new Rect(0, 0, 5, 5) };
            var second = new ProbeControl { Bounds = new Rect(6, 0, 5, 5) };
            root.Children.Add(first);
            root.Children.Add(second);
            root.Attach(dispatcher);
            using var pointer = new PointerManager(root);
            var entered = 0;
            var exited = 0;
            root.PointerEntered += (_, _) => entered++;
            root.PointerExited += (_, _) => exited++;

            _ = pointer.Dispatch(new Pointer(new Point(1, 1), null, Buttons.None, PointerAction.Move, 0, 0,
                Modifiers.None, true, false));
            _ = pointer.Dispatch(new Pointer(new Point(7, 1), null, Buttons.None, PointerAction.Move, 0, 0,
                Modifiers.None, true, false));

            entered.ShouldBe(1);
            exited.ShouldBe(0);
            root.IsPointerOver.ShouldBeTrue();
            first.IsPointerOver.ShouldBeFalse();
            second.IsPointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }
}
