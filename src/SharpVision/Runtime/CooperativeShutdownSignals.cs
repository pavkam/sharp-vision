// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using System.Runtime.InteropServices;

/// <summary>
/// Registers the process-lifecycle signals that drive cooperative shutdown - Ctrl+C, <c>SIGTERM</c>,
/// and <c>SIGHUP</c> - and unregisters them exactly once on disposal.
/// </summary>
/// <remarks>
/// Shared by <see cref="ConsoleApplication"/>'s managed hosting entry points and by
/// <see cref="Application"/>'s own lifecycle, so both observe signals through the identical
/// mechanism instead of two independently maintained copies of the same tricky platform logic.
/// <c>SIGTERM</c> and <c>SIGHUP</c> are registered unconditionally on every platform, including
/// Windows: they are the standard graceful-termination signals sent by process managers,
/// containers, systemd, and plain <c>kill</c> on Unix, not Ctrl+C, so an
/// <c>observeCtrlC</c>-equivalent gate must never suppress them. Ctrl+C is observed through
/// <see cref="PosixSignalRegistration"/> (<see cref="PosixSignal.SIGINT"/> and
/// <see cref="PosixSignal.SIGQUIT"/>) on Unix rather than <see cref="Console.CancelKeyPress"/>: the
/// first subscription to <see cref="Console.CancelKeyPress"/> itself initializes the BCL's Unix
/// console, which emits <c>smkx</c> (application keypad mode) and leaves the runtime re-emitting it
/// on every later child-process exit, including this host's own teardown.
/// <see cref="PosixSignal.SIGQUIT"/> is registered alongside <see cref="PosixSignal.SIGINT"/>
/// because <see cref="Console.CancelKeyPress"/> historically fires for both, and a
/// <see cref="PosixSignal.SIGINT"/>-only registration would let Ctrl+\ terminate the process
/// without reverse cleanup. Windows has no equivalent side effect, so it keeps using
/// <see cref="Console.CancelKeyPress"/> directly for Ctrl+C.
/// <para>
/// On Windows, the BCL maps <see cref="PosixSignal.SIGTERM"/> and <see cref="PosixSignal.SIGHUP"/>
/// to the console control events <c>CTRL_SHUTDOWN_EVENT</c> and <c>CTRL_CLOSE_EVENT</c> - a console
/// window closing or the system shutting down. Unlike Unix, which waits indefinitely for cleanup
/// once <see cref="PosixSignalContext.Cancel"/> is set, Windows kills the process once every
/// registered console-control handler has returned (or after about five seconds, whichever comes
/// first), so the <c>SIGTERM</c>/<c>SIGHUP</c> callback on Windows blocks synchronously until the
/// requested stop actually finishes instead of returning immediately - see
/// <see cref="InvokeTerminationSignal"/>. <c>CTRL_LOGOFF_EVENT</c> (user logoff) is deliberately not
/// handled: Microsoft documents it as delivered only to services, never to interactive console
/// applications, which is what this library targets, so there is no event to observe here.
/// </para>
/// </remarks>
internal sealed class CooperativeShutdownSignals: IDisposable
{
    private ConsoleCancelEventHandler? _onCancel;
    private IDisposable? _interrupt;
    private IDisposable? _quit;
    private IDisposable? _terminate;
    private IDisposable? _hangup;

    private CooperativeShutdownSignals()
    {
    }

    /// <summary>Registers the cooperative-shutdown signals.</summary>
    /// <param name="observeCtrlC">
    /// Whether Ctrl+C requests shutdown: <see cref="PosixSignal.SIGINT"/>/<see cref="PosixSignal.SIGQUIT"/>
    /// on Unix, <see cref="Console.CancelKeyPress"/> on Windows. <c>SIGTERM</c>/<c>SIGHUP</c> are
    /// always registered regardless of this value, on every platform.
    /// </param>
    /// <param name="onSignal">
    /// The non-null callback invoked when a registered signal is observed, returning the task that
    /// represents the requested stop finishing. It may run on an arbitrary signal-handling thread
    /// and must be safe to call from one, and the call itself - up to returning that task - must not
    /// throw. On Windows, the <c>SIGTERM</c>/<c>SIGHUP</c> registration blocks synchronously on the
    /// returned task instead of firing it fire-and-forget; see <see cref="InvokeTerminationSignal"/>.
    /// </param>
    /// <returns>A scope that unregisters every signal it registered when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSignal"/> is null.</exception>
    public static CooperativeShutdownSignals Register(bool observeCtrlC, Func<Task> onSignal) =>
        Register(observeCtrlC, onSignal, CreatePosixSignalRegistration);

