// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Opens interactive console streams for a SharpVision application host.</summary>
[PublicAPI]
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
}
