// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Reports one graphics placement that fell back to ordinary cells during the most recent frame.</summary>
[PublicAPI]
public readonly record struct GraphicsPlacementDiagnostic
{
    /// <summary>Initializes one validated skipped-placement diagnostic.</summary>
    /// <param name="ImageIdentity">The skipped placement's stable nonzero process-local image identity.</param>
    /// <param name="Reason">Why the placement could not be encoded.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ImageIdentity"/> is zero or <paramref name="Reason"/> is undefined.
    /// </exception>
    public GraphicsPlacementDiagnostic(
        ulong ImageIdentity,
        GraphicsPlacementSkipReason Reason)
    {
        ArgumentOutOfRangeException.ThrowIfZero(ImageIdentity);

        if (!Enum.IsDefined(Reason))
        {
            throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "The skip reason is undefined.");
        }

        this.ImageIdentity = ImageIdentity;
        this.Reason = Reason;
    }

    /// <summary>Gets the skipped placement's stable nonzero process-local image identity.</summary>
    public ulong ImageIdentity { get; }

    /// <summary>Gets why the placement could not be encoded.</summary>
    public GraphicsPlacementSkipReason Reason { get; }
}
