// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

using System.Reflection;

/// <summary>Verifies the two shutdown contracts a bare <c>Dispatcher</c> owes a direct consumer:
/// a fatal callback failure stays retrievable, and the active callback gets one consistent answer
/// to whether it may still schedule work.
///
/// <para><c>Report</c> used to record the failure only when the <c>UnhandledException</c> handler
/// itself threw. In the other two arms - no subscriber at all, which is the default for a bare
/// dispatcher, and a subscriber leaving <c>Handled</c> false - the dispatcher stopped with the
/// cause recorded nowhere, and <c>DisposeAsync</c> completed successfully for a dead object. The
/// caller's next <c>Post</c> then threw <c>ObjectDisposedException</c> on something nobody had
/// disposed.</para>
///
/// <para>The scheduling half had no stated rule at all, and the six entry points gave three
/// different answers. The rule adopted here: the active callback may run to completion, may invoke
/// inline, and may take pending holds; it may not enqueue new work.</para>
/// </summary>
public sealed class DispatcherShutdownContractTests
{
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
    /// hosted path - <c>Application</c> sets Handled unconditionally - is untouched.</summary>
    [Fact]
    public async Task FatalException_WhenASubscriberHandlesTheFailure_IsNullAndTheLoopSurvivesAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        dispatcher.UnhandledException += (_, eventArgs) => eventArgs.Handled = true;

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
}
