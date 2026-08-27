// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Suspends its first asynchronous read until the test releases it, so other
/// dispatcher-scheduled work can run and commit during the gap.</summary>
internal sealed class PausableReadStream: MemoryStream
{
    private readonly TaskCompletionSource _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _paused;

    /// <summary>Initializes a readable seekable stream over the supplied bytes.</summary>
    /// <param name="buffer">The non-null source bytes.</param>
    internal PausableReadStream(byte[] buffer) : base(buffer)
    {
    }

    /// <summary>Gets a task completed once the first read has parked and is awaiting release.</summary>
    internal Task Entered => _entered.Task;

    /// <summary>Releases the parked read so it can complete.</summary>
    internal void Release() => _release.TrySetResult();

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (!_paused)
        {
            _paused = true;
            _ = _entered.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }
}
