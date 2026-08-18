// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>
/// Verifies <c>ObserveRenderAsync</c> and <c>ObserveOutOfBandAsync</c>'s own completion posts -
/// <c>Dispatcher.Post(() =&gt; CompleteRender(...))</c> and
/// <c>Dispatcher.Post(() =&gt; CompleteOutOfBand(...))</c> - bridge a saturated (but otherwise
/// healthy) dispatcher queue (<see cref="InvalidOperationException"/>) into the dispatcher's own
/// callback-failure path (<see cref="Dispatcher.UnhandledException"/>) instead of silently
/// discarding it. Both methods run their completion post from a background-thread continuation
/// (<c>ConfigureAwait(false)</c>) with no synchronous caller ever able to observe a thrown
/// exception directly - the same fire-and-forget shape <c>TreeViewItem.RunLoadAsync</c> and
/// <c>FileDialogBase.ObserveLoadAsync</c> already bridge for this exact reason - unlike the
/// <c>ApplicationTests.DispatcherFullness</c> sites, which run on a caller's own thread and so
/// correctly propagate a full queue synchronously instead. Each site is paired with a proof that a
/// second full queue on the bridging retry itself is the documented, accepted edge - dropped
/// rather than retried indefinitely, with <see cref="Application.IsRendering"/> still retiring
/// either way so the shutdown drain never wedges.
/// </summary>
public sealed partial class ApplicationTests
{
    /// <summary>Saturates <paramref name="dispatcher"/>'s bounded queue behind a blocked hostage
    /// callback, tracking completion of whichever filler lands in the one slot the queue actually
    /// grants - the rest never get the chance to run.</summary>
    /// <param name="dispatcher">The dispatcher to saturate.</param>
    /// <param name="cancellationToken">Cancels waiting for the hostage to start running.</param>
    /// <returns>The hostage release handle and a completion source that settles once the specific
    /// filler that claimed the queue's one free slot actually runs.</returns>
    private static async Task<(ManualResetEventSlim Release, TaskCompletionSource FillerDrained)>
        SaturateQueueTrackingLastFillerAsync(Dispatcher dispatcher, CancellationToken cancellationToken)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new ManualResetEventSlim();

        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });

        await entered.Task.WaitAsync(cancellationToken);

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            while (true)
            {
                dispatcher.Post(() => fillerDrained.TrySetResult());
            }
        }
        catch (InvalidOperationException)
        {
        }

        return (release, fillerDrained);
    }

    /// <summary>Verifies the render-completion post's bridging retry - given a genuine chance to
    /// succeed once the saturated slot frees, exactly as a live dispatcher queue drains in
    /// practice - reaches <see cref="Dispatcher.UnhandledException"/> with the original
    /// "queue is full" failure, the same outcome a synchronous dispatcher-callback failure already
    /// produces.</summary>
    [Fact]
    public async Task ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

        // The paused flush keeps the very first render in flight indefinitely, so StartAsync
        // never completes along this path; observe its eventual outcome instead of awaiting it.
        _ = starting.ContinueWith(
            static task => _ = task.Exception,
            TestContext.Current.CancellationToken,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var (hostageRelease, fillerDrained) = await SaturateQueueTrackingLastFillerAsync(
            application.Dispatcher,
            TestContext.Current.CancellationToken);

        // Frees the one saturated slot deterministically, in the otherwise nanosecond-wide window
        // between the first (failed) attempt and the bridging retry, instead of racing a genuine
        // drain: releasing the hostage lets the dispatcher thread dequeue and run fillers until it
        // reaches the one this test is tracking, which signals fillerDrained the moment it does,
        // before the retry ever attempts to post.
        application.PostRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        // Unblocks the paused transport flush; RenderAsync's remaining awaits resume off the
        // dispatcher thread (ConfigureAwait(false) throughout), so ObserveRenderAsync's own
        // completion post below runs against the queue this test just saturated.
        terminal.ReleaseFlush();

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies the render-completion post's bridging retry, when it is also rejected for
    /// a full queue - the queue never drains at all in this scenario - drops the fault as the
    /// documented, accepted edge instead of retrying indefinitely, while still retiring
    /// <see cref="Application.IsRendering"/> so the shutdown drain never wedges.</summary>
    [Fact]
    public async Task ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.PauseFlush();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();

        _ = starting.ContinueWith(
            static task => _ = task.Exception,
            TestContext.Current.CancellationToken,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        application.Dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        terminal.ReleaseFlush();

        // Nothing ever frees the saturated slot, so both the original attempt and the bridging
        // retry observe the same full queue; give the off-thread continuation a moment to reach
        // and exhaust both before asserting the drop.
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        unhandledObserved.ShouldBeFalse();
        application.Failure.ShouldBeNull();

        release.Set();
        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
        await application.DisposeAsync();
    }

    /// <summary>Verifies the out-of-band-completion post's bridging retry - the sibling site to
    /// <see cref="ObserveRenderAsync_WhenCompleteRenderPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync"/>
    /// covered above, exercised through <c>Application.PostOutOfBand</c> directly instead of a
    /// frame render - also reaches <see cref="Dispatcher.UnhandledException"/> once the saturated
    /// slot frees.</summary>
    [Fact]
    public async Task ObserveOutOfBandAsync_WhenCompleteOutOfBandPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };
        await application.Dispatcher.InvokeAsync(
            () => application.PostOutOfBand(bytes),
            TestContext.Current.CancellationToken);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var (hostageRelease, fillerDrained) = await SaturateQueueTrackingLastFillerAsync(
            application.Dispatcher,
            TestContext.Current.CancellationToken);

        application.PostRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        terminal.ReleaseFlush();

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the out-of-band-completion post's bridging retry, when it is also
    /// rejected for a full queue, drops the fault instead of retrying indefinitely - the sibling
    /// edge case to the render-completion site's own double-failure drop above.</summary>
    [Fact]
    public async Task ObserveOutOfBandAsync_WhenCompleteOutOfBandPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(10, 4)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        await application.StartAsync(TestContext.Current.CancellationToken);

        terminal.PauseFlush();
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };
        await application.Dispatcher.InvokeAsync(
            () => application.PostOutOfBand(bytes),
            TestContext.Current.CancellationToken);

        await WaitForAsync(() => application.IsRendering, TimeSpan.FromSeconds(5));

        var release = await SaturateQueueAsync(application.Dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        application.Dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        terminal.ReleaseFlush();

        await WaitForAsync(() => !application.IsRendering, TimeSpan.FromSeconds(5));

        unhandledObserved.ShouldBeFalse();
        application.Failure.ShouldBeNull();

        release.Set();
        await WaitForQueueToDrainAsync(application.Dispatcher, TestContext.Current.CancellationToken);
    }
}
