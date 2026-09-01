// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies shared modal-session identity, callbacks, rollback, and replacement.</summary>
public sealed class ModalSessionTests
{
    /// <summary>Verifies a dismissal policy failure propagates without losing the active session
    /// identity or making later cleanup impossible.</summary>
    [Fact]
    public async Task Dismiss_WhenPolicyThrows_PreservesActiveSessionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeControl { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var expected = new InvalidOperationException("dismiss");
            var session = new ModalSession(dismissRequested: _ => throw expected);
            var scope = session.Enter(
                () => modality.Enter(plane, OutsideInteraction.Dismiss),
                static () => true);

            var exception = Should.Throw<InvalidOperationException>(scope.PublishDismissRequested);

            exception.ShouldBeSameAs(expected);
            session.Current.ShouldBeSameAs(scope);
            session.IsActive.ShouldBeTrue();
            session.Exit();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies entry rejects synchronous reentry and retains the successfully returned
    /// active scope as the only current identity.</summary>
    [Fact]
    public async Task Enter_WhenEntryDelegateReenters_RejectsNestedEntryAndTracksOuterAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeControl { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();
            InvalidOperationException? nested = null;

            var scope = session.Enter(
                () =>
                {
                    nested = Should.Throw<InvalidOperationException>(() =>
                        session.Enter(
                            () => modality.Enter(plane),
                            static () => true));
                    return modality.Enter(plane);
                },
                static () => true);

            _ = nested.ShouldNotBeNull();
            session.Current.ShouldBeSameAs(scope);
            session.IsActive.ShouldBeTrue();
            session.Exit();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies clearing identity before external-exit policy lets that callback install a
    /// replacement session that stale cleanup cannot erase.</summary>
    [Fact]
    public async Task Exit_WhenExternalCallbackReenters_PreservesReplacementIdentityAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var firstPlane = new ProbeControl { IsFocusable = true };
            var secondPlane = new ProbeControl { IsFocusable = true };
            root.Children.Add(firstPlane);
            root.Children.Add(secondPlane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            ModalScope? replacement = null;
            ModalSession? session = null;
            session = new ModalSession(
                exited: _ =>
                {
                    var current = session ?? throw new InvalidOperationException("The session must be initialized.");
                    replacement = current.Enter(
                        () => modality.Enter(secondPlane),
                        static () => true);
                });
            var first = session.Enter(
                () => modality.Enter(firstPlane),
                static () => true);

            first.Dispose();

            _ = replacement.ShouldNotBeNull();
            session.Current.ShouldBeSameAs(replacement);
            session.IsActive.ShouldBeTrue();
            session.Exit();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the silent "entry did not take" path — an active scope that fails the
    /// caller's currentness check — disposes the candidate scope and invokes the caller's rollback,
    /// the same as the throwing path does.</summary>
    [Fact]
    public async Task Enter_WhenNotCurrent_DisposesScopeAndInvokesRollbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeControl { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();
            var rolledBack = false;

            var scope = session.Enter(
                () => modality.Enter(plane),
                static () => false,
                () => rolledBack = true);

            scope.IsActive.ShouldBeFalse();
            rolledBack.ShouldBeTrue();
            session.Current.ShouldBeNull();
            session.IsActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the silent "entry did not take" path does not invoke rollback when the
    /// scope itself came back inactive, preserving an ancestor-recovery caller's own decision.</summary>
    [Fact]
    public async Task Enter_WhenScopeInactive_DoesNotInvokeRollbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var plane = new ProbeControl { IsFocusable = true };
            root.Children.Add(plane);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var session = new ModalSession();
            var rolledBack = false;

            var scope = session.Enter(
                () =>
                {
                    var candidate = modality.Enter(plane);
                    candidate.Dispose();
                    return candidate;
                },
                static () => true,
                () => rolledBack = true);

            scope.IsActive.ShouldBeFalse();
            rolledBack.ShouldBeFalse();
            session.Current.ShouldBeNull();
            session.IsActive.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a failed entry remains authoritative when rollback also fails.</summary>
    [Fact]
    public void Enter_WhenEntryAndRollbackFail_RethrowsEntryFailure()
    {
        var expected = new InvalidOperationException("entry");
        var session = new ModalSession();

        var exception = Should.Throw<InvalidOperationException>(() => session.Enter(
            () => throw expected,
            static () => true,
            () => throw new InvalidOperationException("rollback")));

        exception.ShouldBeSameAs(expected);
        session.Current.ShouldBeNull();
    }
}
