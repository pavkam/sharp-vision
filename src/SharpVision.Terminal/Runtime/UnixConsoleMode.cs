// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Owns one best-effort Unix terminal raw-input lease for interactive console hosts.</summary>
internal sealed class UnixConsoleMode: IDisposable
{
    private readonly byte[]? _restore;
    private readonly int _fileDescriptor;
    private readonly Func<int, byte[], bool> _setAttributes;
    private readonly Func<int, byte[], bool> _restoreAttributes;
    private readonly bool _captureControlKeys;
    private int _disposed;

    /// <summary>Initializes one lease with an optional captured terminal restoration state.</summary>
    /// <param name="restore">The captured termios state, or null when the host is unsupported.</param>
    /// <param name="fileDescriptor">The descriptor the lease reads and restores.</param>
    /// <param name="setAttributes">
    /// The termios-write boundary this lease used to enter raw mode - also the boundary
    /// <see cref="Resume"/> re-enters raw mode through after a SIGCONT job-control resume.
    /// </param>
    /// <param name="restoreAttributes">The termios-write boundary this lease restores through.</param>
    /// <param name="captureControlKeys">
    /// Whether Ctrl-key combinations are delivered as input bytes instead of raising signals -
    /// recorded so <see cref="Resume"/> can recompute the exact same raw-mode shape <see cref="Enter"/>
    /// originally derived.
    /// </param>
    private UnixConsoleMode(
        byte[]? restore,
        int fileDescriptor,
        Func<int, byte[], bool> setAttributes,
        Func<int, byte[], bool> restoreAttributes,
        bool captureControlKeys)
    {
        Debug.Assert(setAttributes is not null, "A lease always owns a terminal-attribute write boundary.");
        Debug.Assert(restoreAttributes is not null, "A lease always owns a terminal-attribute restoration boundary.");

        _restore = restore;
        _fileDescriptor = fileDescriptor;
        _setAttributes = setAttributes;
        _restoreAttributes = restoreAttributes;
        _captureControlKeys = captureControlKeys;
    }

    /// <summary>Enters raw no-echo input mode when the current console is a supported Unix terminal.</summary>
    /// <param name="captureControlKeys">Whether Ctrl-key combinations should be delivered as input bytes instead of raising signals.</param>
    /// <param name="getAttributes">
    /// The termios-read boundary, or null for the real <c>tcgetattr</c> call. Tests supply this
    /// together with <paramref name="setAttributes"/> so restoration-failure and pseudoterminal
    /// scenarios never depend on this process's own standard input being a real terminal.
    /// </param>
    /// <param name="setAttributes">
    /// The termios-write boundary, or null for the real <c>tcsetattr</c> call. Tests supply this
    /// to reach the raw-entry failure path without altering the developer's terminal.
    /// </param>
    /// <param name="restoreAttributes">
    /// The flushing termios-restore boundary, or null to use the real <c>tcsetattr</c> call. When
    /// <paramref name="setAttributes"/> is supplied alone, tests retain the legacy shared boundary
    /// for both operations.
    /// </param>
    /// <returns>An idempotent lease that restores the captured state when disposed.</returns>
    /// <exception cref="IOException">The current terminal state cannot be captured or raw mode cannot be enabled.</exception>
    /// <remarks>
    /// `ISIG` is restored after `cfmakeraw` clears it, so Ctrl+C continues to raise the host's
    /// cancellation event while individual key, pointer, paste, and focus bytes arrive without
    /// canonical line buffering. When <paramref name="captureControlKeys"/> is true, `ISIG` is
    /// left disabled so Ctrl-key combinations arrive as input bytes. Restoring `ISIG` also arms
    /// the tty's SUSP character (and, on macOS, DSUSP), so Ctrl+Z raises `SIGTSTP` - the Unix-only
    /// job-control signal registration (see `JobControlSignals`) installs the handler that restores
    /// cooked mode and every leased terminal mode before the process actually stops, and undoes all
    /// of it again through <see cref="Resume"/> once a `SIGCONT` resumes the process.
    /// </remarks>
    [MustDisposeResource]
    public static UnixConsoleMode Enter(
        bool captureControlKeys,
        [InstantHandle] Func<int, byte[]?>? getAttributes = null,
        Func<int, byte[], bool>? setAttributes = null,
        Func<int, byte[], bool>? restoreAttributes = null)
    {
        var read = getAttributes ?? DefaultGetAttributes;
        var write = setAttributes ?? RuntimeInterop.TrySetTerminalAttributes;
        var restoreWrite = restoreAttributes ??
                           (setAttributes is null
                               ? RuntimeInterop.TryRestoreTerminalAttributes
                               : setAttributes);
        var fileDescriptor = RuntimeInterop.StandardInputFileDescriptor;

        if (getAttributes is null && setAttributes is null &&
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new UnixConsoleMode(restore: null, fileDescriptor, write, restoreWrite, captureControlKeys);
        }

        var restore = read(fileDescriptor) ?? throw Failure();
        var raw = RuntimeInterop.ComputeRawTerminalAttributes(restore, captureControlKeys);

        if (!write(fileDescriptor, raw))
        {
            var failure = Failure();

            // Entry already failed. Restoring is best effort here precisely because the caller
            // must see why raw mode could not be established, not why the undo also failed.
            _ = restoreWrite(fileDescriptor, restore);

            throw failure;
        }

        return new UnixConsoleMode(restore, fileDescriptor, write, restoreWrite, captureControlKeys);
    }

