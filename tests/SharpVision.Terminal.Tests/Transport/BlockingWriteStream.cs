// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>
/// Blocks writes and flushes to prove write/flush admission is drained before disposal, and to
/// model a stream whose cancellation support is imperfect so the abandon-on-timeout path can be
/// exercised.
/// </summary>
internal sealed class BlockingWriteStream: MemoryStream
{
    private readonly TaskCompletionSource _firstStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _completed =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _ignoresCancellation;

    /// <summary>Initializes a stream whose write and flush block until <see cref="Release"/> is called.</summary>
    /// <param name="ignoresCancellation">
    /// Whether a blocked write or flush stays blocked when its cancellation token is signalled,
    /// modelling a non-cooperative stream that only ever leaves via <see cref="Release"/>.
    /// </param>
    internal BlockingWriteStream(bool ignoresCancellation = false) => _ignoresCancellation = ignoresCancellation;

    /// <summary>Gets the number of disposal attempts.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>Gets a task completed after the first write or flush starts blocking.</summary>
    internal Task FirstStarted => _firstStarted.Task;

    /// <summary>
    /// Gets a task completed once a blocked write or flush has genuinely returned from its
    /// underlying await, whether by release, cancellation, or fault. Lets a test observe an
    /// abandoned operation actually finishing in the background after disposal has moved on.
    /// </summary>
    internal Task Completed => _completed.Task;

    /// <summary>Releases every blocked write or flush.</summary>
    internal void Release() => _release.TrySetResult();

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        _ = _firstStarted.TrySetResult();

        try
        {
            if (_ignoresCancellation)
            {
                await _release.Task.ConfigureAwait(false);
            }
            else
            {
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _completed.TrySetResult();
        }
    }

    /// <inheritdoc/>
    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        _ = _firstStarted.TrySetResult();

        try
        {
            if (_ignoresCancellation)
            {
                await _release.Task.ConfigureAwait(false);
            }
            else
            {
                await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _ = _completed.TrySetResult();
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        base.Dispose(disposing);
    }
}
