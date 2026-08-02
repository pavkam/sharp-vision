// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics;

/// <summary>Identifies why a graphics placement fell back to ordinary cells instead of a protocol upload.</summary>
[PublicAPI]
public enum GraphicsPlacementSkipReason
{
    /// <summary>The image's pixel format has no encodable path on any protocol the active backend enabled.</summary>
    FormatNotEncodable
}
