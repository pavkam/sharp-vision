// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>The real platform <see cref="IConsoleHost"/>, dispatching to the Unix or Windows
/// console host for the current operating system.</summary>
internal sealed class SystemConsoleHost: IConsoleHost
{
    /// <inheritdoc />
    public bool Interactive =>
        !Console.IsInputRedirected && !Console.IsOutputRedirected;

    /// <inheritdoc />
    public ConsoleConnection Open(ConsoleHostOptions options)
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
