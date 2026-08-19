// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

/// <summary>
/// Verifies <c>Binding.OnSourceInvalidated</c>'s own post-back-to-dispatcher path - reached from an
/// arbitrary background thread whenever the bound source raises <c>PropertyChanged</c> off the
/// target's owning dispatcher - bridges a saturated (but otherwise healthy) dispatcher queue
/// (<see cref="InvalidOperationException"/>) into the dispatcher's own callback-failure path
/// (<see cref="Dispatcher.UnhandledException"/>) instead of silently and permanently discarding
/// the pending synchronization, exactly like the sibling fire-and-forget completion posts in
/// <c>TreeViewItem.RunLoadAsync</c>, <c>FileDialogBase.ObserveLoadAsync</c>, and
/// <c>Application.ObserveRenderAsync</c>/<c>ObserveOutOfBandAsync</c> already bridge for this exact
/// shape. A momentarily full queue is not the same as a stopping dispatcher - the queue drains and
/// the dispatcher keeps running - so a background-thread model mutation racing it must not vanish
/// with no signal anywhere once the target's own binding path never even attempts a later retry.
/// </summary>
public sealed partial class BindingTests
{
    /// <summary>Blocks the dispatcher thread inside one posted callback (which becomes the
    /// currently-running work item, so it no longer counts against the bounded queue itself), then
    /// queues one more filler behind it, saturating a capacity-1 dispatcher for as long as the
    /// returned handle is held.</summary>
    /// <param name="dispatcher">The capacity-1 dispatcher to saturate.</param>
    /// <param name="cancellationToken">Cancels waiting for the hostage to start running.</param>
    /// <param name="filler">The queued filler; defaults to a no-op when null.</param>
    private static async Task<ManualResetEventSlim> SaturateSingleSlotQueueAsync(
        Dispatcher dispatcher,
        CancellationToken cancellationToken,
        Action? filler = null)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(cancellationToken);
        dispatcher.Post(filler ?? (static () => { }));
        return release;
    }

    /// <summary>Verifies a background-thread source change that finds the queue full, then given a
    /// genuine chance to succeed once the saturated slot frees - exactly as a live dispatcher queue
    /// drains in practice - reaches <see cref="Dispatcher.UnhandledException"/> and
    /// <see cref="Dispatcher.FatalException"/> with the original "queue is full" failure, the same
    /// outcome a synchronous dispatcher-callback failure already produces, instead of vanishing
    /// with the target left silently stale forever.</summary>
    [Fact]
    public async Task Source_WhenChangedOffDispatcherAndPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var model = new BindingModel { Name = "Before" };
        ControlText target = null!;
        Binding binding = null!;

        await dispatcher.InvokeAsync(() =>
        {
            target = new ControlText();
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostageRelease = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        // Frees the one saturated slot deterministically, in the otherwise nanosecond-wide window
        // between the first (failed) attempt and the bridging retry, instead of racing a genuine
        // drain: releasing the hostage lets the dispatcher thread dequeue and run the filler above,
        // which signals fillerDrained the moment it does, before the retry ever attempts to post.
        binding.PostRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        // Name's setter raises PropertyChanged synchronously on this background thread, so by the
        // time Task.Run's continuation completes, OnSourceInvalidated has already run inline and
        // exhausted its own first Post attempt against the saturated queue - no timing race.
        await Task.Run(() => { model.Name = "After"; }, TestContext.Current.CancellationToken);

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");

        await Should.NotThrowAsync(async () =>
            await dispatcher.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        dispatcher.FatalException.ShouldBeSameAs(reported);
    }

    /// <summary>Verifies the bridging retry, when it is also rejected for a full queue, drops the
    /// fault as the documented, accepted edge instead of retrying indefinitely - the target stays
    /// stale until a later source change gets a genuine chance to schedule successfully.</summary>
    [Fact]
    public async Task Source_WhenChangedOffDispatcherAndPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var model = new BindingModel { Name = "Before" };
        ControlText target = null!;
        Binding binding = null!;

        await dispatcher.InvokeAsync(() =>
        {
            target = new ControlText();
            target.Attach(dispatcher);
            binding = target.Bind(model, source => source.Name);
        }, TestContext.Current.CancellationToken);

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        await Task.Run(() => { model.Name = "After"; }, TestContext.Current.CancellationToken);

        release.Set();

        // Waits for the specific filler that claimed the queue's one slot to actually run,
        // proving the queue has genuinely drained - rather than racing an arbitrary delay - before
        // asserting the drop is permanent rather than merely delayed: nothing ever reschedules
        // DrainSourceUpdates for this already-lost notification.
        await fillerDrained.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        target.Content.ShouldBe("Before");
        unhandledObserved.ShouldBeFalse();

        // A later change gets a genuine, unsaturated chance to schedule and still converges. The
        // capacity-1 queue leaves no room for a second Post to observe completion, so waiting for
        // the dispatcher's own Idle transition - rather than another InvokeAsync racing the very
        // work item this change just enqueued - is what proves it ran.
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Idle += (_, _) => idle.TrySetResult();
        model.Name = "AfterAgain";
        await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        target.Content.ShouldBe("AfterAgain");
    }
}
