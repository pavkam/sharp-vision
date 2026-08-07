// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

/// <summary>Verifies dispatcher affinity, bounded work, idle, and shutdown.</summary>
public sealed class DispatcherTests
{
    /// <summary>Verifies the dispatcher owns one distinct named background thread.</summary>
    [Fact]
    public async Task Start_WhenCreated_OwnsDedicatedThreadAsync()
    {
        await using var dispatcher = Dispatcher.Start(name: "SharpVision.Test");

        var (ownerThreadId, ownerName, isBackground) = await dispatcher.InvokeAsync(
            static () =>
                (Environment.CurrentManagedThreadId,
                    Thread.CurrentThread.Name,
                    Thread.CurrentThread.IsBackground),
            TestContext.Current.CancellationToken);

        dispatcher.CheckAccess().ShouldBeFalse();
        ownerThreadId.ShouldNotBe(Environment.CurrentManagedThreadId);
        ownerName.ShouldBe("SharpVision.Test");
        isBackground.ShouldBeTrue();
    }

    /// <summary>Verifies access checks distinguish the owner before any mutation.</summary>
    [Fact]
    public async Task VerifyAccess_WhenCalledOffThread_ThrowsInvalidOperationExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        _ = Should.Throw<InvalidOperationException>(dispatcher.VerifyAccess);
        var allowed = await dispatcher.InvokeAsync(
            () =>
            {
                dispatcher.VerifyAccess();
                return dispatcher.CheckAccess();
            },
            TestContext.Current.CancellationToken);

