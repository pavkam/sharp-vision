// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Describes one protocol-owned replacement cell projected over semantic frame content.</summary>
internal readonly record struct GraphicsCellOverlayValue
{
    /// <summary>Initializes one active Kitty placeholder cell.</summary>
    /// <param name="imageId">The nonzero terminal-assigned image identifier.</param>
    /// <param name="placementId">The nonzero virtual placement identifier.</param>
    /// <param name="row">The zero-based row within the virtual placement.</param>
    /// <param name="column">The zero-based column within the virtual placement.</param>
    /// <param name="background">The semantic underlay background visible through transparency.</param>
    /// <param name="identityColorDepth">The exact color encoding used for protocol identifiers.</param>
    public GraphicsCellOverlayValue(
        uint imageId,
        uint placementId,
        int row,
        int column,
        Color background,
        ColorDepth identityColorDepth)
    {
        Debug.Assert(imageId != 0, "An active placeholder identifies one assigned image.");
        Debug.Assert(placementId != 0, "An active placeholder identifies one virtual placement.");
        Debug.Assert(row >= 0, "A placeholder row is non-negative.");
        Debug.Assert(column >= 0, "A placeholder column is non-negative.");
        ImageId = imageId;
        PlacementId = placementId;
        Row = row;
        Column = column;
        Background = background;
        IdentityColorDepth = identityColorDepth;
    }

    /// <summary>Gets whether this cell replaces semantic text.</summary>
    public bool IsActive => ImageId != 0;

    /// <summary>Gets the terminal-assigned image identifier.</summary>
    public uint ImageId { get; }

    /// <summary>Gets the virtual placement identifier.</summary>
    public uint PlacementId { get; }

    /// <summary>Gets the zero-based row within the virtual placement.</summary>
    public int Row { get; }

    /// <summary>Gets the zero-based column within the virtual placement.</summary>
    public int Column { get; }

    /// <summary>Gets the semantic underlay background.</summary>
    public Color Background { get; }

    /// <summary>Gets whether identifiers use exact indexed or true-color SGR.</summary>
    public ColorDepth IdentityColorDepth { get; }
}
