// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using SharpVision.Terminal.Graphics;

/// <summary>Retains one semantic placement and its exact image-placement identity pair.</summary>
internal readonly struct KittyGraphicsPlacementState
{
    /// <summary>Initializes one retained placement state.</summary>
    public KittyGraphicsPlacementState(
        Placement placement,
        uint imageId,
        uint placementId,
        bool usesImageNumber = false)
    {
        if (placement.IsEmpty)
        {
            throw new ArgumentException("A backend placement cannot be empty.", nameof(placement));
        }

        ArgumentOutOfRangeException.ThrowIfZero(imageId);
        ArgumentOutOfRangeException.ThrowIfZero(placementId);
        Placement = placement;
        ImageId = imageId;
        PlacementId = placementId;
        UsesImageNumber = usesImageNumber;
    }

    /// <summary>Gets the semantic placement.</summary>
    public Placement Placement { get; }

    /// <summary>Gets the owning image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the renderer-owned placement identifier.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets whether <see cref="ImageId"/> is a client image number awaiting assignment.</summary>
    public bool UsesImageNumber { get; }

    /// <summary>Creates the same placement using its terminal-assigned image identifier.</summary>
    public KittyGraphicsPlacementState WithAssignedImageId(uint imageId) =>
        new(Placement, imageId, PlacementId);
}
