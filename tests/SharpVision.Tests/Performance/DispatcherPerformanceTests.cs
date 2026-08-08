// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

/// <summary>Gates Dispatcher.Post allocation cost under bounded posting.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class DispatcherPerformanceTests
{
    /// <summary>Verifies bounded dispatcher posting stays within its object budget.</summary>
    [Fact]
    public async Task Post_WhenDispatcherIsWarm_StaysWithinAllocationBudgetAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var warmed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(warmed.SetResult);
        await warmed.Task.WaitAsync(TestContext.Current.CancellationToken);
        const int iterations = 1_000;
        var watch = Stopwatch.StartNew();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < iterations; index++)
        {
            dispatcher.Post(static () => { });
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        dispatcher.Post(completed.SetResult);
        await completed.Task.WaitAsync(TestContext.Current.CancellationToken);
        watch.Stop();

        allocated.ShouldBeLessThanOrEqualTo(256L * iterations);
        Report("dispatcher post/drain", watch.Elapsed, iterations);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} measured iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
