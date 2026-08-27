// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies either a client image number or a terminal-assigned image id.</summary>
internal readonly record struct KittyGraphicsImageReference
{
    /// <summary>Initializes one nonzero typed image reference.</summary>
    public KittyGraphicsImageReference(uint value, bool usesImageNumber)
    {
        Debug.Assert(value != 0, "A Kitty image reference must be nonzero.");
        Value = value;
        UsesImageNumber = usesImageNumber;
    }

    /// <summary>Gets the nonzero numeric reference.</summary>
    public uint Value { get; }

    /// <summary>Gets whether <see cref="Value"/> is encoded with the <c>I</c> image-number key.</summary>
    public bool UsesImageNumber { get; }
}
