// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies exact control attachment identities and guarded dispatcher marshalling.</summary>
public sealed class ControlAttachmentTokenTests
{
    /// <summary>Verifies away-and-back attachment to the same dispatcher invalidates a token.</summary>
    [Fact]
    public async Task IsCurrent_WhenDetachedAndReattachedToSameDispatcher_ReturnsFalseAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var result = await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            var token = control.CaptureAttachment();
            control.Detach();
            control.Attach(dispatcher);
            return control.IsCurrent(token);
        }, TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    /// <summary>Verifies reattachment to another dispatcher invalidates the prior identity.</summary>
    [Fact]
    public async Task IsCurrent_WhenReattachedToAnotherDispatcher_ReturnsFalseAsync()
    {
        await using var firstDispatcher = Dispatcher.Start();
        await using var secondDispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var token = await firstDispatcher.InvokeAsync(() =>
        {
            control.Attach(firstDispatcher);
            var captured = control.CaptureAttachment();
            control.Detach();
            return captured;
        }, TestContext.Current.CancellationToken);

        await secondDispatcher.InvokeAsync(
            () => control.Attach(secondDispatcher),
            TestContext.Current.CancellationToken);

        control.IsCurrent(token).ShouldBeFalse();
    }

    /// <summary>Verifies a queued callback discarded after same-dispatcher reattachment never runs.</summary>
    [Fact]
    public async Task PostForCurrentAttachment_WhenAttachmentChangesBeforeDispatch_DropsCallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var token = await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            return control.CaptureAttachment();
        }, TestContext.Current.CancellationToken);
        var calls = 0;

        dispatcher.Post(() =>
        {
            control.Detach();
            control.Attach(dispatcher);
        });
        control.PostForCurrentAttachment(token, () => calls++);
        await dispatcher.InvokeAsync(() => { }, TestContext.Current.CancellationToken);

        calls.ShouldBe(0);
    }

    /// <summary>Verifies current guarded work runs once on its exact dispatcher.</summary>
    [Fact]
    public async Task PostForCurrentAttachment_WhenCurrent_RunsOnceOnDispatcherAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var token = await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            return control.CaptureAttachment();
        }, TestContext.Current.CancellationToken);
        var calls = 0;

        control.PostForCurrentAttachment(token, () =>
        {
            dispatcher.CheckAccess().ShouldBeTrue();
            calls++;
        });
        await dispatcher.InvokeAsync(() => { }, TestContext.Current.CancellationToken);

        calls.ShouldBe(1);
    }

    /// <summary>Verifies an additional domain predicate can reject otherwise current work.</summary>
    [Fact]
    public async Task InvokeForCurrentAttachmentAsync_WhenOperationIsStale_DropsCallbackAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var token = await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            return control.CaptureAttachment();
        }, TestContext.Current.CancellationToken);
        var calls = 0;

        await control.InvokeForCurrentAttachmentAsync(token, () => calls++, () => false);

        calls.ShouldBe(0);
    }

    /// <summary>Verifies caller cleanup runs when a disposed queue rejects the post synchronously.</summary>
    [Fact]
    public async Task PostForCurrentAttachment_WhenQueueIsDisposedAndCleanupSelected_RunsCleanupAsync()
    {
        var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        var token = await dispatcher.InvokeAsync(() =>
        {
            control.Attach(dispatcher);
            return control.CaptureAttachment();
        }, TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();
        var cleanups = 0;

        control.PostForCurrentAttachment(
            token,
            () => throw new InvalidOperationException("Rejected work must not run."),
            onDiscarded: () => cleanups++,
            rejectionPolicy: ControlAttachmentQueueRejectionPolicy.RunCleanup);

        cleanups.ShouldBe(1);
    }
}
