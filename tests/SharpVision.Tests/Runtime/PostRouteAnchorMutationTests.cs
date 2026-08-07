// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies the post-route Tab traversal command survives every way its captured anchor
/// can go stale during the route that captured it.
///
/// <para><c>Router</c> never truncates a walk, so a <c>handledEventsToo</c> ancestor handler runs
/// after the control that requested the command and can legally mutate the tree beneath it.
/// <c>Application.Dispatch</c> then executes the traversal against an anchor captured before that
/// handler ran. An earlier fix covered exactly one mutation - detachment - by gating the anchor on
/// <c>FocusManager.IsMember</c>. That gate is a parent-edge walk from the focus root, so it says
/// nothing about disposal, visibility, reparenting, or modal-plane membership; those went
/// uncovered. The contract that fix established is the one asserted for all of them here: a stale
/// anchor must degrade to the manager's own focused-control overload, never fault the
/// application.</para>
///
/// <para>Deleting that <c>IsMember</c> gate fails <see cref="Mutation.Detached"/> and
/// <see cref="Mutation.Disposed"/> and no others, which is worth knowing about this table: only
/// those two leave the anchor unreachable from the focus root, and only they exercise the gate. The
/// remaining four keep the anchor a member and are safe through traversal itself - they are pinned
/// because nothing else states that, not because they reproduce a fault today.</para>
/// </summary>
public sealed class PostRouteAnchorMutationTests
{
    /// <summary>One way a handledEventsToo handler can invalidate the captured anchor mid-route.</summary>
    public enum Mutation
    {
        /// <summary>The originally-fixed case, kept here so the whole table lives in one place.</summary>
        Detached,

        /// <summary>Disposed outright, which also unparents but runs far more teardown first.</summary>
        Disposed,

        /// <summary>Still parented and still a member, but no longer a focus candidate.</summary>
        Collapsed,

        /// <summary>Still a member, at a different place in the traversal order.</summary>
        Reparented,

        /// <summary>Still a member by parent edges, but outside the plane traversal now scopes to -
        /// the case IsMember cannot see, because it never consults modality.</summary>
        ModalPlaneEntered,

        /// <summary>Was inside a plane when captured; the plane is gone by the time it runs.</summary>
        ModalPlaneExited
    }

    /// <summary>Verifies one Tab keystroke through a mounted application never faults, whichever way
    /// a handledEventsToo handler invalidates the captured anchor mid-route.</summary>
    [Theory]
    [InlineData(Mutation.Detached)]
    [InlineData(Mutation.Disposed)]
    [InlineData(Mutation.Collapsed)]
    [InlineData(Mutation.Reparented)]
    [InlineData(Mutation.ModalPlaneEntered)]
    [InlineData(Mutation.ModalPlaneExited)]
    public async Task Input_WhenHandlerInvalidatesTheAnchorDuringTabRoute_DoesNotFaultAsync(Mutation mutation)
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(30, 8)));

        var first = new Button { Text = "First" };
        var second = new Button { Text = "Second" };
        var elsewhere = new Button { Text = "Elsewhere" };
        var sideBay = new Stack { Children = { elsewhere } };
        var mainBay = new Stack { Children = { first, second } };
        var root = new Stack { Children = { mainBay, sideBay } };

        ModalScope? scope = null;
        var mutated = false;
        await using Application application = new(root, terminal, terminal, TerminalOptions.Minimal);

        _ = root.AddHandler(
            Events.Key,
            (_, eventArgs) =>
            {
                // Once only. The second Tab below exists to prove the application still works,
                // not to mutate again - re-running these would fail on their own preconditions
                // (adding an already-parented control, disposing twice) and mask the real result.
                if (eventArgs.Phase != RoutingPhase.Bubble ||
                    eventArgs.Stroke.Code != Code.Tab ||
                    !eventArgs.Handled ||
                    mutated)
                {
                    return;
                }

                mutated = true;

                switch (mutation)
                {
                    case Mutation.Detached:
                        _ = mainBay.Children.Remove(first);
                        break;
                    case Mutation.Disposed:
                        first.Dispose();
                        break;
                    case Mutation.Collapsed:
                        first.Visibility = Visibility.Collapsed;
                        break;
                    case Mutation.Reparented:
                        _ = mainBay.Children.Remove(first);
                        sideBay.Children.Add(first);
                        break;
                    case Mutation.ModalPlaneEntered:
                        // Excludes the anchor: traversal now scopes to sideBay, which first is not
                        // in, while IsMember still reports it as a member of the focus root.
                        scope = application.Modality.Enter(sideBay);
                        break;
                    case Mutation.ModalPlaneExited:
                        scope?.Dispose();
                        scope = null;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
                }
            },
            handledEventsToo: true);

        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                if (mutation == Mutation.ModalPlaneExited)
                {
                    // Captured while a plane was active, so the anchor's own scope is what the
                    // handler tears down mid-route.
                    scope = application.Modality.Enter(mainBay);
                }

                application.Focus.Focus(first).ShouldBeTrue();
            },
            TestContext.Current.CancellationToken);

        var stroke = new Stroke(Code.Tab, character: null, nativeCode: 0, Modifiers.None, KeyAction.Press);

        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);

        application.Failure.ShouldBeNull($"a {mutation} anchor must not fault the application");

        // Still usable: a second Tab must also be delivered and survive, which a latched fault or a
        // corrupted focus ring would not allow.
        application.Input(in stroke);
        await application.Dispatcher.InvokeAsync(static () => { }, TestContext.Current.CancellationToken);
        application.Failure.ShouldBeNull($"the application must stay usable after a {mutation} anchor");

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                scope?.Dispose();
                scope = null;
            },
            TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
