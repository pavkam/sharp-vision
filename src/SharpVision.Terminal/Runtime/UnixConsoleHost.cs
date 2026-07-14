// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using System.Runtime.Versioning;

using SharpVision.Terminal.Transport;

/// <summary>Opens an interactive console on Linux and macOS with SIGWINCH pixel resize.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixConsoleHost
{
    /// <summary>Enters raw mode and opens tty streams, a SIGWINCH resize source, and a restore lease.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    internal static ConsoleConnection Open(ConsoleHostOptions options)
    {
        UnixConsoleMode mode = UnixConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            FileStream input = new(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1,
                });
            Stream output = Console.OpenStandardOutput();
            StreamTransport transport = new(input, output, leaveOpen: true);

            // The tty read descriptor answers TIOCGWINSZ, giving cell and pixel
            // dimensions and SIGWINCH-driven resize rather than cell-only polling.
            int descriptor = (int) input.SafeFileHandle.DangerousGetHandle();
            UnixResizeSource resize = new(descriptor);

            return new ConsoleConnection(transport, resize, mode);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
