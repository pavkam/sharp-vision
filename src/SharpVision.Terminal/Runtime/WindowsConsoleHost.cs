// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Runtime.Versioning;

using SharpVision.Terminal.Transport;

/// <summary>Opens an interactive console on Windows using VT input and VT processing.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsConsoleHost
{
    /// <summary>Enters VT console mode and opens standard streams and a polling resize source.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the saved console modes.</returns>
    /// <exception cref="IOException">The console mode cannot be configured.</exception>
    internal static ConsoleConnection Open(ConsoleHostOptions options)
    {
        WindowsConsoleMode mode = WindowsConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            Stream input = Console.OpenStandardInput(bufferSize: 1);
            Stream output = Console.OpenStandardOutput();
            StreamTransport transport = new(input, output, leaveOpen: true);

            // The standard Windows console does not report pixel dimensions, so
            // resize is cell-only polling.
            ConsoleResizeSource resize = new(options.ResizeInterval);

            return new ConsoleConnection(transport, resize, mode);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
