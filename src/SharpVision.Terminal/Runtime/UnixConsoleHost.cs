// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

using Microsoft.Win32.SafeHandles;

/// <summary>Opens an interactive console on Linux and macOS with SIGWINCH pixel resize.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixConsoleHost
{
    /// <summary>Enters raw mode and opens tty streams, a SIGWINCH resize source, and a restore lease.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options) =>
        Open(
            options,
            static handle => new UnixResizeSource((int) handle.DangerousGetHandle()));

    /// <summary>
    /// Enters raw mode and opens tty streams, a resize source built by the supplied factory, and a
    /// restore lease.
    /// </summary>
    /// <remarks>
    /// The factory exists for two test obligations that cannot be reached through argument
    /// validation. It can fail after the tty stream and transport already exist, which is the only
    /// ordering in which a partially constructed console must unwind, and it exposes the owned tty
    /// handle so a test can assert that handle is genuinely closed at the end of the lifecycle
    /// rather than inferring it from a process-wide descriptor count.
    /// </remarks>
    /// <param name="options">The validated host policy.</param>
    /// <param name="createResize">Builds the resize source from the borrowed tty handle.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    internal static ConsoleConnection Open(
        ConsoleHostOptions options,
        Func<SafeFileHandle, IResizeSource> createResize)
    {
        Debug.Assert(createResize is not null, "The console host always supplies a resize factory.");
        var mode = UnixConsoleMode.Enter(options.CaptureControlKeys);
        FileStream? input = null;
        StreamTransport? transport = null;
        IResizeSource? resize = null;

        try
        {
            input = new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1
                });
            // Never touch System.Console here. Writing through Console.OpenStandardOutput()
            // (rather than merely opening it) initializes the BCL's Unix console, which reads
            // terminfo and emits smkx (application keypad mode) on the first write - and once
            // initialized, the runtime re-emits smkx every time any child process exits, so a
            // later stty-style child spawned during teardown re-arms it after this host has
            // already restored every mode. A raw FileStream over the borrowed descriptor never
            // initializes that BCL state.
            var output = new FileStream(
                new SafeFileHandle(RuntimeInterop.StandardOutputFileDescriptor, ownsHandle: false),
                FileAccess.Write);

            // Ownership differs per stream. This host opened /dev/tty itself, so the transport
            // must close that descriptor during ordinary shutdown, while standard output belongs
            // to the process and is only borrowed.
            transport = new StreamTransport(input, output, leaveInputOpen: false, leaveOutputOpen: true);

            // The tty read descriptor answers TIOCGWINSZ, giving cell and pixel
            // dimensions and SIGWINCH-driven resize rather than cell-only polling.
            resize = createResize(input.SafeFileHandle);

            return new ConsoleConnection(
                transport,
                resize,
                mode,
                DescriptionPlatform.Unix,
                // The descriptor this host actually opened and verified refers to a terminal,
                // rather than an assumed standard-output descriptor: when stdout is redirected —
                // precisely the case the /dev/tty open above exists to handle — description
                // resolution against a hardcoded 1 would query a descriptor that is not a
                // terminal at all.
                outputFileDescriptor: (int) input.SafeFileHandle.DangerousGetHandle(),
                windowsVirtualTerminal: false);
        }
        catch
        {
            // Unwind in exact reverse construction order. The resize source borrows the raw tty
            // descriptor, so its SIGWINCH registration is released before the stream that owns
            // that descriptor, and the raw-mode lease is restored last because it was taken first.
            ConsoleHostResourceRelease.ReleaseResource(resize);
            ConsoleHostResourceRelease.ReleaseResource(transport);
            ConsoleHostResourceRelease.ReleaseResource(input);
            ConsoleHostResourceRelease.ReleaseMode(mode);

            throw;
        }
    }
}
