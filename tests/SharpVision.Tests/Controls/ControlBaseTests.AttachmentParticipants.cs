// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies deterministic owner-bound attachment participant lifetime.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies same- and cross-dispatcher reattachment and final disposal.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenOwnerReattaches_FollowsEveryCommittedLifetimeAsync()
    {
        await using var first = Dispatcher.Start();
        await using var second = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var participant = new AttachmentParticipantProbe();
        owner.Register(participant);

        await first.InvokeAsync(() =>
        {
            owner.Attach(first);
            owner.Detach();
            owner.Attach(first);
            owner.Detach();
        }, TestContext.Current.CancellationToken);
        await second.InvokeAsync(() =>
        {
            owner.Attach(second);
            owner.Dispose();
        }, TestContext.Current.CancellationToken);

        participant.Attachments.ShouldBe([first, first, second]);
        participant.DetachCalls.ShouldBe(2);
        participant.DisposeCalls.ShouldBe(1);
    }

    /// <summary>Verifies one participant failure does not skip later registration-order callbacks.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenEarlierAttachThrows_StillNotifiesLaterParticipantAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var events = new List<string>();
        var failing = new AttachmentParticipantProbe("failing", events) { ThrowOnAttach = true };
        var later = new AttachmentParticipantProbe("later", events);
        owner.Register(failing);
        owner.Register(later);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(
                () => owner.Attach(dispatcher),
                TestContext.Current.CancellationToken));

        later.Attachments.ShouldBe([dispatcher]);
        events.ShouldBe(["failing:attach", "later:attach"]);
        await dispatcher.InvokeAsync(owner.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies final disposal is exact-once, ordered, and exhaustive after a failure.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenDetachedOwnerDisposes_CleansEveryParticipantOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var events = new List<string>();
        var failing = new AttachmentParticipantProbe("failing", events) { ThrowOnDispose = true };
        var later = new AttachmentParticipantProbe("later", events);
        owner.Register(failing);
        owner.Register(later);
        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            owner.Detach();
        }, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(owner.Dispose, TestContext.Current.CancellationToken));
        owner.Dispose();

        failing.DisposeCalls.ShouldBe(1);
        later.DisposeCalls.ShouldBe(1);
        events.ShouldBe([
            "failing:attach",
            "later:attach",
            "failing:detach",
            "later:detach",
            "failing:dispose",
            "later:dispose",
        ]);
    }

    /// <summary>Verifies duplicate identity registration is rejected before attachment.</summary>
    [Fact]
    public void RegisterAttachmentParticipant_WhenIdentityRepeats_Throws()
    {
        var owner = new AttachmentParticipantOwner();
        var participant = new AttachmentParticipantProbe();
        owner.Register(participant);

        _ = Should.Throw<ArgumentException>(() => owner.Register(participant));
    }
}
