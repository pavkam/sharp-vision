// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Graphics;

using Rendering;

using MultiplexerRoute = Multiplexing.Route;

/// <summary>Configures the shared non-retained backend for iTerm2 multipart PNG placement.</summary>
internal sealed class ItermGraphicsBackend: IGraphicsBackend
{
    private readonly NonRetainedGraphicsBackend _inner;

    /// <summary>Initializes bounded multipart output and an optional authorized tmux route.</summary>
    /// <param name="maxPreparedBytes">The positive complete transaction byte bound.</param>
    /// <param name="route">An optional explicit tmux-only graphics route.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPreparedBytes"/> is not positive.</exception>
    /// <exception cref="NotSupportedException"><paramref name="route"/> cannot carry graphics.</exception>
    public ItermGraphicsBackend(
        int maxPreparedBytes = 16 * 1024 * 1024,
        MultiplexerRoute? route = null)
    {
        _inner = new NonRetainedGraphicsBackend(
            enableSixel: false,
            enableIterm: true,
            maxPreparedBytes,
            route);
    }

    /// <inheritdoc />
    public GraphicsBackendResult Prepare(
        Frame? front,
        Frame back,
        bool full,
        GraphicsContext context = default) => _inner.Prepare(front, back, full, context);

    /// <inheritdoc />
    public void WriteUploads(IBufferWriter<byte> destination) => _inner.WriteUploads(destination);

    /// <inheritdoc />
    public void WritePlacements(IBufferWriter<byte> destination) => _inner.WritePlacements(destination);

    /// <inheritdoc />
    public void WriteRemovals(IBufferWriter<byte> destination) => _inner.WriteRemovals(destination);

    /// <inheritdoc />
    public void Commit() => _inner.Commit();

    /// <inheritdoc />
    public void Invalidate() => _inner.Invalidate();

    /// <inheritdoc />
    public int PrepareCleanup() => _inner.PrepareCleanup();

    /// <inheritdoc />
    public void WriteCleanup(IBufferWriter<byte> destination) => _inner.WriteCleanup(destination);

    /// <inheritdoc />
    public void CommitCleanup() => _inner.CommitCleanup();

    /// <inheritdoc />
    public void Dispose() => _inner.Dispose();
}
