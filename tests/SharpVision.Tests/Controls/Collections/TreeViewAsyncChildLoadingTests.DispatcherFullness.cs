// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>
/// Verifies the three <c>TreeViewItem.RunLoadAsync</c> call sites that guard a
/// <c>Dispatcher.Post</c> attempt - the success-commit post, the failure-commit post, and the
/// <c>finally</c> block's admission-slot release post - now bridge a saturated (but otherwise
/// healthy) dispatcher queue (<see cref="InvalidOperationException"/>) into the dispatcher's own
/// callback-failure path (<see cref="Dispatcher.UnhandledException"/> / <see cref="Dispatcher.FatalException"/>)
/// instead of letting it fault the unobserved <see cref="TreeViewItem.LastChildLoadObservation"/>
/// task where nothing but a test ever looks. Each site is also paired with a proof that the
/// original <see cref="ObjectDisposedException"/> no-op is unchanged, and that a second full queue
/// on the bridging retry itself is the documented, accepted edge - dropped rather than retried
/// indefinitely.
/// </summary>
public sealed partial class TreeViewTests
{
    private static TreeViewChildDescription[] OneLeafChild() =>
        [new TreeViewChildDescription("a", "A") { Presence = TreeViewChildPresence.Leaf }];

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

    /// <summary>Verifies the success-commit post's bridging retry - given a genuine chance to
    /// succeed once the saturated slot frees, exactly as a live dispatcher queue drains in
    /// practice - reaches <see cref="Dispatcher.UnhandledException"/> and
    /// <see cref="Dispatcher.FatalException"/> with the original "queue is full" failure, the same
    /// outcome a synchronous dispatcher-callback failure already produces. The commit itself never
    /// runs (only the rethrow was ever queued), so the item stays stuck Loading - loading state is
    /// deliberately not reset as a substitute for this contract.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSuccessCommitPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostageRelease = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        // Frees the one saturated slot deterministically, in the otherwise nanosecond-wide window
        // between the first (failed) attempt and the bridging retry, instead of racing a genuine
        // drain: releasing the hostage lets the dispatcher thread dequeue and run the filler above,
        // which signals fillerDrained the moment it does, before the retry ever attempts to post.
        dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        completion.SetResult(OneLeafChild());

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);

        await Should.NotThrowAsync(async () =>
            await dispatcher.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        dispatcher.FatalException.ShouldBeSameAs(reported);
    }

    /// <summary>Verifies the success-commit post's bridging retry, when it is also rejected for a
    /// full queue - the queue never drains at all in this scenario - drops the fault as the
    /// documented, accepted edge instead of retrying indefinitely: the observation completes
    /// successfully, nothing reaches <see cref="Dispatcher.UnhandledException"/>, and the item
    /// stays stuck Loading with no observable signal beyond that stall.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSuccessCommitPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;
        var release = await SaturateSingleSlotQueueAsync(dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        completion.SetResult(OneLeafChild());

        // Nothing ever frees the saturated slot, so both the original attempt and the bridging
        // retry observe the same full queue; give the off-thread continuation a moment to reach and
        // exhaust both before asserting the drop.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        release.Set();

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        unhandledObserved.ShouldBeFalse();
    }

    /// <summary>Verifies the success-commit and slot-release posts still no-op silently once the
    /// dispatcher is disposed, exactly as before.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenDispatcherIsDisposedAfterSuccess_CompletesWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        await dispatcher.DisposeAsync();

        completion.SetResult(OneLeafChild());

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies the failure-commit post's bridging retry - given the same genuine chance
    /// to succeed as the success-commit site above - reaches
    /// <see cref="Dispatcher.UnhandledException"/> with the original "queue is full" failure.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenFailureCommitPostFindsQueueFullThenFrees_BridgesToUnhandledExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        var fillerDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hostageRelease = await SaturateSingleSlotQueueAsync(
            dispatcher,
            TestContext.Current.CancellationToken,
            filler: () => fillerDrained.SetResult());

        dispatcher.BackgroundCompletionRetryHookForTests = () =>
        {
            hostageRelease.Set();
            _ = fillerDrained.Task.Wait(TimeSpan.FromSeconds(5));
        };

        var unhandled = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) => unhandled.TrySetResult(eventArgs.Exception);

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        var reported = await unhandled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        reported.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("The dispatcher queue is full.");
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();

        // The failure commit never ran either (same bridge, same only-the-rethrow-was-queued
        // shape), so LastChildLoadError is never published and the item stays stuck Loading rather
        // than advancing to LoadFailed.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        item.LastChildLoadError.ShouldBeNull();
    }

    /// <summary>Verifies the failure-commit post's bridging retry, when it is also rejected for a
    /// full queue, drops the fault instead of retrying indefinitely - the sibling edge case to the
    /// success-commit site's own double-failure drop above.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenFailureCommitPostFindsQueueFullOnBothAttempts_DropsTheFaultAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;
        var release = await SaturateSingleSlotQueueAsync(dispatcher, TestContext.Current.CancellationToken);

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        release.Set();

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        item.LastChildLoadError.ShouldBeNull();
        unhandledObserved.ShouldBeFalse();
    }

    /// <summary>Verifies the failure-commit and slot-release posts still no-op silently once the
    /// dispatcher is disposed, exactly as before.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenDispatcherIsDisposedAfterFailure_CompletesWithoutFaultingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        await dispatcher.DisposeAsync();

        completion.SetException(new InvalidOperationException("simulated child-source failure"));

        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        observation.IsCompletedSuccessfully.ShouldBeTrue();
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
    }

    /// <summary>Verifies the <c>finally</c> block's own admission-slot release post, when its own
    /// bridging retry is also rejected for a full queue - isolated from the other two sites by
    /// letting the success-commit post through first - drops the fault as the documented, accepted
    /// edge instead of retrying indefinitely, and that the already-queued commit still runs once
    /// the dispatcher is unblocked even though the slot itself was never released.</summary>
    [Fact]
    public async Task RunLoadAsync_WhenSlotReleasePostFindsQueueFullOnBothAttempts_DropsTheFaultButStillCommitsAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var source = new FakeTreeViewChildSource();
        var completion = source.DeferNext(null);
        TreeView tree = null!;
        TreeViewItem item = null!;

        await dispatcher.InvokeAsync(() =>
        {
            item = new TreeViewItem("Root") { ChildSource = source, IsExpanded = false };
            tree = new TreeView { Items = { item } };
            tree.Attach(dispatcher);
            item.IsExpanded = true;
        }, TestContext.Current.CancellationToken);

        var observation = item.LastChildLoadObservation!;

        using ManualResetEventSlim release = new();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // No filler this time: with capacity 1 and the hostage already dequeued, the queue is
        // empty, so the success-commit post below succeeds and consumes the one slot the queue
        // allows - isolating the finally block's own slot-release post (and its own bridging
        // retry) as the one that finds it full on both attempts.
        completion.SetResult(OneLeafChild());

        var unhandledObserved = false;
        dispatcher.UnhandledException += (_, _) => unhandledObserved = true;

        // The success commit's own post succeeded immediately (queue was empty), so give the
        // off-thread continuation a moment to reach the finally block and exhaust both of its own
        // post attempts against the now-saturated queue before asserting the drop.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // The success commit was already queued behind the still-blocked dispatcher thread before
        // the finally's own posts failed, so it has not run yet.
        item.ChildState.ShouldBe(TreeViewChildState.Loading);
        unhandledObserved.ShouldBeFalse();

        release.Set();

        await TreeViewChildLoadWait.UntilAsync(
            item,
            () => item.ChildState == TreeViewChildState.Loaded,
            TestContext.Current.CancellationToken);

        item.Children.Count.ShouldBe(1);
        await observation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        observation.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
