// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Graphics;

/// <summary>Records renderer/backend lifecycle ordering without selecting a real protocol.</summary>
internal sealed class FakeGraphicsBackend: IGraphicsBackend
{
    private static readonly byte[] _placements = "<place>"u8.ToArray();
    private static readonly byte[] _removals = "<remove>"u8.ToArray();
    private static readonly byte[] _uploads = "<upload>"u8.ToArray();
    private static readonly byte[] _cleanup = "<cleanup>"u8.ToArray();
    private ManualResetEventSlim? _prepareBlock;
    private TaskCompletionSource? _prepareStarted;

    /// <summary>Gets full-invalidation flags supplied to prepare calls.</summary>
    internal List<bool> FullPreparations { get; } = [];

    /// <summary>Gets the number of committed backend transactions.</summary>
    internal int CommitCount { get; private set; }

    /// <summary>Gets the number of invalidated backend transactions.</summary>
    internal int InvalidateCount { get; private set; }

    /// <summary>Gets the number of disposal calls.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>Gets the number of remote cleanup preparations.</summary>
    internal int CleanupPrepareCount { get; private set; }

    /// <summary>Gets the number of committed remote cleanups.</summary>
    internal int CleanupCommitCount { get; private set; }

    /// <summary>Gets the number of upload writer calls.</summary>
    internal int UploadWriteCount { get; private set; }

    /// <summary>Gets the number of placement writer calls.</summary>
    internal int PlacementWriteCount { get; private set; }

    /// <summary>Gets or sets whether placement encoding fails.</summary>
    internal bool FailPlacements { get; set; }

    /// <summary>Gets or sets whether transaction preparation fails.</summary>
    internal bool FailPrepare { get; set; }

    /// <summary>Gets or sets the exact disposal exception.</summary>
    internal Exception? DisposeFailure { get; set; }

    /// <summary>Gets a task completed when blocked preparation begins.</summary>
    internal Task PrepareStarted => _prepareStarted?.Task ?? Task.CompletedTask;

    /// <summary>Blocks subsequent preparation until explicitly released.</summary>
    internal void BlockPrepare()
    {
        _prepareBlock = new ManualResetEventSlim(initialState: false);
        _prepareStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Releases blocked preparation.</summary>
    internal void ReleasePrepare() => _prepareBlock?.Set();

    /// <inheritdoc />
    public GraphicsBackendResult Prepare(
        Frame? front,
        Frame back,
        bool full,
        GraphicsContext context = default)
    {
        FullPreparations.Add(full);
        _ = _prepareStarted?.TrySetResult();
        var block = _prepareBlock;
        block?.Wait();
        _prepareBlock = null;
        _prepareStarted = null;
        block?.Dispose();

        for (var index = 0; index < back.PlacementCount; index++)
        {
            var placement = back.GetPlacement(index);
            Debug.Assert(!placement.IsEmpty, "Active backend placements cannot be empty sentinels.");
            Debug.Assert(placement.Image is not null, "A nonempty placement must retain its immutable image.");
        }

        return FailPrepare
            ? throw new InvalidOperationException("prepare failure")
            : new GraphicsBackendResult(
            changed: Damage.PlacementsChanged(front, back, full),
            uploads: back.PlacementCount,
            placements: back.PlacementCount,
            removals: front?.PlacementCount ?? 0);
    }

    /// <inheritdoc />
    public void WriteUploads(IBufferWriter<byte> destination)
    {
        UploadWriteCount++;
        destination.Write(_uploads);
    }

    /// <inheritdoc />
    public void WritePlacements(IBufferWriter<byte> destination)
    {
        PlacementWriteCount++;

        if (FailPlacements)
        {
            throw new InvalidOperationException("placement failure");
        }

        destination.Write(_placements);
    }

    /// <inheritdoc />
    public void WriteRemovals(IBufferWriter<byte> destination) => destination.Write(_removals);

    /// <inheritdoc />
    public void Commit() => CommitCount++;

    /// <inheritdoc />
    public void Invalidate() => InvalidateCount++;

    /// <inheritdoc />
    public int PrepareCleanup()
    {
        CleanupPrepareCount++;
        return CommitCount == 0 ? 0 : 1;
    }

    /// <inheritdoc />
    public void WriteCleanup(IBufferWriter<byte> destination) => destination.Write(_cleanup);

    /// <inheritdoc />
    public void CommitCleanup() => CleanupCommitCount++;

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeCount++;
        _prepareBlock?.Dispose();
        _prepareBlock = null;

        if (DisposeFailure is { } exception)
        {
            throw exception;
        }
    }
}
