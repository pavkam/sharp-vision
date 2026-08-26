// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies Kitty compression metadata.</summary>
[PublicAPI]
public enum KittyGraphicsCompression
{
    /// <summary>Source bytes are transmitted without compression.</summary>
    None,

    /// <summary>Source bytes use RFC 1950 zlib wrapping; <see cref="KittyGraphicsWriter.WriteTransmission"/>
    /// compresses the raw payload before chunking and emits the <c>o=z</c> field.</summary>
    Zlib
}
