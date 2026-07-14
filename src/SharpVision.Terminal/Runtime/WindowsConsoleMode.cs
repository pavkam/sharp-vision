// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Owns one Windows console raw/VT mode lease with guaranteed restoration.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsConsoleMode: IDisposable
{
    private readonly nint _input;
    private readonly nint _output;
    private readonly uint _savedInput;
    private readonly uint _savedOutput;
    private int _disposed;

    private WindowsConsoleMode(nint input, nint output, uint savedInput, uint savedOutput)
    {
        _input = input;
        _output = output;
        _savedInput = savedInput;
        _savedOutput = savedOutput;
    }

    /// <summary>Saves the current console modes and enters VT input and VT processing.</summary>
    /// <param name="captureControlKeys">Whether Ctrl+C is delivered as input.</param>
    /// <returns>A lease that restores both saved modes when disposed.</returns>
    /// <exception cref="IOException">A console mode cannot be read or written.</exception>
    internal static WindowsConsoleMode Enter(bool captureControlKeys)
    {
        nint input = Native.GetStandardHandle(Native.StdInputHandle);
        nint output = Native.GetStandardHandle(Native.StdOutputHandle);

        if (!Native.TryGetConsoleMode(input, out uint savedInput) ||
            !Native.TryGetConsoleMode(output, out uint savedOutput))
        {
            throw Failure();
        }

        if (!Native.TrySetConsoleMode(input, Native.ComputeInputMode(savedInput, captureControlKeys)))
        {
            throw Failure();
        }

        if (!Native.TrySetConsoleMode(output, Native.ComputeOutputMode(savedOutput)))
        {
            IOException failure = Failure();
            _ = Native.TrySetConsoleMode(input, savedInput);
            throw failure;
        }

        return new WindowsConsoleMode(input, output, savedInput, savedOutput);
    }

    /// <summary>Restores the saved input and output console modes once, best effort.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = Native.TrySetConsoleMode(_input, _savedInput);
            _ = Native.TrySetConsoleMode(_output, _savedOutput);
        }
    }

    private static IOException Failure() =>
        new("The Windows console mode could not be configured.",
            new Win32Exception(Marshal.GetLastPInvokeError()));
}
