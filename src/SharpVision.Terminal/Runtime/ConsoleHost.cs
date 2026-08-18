// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Opens interactive console streams for a SharpVision application host.</summary>
/// <remarks>
/// These static members delegate to <see cref="Default"/>, the real platform
/// <see cref="IConsoleHost"/>. A caller that wants an injectable seam — for example to substitute
/// a fake host in a test, instead of hand-wrapping these statics in delegates — depends on
/// <see cref="IConsoleHost"/> directly instead of this static class.
/// </remarks>
[PublicAPI]
public static class ConsoleHost
{
    /// <summary>Gets the real platform console host backing this class's static members.</summary>
    public static IConsoleHost Default { get; } = new SystemConsoleHost();

    /// <summary>Gets whether standard input and output are attached to an interactive console.</summary>
    public static bool Interactive => Default.Interactive;

    /// <summary>Opens the interactive console for the current platform.</summary>
    /// <param name="options">The non-null host policy.</param>
    /// <returns>A connection exposing the transport and resize source and owning the restore lease.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="PlatformNotSupportedException">The current platform is not supported.</exception>
    /// <exception cref="IOException">The console cannot enter raw or VT mode.</exception>
    public static ConsoleConnection Open(ConsoleHostOptions options) => Default.Open(options);
}
