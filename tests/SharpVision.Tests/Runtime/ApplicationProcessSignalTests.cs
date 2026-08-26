// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Reflection;

/// <summary>
/// Verifies that an <see cref="Application"/> constructed with <c>observeProcessSignals: true</c> -
/// exactly what <see cref="ConsoleApplicationBuilder.Build"/> now passes whenever
/// <c>TreatControlCAsInput</c> is false - reaches cooperative shutdown through the same private
/// <c>RequestCooperativeStop</c> callback a real signal invokes, with no
/// <c>ConsoleApplication.RunCoreAsync</c> wrapping it at all. This is the bare
/// <c>Application app = ConsoleApplication.CreateBuilder(screen).Build(); await app.RunAsync();</c>
/// shape documented in docs/concepts/hosting.md, which previously had zero signal handling: every
/// <c>PosixSignalRegistration</c>/<c>Console.CancelKeyPress</c> subscription lived exclusively inside
/// <c>RunCoreAsync</c>, which this shape never reaches.
/// </summary>
/// <remarks>
/// These tests drive the private <c>RequestCooperativeStop</c> method directly via reflection
/// instead of raising a real POSIX signal against the current process:
/// <see cref="ConsoleApplicationTests.RunCoreAsync_WhenRealPosixSignalRaised_StopsCleanlyAsync(int)"/>
/// already proves, end-to-end, that <c>CooperativeShutdownSignals.Register</c> - the exact same
/// shared type <c>Application</c>'s constructor calls - wires a real signal to its callback; raising
/// another real signal here would only re-prove that shared plumbing while adding process-wide
/// signal traffic that Microsoft.Testing.Platform's own graceful-cancellation signal handling can
/// mistake for a request to abort the whole test run, since every live registration for a given
/// signal number is invoked regardless of which test raised it. What genuinely differs per test here
/// is <c>Application</c>'s own reaction once its callback fires - the pre-start latch and the
/// post-start <c>StopAsync</c> integration - and reflection exercises the real method precisely and
/// deterministically without that risk. Registration itself is still checked, via the private
/// <c>_processSignals</c> field, so "did construction actually register" stays covered.
/// </remarks>
public sealed class ApplicationProcessSignalTests
{
    /// <summary>Verifies a signal delivered after the application is fully live reaches the guarded
    /// <c>StopAsync</c> path and restores the host lease.</summary>
    [Fact]
    public async Task RequestCooperativeStop_WhenCalledAfterStart_StopsCleanlyAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var hostLease = new ProcessSignalRestoreLeaseProbe();
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal,
            hostLease: hostLease,
            // Exactly what ConsoleApplicationBuilder.Build() passes as !TreatControlCAsInput when
            // that option is left at its false default.
            observeProcessSignals: true);
        _ = GetProcessSignals(application).ShouldNotBeNull();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.Started += (_, _) => started.TrySetResult();

        // Act
        var running = application.RunAsync(TestContext.Current.CancellationToken);
        await started.Task.WaitAsync(TestContext.Current.CancellationToken);

        InvokeRequestCooperativeStop(application);
        await running.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        hostLease.Disposals.ShouldBe(1);

        await application.DisposeAsync();
    }

    /// <summary>Verifies a signal delivered before <c>StartAsync</c> is ever called - standing in for
    /// one landing during <c>Build()</c>'s synchronous <c>OnAttach</c>, which runs before the caller
    /// can reach <c>StartAsync</c> at all - still resolves cleanly through
    /// <c>RunAsync(CancellationToken)</c> instead of being lost. This is the one gap the bare
    /// <c>Build()</c> + <c>app.RunAsync()</c> shape has that <c>ConsoleApplication.RunCoreAsync</c>'s
    /// own two entry points do not (they pre-cancel a linked token before <c>Build()</c> even runs);
    /// closing it for the window from construction onward is exactly what this fix adds.</summary>
    [Fact]
    public async Task RequestCooperativeStop_WhenCalledBeforeStart_StopsWithoutEverGoingLiveAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var hostLease = new ProcessSignalRestoreLeaseProbe();
        var application = new Application(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal,
            hostLease: hostLease,
            observeProcessSignals: true);

        // Act - the signal lands before RunAsync (and therefore StartAsync) is ever called.
        InvokeRequestCooperativeStop(application);

        await application.RunAsync(TestContext.Current.CancellationToken)
            .WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        // Assert
        application.Completion.IsCompletedSuccessfully.ShouldBeTrue();
        hostLease.Disposals.ShouldBe(1);

        await application.DisposeAsync();
    }

    /// <summary>Verifies the compatible default: direct construction with no
    /// <c>observeProcessSignals</c> argument registers nothing, so a real signal reaches the OS
    /// default disposition exactly as it always has for every other test in this suite that
    /// constructs an <c>Application</c> directly. This is what keeps the fix from hijacking process
    /// signals for unrelated embedders and for this test process itself.</summary>
    [Fact]
    public async Task RunAsync_WhenObserveProcessSignalsOmitted_RegistersNoSignalHandlingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        GetProcessSignals(application).ShouldBeNull();

        // There is no observable signal-registration surface to assert against directly beyond the
        // field above, so this also pins the one thing that would differ if registration happened
        // anyway: ordinary StartAsync/StopAsync behaves exactly as every other direct-construction
        // test expects, with no extra Stopping/Stopped activity from a signal hook that should not
        // exist.
        List<string> order = [];
        application.Stopping += (_, _) => order.Add("stopping");
        application.Stopped += (_, _) => order.Add("stopped");

        await application.StartAsync(TestContext.Current.CancellationToken);
        await application.StopAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(["stopping", "stopped"]);
    }

    private static object? GetProcessSignals(Application application) =>
        typeof(Application)
            .GetField("_processSignals", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(application);

    private static void InvokeRequestCooperativeStop(Application application) =>
        typeof(Application)
            .GetMethod("RequestCooperativeStop", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(application, null);

    /// <summary>Records host-lease disposal as a proxy for "terminal mode restored" - the same role
    /// <c>ConsoleApplicationRestoreLease</c> plays for the <c>ConsoleApplicationBuilder</c>-driven
    /// tests, adapted to the <c>IAsyncDisposable</c> shape <c>Application</c>'s own <c>hostLease</c>
    /// constructor parameter expects.</summary>
    private sealed class ProcessSignalRestoreLeaseProbe: IAsyncDisposable
    {
        internal int Disposals { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
}