        allowed.ShouldBeTrue();
    }

    /// <summary>Verifies posted callbacks execute in FIFO order.</summary>
    [Fact]
    public async Task Post_WhenCallbacksAreQueued_ExecutesFifoAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        List<int> order = [];
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        for (var index = 0; index < 1_000; index++)
        {
            var value = index;
            dispatcher.Post(() => order.Add(value));
        }

        dispatcher.Post(completed.SetResult);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(Enumerable.Range(0, 1_000));
    }

    /// <summary>Verifies queue capacity rejects work without disturbing queued callbacks.</summary>
    [Fact]
    public async Task Post_WhenQueueIsFull_ThrowsBeforeEnqueueAsync()
    {
        await using var dispatcher = Dispatcher.Start(capacity: 1);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        dispatcher.Post(static () => { });

        _ = Should.Throw<InvalidOperationException>(() => dispatcher.Post(static () => { }));
        release.Set();
    }

    /// <summary>Verifies invocation returns values and preserves callback exception identity.</summary>
    [Fact]
    public async Task InvokeAsync_WhenCallbackCompletes_TransfersResultOrExceptionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var failure = new InvalidOperationException("callback");

        var result = await dispatcher.InvokeAsync(
            static () => 42,
            TestContext.Current.CancellationToken);
        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync<int>(
                () => throw failure,
                TestContext.Current.CancellationToken));

        result.ShouldBe(42);
        thrown.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies cancellation before execution prevents callback invocation.</summary>
    [Fact]
    public async Task InvokeAsync_WhenCancelledBeforeExecution_DoesNotInvokeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var invoked = false;
        var pending = dispatcher.InvokeAsync(
            () => invoked = true,
            cancellation.Token).AsTask();

        cancellation.Cancel();
        release.Set();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);

        invoked.ShouldBeFalse();
    }

    /// <summary>Verifies post failures are reported without holding the queue lock.</summary>
    [Fact]
    public async Task Post_WhenCallbackThrows_ReportsAndCanPostFromHandlerAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var failure = new InvalidOperationException("post");
        var observed = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var reposted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.UnhandledException += (_, eventArgs) =>
        {
            eventArgs.Handled = true;
            observed.SetResult(eventArgs.Exception);
            dispatcher.Post(reposted.SetResult);
        };

        dispatcher.Post(() => throw failure);

        (await observed.Task.WaitAsync(TestContext.Current.CancellationToken))
            .ShouldBeSameAs(failure);
        await reposted.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies idle-posted work drains before the dispatcher waits again.</summary>
    [Fact]
    public async Task Idle_WhenHandlerPostsWork_DrainsBeforeNextIdleAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var idleCount = 0;
        var posted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondIdle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Idle += (_, _) =>
        {
            if (Interlocked.Increment(ref idleCount) == 1)
            {
                dispatcher.Post(() => posted.SetResult());
            }
            else
            {
                secondIdle.SetResult();
            }
        };

        dispatcher.Post(static () => { });

        await posted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await secondIdle.Task.WaitAsync(TestContext.Current.CancellationToken);
        idleCount.ShouldBe(2);
    }

    /// <summary>Verifies the cancellable Post overload invokes its action normally when the work
    /// actually runs.</summary>
    [Fact]
    public async Task Post_WithOnCancelled_WhenWorkRuns_InvokesActionNotOnCancelledAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = false;

        dispatcher.Post(completed.SetResult, () => cancelled = true);

        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        cancelled.ShouldBeFalse();
    }

    /// <summary>Verifies the cancellable Post overload invokes onCancelled - not action - when
    /// dispatcher shutdown cancels the already-queued work before the dispatcher thread reaches
    /// it, closing the race a caller's own state (e.g. DispatcherTimer's pending flag) would
    /// otherwise latch forever waiting for a callback that never runs.</summary>
    [Fact]
    public async Task Post_WithOnCancelled_WhenDispatcherStopsBeforeRunning_InvokesOnCancelledInsteadAsync()
    {
        using var releaseGate = new ManualResetEventSlim(initialState: false);
        var dispatcher = Dispatcher.StartPaused(releaseGate);
        var actionRan = false;
        var cancelledRan = false;

        dispatcher.Post(() => actionRan = true, () => cancelledRan = true);

        // DisposeAsync cancels every still-queued work item synchronously, before the dispatcher
        // thread (still blocked on releaseGate) ever gets a chance to run it; only awaiting the
        // returned ValueTask needs that thread to actually resume and exit, so the gate is
        // released only after the cancellation itself has already happened.
        var disposing = dispatcher.DisposeAsync();
        releaseGate.Set();
        await disposing;

        actionRan.ShouldBeFalse();
        cancelledRan.ShouldBeTrue();
    }

    /// <summary>Verifies Idle fires for a freshly started dispatcher that never receives any work.</summary>
    /// <remarks>
    /// Ordinary <see cref="Dispatcher.Start"/> returns as soon as the background thread exists,
    /// which races that thread's own near-immediate first idle-detection attempt against this
    /// test subscribing to <see cref="Dispatcher.Idle"/>. <see cref="Dispatcher.StartPaused"/>
    /// closes that race deterministically: the dispatcher thread blocks immediately before its
    /// first attempt until the test explicitly releases it, guaranteeing the subscription below
    /// happens-before the one-shot notification it observes.
    /// </remarks>
    [Fact]
    public async Task Idle_WhenDispatcherStartsWithNoPriorWork_FiresAsync()
    {
        using var releaseGate = new ManualResetEventSlim(initialState: false);
        await using var dispatcher = Dispatcher.StartPaused(releaseGate);
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Idle += (_, _) => idle.TrySetResult();
        releaseGate.Set();

        await idle.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies pending asynchronous phase work suppresses idle.</summary>
    [Fact]
    public async Task Idle_WhenPendingLeaseExists_WaitsForReleaseAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        var lease = await dispatcher.InvokeAsync(
            dispatcher.Hold,
            TestContext.Current.CancellationToken);

        // Subscribing only after the lease exists (rather than before) means there is no
        // window where the freshly started dispatcher's own spontaneous initial Idle (see
        // Idle_WhenDispatcherStartsWithNoPriorWork_FiresAsync) could race this assertion:
        // Hold already forces pending > 0, so Idle cannot fire between here and Dispose.
        var idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Idle += (_, _) => _ = idle.TrySetResult();

        idle.Task.IsCompleted.ShouldBeFalse();
        lease.Dispose();
        await idle.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies shutdown cancels queued invocations and remains idempotent.</summary>
    [Fact]
    public async Task DisposeAsync_WhenInvocationIsQueued_CancelsAndStopsAsync()
    {
        var dispatcher = Dispatcher.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var pending = dispatcher.InvokeAsync(
            static () => 42,
            TestContext.Current.CancellationToken).AsTask();

        var disposal = dispatcher.DisposeAsync().AsTask();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        release.Set();
        await disposal;
        await dispatcher.DisposeAsync();

        _ = Should.Throw<ObjectDisposedException>(() => dispatcher.Post(static () => { }));
    }

    /// <summary>Verifies invalid construction and null callbacks fail immediately.</summary>
    [Fact]
    public async Task PublicMethods_WhenArgumentsAreInvalid_ThrowBeforeMutationAsync()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => Dispatcher.Start(capacity: 0));
        _ = Should.Throw<ArgumentException>(() => Dispatcher.Start(name: " "));
        await using var dispatcher = Dispatcher.Start();

        _ = Should.Throw<ArgumentNullException>(() => dispatcher.Post(null!));
        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await dispatcher.InvokeAsync(
                action: null!,
                TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<ArgumentNullException>(async () =>
            await dispatcher.InvokeAsync<int>(
                function: null!,
                TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies the dispatcher thread has a SynchronizationContext that routes back to itself.</summary>
    [Fact]
    public async Task Run_WhenStarted_InstallsSynchronizationContextAsync()
    {
        await using var dispatcher = Dispatcher.Start(name: "SharpVision.SyncCtx");

        var context = await dispatcher.InvokeAsync(
            static () => SynchronizationContext.Current,
            TestContext.Current.CancellationToken);

        _ = context.ShouldNotBeNull();
    }

    /// <summary>Verifies an await on the dispatcher thread resumes on the dispatcher thread.</summary>
    [Fact]
    public async Task Await_WhenOnDispatcherThread_ResumesOnDispatcherThreadAsync()
    {
        await using var dispatcher = Dispatcher.Start(name: "SharpVision.Resume");
        var dispatcherThreadId = await dispatcher.InvokeAsync(
            static () => Environment.CurrentManagedThreadId,
            TestContext.Current.CancellationToken);

        var tcs = new TaskCompletionSource<int>();
        dispatcher.Post(async () =>
        {
            await Task.Delay(10);
            tcs.SetResult(Environment.CurrentManagedThreadId);
        });
        var resumedThreadId = await tcs.Task;

        resumedThreadId.ShouldBe(dispatcherThreadId);
    }

    /// <summary>Verifies SynchronizationContext.Post silently drops work after disposal.</summary>
    [Fact]
    public async Task Post_WhenDispatcherIsDisposed_DoesNotThrowAsync()
    {
        var dispatcher = Dispatcher.Start(name: "SharpVision.PostDisposed");
        var context = await dispatcher.InvokeAsync(
            static () => SynchronizationContext.Current!,
            TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();

        Should.NotThrow(() => context.Post(_ => { }, null));
    }
}
