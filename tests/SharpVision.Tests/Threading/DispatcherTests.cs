// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

using System.Reflection;

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
            eventArgs.IsHandled = true;
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

    /// <summary>Verifies shutdown cancels a queued function invocation, remains idempotent, and
    /// preserves the caller-supplied token identity on the resulting cancellation - not a
    /// disconnected fabricated one.</summary>
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
        var invocationToken = TestContext.Current.CancellationToken;
        var pending = dispatcher.InvokeAsync(
            static () => 42,
            invocationToken).AsTask();

        var disposal = dispatcher.DisposeAsync().AsTask();
        var thrown = await Should.ThrowAsync<OperationCanceledException>(pending);
        thrown.CancellationToken.ShouldBe(invocationToken);
        release.Set();
        await disposal;
        await dispatcher.DisposeAsync();

        _ = Should.Throw<ObjectDisposedException>(() => dispatcher.Post(static () => { }));
    }

    /// <summary>Verifies the same token-identity preservation for a queued action invocation
    /// (the <see cref="Dispatcher.InvokeAsync(Action, CancellationToken)"/> overload, backed by
    /// ActionWork rather than FunctionWork&lt;T&gt;).</summary>
    [Fact]
    public async Task DisposeAsync_WhenActionInvocationIsQueued_CancelsWithSameTokenAsync()
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
        var invocationToken = TestContext.Current.CancellationToken;
        var invoked = false;
        var pending = dispatcher.InvokeAsync(
            () => invoked = true,
            invocationToken).AsTask();

        var disposal = dispatcher.DisposeAsync().AsTask();
        var thrown = await Should.ThrowAsync<OperationCanceledException>(pending);
        thrown.CancellationToken.ShouldBe(invocationToken);
        release.Set();
        await disposal;

        invoked.ShouldBeFalse();
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
            // Forces an asynchronous resumption - only await proves the continuation returns
            // through SynchronizationContext.Post rather than running synchronously inline.
            await Task.Yield();
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

    /// <summary>The erasure this file exists to pin, in its default form - a bare dispatcher with
    /// no subscriber, where the failure previously existed nowhere at all.</summary>
    [Fact]
    public async Task FatalException_WhenACallbackThrowsWithNoSubscriber_RetainsTheCauseAsync()
    {
        var dispatcher = Dispatcher.Start();
        InvalidOperationException failure = new("callback-boom");

        dispatcher.Post(() => throw failure);
        await WaitForStopAsync(dispatcher);

        dispatcher.FatalException.ShouldBeSameAs(failure);
        await dispatcher.DisposeAsync();
    }

    /// <summary>Verifies the same for a subscriber that received the exception and declined to
    /// handle it - the dispatcher still dies, so the cause must still be retrievable.</summary>
    [Fact]
    public async Task FatalException_WhenASubscriberLeavesTheFailureUnhandled_RetainsTheCauseAsync()
    {
        var dispatcher = Dispatcher.Start();
        InvalidOperationException failure = new("callback-boom");
        var observed = 0;
        dispatcher.UnhandledException += (_, _) => observed++;

        dispatcher.Post(() => throw failure);
        await WaitForStopAsync(dispatcher);

        observed.ShouldBe(1);
        dispatcher.FatalException.ShouldBeSameAs(failure);
        await dispatcher.DisposeAsync();
    }

    /// <summary>Verifies a handler that itself throws still preserves both exceptions, which was
    /// the one arm that already worked and must keep working.</summary>
    [Fact]
    public async Task FatalException_WhenTheHandlerItselfThrows_RetainsBothAsync()
    {
        var dispatcher = Dispatcher.Start();
        InvalidOperationException failure = new("callback-boom");
        InvalidOperationException handlerFailure = new("handler-boom");
        dispatcher.UnhandledException += (_, _) => throw handlerFailure;

        dispatcher.Post(() => throw failure);
        await WaitForStopAsync(dispatcher);

        var aggregate = dispatcher.FatalException.ShouldBeOfType<AggregateException>();
        aggregate.InnerExceptions.ShouldBe([failure, handlerFailure]);
        await dispatcher.DisposeAsync();
    }

    /// <summary>Verifies disposal never faults, in the arm that used to. A faulting completion
    /// made an unawaited DisposeAsync surface as a process-wide unobserved task exception, and
    /// would fault every <c>await using</c> at implicit dispose.</summary>
    [Fact]
    public async Task DisposeAsync_AfterAFatalCallbackFailure_CompletesSuccessfullyAsync()
    {
        var dispatcher = Dispatcher.Start();
        dispatcher.UnhandledException += (_, _) => throw new InvalidOperationException("handler-boom");

        dispatcher.Post(static () => throw new InvalidOperationException("callback-boom"));
        await WaitForStopAsync(dispatcher);

        await dispatcher.DisposeAsync();
        _ = dispatcher.FatalException.ShouldNotBeNull("the cause survives the successful disposal");
    }

    /// <summary>The counter-case that keeps the property honest: an ordinary requested shutdown
    /// leaves no fatal exception behind.</summary>
    [Fact]
    public async Task FatalException_WhenTheDispatcherStopsOnRequest_IsNullAsync()
    {
        var dispatcher = Dispatcher.Start();
        var ran = 0;

        await dispatcher.InvokeAsync(() => { ran++; }, TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();

        ran.ShouldBe(1);
        dispatcher.FatalException.ShouldBeNull();
    }

    /// <summary>Verifies a handled failure neither stops the dispatcher nor records a cause, so the
    /// hosted path - <c>Application</c> sets IsHandled unconditionally - is untouched.</summary>
    [Fact]
    public async Task FatalException_WhenASubscriberHandlesTheFailure_IsNullAndTheLoopSurvivesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        dispatcher.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;

        dispatcher.Post(static () => throw new InvalidOperationException("callback-boom"));
        var ran = 0;
        await dispatcher.InvokeAsync(() => { ran++; }, TestContext.Current.CancellationToken);

        ran.ShouldBe(1, "a handled failure leaves the dispatcher running");
        dispatcher.FatalException.ShouldBeNull();
    }

    /// <summary>The scheduling rule's permissive half: the active callback may take a hold after
    /// shutdown has started. VerifyAccess makes Hold reachable only from that callback, and a hold
    /// enqueues nothing, so revoking it only punished the one caller entitled to it.</summary>
    [Fact]
    public async Task Hold_WhenTakenFromTheActiveCallbackDuringShutdown_SucceedsAsync()
    {
        var dispatcher = Dispatcher.Start();
        Exception? observed = null;
        var taken = false;
        using ManualResetEventSlim reached = new();

        dispatcher.Post(() =>
        {
            reached.Set();

            // Spin until DisposeAsync has actually set the stopping flag, so the hold below is
            // provably taken during shutdown rather than racing it. This runs synchronously inside
            // the dispatcher's own posted callback - the one thread capable of observing the flag
            // flip is busy spinning here, so there is no dispatcher-owned signal (event, TCS, or
            // otherwise) it could instead await without deadlocking itself. The cross-thread flag
            // set by DisposeAsync is the only other side, and polling it is the only option left.
            while (!IsStopping(dispatcher))
            {
                Thread.Sleep(1);
            }

            try
            {
                using var hold = dispatcher.Hold();
                taken = true;
            }
            catch (Exception exception)
            {
                observed = exception;
            }
        });

        reached.Wait(TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();

        observed.ShouldBeNull();
        taken.ShouldBeTrue("the active callback may take a pending hold during shutdown");
    }

    /// <summary>The rule's restrictive half, alongside the inline exemption: enqueueing is refused
    /// once shutdown starts, while an inline invoke on the dispatcher thread still runs.</summary>
    [Fact]
    public async Task PostAndInlineInvoke_DuringShutdown_RefuseAndRunRespectivelyAsync()
    {
        var dispatcher = Dispatcher.Start();
        Exception? postFailure = null;
        var inlineRan = false;
        using ManualResetEventSlim reached = new();

        dispatcher.Post(() =>
        {
            reached.Set();

            // Same spin as above, and for the same reason: this callback is itself the only thread
            // that could observe a signal here, so it cannot wait on one without deadlocking itself.
            while (!IsStopping(dispatcher))
            {
                Thread.Sleep(1);
            }

            try
            {
                dispatcher.Post(static () => { });
            }
            catch (Exception exception)
            {
                postFailure = exception;
            }

            // Inline: schedules nothing, so the rule permits it. The ValueTask is already
            // completed on this path.
            dispatcher.InvokeAsync(() => { inlineRan = true; }).AsTask().GetAwaiter().GetResult();
        });

        reached.Wait(TestContext.Current.CancellationToken);
        await dispatcher.DisposeAsync();

        _ = postFailure.ShouldBeOfType<ObjectDisposedException>();
        inlineRan.ShouldBeTrue("an inline invoke enqueues nothing, so shutdown does not refuse it");
    }

    private static bool IsStopping(Dispatcher dispatcher) =>
        (bool) typeof(Dispatcher)
            .GetField("_stopping", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dispatcher)!;

    // The run loop's own completion signal, reached the same way IsStopping reaches the flag
    // above: there is no public way to observe a bare Dispatcher's shutdown, so this reflects out
    // the private TaskCompletionSource the run loop itself settles when it exits, rather than
    // polling Post for the ObjectDisposedException that means the same thing.
    private static async Task WaitForStopAsync(Dispatcher dispatcher)
    {
        var stopped = (TaskCompletionSource) typeof(Dispatcher)
            .GetField("_stopped", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(dispatcher)!;

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing a freshly started dispatcher synchronously and immediately - from
    /// the same thread that just called <see cref="Dispatcher.Start"/>, with nothing yet posted -
    /// actually stops the background thread rather than leaking it. This is the exact shape
    /// <c>Application</c>'s own constructor now uses in its catch block: if a step after
    /// <c>Dispatcher.Start</c> throws, it blocks on <c>Dispatcher.DisposeAsync().AsTask()
    /// .GetAwaiter().GetResult()</c> before rethrowing, since <c>ConsoleApplicationBuilder.Build()</c>'s
    /// own catch has no application reference to reach the dispatcher through once the constructor
    /// never returns one. There is no dependency reachable through <c>Application</c>'s public
    /// constructor arguments that fails deterministically in that exact post-<c>Start</c> window -
    /// the one call capable of it, <c>CooperativeShutdownSignals.Register</c>, only throws depending
    /// on platform signal support - so the mechanism the fix relies on is exercised directly here
    /// instead.</summary>
    [Fact]
    public async Task DisposeAsync_WhenCalledSynchronouslyRightAfterStartWithNothingPosted_StopsTheThreadAsync()
    {
        var dispatcher = Dispatcher.Start(name: "SharpVision.PostStartFailure");

        var exited = await Task.Run(() =>
        {
            // Mirrors Application's constructor catch block exactly.
            dispatcher.DisposeAsync().AsTask().GetAwaiter().GetResult();

            var thread = (Thread) typeof(Dispatcher)
                .GetField("_thread", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(dispatcher)!;

            return thread.Join(TimeSpan.FromSeconds(5));
        }).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        exited.ShouldBeTrue("the background thread must exit rather than leak");
        Should.Throw<ObjectDisposedException>(() => dispatcher.Post(static () => { }));
    }

    #region Background completion

    /// <summary>Verifies accepted completion runs once on the dispatcher without abandonment.</summary>
    [Fact]
    public async Task PostBackgroundCompletion_WhenQueueAccepts_RunsOnlyCompletionAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var abandoned = 0;

        dispatcher.PostBackgroundCompletion(
            () =>
            {
                dispatcher.CheckAccess().ShouldBeTrue();
                completed.SetResult();
            },
            () => abandoned++);

        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        abandoned.ShouldBe(0);
    }

    /// <summary>Verifies disposed dispatchers abandon completion exactly once.</summary>
    [Fact]
    public async Task PostBackgroundCompletion_WhenDispatcherIsDisposed_AbandonsOnceAsync()
    {
        var dispatcher = Dispatcher.Start();
        await dispatcher.DisposeAsync();
        var completed = 0;
        var abandoned = 0;

        dispatcher.PostBackgroundCompletion(() => completed++, () => abandoned++);

        completed.ShouldBe(0);
        abandoned.ShouldBe(1);
    }

    /// <summary>Verifies work accepted before shutdown is abandoned when cancellation prevents execution.</summary>
    [Fact]
    public async Task PostBackgroundCompletion_WhenAcceptedWorkIsCancelledByShutdown_AbandonsOnceAsync()
    {
        using var release = new ManualResetEventSlim();
        var dispatcher = Dispatcher.Start();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var completed = 0;
        var abandoned = 0;
        dispatcher.PostBackgroundCompletion(() => completed++, () => abandoned++);

        var disposal = dispatcher.DisposeAsync();
        release.Set();
        await disposal;

        completed.ShouldBe(0);
        abandoned.ShouldBe(1);
    }

    #endregion
}
