// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies the two halves of the lifecycle-state contract that a throwing handler used to
/// break: the stopping and stopped flags stay monotonic, and a lifecycle callback cannot skip the
/// state transition that follows it.
///
/// <para>The flags each had exactly one writer, but <c>FinalizeStopped</c> - the sole writer of
/// <c>_stopped</c> - was reachable from two callers that never passed through <c>BeginStopping</c>,
/// the sole writer of <c>_stopping</c>. That produced "stopped but not stopping", which the
/// lifecycle model cannot express: the host's follow-up <c>StopAsync</c> then read it as
/// not-yet-stopping and raised <c>Stopping</c> after <c>Stopped</c>.</para>
///
/// <para>Separately, the shutdown path already raised its callbacks through <c>CaptureCleanup</c>,
/// but the startup and frame paths raised theirs bare. A handler throwing there skipped the very
/// next line - and because <c>MarkStarted</c> latches before it invokes, the startup completion
/// source could be stranded with no second chance to settle it.</para>
/// </summary>
public sealed partial class ApplicationTests
{
    /// <summary>The ordering regression this file exists to pin. A Starting handler that throws
    /// takes the terminal path without <c>BeginStopping</c>, and the host's guarded StopAsync must
    /// not then raise Stopping against an application that has already stopped.</summary>
    [Fact]
    public async Task StopAsync_AfterAStartingHandlerThrew_DoesNotRaiseStoppingAfterStoppedAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<string> order = [];
        application.Starting += (_, _) => throw new InvalidOperationException("starting-boom");
        application.Stopping += (_, _) => order.Add("stopping");
        application.Stopped += (_, _) => order.Add("stopped");

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        // Exactly what ConsoleApplication.RunApplicationAsync does with the rethrow. It must now
        // reach StopAsync's documented rethrow rather than detouring through a second
        // BeginStopping - previously a Stopping handler setting Cancel here returned early and
        // silently suppressed the error the host was about to report.
        var rethrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(CancellationToken.None));

        rethrown.Message.ShouldBe("starting-boom");
        order.ShouldBe(["stopped"]);
    }

    /// <summary>Verifies the second, permanent form of the same divergent state: disposing an
    /// application that was never started also reaches the terminal path directly.</summary>
    [Fact]
    public async Task DisposeAsync_WhenTheApplicationNeverStarted_DoesNotRaiseStoppingAfterStoppedAsync()
    {
        await using FakeTerminal terminal = new();
        Application application = new(new ProbeControl(), terminal, terminal, TerminalOptions.Minimal);
        List<string> order = [];
        application.Stopping += (_, _) => order.Add("stopping");
        application.Stopped += (_, _) => order.Add("stopped");

        await application.DisposeAsync();

        order.ShouldBe(["stopped"]);
    }

    /// <summary>Verifies the guard that documents "stopping or stopped" now rejects a stopped
    /// application, which it could not while the two flags were able to diverge.</summary>
    [Fact]
    public async Task RefreshScreen_WhenCalledFromAStoppedHandler_ThrowsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        Exception? observed = null;
        application.Starting += (_, _) => throw new InvalidOperationException("starting-boom");
        application.Stopped += (sender, _) =>
        {
            try
            {
                ((Application) sender!).RefreshScreen();
            }
            catch (Exception exception)
            {
                observed = exception;
            }
        };

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StartAsync(TestContext.Current.CancellationToken));

        _ = observed.ShouldBeOfType<ObjectDisposedException>();
    }

    /// <summary>The unrecoverable half of the isolation regression. <c>MarkStarted</c> latches
    /// before it invokes and <c>TrySetResult</c> has no other caller, so a throwing Started handler
    /// used to strand StartAsync on an application that was otherwise fully live.</summary>
    [Fact]
    public async Task StartAsync_WhenAStartedHandlerThrowsAndIsHandled_StillReturnsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<Exception> reported = [];
        application.UnhandledException += (_, eventArgs) =>
        {
            reported.Add(eventArgs.Exception);
            eventArgs.IsHandled = true;
        };
        application.Started += (_, _) => throw new InvalidOperationException("started-boom");

        // Without isolation this never completes: _started is stranded, and because the handler
        // marked the failure handled, _completion never settles either.
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);

        reported.Select(static exception => exception.Message).ShouldContain("started-boom");

        // Handling the failure lets the application keep running but does not erase Failure
        // (docs/architecture/error-handling.md), so StopAsync still surfaces it. What this test
        // pins is that StartAsync returned at all.
        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("started-boom");
    }

    /// <summary>Pins the ordering inside the isolation: the Started-handler failure is reported
    /// through UnhandledException BEFORE StartAsync's awaiter is released. Settling first raced
    /// the report against the caller's continuation, so a test (or application) reading the
    /// failure list right after StartAsync returned could observe it mid-write - the assertion
    /// above flaked exactly that way. The deliberate pause inside the handler makes the inverted
    /// order fail dependably rather than one run in three: with the settle first, StartAsync
    /// resumes during the pause and the flag is already set by the time the handler samples it.</summary>
    [Fact]
    public async Task StartAsync_WhenAStartedHandlerThrows_ReportsTheFailureBeforeCompletingAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);

        var startReturned = false;
        var sampledAfterReturn = new List<bool>();
        application.UnhandledException += (_, eventArgs) =>
        {
            Thread.Sleep(50);
            sampledAfterReturn.Add(Volatile.Read(ref startReturned));
            eventArgs.IsHandled = true;
        };
        application.Started += (_, _) => throw new InvalidOperationException("started-boom");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);
        Volatile.Write(ref startReturned, true);

        sampledAfterReturn.ShouldBe([false]);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("started-boom");
    }

    /// <summary>Verifies the self-healing half: a throwing FrameRendered handler must not suppress
    /// the invalidation pump behind it, so an ordinary Invalidate still produces a frame.</summary>
    [Fact]
    public async Task Invalidate_WhenAFrameRenderedHandlerThrowsAndIsHandled_StillRendersTheNextFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        var control = new ProbeControl();
        await using Application application = new(control, terminal, terminal, TerminalOptions.Minimal);
        var frames = 0;
        int? threshold = null;
        var exceededThreshold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
        application.FrameRendered += (_, _) =>
        {
            frames++;

            if (threshold is { } value && frames > value)
            {
                _ = exceededThreshold.TrySetResult();
            }

            throw new InvalidOperationException("frame-boom");
        };

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);
        var afterStart = frames;
        threshold = afterStart;

        await application.Dispatcher.InvokeAsync(
            () => control.Invalidate(Invalidation.Render),
            TestContext.Current.CancellationToken);
        await exceededThreshold.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        frames.ShouldBeGreaterThan(afterStart, "the pump behind a throwing handler must still run");
        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies the suspended-startup path, which reaches MarkStarted through the Resize
    /// callback instead - the same hang with FrameRendered never involved.</summary>
    [Fact]
    public async Task StartAsync_WhenAResizeHandlerThrowsOnASuspendedStart_StillReturnsAsync()
    {
        await using FakeTerminal terminal = new();

        // A zero axis is the suspended branch: layout runs, no frame is produced, and MarkStarted
        // is reached from DrainResize rather than from CompleteRender.
        terminal.QueueResize(new Dimensions(new Size(0, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        application.UnhandledException += (_, eventArgs) => eventArgs.IsHandled = true;
        application.Resize += (_, _) => throw new InvalidOperationException("resize-boom");

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await application.StartAsync(timeout.Token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            async () => await application.StopAsync(TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("resize-boom");
    }

    /// <summary>The counter-case that keeps the isolation honest: with no handler throwing, the
    /// documented frame-before-started ordering is unchanged.</summary>
    [Fact]
    public async Task StartAsync_WhenNoHandlerThrows_StillRaisesStartedAfterTheFirstFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(20, 6)));
        await using Application application = new(
            new ProbeControl(),
            terminal,
            terminal,
            TerminalOptions.Minimal);
        List<string> order = [];
        application.FrameRendered += (_, _) => order.Add("frame");
        application.Started += (_, _) => order.Add("started");

        await application.StartAsync(TestContext.Current.CancellationToken);

        order.IndexOf("frame").ShouldBeLessThan(order.IndexOf("started"));
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
