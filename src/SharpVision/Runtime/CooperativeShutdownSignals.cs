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
/// <c>SIGTERM</c> and <c>SIGHUP</c> are registered unconditionally on Unix: they are the standard
/// graceful-termination signals sent by process managers, containers, systemd, and plain
/// <c>kill</c>, not Ctrl+C, so an <c>observeCtrlC</c>-equivalent gate must never
/// suppress them. Ctrl+C is observed through <see cref="PosixSignalRegistration"/>
/// (<see cref="PosixSignal.SIGINT"/> and <see cref="PosixSignal.SIGQUIT"/>) on Unix rather than
/// <see cref="Console.CancelKeyPress"/>: the first subscription to
/// <see cref="Console.CancelKeyPress"/> itself initializes the BCL's Unix console, which emits
/// <c>smkx</c> (application keypad mode) and leaves the runtime re-emitting it on every later
/// child-process exit, including this host's own teardown.
/// <see cref="PosixSignal.SIGQUIT"/> is registered alongside <see cref="PosixSignal.SIGINT"/>
/// because <see cref="Console.CancelKeyPress"/> historically fires for both, and a
/// <see cref="PosixSignal.SIGINT"/>-only registration would let Ctrl+\ terminate the process
/// without reverse cleanup. Windows has no equivalent side effect, so it keeps using
/// <see cref="Console.CancelKeyPress"/> directly, and has no unconditional registration standing in
/// for <c>SIGTERM</c>/<c>SIGHUP</c> yet.
/// </remarks>
internal sealed class CooperativeShutdownSignals: IDisposable
{
    private ConsoleCancelEventHandler? _onCancel;
    private PosixSignalRegistration? _interrupt;
    private PosixSignalRegistration? _quit;
    private PosixSignalRegistration? _terminate;
    private PosixSignalRegistration? _hangup;

    private CooperativeShutdownSignals()
    {
    }

    /// <summary>Registers the cooperative-shutdown signals.</summary>
    /// <param name="observeCtrlC">
    /// Whether Ctrl+C requests shutdown: <see cref="PosixSignal.SIGINT"/>/<see cref="PosixSignal.SIGQUIT"/>
    /// on Unix, <see cref="Console.CancelKeyPress"/> on Windows. <c>SIGTERM</c>/<c>SIGHUP</c> on Unix
    /// are always registered regardless of this value.
    /// </param>
    /// <param name="onSignal">
    /// The non-null callback invoked when a registered signal is observed. It may run on an
    /// arbitrary signal-handling thread and must be safe to call from one, and must not throw.
    /// </param>
    /// <returns>A scope that unregisters every signal it registered when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onSignal"/> is null.</exception>
    public static CooperativeShutdownSignals Register(bool observeCtrlC, Action onSignal)
    {
        ArgumentNullException.ThrowIfNull(onSignal);

        var scope = new CooperativeShutdownSignals();

        void OnPosixSignal(PosixSignalContext context)
        {
            context.Cancel = true;
            onSignal();
        }

        if (!OperatingSystem.IsWindows())
        {
            scope._terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnPosixSignal);
            scope._hangup = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnPosixSignal);
        }

        if (observeCtrlC)
        {
            if (OperatingSystem.IsWindows())
            {
                scope._onCancel = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    onSignal();
                };
                Console.CancelKeyPress += scope._onCancel;
            }
            else
            {
                scope._interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, OnPosixSignal);
                scope._quit = PosixSignalRegistration.Create(PosixSignal.SIGQUIT, OnPosixSignal);
            }
        }

        return scope;
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
