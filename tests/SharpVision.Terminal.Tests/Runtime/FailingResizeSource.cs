// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

/// <summary>Provides a resize source whose disposal always fails with one exact exception.</summary>
/// <remarks>
/// The session disposes the resize source before the transport, so this fake is what proves the
/// transport is still disposed after the earlier owned resource throws.
/// </remarks>
internal sealed class FailingResizeSource: IResizeSource
{
    /// <summary>Gets the exact exception every disposal attempt throws.</summary>
    internal IOException Failure { get; } = new("resize disposal failed");

    /// <summary>Gets the number of disposal attempts this source observed.</summary>
    internal int DisposeCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<Dimensions> ReadAsync(CancellationToken cancellationToken) =>
        new(Task.FromCanceled<Dimensions>(
            cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true)));

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCount++;

        return ValueTask.FromException(Failure);
    }
}
