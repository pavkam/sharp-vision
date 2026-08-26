// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using SharpVision.Runtime;

/// <summary>Provides the fluent entry point for interactive console applications.</summary>
[PublicAPI]
public static class ConsoleApplication
{
    /// <summary>Creates a builder for one detached screen.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <returns>A fluent builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ConsoleApplicationBuilder CreateBuilder(Screen screen) => new(screen);

    /// <summary>Configures and runs an interactive console application.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="configure">Optional fluent configuration.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="screen"/> is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(
        Screen screen,
        Action<ConsoleApplicationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(screen);
        var builder = new ConsoleApplicationBuilder(screen);
        configure?.Invoke(builder);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    /// <summary>Runs an interactive console application with prebuilt options.</summary>
    /// <param name="screen">The non-null detached screen.</param>
    /// <param name="options">The non-null run options.</param>
    /// <returns>The run status.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public static ValueTask<ConsoleRunStatus> RunAsync(Screen screen, ConsoleRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(options);
        var builder = new ConsoleApplicationBuilder(screen).ConfigureOptions(_ => options);
        return RunCoreAsync(builder, CancellationToken.None);
    }

    internal static async ValueTask<ConsoleRunStatus> RunCoreAsync(
        ConsoleApplicationBuilder builder,
        CancellationToken cancellationToken)
    {
        if (!builder.IsInteractive)
        {
            if (builder.Options.RedirectedMessage is { Length: > 0 } message)
            {
                builder.WriteLine(message);
            }

            return ConsoleRunStatus.Redirected;
        }

        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var observeCtrlC = !builder.Options.TreatControlCAsInput;

        // CooperativeShutdownSignals owns the actual SIGINT/SIGQUIT/SIGTERM/SIGHUP/CancelKeyPress
        // registration - the same mechanism Application's own StartAsync uses for the bare
        // `Build()` + `app.RunAsync()` shape - so the tricky BCL-initialization-avoidance logic
        // documented on that type is never duplicated here.
        //
        // The callback cannot be the bare `cancellation.Cancel` method group: Register's contract
        // requires onSignal to never throw, but a signal can still be in flight on its own thread
        // when this method's `using var cancellation` disposes it below - Dispose does not wait for
        // a concurrently-running callback - and Cancel() on an already-disposed source throws
        // ObjectDisposedException, which is fatal to the whole process from a signal-handling
        // thread. Once teardown has started, the token no longer matters, so this is a plain no-op.
        var signals = CooperativeShutdownSignals.Register(observeCtrlC, () =>
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        });

        // The registration above must be live before `builder.Build()` runs, not just around the
        // post-build run loop: `Build()` enters raw/VT terminal mode and then synchronously runs
        // arbitrary user `OnAttach` code, and a signal landing in that window has to be observed
        // here rather than falling through to the OS default disposition (process killed) before
        // the terminal-mode restore lease inside `Build()` ever has a chance to run. If a signal
        // fires while `Build()` is blocked inside synchronous `OnAttach` user code, this cancels
        // the linked token and prevents the process kill, but cannot abort `Build()` early -
        // aborting arbitrary user code mid-execution would be unsafe. `RunApplicationAsync` then
        // observes the already-cancelled token exactly as it does for a caller-supplied
        // pre-cancelled token, driving the same guarded `StopAsync` restoration path.
        try
        {
            Application application;

            try
            {
                application = builder.Build();
            }
            catch (UnsupportedTerminalException)
            {
                if (builder.Options.UnsupportedTerminalMessage is { Length: > 0 } message)
                {
                    builder.WriteLine(message);
                }

                return ConsoleRunStatus.UnsupportedTerminal;
            }

            await using (application)
            {
                return await RunApplicationAsync(application, builder, cancellation)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            signals.Dispose();
        }
    }

    private static async ValueTask<ConsoleRunStatus> RunApplicationAsync(
        Application application,
        ConsoleApplicationBuilder builder,
        CancellationTokenSource cancellation)
    {
        try
        {
            await application.StartAsync(cancellation.Token).ConfigureAwait(false);
            var completion = application.Completion;
            var winner = await Task.WhenAny(
                completion,
                Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token)).ConfigureAwait(false);

            // Task.WhenAny never adopts the winning task's status; a post-startup application
            // fault on Completion must be awaited here to surface it, or it is lost and this
            // method falls through to the guarded StopAsync call below. When the
            // delay wins instead, cancellation is already handled by the checks after the
            // try/catch, so Completion is left unobserved.
            if (ReferenceEquals(winner, completion))
            {
                await completion.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            return ConsoleRunStatus.Cancelled;
        }
        catch (Exception exception)
        {
            try
            {
                await application.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Suppress cleanup errors so the original exception propagates.
            }

            builder.WriteErrorLine(exception.ToString());
            return ConsoleRunStatus.Failed;
        }

        // The signal-unregistration finally lives in the caller (RunCoreAsync) now, wrapping both
        // `Build()` and this whole method, so a restoration failure here propagates exactly as it
        // did before rather than being caught and driving a second StopAsync. The whole reverse
        // restoration runs here - renderer VT cleanup, the session's mode-lease walk, host-lease
        // disposal, the termios restore.
        await application.StopAsync(CancellationToken.None).ConfigureAwait(false);

        return application.Failure is not null
            ? ConsoleRunStatus.Failed
            : cancellation.IsCancellationRequested || application.StopRequestedBySignal
                ? ConsoleRunStatus.Cancelled
                : ConsoleRunStatus.Completed;
    }
}
