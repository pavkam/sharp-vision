// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>
/// Verifies <see cref="CooperativeShutdownSignals.InvokeTerminationSignal"/> - the branch that
/// makes Windows' <c>SIGTERM</c>/<c>SIGHUP</c> registration (mapped to
/// <c>CTRL_SHUTDOWN_EVENT</c>/<c>CTRL_CLOSE_EVENT</c>) block synchronously until the requested stop
/// actually finishes, instead of returning immediately the way every other registration does.
/// </summary>
/// <remarks>
/// This drives the extracted static helper directly, with an injected <c>isWindows</c> flag,
/// rather than raising a real signal or depending on the platform this test process actually runs
/// on: the point under test is the blocking/non-blocking branch itself, not the OS-specific
/// plumbing that decides which branch a real signal reaches (that plumbing already has coverage in
/// <see cref="ConsoleApplicationTests.RunCoreAsync_WhenRealPosixSignalRaised_StopsCleanlyAsync(int)"/>
/// and <see cref="ApplicationProcessSignalTests"/>). Windows kills a process once every registered
/// console-control handler has returned (or after about five seconds), unlike Unix, which waits
/// indefinitely once the signal handler cancels it - so a callback that merely fires-and-forgets on
/// that path risks the OS tearing the process down mid-cleanup.
/// </remarks>
public sealed class CooperativeShutdownSignalsTests
{
    /// <summary>Verifies the Windows branch genuinely blocks: it does not return while the
    /// callback's returned task is still pending, no matter how long that task takes.</summary>
    [Fact]
    public void InvokeTerminationSignal_WhenIsWindows_BlocksUntilCallbackTaskCompletes()
    {
        // Arrange
        var completionSource = new TaskCompletionSource();
        var entered = new ManualResetEventSlim(initialState: false);
        var completed = new ManualResetEventSlim(initialState: false);

        var blockingCall = new Thread(() =>
        {
            CooperativeShutdownSignals.InvokeTerminationSignal(
                () =>
                {
                    entered.Set();

                    return completionSource.Task;
                },
                isWindows: true);
            completed.Set();
        })
        {
            IsBackground = true,
        };

        // Act
        blockingCall.Start();
        entered.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();

        // Assert - nothing has completed the callback's task yet, so the call above must still be
        // blocked inside InvokeTerminationSignal regardless of scheduling: completed can only be
        // set after that call returns, which cannot happen before completionSource.Task does.
        Thread.Sleep(50);
        completed.IsSet.ShouldBeFalse();

        completionSource.SetResult();

        completed.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).ShouldBeTrue();
    }

    /// <summary>Verifies the non-Windows branch fires the callback without waiting for its
    /// returned task - Unix's existing behavior, unchanged by this fix.</summary>
    [Fact]
    public async Task InvokeTerminationSignal_WhenNotWindows_ReturnsWithoutWaitingForCallbackTaskAsync()
    {
        // Arrange - a task that never completes; if this branch waited on it, the call task below
        // would never complete and the WaitAsync below would time out and throw instead of
        // returning.
        var neverCompletes = new TaskCompletionSource().Task;

        // Act
        var call = Task.Run(
            () => CooperativeShutdownSignals.InvokeTerminationSignal(() => neverCompletes, isWindows: false),
            TestContext.Current.CancellationToken);

        // Assert - wait on the task itself rather than a side-channel signal set from inside the
        // lambda: a signal like that can be observed by this thread before the runtime has finished
        // transitioning the task object to the completed state, which would make the following
        // IsCompletedSuccessfully check flaky under scheduler pressure. Waiting on the task directly
        // cannot race, because WaitAsync only completes after the task has already reached a
        // terminal state.
        await call.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        call.IsCompletedSuccessfully.ShouldBeTrue();
    }

    /// <summary>Verifies a faulted callback task on the blocking Windows branch never escapes as an
    /// exception - the same "must never throw onto the signal-handling thread" contract every
    /// other path into a registered signal callback upholds.</summary>
    [Fact]
    public void InvokeTerminationSignal_WhenIsWindowsAndCallbackTaskFaults_DoesNotThrow()
    {
        // Arrange
        var faulted = new TaskCompletionSource();
        faulted.SetException(new InvalidOperationException("cleanup failed"));

        // Act & Assert
        Should.NotThrow(() =>
            CooperativeShutdownSignals.InvokeTerminationSignal(() => faulted.Task, isWindows: true));
    }
}
