// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

/// <summary>Provides asynchronous terminal dimension changes without polling callers.</summary>
[PublicAPI]
public interface IResizeSource: IAsyncDisposable
{
    /// <summary>Attempts to read the current local dimensions without waiting for a change.</summary>
    /// <param name="value">Receives locally observed dimensions on success.</param>
    /// <returns>Whether the host exposes a synchronous current-size observation.</returns>
    public bool TryReadCurrent(out Dimensions value)
    {
        value = default;
        return false;
    }

    /// <summary>Waits for and returns the next observed dimensions.</summary>
    /// <param name="cancellationToken">Cancels the pending wait.</param>
    /// <returns>The next immutable dimensions.</returns>
    public ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken);
}
