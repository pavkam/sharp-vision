// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Opens interactive console streams for a SharpVision application host, as an injectable seam
/// over the operating-system boundary.
/// </summary>
/// <remarks>
/// <see cref="ConsoleHost"/>'s static members are backed by
/// <see cref="ConsoleHost.Default"/>, the real platform implementation. A caller that
/// wants to substitute a fake host for a test — instead of hand-wrapping the static members in
/// delegates, as callers previously had to — implements this interface directly.
/// </remarks>
[PublicAPI]
public interface IConsoleHost
{
    /// <summary>Gets whether standard input and output are attached to an interactive console.</summary>
    public bool Interactive { get; }

    /// <summary>Opens the interactive console for the current platform.</summary>
    /// <param name="options">The non-null host policy.</param>
    /// <returns>A connection exposing the transport and resize source and owning the restore lease.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <exception cref="IOException">The console cannot enter raw or VT mode.</exception>
    public ConsoleConnection Open(ConsoleHostOptions options);
}
