// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

/// <summary>Cancels an owned token source immediately before its first EOF result is observed.</summary>
internal sealed class CancelAtEndStream: MemoryStream
{
    private readonly CancellationTokenSource _cancellation;

    /// <summary>Initializes a readable seekable stream over the supplied bytes.</summary>
    /// <param name="buffer">The non-null source bytes.</param>
    /// <param name="cancellation">The non-null source canceled at EOF.</param>
    internal CancelAtEndStream(byte[] buffer, CancellationTokenSource cancellation) : base(buffer)
    {
        ArgumentNullException.ThrowIfNull(cancellation);
        _cancellation = cancellation;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        var count = await base.ReadAsync(buffer, cancellationToken);

        if (count == 0)
        {
            await _cancellation.CancelAsync();
        }

        return count;
    }
}