    private static byte[]? DefaultGetAttributes(int fileDescriptor) =>
        RuntimeInterop.TryGetTerminalAttributes(fileDescriptor, out var state) ? state : null;

    /// <summary>
    /// Restores cooked termios ahead of a SIGTSTP job-control suspend, without releasing this
    /// lease: unlike <see cref="Dispose"/>, this can run more than once, and <see cref="Resume"/>
    /// re-derives and re-applies the identical raw state afterward instead of leaving the lease
    /// consumed.
    /// </summary>
    /// <returns>
    /// True when cooked mode was restored, or this lease never captured a state to restore (an
    /// unsupported host, where suspend/resume have nothing to do). False on a failed write - the
    /// caller's job-control signal path treats this as best effort, the same way <see cref="Enter"/>'s
    /// own best-effort undo does on a failed entry.
    /// </returns>
    public bool Suspend() => _restore is null || _restoreAttributes(_fileDescriptor, _restore);

    /// <summary>
    /// Re-enters raw mode after a SIGCONT job-control resume, recomputing the exact raw termios
    /// state <see cref="Enter"/> originally derived from the captured cooked state.
    /// </summary>
    /// <returns>
    /// True when raw mode was re-established, or this lease never captured a state to derive from.
    /// False on a failed write, treated as best effort by the caller for the same reason
    /// <see cref="Suspend"/> is.
    /// </returns>
    public bool Resume()
    {
        if (_restore is null)
        {
            return true;
        }

        var raw = RuntimeInterop.ComputeRawTerminalAttributes(_restore, _captureControlKeys);
        return _setAttributes(_fileDescriptor, raw);
    }

    /// <summary>Restores the captured terminal input state exactly once.</summary>
    /// <remarks>
    /// Restoration failure is reported rather than swallowed. Silently discarding it let cleanup
    /// claim success while the user's terminal stayed raw, without echo and without canonical
    /// input, and gave no layer above a chance to react. The owning connection folds this into a
    /// cleanup diagnostic, so a primary application failure is still preserved.
    /// </remarks>
    /// <exception cref="IOException">The captured terminal state could not be restored.</exception>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _restore is null)
        {
            return;
        }

        if (!_restoreAttributes(_fileDescriptor, _restore))
        {
            throw Failure();
        }
    }

    private static IOException Failure() =>
        new(
            "The terminal raw mode could not be configured.",
            new Win32Exception(Marshal.GetLastPInvokeError()));
}
