// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using MustDisposeResource = JetBrains.Annotations.MustDisposeResourceAttribute;

/// <summary>The real platform <see cref="IConsoleHost"/>, dispatching to the Unix or Windows
/// console host for the current operating system.</summary>
/// <remarks>
/// <see cref="Open"/> guards against a second concurrent open on this host with an
/// <see cref="Interlocked"/> gate, mirroring the shape <see cref="Session.RunAsync"/> uses for
/// its own "reject a second concurrent entry" guard. A second platform mode entered against the
/// same descriptor while the first is still live would snapshot the first host's already-modified
/// raw state as its own restore target, silently stranding the terminal in raw mode once both
/// hosts dispose. Production only ever constructs one instance of this class
/// (<see cref="ConsoleHost.Default"/>), so an instance-level gate is process-wide in practice.
/// </remarks>
internal sealed class SystemConsoleHost: IConsoleHost
{
    private readonly Func<bool> _isInteractive;
    private readonly Func<ConsoleHostOptions, ConsoleConnection> _openPlatform;
    private int _open;

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
    /// <exception cref="InvalidOperationException">
    /// Standard input or output is redirected, or a connection opened by an earlier call is
    /// still live.
    /// </exception>
    [MustDisposeResource]
    public ConsoleConnection Open(ConsoleHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Interactive)
        {
            throw new InvalidOperationException(
                "Console hosting requires interactive standard input and output streams.");
        }

        if (Interlocked.CompareExchange(ref _open, 1, 0) != 0)
        {
            throw new InvalidOperationException("A console host is already open.");
        }

        try
        {
            var connection = _openPlatform(options);
            connection.DisposalCallback = ReleaseGate;

            return connection;
        }
        catch
        {
            ReleaseGate();
            throw;
        }
    }

    // Runs once, from ConsoleConnection.DisposeAsync, so a legitimate sequential
    // open -> close -> open still works instead of permanently locking this host out.
    private void ReleaseGate() => Interlocked.Exchange(ref _open, 0);

    private static ConsoleConnection OpenPlatform(ConsoleHostOptions options) =>
        OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()
            ? UnixConsoleHost.Open(options)
            : OperatingSystem.IsWindows()
                ? WindowsConsoleHost.Open(options)
                : throw new PlatformNotSupportedException(
                    "Interactive console hosting is supported only on Linux, macOS, and Windows.");
}
