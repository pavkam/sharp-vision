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
        await using Dispatcher dispatcher = Dispatcher.Start(name: "SharpVision.Test");

        (int ownerThreadId, string? ownerName, bool isBackground) = await dispatcher.InvokeAsync(
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
        await using Dispatcher dispatcher = Dispatcher.Start();

        _ = Should.Throw<InvalidOperationException>(dispatcher.VerifyAccess);
        bool allowed = await dispatcher.InvokeAsync(
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        List<int> order = [];
        TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        for (int index = 0; index < 1_000; index++)
        {
            int value = index;
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
        await using Dispatcher dispatcher = Dispatcher.Start(capacity: 1);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        InvalidOperationException failure = new("callback");

        int result = await dispatcher.InvokeAsync(
            static () => 42,
            TestContext.Current.CancellationToken);
        InvalidOperationException thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        using CancellationTokenSource cancellation = new();
        bool invoked = false;
        Task<bool> pending = dispatcher.InvokeAsync(
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        InvalidOperationException failure = new("post");
        TaskCompletionSource<Exception> observed = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource reposted = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        int idleCount = 0;
        TaskCompletionSource posted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource secondIdle = new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    /// <summary>Verifies pending asynchronous phase work suppresses idle.</summary>
    [Fact]
    public async Task Idle_WhenPendingLeaseExists_WaitsForReleaseAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        TaskCompletionSource idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Idle += (_, _) => _ = idle.TrySetResult();

        IDisposable lease = await dispatcher.InvokeAsync(
            dispatcher.Hold,
            TestContext.Current.CancellationToken);

        idle.Task.IsCompleted.ShouldBeFalse();
        lease.Dispose();
        await idle.Task.WaitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies shutdown cancels queued invocations and remains idempotent.</summary>
    [Fact]
    public async Task DisposeAsync_WhenInvocationIsQueued_CancelsAndStopsAsync()
    {
        Dispatcher dispatcher = Dispatcher.Start();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new();
        dispatcher.Post(() =>
        {
            entered.SetResult();
            release.Wait();
        });
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        Task<int> pending = dispatcher.InvokeAsync(
            static () => 42,
            TestContext.Current.CancellationToken).AsTask();

        Task disposal = dispatcher.DisposeAsync().AsTask();
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
        await using Dispatcher dispatcher = Dispatcher.Start();

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
}
