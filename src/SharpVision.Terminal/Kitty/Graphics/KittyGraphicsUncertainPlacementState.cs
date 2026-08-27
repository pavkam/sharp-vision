// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies one placement command that may have partially reached the terminal.</summary>
internal readonly struct KittyGraphicsUncertainPlacementState
{
    /// <summary>Initializes one nonzero image and placement identity pair.</summary>
    /// <param name="imageId">The nonzero renderer-owned image identifier.</param>
    /// <param name="placementId">The nonzero renderer-owned placement identifier.</param>
    /// <param name="usesImageNumber">Whether the image reference uses Kitty's <c>I</c> key.</param>
    public KittyGraphicsUncertainPlacementState(uint imageId, uint placementId, bool usesImageNumber = false)
    {
        Debug.Assert(imageId != 0, "An uncertain placement must reference a nonzero image.");
        Debug.Assert(placementId != 0, "An uncertain placement must retain a nonzero identity.");
        ImageId = imageId;
        PlacementId = placementId;
        UsesImageNumber = usesImageNumber;
    }

    /// <summary>Gets the renderer-owned image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the renderer-owned placement identifier.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets whether the image reference is a client image number.</summary>
    public bool UsesImageNumber { get; }

    /// <summary>Creates the same uncertain placement using a terminal-assigned image id.</summary>
    public KittyGraphicsUncertainPlacementState WithAssignedImageId(uint imageId) =>
        new(imageId, PlacementId);
}
