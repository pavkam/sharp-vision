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
        bool usesImageNumber = false,
        bool usedPlaceholder = false)
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
        UsedPlaceholder = usedPlaceholder;
    }

    /// <summary>Gets the semantic placement.</summary>
    public Placement Placement { get; }

    /// <summary>Gets the owning image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the renderer-owned placement identifier.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets whether <see cref="ImageId"/> is a client image number awaiting assignment.</summary>
    public bool UsesImageNumber { get; }

    /// <summary>
    /// Gets whether this placement was eligible for (and rendered through) a virtual/Unicode
    /// placeholder in the <c>Prepare</c> call that produced this state, rather than an explicit
    /// Kitty placement command. Captured at <c>Commit()</c> time alongside the rest of this state
    /// so a later <c>Prepare</c> call can detect a flip in placeholder eligibility on its own,
    /// without depending on the caller forcing a full reconstruct.
    /// </summary>
    public bool UsedPlaceholder { get; }

    /// <summary>Creates the same placement using its terminal-assigned image identifier.</summary>
    public KittyGraphicsPlacementState WithAssignedImageId(uint imageId) =>
        new(Placement, imageId, PlacementId, usedPlaceholder: UsedPlaceholder);

    /// <summary>Creates the same placement recording whether it used a placeholder this frame.</summary>
    public KittyGraphicsPlacementState WithUsedPlaceholder(bool usedPlaceholder) =>
        new(Placement, ImageId, PlacementId, UsesImageNumber, usedPlaceholder);
}
