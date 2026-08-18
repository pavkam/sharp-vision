// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Fails every read with one exact errno, standing in for a Unix device hang-up.</summary>
/// <remarks>
/// A real pseudoterminal reports a hang-up as <c>EIO</c> only on some interleavings - measured at 9
/// times in 400 on Linux x64 - so driving the translation from an actual device would be a coin
/// toss. This reproduces the exception .NET raises for a failing Unix read exactly: an
/// <see cref="IOException"/> whose <see cref="Exception.HResult"/> carries the raw errno.
/// </remarks>
internal sealed class HangUpStream: MemoryStream
{
    private readonly int _errorNumber;

    /// <summary>Initializes a stream whose reads fail with one errno.</summary>
    /// <param name="errorNumber">The positive errno every read reports.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="errorNumber"/> is not positive.</exception>
    internal HangUpStream(int errorNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(errorNumber);
        _errorNumber = errorNumber;
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException<int>(new IOException("Input/output error", _errorNumber));
}
