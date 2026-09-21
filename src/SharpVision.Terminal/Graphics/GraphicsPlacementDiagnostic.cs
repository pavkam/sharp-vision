// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Reports one graphics placement that fell back to ordinary cells during the most recent frame.</summary>
[PublicAPI]
public readonly record struct GraphicsPlacementDiagnostic
{
    /// <summary>Initializes one validated skipped-placement diagnostic.</summary>
    /// <param name="imageIdentity">The skipped placement's stable nonzero process-local image identity.</param>
    /// <param name="reason">Why the placement could not be encoded.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="imageIdentity"/> is zero or <paramref name="reason"/> is undefined.
    /// </exception>
    public GraphicsPlacementDiagnostic(
        ulong imageIdentity,
        GraphicsPlacementSkipReason reason)
    {
        ArgumentOutOfRangeException.ThrowIfZero(imageIdentity);

        ArgumentOutOfRangeException.ThrowIfNotDefined(reason, nameof(reason), "The skip reason is undefined.");

        ImageIdentity = imageIdentity;
        Reason = reason;
    }

    /// <summary>Gets the skipped placement's stable nonzero process-local image identity.</summary>
    public ulong ImageIdentity { get; }

    /// <summary>Gets why the placement could not be encoded.</summary>
    public GraphicsPlacementSkipReason Reason { get; }
}
