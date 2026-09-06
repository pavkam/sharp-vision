// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Owns one Windows console raw/VT mode lease with guaranteed restoration.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleMode: IDisposable
{
    private readonly nint _input;
    private readonly nint _output;
    private readonly uint _savedInput;
    private readonly uint _savedOutput;
    private readonly Func<nint, uint, bool> _setConsoleMode;
    private readonly Func<nint, bool> _flushConsoleInput;
    private int _disposed;

    private WindowsConsoleMode(
        nint input,
        nint output,
        uint savedInput,
        uint savedOutput,
        Func<nint, uint, bool> setConsoleMode,
        Func<nint, bool> flushConsoleInput)
    {
        Debug.Assert(setConsoleMode is not null, "A lease always owns a console-mode boundary.");
        Debug.Assert(flushConsoleInput is not null, "A lease always owns an input-flush boundary.");

        _input = input;
        _output = output;
        _savedInput = savedInput;
        _savedOutput = savedOutput;
        _setConsoleMode = setConsoleMode;
        _flushConsoleInput = flushConsoleInput;
    }

    /// <summary>Saves the current console modes and enters VT input and VT processing.</summary>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <param name="enableMouseInput">Whether mouse tracking is negotiated for this run.</param>
    /// <param name="setConsoleMode">
    /// The console-mode write boundary, or null for the real native call. Tests supply this to
    /// reach the restoration-failure path, which cannot be provoked against a real console.
    /// </param>
    /// <param name="flushConsoleInput">
    /// The console-input-buffer flush boundary, or null for the real native call. Tests supply
    /// this to assert the flush runs before the input mode is restored and that a flush failure
    /// still lets both modes restore, neither of which can be provoked against a real console.
    /// </param>
    /// <returns>A lease that restores both saved modes when disposed.</returns>
    /// <exception cref="IOException">A console mode cannot be read or written.</exception>
    [MustDisposeResource]
    public static WindowsConsoleMode Enter(
        bool captureControlKeys,
        bool enableMouseInput,
        Func<nint, uint, bool>? setConsoleMode = null,
        Func<nint, bool>? flushConsoleInput = null)
    {
        var write = setConsoleMode ?? RuntimeInterop.TrySetConsoleMode;
        var flush = flushConsoleInput ?? RuntimeInterop.TryFlushConsoleInput;
        var input = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle);
        var output = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdOutputHandle);

        if (!RuntimeInterop.TryGetConsoleMode(input, out var savedInput) ||
            !RuntimeInterop.TryGetConsoleMode(output, out var savedOutput))
        {
            throw Failure();
        }

        if (!write(input, RuntimeInterop.ComputeInputMode(savedInput, captureControlKeys, enableMouseInput)))
        {
            throw Failure();
        }

        if (!write(output, RuntimeInterop.ComputeOutputMode(savedOutput)))
        {
            var failure = Failure();
            _ = write(input, savedInput);
            throw failure;
        }

        return new WindowsConsoleMode(input, output, savedInput, savedOutput, write, flush);
    }

    /// <summary>
    /// Flushes unread console input, then restores the saved input and output console modes,
    /// exactly once.
    /// </summary>
    /// <remarks>
    /// Both handles are always attempted, so a failure on input never leaves output altered.
    /// Ignoring these results let cleanup claim success while the console kept modified input or
    /// output modes. The owning connection folds the failure into a cleanup diagnostic, so a
    /// primary application failure is still preserved.
    /// <para>
    /// The input buffer is flushed immediately before the input mode is restored, mirroring
    /// <c>UnixConsoleMode</c>'s use of <c>TCSAFLUSH</c>: a negotiation reply (DA1, CPR, DECRQM) or
    /// a mouse or focus report that arrived after this process's last read would otherwise sit in
    /// the console's input buffer and be delivered to the resumed shell as literal keystrokes once
    /// canonical line input comes back. A failing flush is folded into the same reported failure
    /// as a failing mode restore instead of being silently discarded, but it never skips either
    /// mode restore - an unflushed buffer is a smaller hazard than a console left in raw VT mode.
    /// </para>
    /// </remarks>
    /// <exception cref="IOException">The console input could not be flushed or a saved console mode could not be restored.</exception>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var restoredOutput = _setConsoleMode(_output, _savedOutput);
        var failure = restoredOutput ? null : Failure();

        if (!_flushConsoleInput(_input))
        {
            failure ??= Failure();
        }

        if (!_setConsoleMode(_input, _savedInput))
        {
            failure ??= Failure();
        }

        if (failure is not null)
        {
            throw failure;
        }
    }

    private static IOException Failure() =>
        new("The Windows console mode could not be configured.",
            new Win32Exception(Marshal.GetLastPInvokeError()));
}
