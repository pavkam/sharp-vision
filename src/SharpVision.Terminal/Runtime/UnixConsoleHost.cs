// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using Capabilities;

using Microsoft.Win32.SafeHandles;

using InstantHandle = JetBrains.Annotations.InstantHandleAttribute;
using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>Opens an interactive console on Linux and macOS with SIGWINCH pixel resize.</summary>
[SupportedOSPlatform("linux")]
[SupportedOSPlatform("macos")]
internal static class UnixConsoleHost
{
    /// <summary>Enters raw mode and opens tty streams, a SIGWINCH resize source, and a restore lease.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">Raw mode or the tty streams cannot be prepared.</exception>
    [MustDisposeResource]
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
    [MustDisposeResource]
    internal static ConsoleConnection Open(
        ConsoleHostOptions options,
        [InstantHandle] Func<SafeFileHandle, IResizeSource> createResize) =>
        Open(
            options,
            static () => new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1
                }),
            static captureControlKeys => UnixConsoleMode.Enter(captureControlKeys),
            createResize,
            RuntimeInterop.TerminalDevicesMatch);

    /// <summary>Opens a Unix console through deterministic device, mode, resize, and identity
    /// boundaries so tests can prove mismatched terminals are rejected before mutation.</summary>
    /// <param name="options">The validated host policy.</param>
    /// <param name="openInput">Opens the controlling terminal input descriptor.</param>
    /// <param name="enterMode">Enters raw mode after terminal identity is established.</param>
    /// <param name="createResize">Builds resize observation over the controlling terminal.</param>
    /// <param name="terminalDevicesMatch">Compares two terminal descriptors for identity.</param>
    /// <returns>A connection whose disposal restores the terminal input mode.</returns>
    /// <exception cref="IOException">The descriptors differ or console resources cannot be prepared.</exception>
    [MustDisposeResource]
    internal static ConsoleConnection Open(
        ConsoleHostOptions options,
        Func<FileStream> openInput,
        Func<bool, IDisposable> enterMode,
        [InstantHandle] Func<SafeFileHandle, IResizeSource> createResize,
        Func<int, int, bool> terminalDevicesMatch)
    {
        Debug.Assert(openInput is not null, "The console host always supplies an input factory.");
        Debug.Assert(enterMode is not null, "The console host always supplies a mode factory.");
        Debug.Assert(createResize is not null, "The console host always supplies a resize factory.");
        Debug.Assert(terminalDevicesMatch is not null, "The console host always supplies terminal identity.");
        IDisposable? mode = null;
        FileStream? input = null;
        StreamTransport? transport = null;
        IResizeSource? resize = null;

        try
        {
            input = openInput();

            var inputFileDescriptor = (int) input.SafeFileHandle.DangerousGetHandle();

            if (!terminalDevicesMatch(
                    RuntimeInterop.StandardInputFileDescriptor,
                    inputFileDescriptor) ||
                !terminalDevicesMatch(
                    RuntimeInterop.StandardOutputFileDescriptor,
                    inputFileDescriptor))
            {
                throw new IOException(
                    "Standard input, standard output, and the controlling terminal must identify the same terminal device.");
            }

            mode = enterMode(options.CaptureControlKeys);
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
                // Description resolution uses the descriptor this host opened and verified as
                // the shared terminal identity, instead of re-assuming the standard-output
                // descriptor after validation.
                outputFileDescriptor: inputFileDescriptor,
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
