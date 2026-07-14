// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

using SharpVision.Terminal.Transport;

/// <summary>
/// Bundles the transport and resize source opened for one interactive console and
/// owns the platform terminal-mode restore lease.
/// </summary>
/// <remarks>
/// The running session disposes Transport and Resize;
/// this connection restores the platform terminal mode when disposed, which the
/// host performs after the session's reverse mode cleanup.
/// </remarks>
public sealed class ConsoleConnection: IAsyncDisposable
{
    private readonly IDisposable _restore;
    private int _disposed;

    /// <summary>Initializes a connection over opened console resources.</summary>
    /// <param name="transport">The non-null transport over the console streams.</param>
    /// <param name="resize">The non-null resize source.</param>
    /// <param name="restore">The non-null platform terminal-mode restore lease.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    public ConsoleConnection(ITransport transport, IResizeSource resize, IDisposable restore)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(resize);
        ArgumentNullException.ThrowIfNull(restore);

        Transport = transport;
        Resize = resize;
        _restore = restore;
    }

    /// <summary>Gets the transport over the interactive console streams.</summary>
    public ITransport Transport { get; }

    /// <summary>Gets the resize source for the interactive console.</summary>
    public IResizeSource Resize { get; }

    /// <summary>Restores the platform terminal mode once. Never disposes transport or resize.</summary>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _restore.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
