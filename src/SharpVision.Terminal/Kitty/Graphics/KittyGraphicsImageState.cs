// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using SharpVision.Terminal.Graphics;

/// <summary>Retains one semantic image and its renderer-owned protocol identifier.</summary>
internal readonly struct KittyGraphicsImageState
{
    /// <summary>Initializes one retained image state.</summary>
    public KittyGraphicsImageState(ImageSource image, uint number, uint assignedId = 0)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfZero(number);
        Image = image;
        Number = number;
        AssignedId = assignedId;
    }

    /// <summary>Gets the retained immutable image.</summary>
    public ImageSource Image { get; }

    /// <summary>Gets the nonzero client image number retained by the local allocator.</summary>
    public uint Number { get; }

    /// <summary>Gets the terminal-assigned image identifier, or zero until acknowledged.</summary>
    public uint AssignedId { get; }

    /// <summary>Gets the currently safe protocol reference.</summary>
    public uint Reference => AssignedId == 0 ? Number : AssignedId;

    /// <summary>Gets whether <see cref="Reference"/> is a client image number awaiting a terminal-assigned id.</summary>
    public bool UsesImageNumber => AssignedId == 0;

    /// <summary>Creates the same semantic image with its terminal-assigned identifier.</summary>
    public KittyGraphicsImageState WithAssignedId(uint id)
    {
        ArgumentOutOfRangeException.ThrowIfZero(id);
        return new KittyGraphicsImageState(Image, Number, id);
    }
}
