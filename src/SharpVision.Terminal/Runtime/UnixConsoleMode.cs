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
    private int _disposed;

    /// <summary>Initializes one lease with an optional captured terminal restoration state.</summary>
    /// <param name="restore">The captured termios state, or null when the host is unsupported.</param>
    /// <param name="fileDescriptor">The descriptor the lease reads and restores.</param>
    /// <param name="setAttributes">The termios-write boundary this lease restores through.</param>
    private UnixConsoleMode(byte[]? restore, int fileDescriptor, Func<int, byte[], bool> setAttributes)
    {
        Debug.Assert(setAttributes is not null, "A lease always owns a terminal-attribute boundary.");

        _restore = restore;
        _fileDescriptor = fileDescriptor;
        _setAttributes = setAttributes;
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
    /// to reach the restoration-failure path, which cannot be provoked through the real syscall
    /// without altering the developer's terminal.
    /// </param>
    /// <returns>An idempotent lease that restores the captured state when disposed.</returns>
    /// <exception cref="IOException">The current terminal state cannot be captured or raw mode cannot be enabled.</exception>
    /// <remarks>
    /// `ISIG` is restored after `cfmakeraw` clears it, so Ctrl+C continues to raise the host's
    /// cancellation event while individual key, pointer, paste, and focus bytes arrive without
    /// canonical line buffering. When <paramref name="captureControlKeys"/> is true, `ISIG` is
    /// left disabled so Ctrl-key combinations arrive as input bytes.
    /// </remarks>
    [MustDisposeResource]
    public static UnixConsoleMode Enter(
        bool captureControlKeys,
        [InstantHandle] Func<int, byte[]?>? getAttributes = null,
        Func<int, byte[], bool>? setAttributes = null)
    {
        var read = getAttributes ?? DefaultGetAttributes;
        var write = setAttributes ?? RuntimeInterop.TrySetTerminalAttributes;
        var fileDescriptor = RuntimeInterop.StandardInputFileDescriptor;

        if (getAttributes is null && setAttributes is null &&
            !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return new UnixConsoleMode(restore: null, fileDescriptor, write);
        }

        var restore = read(fileDescriptor) ?? throw Failure();
        var raw = RuntimeInterop.ComputeRawTerminalAttributes(restore, captureControlKeys);

        if (!write(fileDescriptor, raw))
        {
            var failure = Failure();

            // Entry already failed. Restoring is best effort here precisely because the caller
            // must see why raw mode could not be established, not why the undo also failed.
            _ = write(fileDescriptor, restore);

            throw failure;
        }

        return new UnixConsoleMode(restore, fileDescriptor, write);
    }

    private static byte[]? DefaultGetAttributes(int fileDescriptor) =>
        RuntimeInterop.TryGetTerminalAttributes(fileDescriptor, out var state) ? state : null;

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

        if (!_setAttributes(_fileDescriptor, _restore))
        {
            throw Failure();
        }
    }

    private static IOException Failure() =>
        new(
            "The terminal raw mode could not be configured.",
            new Win32Exception(Marshal.GetLastPInvokeError()));
}