    /// <summary>
    /// Registers with an injected registration factory in place of <see cref="PosixSignalRegistration.Create"/>,
    /// so a test can make any one signal's registration fail without touching a real signal, and
    /// assert that every earlier registration is unwound rather than leaked.
    /// </summary>
    /// <param name="observeCtrlC">See <see cref="Register(bool, Func{Task})"/>.</param>
    /// <param name="onSignal">See <see cref="Register(bool, Func{Task})"/>.</param>
    /// <param name="createRegistration">
    /// The non-null factory invoked once per Unix signal, in place of <see cref="PosixSignalRegistration.Create"/>.
    /// </param>
    /// <returns>A scope that unregisters every signal it registered when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSignal"/> or <paramref name="createRegistration"/> is null.</exception>
    internal static CooperativeShutdownSignals Register(
        bool observeCtrlC,
        Func<Task> onSignal,
        Func<PosixSignal, Action<PosixSignalContext>, IDisposable> createRegistration)
    {
        ArgumentNullException.ThrowIfNull(onSignal);
        ArgumentNullException.ThrowIfNull(createRegistration);

        var scope = new CooperativeShutdownSignals();

        void OnPosixSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            _ = onSignal();
        }

        void OnPosixTerminationSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            InvokeTerminationSignal(onSignal, OperatingSystem.IsWindows());
        }

        try
        {
            scope._terminate = createRegistration(PosixSignal.SIGTERM, OnPosixTerminationSignal);
            scope._hangup = createRegistration(PosixSignal.SIGHUP, OnPosixTerminationSignal);

            if (observeCtrlC)
            {
                if (OperatingSystem.IsWindows())
                {
                    scope._onCancel = (_, eventArgs) =>
                    {
                        eventArgs.Cancel = true;
                        _ = onSignal();
                    };
                    Console.CancelKeyPress += scope._onCancel;
                }
                else
                {
                    scope._interrupt = createRegistration(PosixSignal.SIGINT, OnPosixSignal);
                    scope._quit = createRegistration(PosixSignal.SIGQUIT, OnPosixSignal);
                }
            }
        }
        catch
        {
            // Unwind whatever was already registered before the failing create, mirroring
            // UnixConsoleHost.Open's reverse-order unwind for partial construction - otherwise an
            // earlier registration's native handler leaks for the process lifetime with no scope
            // left to dispose it.
            scope.Dispose();

            throw;
        }

        return scope;
    }

    /// <summary>The production registration factory: <see cref="PosixSignalRegistration.Create"/> unchanged.</summary>
    /// <param name="signal">The signal to register a handler for.</param>
    /// <param name="handler">The handler to invoke when the signal is observed.</param>
    /// <returns>The registration created for <paramref name="signal"/>.</returns>
    private static PosixSignalRegistration CreatePosixSignalRegistration(
        PosixSignal signal, Action<PosixSignalContext> handler) =>
        PosixSignalRegistration.Create(signal, handler);

    /// <summary>
    /// Invokes a <c>SIGTERM</c>/<c>SIGHUP</c> callback, blocking synchronously until its returned
    /// task completes when <paramref name="isWindows"/> is true, and firing it without waiting
    /// otherwise - matching Unix's existing non-blocking behavior exactly. Extracted out of
    /// <see cref="Register(bool, Func{Task})"/> so the blocking branch has a deterministic unit test
    /// regardless of the platform the test process actually runs on, and without any real signal
    /// plumbing.
    /// </summary>
    /// <param name="onSignal">The non-null callback to invoke.</param>
    /// <param name="isWindows">Whether to block on the callback's returned task.</param>
    /// <remarks>
    /// See the type remarks for why Windows needs this: it kills the process once every registered
    /// console-control handler has returned (or after about five seconds), unlike Unix, which waits
    /// indefinitely once <see cref="PosixSignalContext.Cancel"/> is set. Any exception the awaited
    /// task raises is swallowed rather than rethrown - like every other path into a registered
    /// signal callback, this must never throw onto the signal-handling thread, and by the time a
    /// termination signal has fired there is nothing further to do with a cleanup failure here.
    /// </remarks>
    internal static void InvokeTerminationSignal(Func<Task> onSignal, bool isWindows)
    {
        if (!isWindows)
        {
            _ = onSignal();

            return;
        }

        try
        {
            onSignal().GetAwaiter().GetResult();
        }
        catch
        {
            // See the remarks above: swallowed deliberately, this callback must never throw onto
            // the signal-handling thread.
        }
    }

    /// <summary>Unregisters every signal this scope registered. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_onCancel is { } onCancel)
        {
            Console.CancelKeyPress -= onCancel;
            _onCancel = null;
        }

        _interrupt?.Dispose();
        _interrupt = null;
        _quit?.Dispose();
        _quit = null;
        _terminate?.Dispose();
        _terminate = null;
        _hangup?.Dispose();
        _hangup = null;
    }
}
