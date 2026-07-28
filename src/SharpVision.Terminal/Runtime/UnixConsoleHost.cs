// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

/// <summary>Opens an interactive console on Linux and macOS with SIGWINCH pixel resize.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixConsoleHost
{
    /// <summary>Enters raw mode and opens tty streams, a SIGWINCH resize source, and a restore lease.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options)
    {
        var mode = UnixConsoleMode.Enter(options.CaptureControlKeys);

        try
        {
            var input = new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1
                });
            var output = Console.OpenStandardOutput();
            var transport = new StreamTransport(input, output, leaveOpen: true);

            // The tty read descriptor answers TIOCGWINSZ, giving cell and pixel
            // dimensions and SIGWINCH-driven resize rather than cell-only polling.
            var descriptor = (int) input.SafeFileHandle.DangerousGetHandle();
            var resize = new UnixResizeSource(descriptor);

            return new ConsoleConnection(
                transport,
                resize,
                mode,
                DescriptionPlatform.Unix,
                outputFileDescriptor: 1,
                windowsVirtualTerminal: false);
        }
        catch
        {
            mode.Dispose();
            throw;
        }
    }
}
