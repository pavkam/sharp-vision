// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Transport;

/// <summary>Counts disposal attempts and fails them with one exact exception.</summary>
/// <remarks>
/// Cleanup must be attempted for every owned resource even after an earlier one throws, so a test
/// needs to prove both that the failing stream was attempted and that the failure did not prevent
/// the next attempt.
/// </remarks>
internal sealed class FailingStream: MemoryStream
{
    /// <summary>Gets the exact exception every disposal attempt throws.</summary>
    internal IOException Failure { get; } = new("stream disposal failed");

    /// <summary>Gets the number of disposal attempts this stream observed.</summary>
    internal int DisposeCount { get; private set; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        DisposeCount++;
        base.Dispose(disposing);

        throw Failure;
    }
}
