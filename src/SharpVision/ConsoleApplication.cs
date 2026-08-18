// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using System.Runtime.InteropServices;

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
        ConsoleCancelEventHandler? onCancel = null;
        PosixSignalRegistration? interruptRegistration = null;
        PosixSignalRegistration? quitRegistration = null;
        PosixSignalRegistration? terminateRegistration = null;
        PosixSignalRegistration? hangupRegistration = null;

        void OnPosixSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            cancellation.Cancel();
        }

        if (!OperatingSystem.IsWindows())
        {
            // SIGTERM is the standard graceful-termination signal sent by process managers,
            // containers, systemd, and plain `kill`; SIGHUP is wired the same way alongside it.
            // Both are process-lifecycle signals, not Ctrl+C, so they are registered
            // unconditionally here rather than being folded into the `observeCtrlC` gate below:
            // `TreatControlCAsInput` only changes how Ctrl+C is delivered (decoded input vs. an
            // OS signal) and must not suppress cooperative shutdown on process-manager-initiated
            // termination.
            terminateRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnPosixSignal);
            hangupRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnPosixSignal);
        }

        if (observeCtrlC)
        {
            // Console.CancelKeyPress itself initializes the BCL's Unix console on first
            // subscription, which is exactly the side effect this registration exists to avoid: once
            // initialized, the runtime re-emits application-keypad-mode bytes on every later
            // child-process exit, including this host's own teardown. PosixSignalRegistration
            // observes Ctrl+C without that initialization. SIGQUIT is registered alongside SIGINT
            // because Console.CancelKeyPress historically fires for both, and a SIGINT-only
            // registration would let Ctrl+\ terminate the process without reverse cleanup.
            if (OperatingSystem.IsWindows())
            {
                onCancel = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += onCancel;
            }
            else
            {
                interruptRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnPosixSignal);
                quitRegistration = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, OnPosixSignal);
            }
        }

        // The registrations above must be live before `builder.Build()` runs, not just around the
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
            if (onCancel is not null)
            {
                Console.CancelKeyPress -= onCancel;
            }

            interruptRegistration?.Dispose();
            quitRegistration?.Dispose();
            terminateRegistration?.Dispose();
            hangupRegistration?.Dispose();
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
            : cancellation.IsCancellationRequested
                ? ConsoleRunStatus.Cancelled
                : ConsoleRunStatus.Completed;
    }
}
