// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Runtime.InteropServices;

#pragma warning disable SYSLIB1054 // A two-method native boundary this small stays non-partial and explicit.

/// <summary>
/// Registers the Unix job-control signals - <c>SIGTSTP</c> and <c>SIGCONT</c> - that suspend and
/// resume a full-screen terminal session cleanly: a caught Ctrl+Z restores cooked mode and every
/// leased terminal mode before the process actually stops, and <c>fg</c> re-enters raw mode,
/// replays those leases, and lets the caller force a repaint once the shell resumes it.
/// </summary>
/// <remarks>
/// <para>
/// This is a deliberately separate registration from the hosting assembly's own
/// <c>CooperativeShutdownSignals</c> (Ctrl+C/SIGTERM/SIGHUP): that type's callback shape - cancel a
/// pending shutdown task, run fire-and-forget or block on a returned <see cref="Task"/> - fits
/// one-shot termination, not the synchronous restore-then-actually-stop-then-reverse-on-resume
/// sequence job control needs. Job control is also inherently Unix-terminal-specific (there is no
/// Windows equivalent to catch), unlike the shutdown signals, which are also observed, differently,
/// on Windows.
/// </para>
/// <para>
/// Leaving <see cref="PosixSignalContext.Cancel"/> false and relying on the runtime to let the OS
/// default disposition run afterward - the pattern <c>CooperativeShutdownSignals</c> uses for
/// termination signals - does not work here. The .NET runtime's native signal PAL
/// (<c>SystemNative_HandleNonCanceledPosixSignal</c> in <c>pal_signal.c</c>) only re-raises a
/// signal through <c>kill()</c> for signals whose default disposition is Terminate; for every
/// Stop-disposition signal (<c>SIGTSTP</c>, <c>SIGTTIN</c>, <c>SIGTTOU</c>) and for
/// <c>SIGCONT</c>, an uncancelled registration is a documented no-op in that native handler once
/// any managed handler is registered for the signal. Left alone, a <c>SIGTSTP</c> registration
/// with <c>Cancel = false</c> would restore cooked mode and every leased terminal mode and then
/// never actually stop the process at all.
/// </para>
/// <para>
/// Both registrations here therefore set <c>Cancel = true</c> and this type raises <c>SIGSTOP</c>
/// on the whole process itself, once the suspend callback's synchronous restore work finishes -
/// the same technique long-lived POSIX programs (shells, pagers, terminal multiplexers) use to
/// implement a catchable Ctrl+Z. A stop signal a process sends to itself is delivered synchronously
/// enough, as part of the same <c>kill()</c>/<c>raise()</c> call, that nothing after it runs before
/// a later <c>SIGCONT</c> actually continues the process - this is standard, portable POSIX signal
/// behavior (stop and continue dispositions act on every thread of the process, not just the
/// calling one), not a .NET-specific guarantee, so it holds regardless of which thread the runtime
/// happens to invoke this type's <c>SIGTSTP</c> callback on.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal sealed class JobControlSignals: IDisposable
{
    private PosixSignalRegistration? _suspend;
    private PosixSignalRegistration? _resume;

    private JobControlSignals()
    {
    }

    /// <summary>Registers the SIGTSTP/SIGCONT job-control handlers.</summary>
    /// <param name="onSuspend">
    /// The non-null callback run synchronously on SIGTSTP, before the process actually stops -
    /// expected to restore cooked termios and unwind leased terminal modes. Must be safe to call
    /// from an arbitrary signal-handling thread; any exception it raises is swallowed so the
    /// process still stops.
    /// </param>
    /// <param name="onResume">
    /// The non-null callback run synchronously on SIGCONT, after the shell resumes the process -
    /// expected to re-enter raw mode, replay leases, and request a repaint. Must be safe to call
    /// from an arbitrary signal-handling thread; any exception it raises is swallowed.
    /// </param>
    /// <returns>A scope that unregisters both handlers when disposed.</returns>
    /// <exception cref="ArgumentNullException">A required callback is null.</exception>
    public static JobControlSignals Register(Action onSuspend, Action onResume) =>
        Register(onSuspend, onResume, RaiseStop);

    /// <summary>
    /// Registers with an injected stop-raising boundary, so a test can verify the suspend callback
    /// always finishes, and in what order relative to the stop, without a real <c>SIGSTOP</c> ever
    /// reaching the test process - see <see cref="InvokeSuspend"/>, the pure static helper this
    /// registration path delegates to.
    /// </summary>
    /// <param name="onSuspend">The non-null suspend callback - see <see cref="Register(Action, Action)"/>.</param>
    /// <param name="onResume">The non-null resume callback - see <see cref="Register(Action, Action)"/>.</param>
    /// <param name="raiseStop">The non-null boundary that stops the process once <paramref name="onSuspend"/> finishes.</param>
    /// <returns>A scope that unregisters both handlers when disposed.</returns>
    /// <exception cref="ArgumentNullException">A required delegate is null.</exception>
    internal static JobControlSignals Register(Action onSuspend, Action onResume, Action raiseStop)
    {
        ArgumentNullException.ThrowIfNull(onSuspend);
        ArgumentNullException.ThrowIfNull(onResume);
        ArgumentNullException.ThrowIfNull(raiseStop);

        var scope = new JobControlSignals
        {
            _suspend = PosixSignalRegistration.Create(PosixSignal.SIGTSTP, context =>
            {
                // See the type remarks: SIGTSTP's default disposition is a no-op in the runtime's
                // own native handler once any managed handler is registered, so Cancel here is
                // purely documentation of intent, not what actually stops the process - RaiseStop
                // is.
                context.Cancel = true;
                InvokeSuspend(onSuspend, raiseStop);
            }),
            _resume = PosixSignalRegistration.Create(PosixSignal.SIGCONT, context =>
            {
                context.Cancel = true;
                InvokeResume(onResume);
            })
        };

        return scope;
    }

    /// <summary>
    /// Runs the suspend callback and then raises the stop signal, swallowing any exception from
    /// either step - extracted so a test can assert the exact ordering and the "never throw onto
    /// the signal-handling thread" contract without any real <see cref="PosixSignalRegistration"/>
    /// or process-stopping signal involved.
    /// </summary>
    /// <param name="onSuspend">The non-null suspend callback.</param>
    /// <param name="raiseStop">The non-null boundary that stops the process.</param>
    internal static void InvokeSuspend(Action onSuspend, Action raiseStop)
    {
        try
        {
            onSuspend();
        }
        catch
        {
            // Must never throw onto the signal-handling thread. A failed restore is still followed
            // by the actual stop below - there is nothing more useful this callback can do with the
            // failure, and the caller's own state (Session.LastSuspendException) already recorded
            // whatever this callback's own lease-unwind step could attribute.
        }

        try
        {
            raiseStop();
        }
        catch
        {
            // Same contract as above.
        }
    }

    /// <summary>Runs the resume callback, swallowing any exception it raises.</summary>
    /// <param name="onResume">The non-null resume callback.</param>
    internal static void InvokeResume(Action onResume)
    {
        try
        {
            onResume();
        }
        catch
        {
            // Must never throw onto the signal-handling thread - see the type remarks.
        }
    }

    // SIGSTOP is not a PosixSignal member - it can never be caught, blocked, or ignored, so there
    // is nothing to register a handler for, only a reason to raise it. Its numeric value differs
    // by platform: 19 on every Linux architecture this library targets (x86, x86-64, arm, arm64,
    // riscv64, s390x, loongarch64 and ppc64le all share the generic Linux signal numbering - only
    // alpha, mips, parisc and sparc, none of which this library supports, renumber signals), and 17
    // on macOS/BSD (<sys/signal.h>: "#define SIGSTOP 17").
    private const int _linuxSigStop = 19;
    private const int _macOsSigStop = 17;

    private static void RaiseStop() => _ = Kill(GetPid(), OperatingSystem.IsMacOS() ? _macOsSigStop : _linuxSigStop);

    [DllImport("libc", EntryPoint = "kill", ExactSpelling = true, SetLastError = true)]
    private static extern int Kill(int processId, int signal);

    [DllImport("libc", EntryPoint = "getpid", ExactSpelling = true)]
    private static extern int GetPid();

    /// <summary>Unregisters both signal handlers. Safe to call more than once.</summary>
    public void Dispose()
    {
        _suspend?.Dispose();
        _suspend = null;
        _resume?.Dispose();
        _resume = null;
    }
}

#pragma warning restore SYSLIB1054
