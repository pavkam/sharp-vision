// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>The real platform <see cref="IConsoleHost"/>, dispatching to the Unix or Windows
/// console host for the current operating system.</summary>
internal sealed class SystemConsoleHost: IConsoleHost
{
    private readonly Func<bool> _isInteractive;
    private readonly Func<ConsoleHostOptions, ConsoleConnection> _openPlatform;

    /// <summary>Initializes the real host over live process and platform boundaries.</summary>
    public SystemConsoleHost() : this(
        static () => !Console.IsInputRedirected && !Console.IsOutputRedirected,
        OpenPlatform)
    {
    }

    /// <summary>Initializes a deterministic host for proving precondition ordering.</summary>
    /// <param name="isInteractive">Reports whether both standard streams are interactive.</param>
    /// <param name="openPlatform">Opens the selected platform boundary.</param>
    /// <exception cref="ArgumentNullException">A boundary is null.</exception>
    internal SystemConsoleHost(
        Func<bool> isInteractive,
        Func<ConsoleHostOptions, ConsoleConnection> openPlatform)
    {
        ArgumentNullException.ThrowIfNull(isInteractive);
        ArgumentNullException.ThrowIfNull(openPlatform);

        _isInteractive = isInteractive;
        _openPlatform = openPlatform;
    }

    /// <inheritdoc />
    public bool Interactive => _isInteractive();

    /// <inheritdoc />
    [MustDisposeResource]
    public ConsoleConnection Open(ConsoleHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !Interactive
            ? throw new InvalidOperationException(
                "Console hosting requires interactive standard input and output streams.")
            : _openPlatform(options);
    }

    private static ConsoleConnection OpenPlatform(ConsoleHostOptions options) =>
        OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? UnixConsoleHost.Open(options)
            : OperatingSystem.IsWindows()
                ? WindowsConsoleHost.Open(options)
                : throw new PlatformNotSupportedException(
                    "Interactive console hosting is supported only on Linux, macOS, and Windows.");
}
