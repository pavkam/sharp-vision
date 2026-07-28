// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Identifies one placement command that may have partially reached the terminal.</summary>
internal readonly struct UncertainPlacementState
{
    /// <summary>Initializes one nonzero image and placement identity pair.</summary>
    /// <param name="imageId">The nonzero renderer-owned image identifier.</param>
    /// <param name="placementId">The nonzero renderer-owned placement identifier.</param>
    public UncertainPlacementState(uint imageId, uint placementId)
    {
        Debug.Assert(imageId != 0, "An uncertain placement must reference a nonzero image.");
        Debug.Assert(placementId != 0, "An uncertain placement must retain a nonzero identity.");
        ImageId = imageId;
        PlacementId = placementId;
    }

    /// <summary>Gets the renderer-owned image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the renderer-owned placement identifier.</summary>
    public uint PlacementId { get; }
}
