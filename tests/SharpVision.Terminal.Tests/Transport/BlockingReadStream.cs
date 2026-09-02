// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>
/// Blocks reads to prove read admission is drained before disposal, and to model a stream whose
/// cancellation support is imperfect so the abandon-on-timeout path can be exercised.
/// </summary>
internal sealed class BlockingReadStream: MemoryStream
{
    private readonly TaskCompletionSource _firstStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _ignoresCancellation;
    private int _result;

    /// <summary>Initializes a stream whose read blocks until <see cref="Release"/> is called.</summary>
    /// <param name="ignoresCancellation">
    /// Whether a blocked read stays blocked when its cancellation token is signalled, modelling a
    /// non-cooperative stream that only ever leaves via <see cref="Release"/>.
    /// </param>
    internal BlockingReadStream(bool ignoresCancellation = false) => _ignoresCancellation = ignoresCancellation;

    /// <summary>Gets the number of disposal attempts.</summary>
    internal int DisposeCount { get; private set; }

    /// <summary>Gets a task completed after the first read starts blocking.</summary>
    internal Task FirstStarted => _firstStarted.Task;

    /// <summary>Releases the blocked read, letting it return <paramref name="result"/>.</summary>
    /// <param name="result">The byte count the blocked read returns.</param>
    internal void Release(int result = 0)
    {
        _result = result;
        _ = _release.TrySetResult();
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken = default)
    {
        _ = _firstStarted.TrySetResult();

        if (_ignoresCancellation)
        {
            await _release.Task.ConfigureAwait(false);
        }
        else
        {
            await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return _result;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        base.Dispose(disposing);
    }
}
