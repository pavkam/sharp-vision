// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

/// <summary>
/// Defines the asynchronous byte boundary between terminal logic and an endpoint.
/// </summary>
/// <remarks>
/// Read and write memory is borrowed until the returned operation completes.
/// A successful write transfers the complete source; partial transfer must throw.
/// </remarks>
[PublicAPI]
public interface ITransport: IAsyncDisposable
{
    /// <summary>Reads terminal input into caller-owned memory.</summary>
    /// <param name="destination">The destination borrowed until completion.</param>
    /// <param name="cancellationToken">Cancels the pending read.</param>
    /// <returns>The byte count, or zero for orderly closure.</returns>
    public ValueTask<int> ReadAsync(
        Memory<byte> destination,
        CancellationToken cancellationToken);

    /// <summary>Writes the complete borrowed source or throws.</summary>
    /// <param name="source">The source borrowed until completion.</param>
    /// <param name="cancellationToken">Cancels the pending write.</param>
    /// <returns>An operation completing only after full transfer.</returns>
    public ValueTask WriteAsync(
        ReadOnlyMemory<byte> source,
        CancellationToken cancellationToken);

    /// <summary>Flushes buffered output to the endpoint.</summary>
    /// <param name="cancellationToken">Cancels the pending flush.</param>
    /// <returns>The flush operation.</returns>
    public ValueTask FlushAsync(CancellationToken cancellationToken);
}
