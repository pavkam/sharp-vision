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
            var output = Console.OpenStandardOutput();

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
                outputFileDescriptor: 1,
                windowsVirtualTerminal: false);
        }
        catch
        {
            // Unwind in exact reverse construction order. The resize source borrows the raw tty
            // descriptor, so its SIGWINCH registration is released before the stream that owns
            // that descriptor, and the raw-mode lease is restored last because it was taken first.
            Release(resize);
            Release(transport);
            Release(input);
            Release(mode);

            throw;
        }
    }

    private static void Release(UnixConsoleMode mode)
    {
        // A partially constructed console must never let a cleanup failure replace the
        // construction failure the caller needs to see.
        try
        {
            mode.Dispose();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }

    private static void Release(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            // Every owned console resource completes disposal synchronously, so this failure path
            // stays synchronous rather than forcing an async signature onto Open.
            var disposal = resource.DisposeAsync();
            Debug.Assert(disposal.IsCompleted, "Console-owned resources dispose synchronously.");
            disposal.AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
        }
    }
}
