// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

/// <summary>Opens an interactive console on Windows using VT input and VT processing.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsConsoleHost
{
    /// <summary>Enters VT console mode and opens standard streams and a polling resize source.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the saved console modes.</returns>
    /// <exception cref="IOException">The console mode cannot be configured.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options)
    {
        var mode = WindowsConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            var input = Console.OpenStandardInput(bufferSize: 1);
            var output = Console.OpenStandardOutput();
            var transport = new StreamTransport(input, output, leaveOpen: true);

            // The standard Windows console does not report pixel dimensions, so
            // resize is cell-only polling.
            var resize = new ConsoleResizeSource(options.ResizeInterval);

            return new ConsoleConnection(
                transport,
                resize,
                mode,
                DescriptionPlatform.Windows,
                outputFileDescriptor: RuntimeInterop.StandardOutputFileDescriptor,
                windowsVirtualTerminal: true);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
