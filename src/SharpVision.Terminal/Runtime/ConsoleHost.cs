// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using SharpVision.Terminal.Transport;

/// <summary>Opens interactive console streams for a SharpVision application host.</summary>
public static class ConsoleHost
{
    /// <summary>Gets whether standard input and output are attached to an interactive console.</summary>
    public static bool IsInteractive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <summary>Opens the interactive console for the current platform.</summary>
    /// <param name="options">The non-null host policy.</param>
    /// <returns>A connection exposing the transport and resize source and owning the restore lease.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <exception cref="IOException">The console cannot enter raw or VT mode.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? UnixConsoleHost.Open(options)
            : OperatingSystem.IsWindows()
                ? WindowsConsoleHost.Open(options)
                : throw new PlatformNotSupportedException(
                    "Interactive console hosting is supported only on Linux, macOS, and Windows.");
    }

    /// <summary>Opens the console input stream used by interactive hosts.</summary>
    /// <returns>A readable stream with one-byte buffering on supported platforms.</returns>
    /// <remarks>Transitional: removed with the console-entry migration once Application.Console.cs is deleted.</remarks>
    public static Stream OpenInputStream()
    {
        return OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? new FileStream(
                "/dev/tty",
                new FileStreamOptions
                {
                    Access = FileAccess.Read,
                    Mode = FileMode.Open,
                    Options = FileOptions.Asynchronous,
                    Share = FileShare.ReadWrite,
                    BufferSize = 1,
                })
            : Console.OpenStandardInput(bufferSize: 1);
    }

    /// <summary>Opens the console output stream used by interactive hosts.</summary>
    /// <returns>The writable standard output stream.</returns>
    /// <remarks>Transitional: removed with the console-entry migration once Application.Console.cs is deleted.</remarks>
    public static Stream OpenOutputStream() => Console.OpenStandardOutput();

    /// <summary>Creates a transport over the interactive console streams.</summary>
    /// <returns>A transport that leaves both streams open for host lifetime.</returns>
    /// <remarks>Transitional: removed with the console-entry migration once Application.Console.cs is deleted.</remarks>
    public static StreamTransport CreateTransport() =>
        new(OpenInputStream(), OpenOutputStream(), leaveOpen: true);
}
