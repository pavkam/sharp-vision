// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Threading;

/// <summary>Verifies bounded background-completion posting and abandonment.</summary>
public sealed partial class DispatcherTests
{
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
}
