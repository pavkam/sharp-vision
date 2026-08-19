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
    private int _disposed;

    private WindowsConsoleMode(
        nint input,
        nint output,
        uint savedInput,
        uint savedOutput,
        Func<nint, uint, bool> setConsoleMode)
    {
        Debug.Assert(setConsoleMode is not null, "A lease always owns a console-mode boundary.");

        _input = input;
        _output = output;
        _savedInput = savedInput;
        _savedOutput = savedOutput;
        _setConsoleMode = setConsoleMode;
    }

    /// <summary>Saves the current console modes and enters VT input and VT processing.</summary>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <param name="setConsoleMode">
    /// The console-mode write boundary, or null for the real native call. Tests supply this to
    /// reach the restoration-failure path, which cannot be provoked against a real console.
    /// </param>
    /// <returns>A lease that restores both saved modes when disposed.</returns>
    /// <exception cref="IOException">A console mode cannot be read or written.</exception>
    [MustDisposeResource]
    public static WindowsConsoleMode Enter(
        bool captureControlKeys,
        Func<nint, uint, bool>? setConsoleMode = null)
    {
        var write = setConsoleMode ?? RuntimeInterop.TrySetConsoleMode;
        var input = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdInputHandle);
        var output = RuntimeInterop.GetStandardHandle(RuntimeInterop.StdOutputHandle);

        if (!RuntimeInterop.TryGetConsoleMode(input, out var savedInput) ||
            !RuntimeInterop.TryGetConsoleMode(output, out var savedOutput))
        {
            throw Failure();
        }

        if (!write(input, RuntimeInterop.ComputeInputMode(savedInput, captureControlKeys)))
        {
            throw Failure();
        }

        if (!write(output, RuntimeInterop.ComputeOutputMode(savedOutput)))
        {
            var failure = Failure();
            _ = write(input, savedInput);
            throw failure;
        }

        return new WindowsConsoleMode(input, output, savedInput, savedOutput, write);
    }

    /// <summary>Restores the saved input and output console modes exactly once.</summary>
    /// <remarks>
    /// Both handles are always attempted, so a failure on input never leaves output altered.
    /// Ignoring these results let cleanup claim success while the console kept modified input or
    /// output modes. The owning connection folds the failure into a cleanup diagnostic, so a
    /// primary application failure is still preserved.
    /// </remarks>
    /// <exception cref="IOException">A saved console mode could not be restored.</exception>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var restored = _setConsoleMode(_input, _savedInput);
        var failure = restored ? null : Failure();

        if (!_setConsoleMode(_output, _savedOutput))
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
