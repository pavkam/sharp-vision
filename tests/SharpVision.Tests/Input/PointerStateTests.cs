// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies physical pointer-path state.</summary>
public sealed class PointerStateTests
{
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

            root.PointerOver.ShouldBeTrue();
            root.PointerDirectlyOver.ShouldBeFalse();
            child.PointerOver.ShouldBeTrue();
            child.PointerDirectlyOver.ShouldBeTrue();
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
            root.PointerOver.ShouldBeTrue();
            first.PointerOver.ShouldBeFalse();
            second.PointerOver.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }
}
