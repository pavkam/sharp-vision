// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Popups;

/// <summary>Proves PopupDropDownCoordinator guards and idempotence that its owners cannot reach
/// through their public surface, plus the disposed-owner exception contract those owners
/// document.</summary>
public sealed class PopupDropDownCoordinatorInteractionTests
{
    /// <summary>Verifies a disposed owner reports ObjectDisposedException from SetOpen and
    /// AcceptAndClose, taking precedence over the detached-coordinator state disposal leaves behind.</summary>
    [Fact]
    public void SetOpen_WhenOwnerIsDisposed_ThrowsObjectDisposedException()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var coordinator = Create(owner, popup, content);
        coordinator.SetOpen(true);
        coordinator.Detach();
        owner.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => coordinator.SetOpen(false));
        _ = Should.Throw<ObjectDisposedException>(coordinator.AcceptAndClose);
    }

    /// <summary>Verifies the disposed-owner contract through the real owners that route their
    /// drop-down through this coordinator: a disposed DateInput or DateTimeInput rejects opening
    /// its popup with ObjectDisposedException rather than the detached-coordinator state.</summary>
    [Fact]
    public void IsOpen_WhenTemporalOwnerIsDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var date = new DateInput();
        var dateTime = new DateTimeInput();
        date.Dispose();
        dateTime.Dispose();

        // Act / Assert
        _ = Should.Throw<ObjectDisposedException>(() => date.IsOpen = true);
        _ = Should.Throw<ObjectDisposedException>(() => dateTime.IsOpen = true);
        date.IsOpen.ShouldBeFalse();
        dateTime.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies a detached coordinator with a live owner keeps reporting
    /// InvalidOperationException for both entry points, and Detach itself is idempotent.</summary>
    [Fact]
    public void Detach_WhenCalledTwice_IsIdempotentAndKeepsRejectingRequests()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var cancellations = 0;
        var coordinator = Create(owner, popup, content, cancelSession: () => cancellations++);
        coordinator.SetOpen(true);

        coordinator.Detach();
        Should.NotThrow(coordinator.Detach);

        cancellations.ShouldBe(1);
        _ = Should.Throw<InvalidOperationException>(() => coordinator.SetOpen(true));
        _ = Should.Throw<InvalidOperationException>(coordinator.AcceptAndClose);
    }

    /// <summary>Verifies AcceptAndClose with no open session is a silent no-op: no acceptance,
    /// no close publication, and the transition version is untouched.</summary>
    [Fact]
    public void AcceptAndClose_WhenClosed_DoesNothing()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var accepted = 0;
        var closed = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownClosed: () => closed++,
            acceptSession: () => accepted++);
        var version = coordinator.TransitionVersion;

        coordinator.AcceptAndClose();

        accepted.ShouldBe(0);
        closed.ShouldBe(0);
        coordinator.TransitionVersion.ShouldBe(version);
        coordinator.IsOpen.ShouldBeFalse();
    }

    /// <summary>Verifies an accepted session releases its open-state guard: the coordinator can
    /// open a fresh session immediately afterwards with a new generation, and that session
    /// cancels (not accepts) when closed without acceptance.</summary>
    [Fact]
    public void SetOpen_WhenReopenedAfterAcceptedClose_StartsFreshCancellableSession()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var accepted = 0;
        var cancelled = 0;
        var begun = 0;
        var coordinator = Create(
            owner,
            popup,
            content,
            beginSession: () => begun++,
            cancelSession: () => cancelled++,
            acceptSession: () => accepted++);
        coordinator.SetOpen(true);
        var firstGeneration = coordinator.SessionGeneration;
        coordinator.AcceptAndClose();
        accepted.ShouldBe(1);
        cancelled.ShouldBe(0);
        coordinator.IsOpen.ShouldBeFalse();

        coordinator.SetOpen(true);

        coordinator.IsOpen.ShouldBeTrue();
        begun.ShouldBe(2);
        coordinator.SessionGeneration.ShouldBeGreaterThan(firstGeneration);

        coordinator.SetOpen(false);

        accepted.ShouldBe(1);
        cancelled.ShouldBe(1);
    }

    /// <summary>Verifies every SetOpen request, including no-op ones, advances the transition
    /// version so continuations captured before it can detect the newer request.</summary>
    [Fact]
    public void SetOpen_WhenRequested_AlwaysAdvancesTransitionVersion()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        var coordinator = Create(owner, popup, content);
        var version = coordinator.TransitionVersion;

        coordinator.SetOpen(false);
        coordinator.TransitionVersion.ShouldBe(version + 1);

        coordinator.SetOpen(true);
        coordinator.TransitionVersion.ShouldBe(version + 2);

        coordinator.SetOpen(true);
        coordinator.TransitionVersion.ShouldBe(version + 3);
    }

    /// <summary>Verifies a DropDownOpened handler that closes the popup ends closed with exactly
    /// one DropDownClosed and one cancellation, ready for a later open.</summary>
    [Fact]
    public void SetOpen_WhenDropDownOpenedHandlerCloses_EndsClosedWithSingleClosedPublication()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        PopupDropDownCoordinator? coordinator = null;
        var closed = 0;
        var cancelled = 0;
        var closeFromOpened = true;
        coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownOpened: () =>
            {
                if (closeFromOpened)
                {
                    coordinator!.SetOpen(false);
                }
            },
            raiseDropDownClosed: () => closed++,
            cancelSession: () => cancelled++);

        coordinator.SetOpen(true);

        coordinator.IsOpen.ShouldBeFalse();
        popup.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        cancelled.ShouldBe(1);

        closeFromOpened = false;
        coordinator.SetOpen(true);

        coordinator.IsOpen.ShouldBeTrue();
        closed.ShouldBe(1);
    }

    /// <summary>Verifies a Popup.Closing observer that re-requests the close reenters harmlessly:
    /// the session cancels once and DropDownClosed publishes once.</summary>
    [Fact]
    public void SetOpen_WhenClosingObserverClosesAgain_CancelsAndPublishesOnce()
    {
        var owner = new ProbeControl();
        var content = new ProbeControl();
        using var popup = new Popup();
        PopupDropDownCoordinator? coordinator = null;
        var closed = 0;
        var cancelled = 0;
        coordinator = Create(
            owner,
            popup,
            content,
            raiseDropDownClosed: () => closed++,
            cancelSession: () => cancelled++);
        popup.Closing += (_, _) => coordinator.SetOpen(false);
        coordinator.SetOpen(true);

        Should.NotThrow(() => coordinator.SetOpen(false));

        coordinator.IsOpen.ShouldBeFalse();
        closed.ShouldBe(1);
        cancelled.ShouldBe(1);
    }

    private static PopupDropDownCoordinator Create(
        ControlBase owner,
        Popup popup,
        ControlBase content,
        Action? raiseDropDownOpened = null,
        Action? raiseDropDownClosed = null,
        Action? beginSession = null,
        Action? cancelSession = null,
        Action? acceptSession = null) =>
        new(
            owner,
            popup,
            content,
            static () => true,
            static () => { },
            raiseDropDownOpened ?? (static () => { }),
            raiseDropDownClosed ?? (static () => { }),
            beginSession: beginSession,
            cancelSession: cancelSession,
            acceptSession: acceptSession);
}
