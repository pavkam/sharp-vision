// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Buffers;

using Terminal.Graphics;
using Terminal.Graphics.Backends;
using Terminal.Kitty.Graphics;

/// <summary>Records asynchronous graphics acknowledgements while remaining byte-quiet.</summary>
internal sealed class RecordingGraphicsBackend: IGraphicsBackend
{
    /// <summary>Gets the last response forwarded by the renderer.</summary>
    public KittyGraphicsResponse? Response { get; private set; }

    /// <inheritdoc/>
    public void Accept(KittyGraphicsResponse response) => Response = response;

    /// <inheritdoc/>
    public GraphicsBackendResult Prepare(Frame? front, Frame back, bool full, GraphicsContext context = default) =>
        new(changed: false, uploads: 0, placements: 0, removals: 0);

    /// <inheritdoc/>
    public void WriteUploads(IBufferWriter<byte> destination)
    {
    }

    /// <inheritdoc/>
    public void WritePlacements(IBufferWriter<byte> destination)
    {
    }

    /// <inheritdoc/>
    public void WriteRemovals(IBufferWriter<byte> destination)
    {
    }

    /// <inheritdoc/>
    public void Commit()
    {
    }

    /// <inheritdoc/>
    public void Invalidate()
    {
    }

    /// <inheritdoc/>
    public int PrepareCleanup() => 0;

    /// <inheritdoc/>
    public void WriteCleanup(IBufferWriter<byte> destination)
    {
    }

    /// <inheritdoc/>
    public void CommitCleanup()
    {
    }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
