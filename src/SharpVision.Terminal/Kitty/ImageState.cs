// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

using Graphics;

/// <summary>Retains one semantic image and its renderer-owned protocol identifier.</summary>
internal readonly struct ImageState
{
    /// <summary>Initializes one retained image state.</summary>
    public ImageState(Image image, uint id)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfZero(id);
        Image = image;
        Id = id;
    }

    /// <summary>Gets the retained immutable image.</summary>
    public Image Image { get; }

    /// <summary>Gets the nonzero protocol identifier.</summary>
    public uint Id { get; }
}
