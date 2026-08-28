// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies deterministic control-owned interaction lifecycle participant fan-out.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies one participant failure cannot skip a later focus cancellation.</summary>
    [Fact]
    public void LifecycleParticipant_WhenEarlierFocusCallbackThrows_NotifiesLaterParticipantInOrder()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        owner.Register(new LifecycleParticipantProbe("first", events) { ThrowOnFocus = true });
        owner.Register(new LifecycleParticipantProbe("second", events));

        var exception = Should.Throw<InvalidOperationException>(() => owner.CommitFocus(true));

        exception.Message.ShouldBe("first focus failed.");
        events.ShouldBe(["first:focus:True", "second:focus:True"]);
    }

    /// <summary>Verifies duplicate identity registration is rejected.</summary>
    [Fact]
    public void RegisterLifecycleParticipant_WhenIdentityRepeats_ThrowsInvalidOperationException()
    {
        var owner = new LifecycleParticipantOwner();
        var participant = new LifecycleParticipantProbe("participant", []);
        owner.Register(participant);

        _ = Should.Throw<InvalidOperationException>(() => owner.Register(participant));
    }

    /// <summary>Verifies capture loss and disposal preserve registration order and precise reasons.</summary>
    [Fact]
    public void LifecycleParticipant_WhenCaptureEndsThenOwnerDisposes_ReceivesBothTransitionsInOrder()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        owner.Register(new LifecycleParticipantProbe("first", events));
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.LoseCapture(PointerCaptureLossReason.Transferred);
        owner.Dispose();

        events.ShouldBe([
            "first:capture:Transferred",
            "second:capture:Transferred",
            "first:unavailable:Disposed",
            "second:unavailable:Disposed",
        ]);
        _ = Should.Throw<ObjectDisposedException>(() =>
            owner.Register(new LifecycleParticipantProbe("late", events)));
    }

    /// <summary>Verifies participant-triggered disposal completes disposal cancellation but does
    /// not resume the superseded focus notification against later participants.</summary>
    [Fact]
    public void LifecycleParticipant_WhenFocusCallbackDisposesOwner_DoesNotResumeStaleFocusFanOut()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        var first = new LifecycleParticipantProbe("first", events) { FocusAction = owner.Dispose };
        owner.Register(first);
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.CommitFocus(true);

        owner.IsDisposed.ShouldBeTrue();
        events.ShouldBe([
            "first:focus:True",
            "first:unavailable:Disposed",
            "second:unavailable:Disposed",
        ]);
    }

    /// <summary>Verifies a nested unavailability publication takes its own stable snapshot without
    /// corrupting the outer registration-order walk.</summary>
    [Fact]
    public void LifecycleParticipant_WhenUnavailableReenters_PreservesBothOrderedSnapshots()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        var first = new LifecycleParticipantProbe("first", events);
        first.UnavailableAction = () =>
        {
            first.UnavailableAction = null;
            owner.BecomeUnavailable(ReleaseReason.Disabled);
        };
        owner.Register(first);
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.BecomeUnavailable(ReleaseReason.Hidden);

        events.ShouldBe([
            "first:unavailable:Hidden",
            "first:unavailable:Disabled",
            "second:unavailable:Disabled",
            "second:unavailable:Hidden",
        ]);
    }
}
