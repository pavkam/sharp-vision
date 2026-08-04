// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

using System.Buffers;

using Terminal.Graphics;
using Terminal.Graphics.Backends;

/// <summary>Throws synchronously out of Prepare, reproducing the fault Renderer.RenderAsync
/// propagates before its first await (see #269).</summary>
internal sealed class ThrowingGraphicsBackend: IGraphicsBackend
{
    /// <summary>Gets or sets the exact exception raised by the next Prepare call.</summary>
    internal Exception Failure { get; set; } = new InvalidOperationException("synchronous prepare failure");

    /// <inheritdoc/>
    public GraphicsBackendResult Prepare(Frame? front, Frame back, bool full, GraphicsContext context = default) =>
        throw Failure;

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
