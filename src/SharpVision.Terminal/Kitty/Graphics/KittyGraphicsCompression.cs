// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

/// <summary>Identifies Kitty compression metadata, including recognized unsupported values.</summary>
[PublicAPI]
public enum KittyGraphicsCompression
{
    /// <summary>Source bytes are transmitted without compression.</summary>
    None,

    /// <summary>Source bytes use RFC 1950 zlib wrapping; transmission rejects this unsupported value.</summary>
    Zlib
}
